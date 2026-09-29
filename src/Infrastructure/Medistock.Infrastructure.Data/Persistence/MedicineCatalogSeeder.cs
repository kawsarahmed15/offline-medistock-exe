using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Microsoft.Data.Sqlite;

namespace Medistock.Infrastructure.Data.Persistence;

public class MedicineCatalogSeeder : IMedicineCatalogSeeder
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    private static readonly (Regex Pattern, int FormId, string Unit)[] DosageMap = new[]
    {
        (new Regex(@"\b(tablet|tab|tabs|caplet)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0, "TAB"),
        (new Regex(@"\b(capsule|cap|caps)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1, "CAP"),
        (new Regex(@"\b(syrup|syr|oral solution|elixir|liquid)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 2, "BTL"),
        (new Regex(@"\b(injection|inj|vial|ampoule|infusion|iv)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 3, "VIAL"),
        (new Regex(@"\b(cream)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 4, "TUBE"),
        (new Regex(@"\b(ointment|oint)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 5, "TUBE"),
        (new Regex(@"\b(gel)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 6, "TUBE"),
        (new Regex(@"\b(drops?|eye drop|ear drop|nasal drop)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 7, "BTL"),
        (new Regex(@"\b(inhaler|rotacap|respule|inhalation)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 8, "INH"),
        (new Regex(@"\b(suspension|susp)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 9, "BTL"),
        (new Regex(@"\b(powder|sachet|granules)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 10, "SCT"),
        (new Regex(@"\b(spray)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 11, "BTL"),
        (new Regex(@"\b(patch|transdermal)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 12, "PCS")
    };

    private static readonly HashSet<string> ScheduleH1Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "alprazolam", "clobazam", "clonazepam", "diazepam", "lorazepam", "midazolam",
        "nitrazepam", "oxazepam", "zolpidem", "tramadol", "codeine", "buprenorphine",
        "pentazocine", "meropenem", "imipenem", "ertapenem", "doripenem", "colistin",
        "tigecycline", "balofloxacin", "gemifloxacin", "moxifloxacin", "gatifloxacin"
    };

    private static readonly HashSet<string> ColdChainKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "insulin", "mixtard", "novorapid", "lantus", "humalog", "apidra",
        "erythropoietin", "vaccine", "tetanus", "rabies", "immunoglobulin"
    };

    private static readonly Regex StripPackRegex = new(@"(?:strip|box|blister|pack)\s+of\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NumPackRegex = new(@"(\d+)\s*(?:tablet|capsule|tab|cap|sachet)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MedicineCatalogSeeder(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> GetProductCountAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM products;");
    }

    public async Task<int> SeedFromJsonFileAsync(
        string jsonFilePath,
        IProgress<MedicineSeedProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jsonFilePath) || !File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException($"Medicine JSON data file was not found at: {jsonFilePath}");
        }

        var stopwatch = Stopwatch.StartNew();
        progress?.Report(new MedicineSeedProgress(0, 0, "Loading medicine data file into memory...", 0));

        await using var stream = File.OpenRead(jsonFilePath);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Invalid JSON format: expected a JSON array of medicine items.");
        }

        var totalItems = root.GetArrayLength();
        if (totalItems == 0)
        {
            return 0;
        }

        progress?.Report(new MedicineSeedProgress(0, totalItems, $"Found {totalItems:N0} medicines. Initializing database...", 2.0));

        using var rawConn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (rawConn is not SqliteConnection conn)
        {
            throw new InvalidOperationException("Underlying connection must be a SqliteConnection.");
        }

        // Optimize SQLite session settings for fast bulk import
        await conn.ExecuteAsync("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = OFF;");

        // Temporarily drop FTS triggers for maximum throughput
        await conn.ExecuteAsync(@"
            DROP TRIGGER IF EXISTS trg_products_fts_insert;
            DROP TRIGGER IF EXISTS trg_products_fts_update;
            DROP TRIGGER IF EXISTS trg_products_fts_delete;
        ");

        const int batchSize = 10000;
        var processedCount = 0;
        var nowIso = DateTime.UtcNow.ToString("o");

        const string insertSql = @"
            INSERT OR REPLACE INTO products (
                id, org_id, name, brand_name, generic_name, composition, strength,
                dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
                schedule, is_prescription_required, is_cold_chain, is_narcotic,
                is_active, min_stock_alert, primary_barcode, manufacturer_name,
                created_at, updated_at
            ) VALUES (
                $id, $org_id, $name, $brand_name, $generic_name, $composition, $strength,
                $dosage_form, $pack_units, $base_unit, $hsn_code, $gst_rate_percent,
                $schedule, $is_prescription_required, $is_cold_chain, $is_narcotic,
                $is_active, $min_stock_alert, $primary_barcode, $manufacturer_name,
                $created_at, $updated_at
            );
        ";

        var enumerator = root.EnumerateArray();
        var hasMore = enumerator.MoveNext();

        while (hasMore)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var transaction = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = insertSql;

            var pId = cmd.Parameters.Add("$id", SqliteType.Text);
            var pOrg = cmd.Parameters.Add("$org_id", SqliteType.Text);
            var pName = cmd.Parameters.Add("$name", SqliteType.Text);
            var pBrand = cmd.Parameters.Add("$brand_name", SqliteType.Text);
            var pGeneric = cmd.Parameters.Add("$generic_name", SqliteType.Text);
            var pComp = cmd.Parameters.Add("$composition", SqliteType.Text);
            var pStrength = cmd.Parameters.Add("$strength", SqliteType.Text);
            var pDosageForm = cmd.Parameters.Add("$dosage_form", SqliteType.Integer);
            var pPackUnits = cmd.Parameters.Add("$pack_units", SqliteType.Integer);
            var pBaseUnit = cmd.Parameters.Add("$base_unit", SqliteType.Text);
            var pHsn = cmd.Parameters.Add("$hsn_code", SqliteType.Text);
            var pGst = cmd.Parameters.Add("$gst_rate_percent", SqliteType.Real);
            var pSchedule = cmd.Parameters.Add("$schedule", SqliteType.Integer);
            var pRx = cmd.Parameters.Add("$is_prescription_required", SqliteType.Integer);
            var pCold = cmd.Parameters.Add("$is_cold_chain", SqliteType.Integer);
            var pNarc = cmd.Parameters.Add("$is_narcotic", SqliteType.Integer);
            var pActive = cmd.Parameters.Add("$is_active", SqliteType.Integer);
            var pMinStock = cmd.Parameters.Add("$min_stock_alert", SqliteType.Real);
            var pBarcode = cmd.Parameters.Add("$primary_barcode", SqliteType.Text);
            var pMfg = cmd.Parameters.Add("$manufacturer_name", SqliteType.Text);
            var pCreated = cmd.Parameters.Add("$created_at", SqliteType.Text);
            var pUpdated = cmd.Parameters.Add("$updated_at", SqliteType.Text);

            var batchCount = 0;
            while (hasMore && batchCount < batchSize)
            {
                var item = enumerator.Current;
                var rawId = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var name = item.TryGetProperty("name", out var nameProp) ? (nameProp.GetString() ?? string.Empty).Trim() : string.Empty;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    var id = string.IsNullOrWhiteSpace(rawId) ? Ulid.NewUlid().ToString() : $"in_med_{rawId.Trim()}";
                    var mfg = item.TryGetProperty("manufacturer_name", out var mfgProp) ? (mfgProp.GetString() ?? string.Empty).Trim() : string.Empty;
                    var comp1 = item.TryGetProperty("short_composition1", out var c1Prop) ? (c1Prop.GetString() ?? string.Empty).Trim() : string.Empty;
                    var comp2 = item.TryGetProperty("short_composition2", out var c2Prop) ? (c2Prop.GetString() ?? string.Empty).Trim() : string.Empty;
                    var packLabel = item.TryGetProperty("pack_size_label", out var plProp) ? (plProp.GetString() ?? string.Empty).Trim() : string.Empty;
                    var isDiscontinued = item.TryGetProperty("Is_discontinued", out var discProp) && string.Equals(discProp.GetString()?.Trim(), "TRUE", StringComparison.OrdinalIgnoreCase);

                    var composition = string.IsNullOrEmpty(comp1) ? comp2 : (string.IsNullOrEmpty(comp2) ? comp1 : $"{comp1} + {comp2}");

                    var (dosageForm, packUnits, baseUnit) = ParseDosageAndPack(name, packLabel);
                    var (schedule, rx, coldChain, narcotic) = DetermineScheduleAndFlags(name, composition);

                    pId.Value = id;
                    pOrg.Value = "org-1";
                    pName.Value = name;
                    pBrand.Value = name;
                    pGeneric.Value = string.IsNullOrWhiteSpace(composition) ? name : composition;
                    pComp.Value = composition;
                    pStrength.Value = string.Empty;
                    pDosageForm.Value = dosageForm;
                    pPackUnits.Value = packUnits;
                    pBaseUnit.Value = baseUnit;
                    pHsn.Value = "3004";
                    pGst.Value = 12.0;
                    pSchedule.Value = schedule;
                    pRx.Value = rx;
                    pCold.Value = coldChain;
                    pNarc.Value = narcotic;
                    pActive.Value = isDiscontinued ? 0 : 1;
                    pMinStock.Value = 10.0;
                    pBarcode.Value = DBNull.Value;
                    pMfg.Value = string.IsNullOrWhiteSpace(mfg) ? DBNull.Value : mfg;
                    pCreated.Value = nowIso;
                    pUpdated.Value = DBNull.Value;

                    cmd.ExecuteNonQuery();
                    processedCount++;
                }

                batchCount++;
                hasMore = enumerator.MoveNext();
            }

            transaction.Commit();

            var pct = 5.0 + ((double)processedCount / totalItems) * 75.0;
            progress?.Report(new MedicineSeedProgress(
                processedCount,
                totalItems,
                $"Seeding medicines: {processedCount:N0} / {totalItems:N0} ({pct:0.0}%)",
                pct
            ));
        }

        // Rebuild FTS Search Table in one pass
        progress?.Report(new MedicineSeedProgress(processedCount, totalItems, "Rebuilding Fast Full-Text Search (FTS5) Index...", 88.0));
        await conn.ExecuteAsync("DELETE FROM fts_products;");
        await conn.ExecuteAsync(@"
            INSERT INTO fts_products (product_id, name, brand_name, generic_name, composition, manufacturer_name, barcode)
            SELECT id, name, brand_name, generic_name, composition, IFNULL(manufacturer_name, ''), IFNULL(primary_barcode, '')
            FROM products;
        ");

        // Recreate Triggers
        progress?.Report(new MedicineSeedProgress(processedCount, totalItems, "Re-enabling real-time database search triggers...", 95.0));
        await conn.ExecuteAsync(@"
            CREATE TRIGGER IF NOT EXISTS trg_products_fts_insert AFTER INSERT ON products
            BEGIN
                INSERT INTO fts_products(product_id, name, brand_name, generic_name, composition, manufacturer_name, barcode)
                VALUES (new.id, new.name, new.brand_name, new.generic_name, new.composition, IFNULL(new.manufacturer_name, ''), IFNULL(new.primary_barcode, ''));
            END;

            CREATE TRIGGER IF NOT EXISTS trg_products_fts_update AFTER UPDATE ON products
            BEGIN
                DELETE FROM fts_products WHERE product_id = old.id;
                INSERT INTO fts_products(product_id, name, brand_name, generic_name, composition, manufacturer_name, barcode)
                VALUES (new.id, new.name, new.brand_name, new.generic_name, new.composition, IFNULL(new.manufacturer_name, ''), IFNULL(new.primary_barcode, ''));
            END;

            CREATE TRIGGER IF NOT EXISTS trg_products_fts_delete AFTER DELETE ON products
            BEGIN
                DELETE FROM fts_products WHERE product_id = old.id;
            END;
        ");

        await conn.ExecuteAsync("PRAGMA foreign_keys = ON;");

        stopwatch.Stop();
        progress?.Report(new MedicineSeedProgress(
            processedCount,
            totalItems,
            $"✓ Successfully imported {processedCount:N0} medicines in {stopwatch.Elapsed.TotalSeconds:0.1}s!",
            100.0
        ));

        return processedCount;
    }

    public async Task<int> SeedSampleStarterPackAsync(
        IProgress<MedicineSeedProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new MedicineSeedProgress(0, 220, "Loading starter catalogue...", 10.0));
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var products = DataSeeder.GetSeedProducts();
        var total = products.Count;

        using var tx = conn.BeginTransaction();
        var count = 0;

        foreach (var p in products)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await conn.ExecuteAsync(@"
                INSERT OR REPLACE INTO products (
                    id, org_id, name, brand_name, generic_name, composition, strength,
                    dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
                    schedule, is_prescription_required, is_cold_chain, is_narcotic,
                    is_active, min_stock_alert, primary_barcode, manufacturer_name, created_at
                ) VALUES (
                    @id, 'org-1', @name, @brand, @generic, @comp, @strength,
                    @form, @pack, @unit, @hsn, @gst,
                    @sch, @rx, @cold, @narc,
                    1, 10.0, @barcode, @mfg, datetime('now')
                );
            ", new
            {
                id = p.Id,
                name = p.Name,
                brand = p.BrandName,
                generic = p.GenericName,
                comp = p.Composition,
                strength = p.Strength,
                form = p.DosageForm,
                pack = p.PackUnits,
                unit = p.BaseUnit,
                hsn = p.HsnCode,
                gst = p.GstRatePercent,
                sch = p.Schedule,
                rx = p.IsPrescriptionRequired ? 1 : 0,
                cold = p.IsColdChain ? 1 : 0,
                narc = p.IsNarcotic ? 1 : 0,
                barcode = p.Barcode,
                mfg = p.Manufacturer
            }, tx);

            // Seed primary batch
            var b1Id = $"b_{p.Id}_1";
            var expiry1 = DateTime.UtcNow.AddDays(p.ExpiryOffsetDays1);
            await conn.ExecuteAsync(@"
                INSERT OR REPLACE INTO batches (id, product_id, org_id, batch_number, expiry_date, mrp, purchase_rate, sale_rate, created_at)
                VALUES (@id, @prodId, 'org-1', @batchNo, @expiry, @mrp, @prate, @srate, datetime('now'));

                INSERT OR REPLACE INTO stock_balances (id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at)
                VALUES (@sbId, @id, @prodId, 'wh-1', @qty, 0.0, datetime('now'));
            ", new
            {
                id = b1Id,
                prodId = p.Id,
                batchNo = p.BatchNo1,
                expiry = expiry1.ToString("o"),
                mrp = p.Mrp,
                prate = p.PurchaseRate,
                srate = p.SaleRate,
                sbId = "sb_" + b1Id,
                qty = p.Quantity1
            }, tx);

            if (!string.IsNullOrEmpty(p.BatchNo2))
            {
                var b2Id = $"b_{p.Id}_2";
                var expiry2 = DateTime.UtcNow.AddDays(p.ExpiryOffsetDays2);
                await conn.ExecuteAsync(@"
                    INSERT OR REPLACE INTO batches (id, product_id, org_id, batch_number, expiry_date, mrp, purchase_rate, sale_rate, created_at)
                    VALUES (@id, @prodId, 'org-1', @batchNo, @expiry, @mrp, @prate, @srate, datetime('now'));

                    INSERT OR REPLACE INTO stock_balances (id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at)
                    VALUES (@sbId, @id, @prodId, 'wh-1', @qty, 0.0, datetime('now'));
                ", new
                {
                    id = b2Id,
                    prodId = p.Id,
                    batchNo = p.BatchNo2,
                    expiry = expiry2.ToString("o"),
                    mrp = p.Mrp,
                    prate = p.PurchaseRate,
                    srate = p.SaleRate,
                    sbId = "sb_" + b2Id,
                    qty = p.Quantity2
                }, tx);
            }

            count++;
            if (count % 25 == 0 || count == total)
            {
                var pct = ((double)count / total) * 100.0;
                progress?.Report(new MedicineSeedProgress(count, total, $"Seeding starter sample: {count}/{total} medicines...", pct));
            }
        }

        tx.Commit();
        progress?.Report(new MedicineSeedProgress(count, total, $"✓ Starter pack of {count} products & stock seeded successfully!", 100.0));
        return count;
    }

    public async Task<int> ClearAllProductsAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync("DELETE FROM stock_balances;", transaction: tx);
        await conn.ExecuteAsync("DELETE FROM batches;", transaction: tx);
        await conn.ExecuteAsync("DELETE FROM product_barcodes;", transaction: tx);
        var deletedCount = await conn.ExecuteAsync("DELETE FROM products;", transaction: tx);
        await conn.ExecuteAsync("DELETE FROM fts_products;", transaction: tx);

        tx.Commit();
        return deletedCount;
    }

    private static (int DosageForm, int PackUnits, string BaseUnit) ParseDosageAndPack(string name, string packLabel)
    {
        var fullStr = $"{name} {packLabel}";
        var dosageForm = 0; // Default Tablet
        var baseUnit = "TAB";

        foreach (var (pattern, formId, unit) in DosageMap)
        {
            if (pattern.IsMatch(fullStr))
            {
                dosageForm = formId;
                baseUnit = unit;
                break;
            }
        }

        var packUnits = 10;
        if (!string.IsNullOrWhiteSpace(packLabel))
        {
            var matchStrip = StripPackRegex.Match(packLabel);
            if (matchStrip.Success && int.TryParse(matchStrip.Groups[1].Value, out var pu))
            {
                packUnits = Math.Max(1, pu);
            }
            else if (dosageForm is 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 11)
            {
                packUnits = 1;
            }
            else
            {
                var matchNum = NumPackRegex.Match(packLabel);
                if (matchNum.Success && int.TryParse(matchNum.Groups[1].Value, out var n))
                {
                    packUnits = Math.Max(1, n);
                }
            }
        }

        return (dosageForm, packUnits, baseUnit);
    }

    private static (int Schedule, int Rx, int ColdChain, int Narcotic) DetermineScheduleAndFlags(string name, string comp)
    {
        var text = $"{name} {comp}".ToLowerInvariant();

        var isCold = 0;
        foreach (var kw in ColdChainKeywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                isCold = 1;
                break;
            }
        }

        var isH1 = false;
        foreach (var kw in ScheduleH1Keywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                isH1 = true;
                break;
            }
        }

        if (isH1)
        {
            return (2, 1, isCold, 0); // Schedule H1
        }

        if (text.Contains("paracetamol", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("antacid", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("cough", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("vitamin", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("calcium", StringComparison.OrdinalIgnoreCase))
        {
            return (0, 0, isCold, 0); // OTC
        }

        return (1, 1, isCold, 0); // Schedule H
    }
}
