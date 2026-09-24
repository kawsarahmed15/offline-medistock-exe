using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Products.Queries;
using Medistock.Application.Sales.Commands;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace Medistock.Infrastructure.Tests;

public class EndToEndPharmacyPosSimulationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseMigrator _migrator;
    private readonly SqliteProductSearchRepository _searchRepo;
    private readonly SqliteStockRepository _stockRepo;
    private readonly SqliteSaleRepository _saleRepo;
    private readonly SqliteOutboxRepository _outboxRepo;
    private readonly SqliteDocumentSequenceService _sequenceService;
    private readonly ProductSearchService _searchService;
    private readonly PosTransactionService _posTransactionService;

    public EndToEndPharmacyPosSimulationTests(ITestOutputHelper output)
    {
        _output = output;
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_sim_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrator = new DatabaseMigrator(_connectionFactory);
        _searchRepo = new SqliteProductSearchRepository(_connectionFactory);
        _stockRepo = new SqliteStockRepository(_connectionFactory);
        _outboxRepo = new SqliteOutboxRepository(_connectionFactory);
        _sequenceService = new SqliteDocumentSequenceService(_connectionFactory);
        _saleRepo = new SqliteSaleRepository(_connectionFactory, _stockRepo, _outboxRepo);
        _searchService = new ProductSearchService(_searchRepo);
        _posTransactionService = new PosTransactionService(_saleRepo, _sequenceService);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            var wal = _dbPath + "-wal";
            var shm = _dbPath + "-shm";
            if (File.Exists(wal)) File.Delete(wal);
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }

    private async Task SeedRealisticPharmacyInventoryAsync()
    {
        await _migrator.MigrateAsync();

        using var conn = await _connectionFactory.CreateConnectionAsync();

        // Seed 10 realistic Indian medicines with barcodes, batches, HSN codes, and schedules
        var products = new[]
        {
            ("p_dolo", "Dolo 650mg Tablet", "Dolo 650", "Paracetamol", "Paracetamol 650mg", "650mg", 0, 15, "TAB", "30049099", 12.0, 0, 0, 0, "8901234567890", "Micro Labs Ltd"),
            ("p_aug", "Augmentin 625 Duo Tablet", "Augmentin", "Amoxicillin and Potassium Clavulanate", "Amoxicillin 500mg + Clavulanic Acid 125mg", "625mg", 0, 10, "TAB", "30041010", 12.0, 1, 1, 0, "8909876543210", "GSK Pharmaceuticals"),
            ("p_pand", "Pan-D Capsule", "Pan-D", "Pantoprazole and Domperidone", "Pantoprazole 40mg + Domperidone 30mg", "70mg", 1, 15, "CAP", "30049099", 12.0, 1, 0, 0, "8901112223334", "Alkem Laboratories"),
            ("p_azith", "Azithral 500mg Tablet", "Azithral 500", "Azithromycin", "Azithromycin 500mg", "500mg", 0, 5, "TAB", "30042010", 12.0, 2, 1, 0, "8902223334445", "Alembic Pharmaceuticals"),
            ("p_telma", "Telma 40mg Tablet", "Telma 40", "Telmisartan", "Telmisartan 40mg", "40mg", 0, 30, "TAB", "30049099", 12.0, 1, 0, 0, "8903334445556", "Glenmark Pharmaceuticals"),
            ("p_glyc", "Glycomet-GP 2 Tablet", "Glycomet-GP 2", "Glimepiride and Metformin", "Glimepiride 2mg + Metformin 500mg", "502mg", 0, 15, "TAB", "30049099", 12.0, 1, 0, 0, "8904445556667", "USV Private Limited"),
            ("p_mont", "Montair-LC Tablet", "Montair-LC", "Montelukast and Levocetirizine", "Montelukast 10mg + Levocetirizine 5mg", "15mg", 0, 10, "TAB", "30049099", 12.0, 0, 0, 0, "8905556667778", "Cipla Ltd"),
            ("p_alpra", "Alprax 0.5mg Tablet", "Alprax 0.5", "Alprazolam", "Alprazolam 0.5mg", "0.5mg", 0, 15, "TAB", "30049099", 12.0, 1, 1, 0, "8906667778889", "Torrent Pharmaceuticals"),
            ("p_insul", "Human Mixtard 30/70 100IU/ml", "Mixtard 30/70", "Insulin Human", "Biphasic Isophane Insulin 100IU", "100IU/ml", 3, 1, "VIAL", "30043110", 5.0, 1, 1, 1, "8907778889990", "Novo Nordisk"),
            ("p_beta", "Betadine 10% Solution 100ml", "Betadine", "Povidone-Iodine", "Povidone-Iodine 10% w/v", "10%", 2, 1, "BTL", "30049099", 12.0, 0, 0, 0, "8908889990001", "Win-Medicare")
        };

        foreach (var p in products)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO products (
                    id, org_id, name, brand_name, generic_name, composition, strength,
                    dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
                    schedule, is_prescription_required, is_cold_chain, is_narcotic,
                    is_active, primary_barcode, manufacturer_name, created_at
                ) VALUES (
                    @id, 'org-1', @name, @brand, @generic, @comp, @strength,
                    @form, @pack, @unit, @hsn, @gst,
                    @sch, @rx, @cold, 0,
                    1, @barcode, @mfg, datetime('now')
                );
            ", new
            {
                id = p.Item1,
                name = p.Item2,
                brand = p.Item3,
                generic = p.Item4,
                comp = p.Item5,
                strength = p.Item6,
                form = p.Item7,
                pack = p.Item8,
                unit = p.Item9,
                hsn = p.Item10,
                gst = p.Item11,
                sch = p.Item12,
                rx = p.Item13,
                cold = p.Item14,
                barcode = p.Item15,
                mfg = p.Item16
            });
        }

        // Insert Batches with FEFO Expiries and Stock Balances
        var batches = new[]
        {
            ("b_dolo_1", "p_dolo", "DL24A", DateTime.UtcNow.AddDays(180), 30.50, 22.00, 30.00, 150.0),
            ("b_dolo_2", "p_dolo", "DL24B", DateTime.UtcNow.AddDays(400), 32.00, 23.50, 32.00, 200.0),
            ("b_aug_1", "p_aug", "AG24A", DateTime.UtcNow.AddDays(120), 205.00, 155.00, 200.00, 50.0),
            ("b_pand_1", "p_pand", "PD24A", DateTime.UtcNow.AddDays(300), 199.00, 140.00, 195.00, 80.0),
            ("b_azith_1", "p_azith", "AZ24A", DateTime.UtcNow.AddDays(240), 125.00, 90.00, 120.00, 60.0),
            ("b_telma_1", "p_telma", "TL24A", DateTime.UtcNow.AddDays(365), 240.00, 180.00, 235.00, 40.0),
            ("b_insul_1", "p_insul", "HM24A", DateTime.UtcNow.AddDays(90), 175.00, 130.00, 175.00, 25.0)
        };

        foreach (var b in batches)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO batches (id, product_id, org_id, batch_number, expiry_date, mrp, purchase_rate, sale_rate, created_at)
                VALUES (@id, @prodId, 'org-1', @batchNo, @expiry, @mrp, @prate, @srate, datetime('now'));

                INSERT INTO stock_balances (id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at)
                VALUES (@sbId, @id, @prodId, 'wh-1', @qty, 0.0, datetime('now'));
            ", new
            {
                id = b.Item1,
                prodId = b.Item2,
                batchNo = b.Item3,
                expiry = b.Item4.ToString("o"),
                mrp = b.Item5,
                prate = b.Item6,
                srate = b.Item7,
                sbId = "sb_" + b.Item1,
                qty = b.Item8
            });
        }
    }

    [Fact]
    public async Task Scenario1_InteractiveProductSearch_MatchesAcrossBrandGenericSalt()
    {
        await SeedRealisticPharmacyInventoryAsync();

        // Warm up JIT
        await _searchService.SearchAsync("dolo", "wh-1");

        // 1. Search by Brand Name: "dolo"
        var sw1 = Stopwatch.StartNew();
        var byBrand = await _searchService.SearchAsync("dolo", "wh-1");
        sw1.Stop();
        _output.WriteLine($"[Search by Brand 'dolo'] Found: {byBrand.Count} items in {sw1.Elapsed.TotalMilliseconds:F2}ms");
        Assert.NotEmpty(byBrand);
        Assert.Equal("Dolo 650mg Tablet", byBrand[0].Name);
        Assert.Equal("DL24A", byBrand[0].BatchNumber); // Verified FEFO (earliest expiry selected)
        Assert.True(sw1.ElapsedMilliseconds < 50, "Brand search should be <50ms");

        // 2. Search by Generic Molecule: "pantoprazole"
        var sw2 = Stopwatch.StartNew();
        var byGeneric = await _searchService.SearchAsync("pantoprazole", "wh-1");
        sw2.Stop();
        _output.WriteLine($"[Search by Generic 'pantoprazole'] Found: {byGeneric.Count} items in {sw2.Elapsed.TotalMilliseconds:F2}ms");
        Assert.NotEmpty(byGeneric);
        Assert.Equal("Pan-D Capsule", byGeneric[0].Name);

        // 3. Search by Composition / Salt combination: "clavulanic"
        var sw3 = Stopwatch.StartNew();
        var bySalt = await _searchService.SearchAsync("clavulanic", "wh-1");
        sw3.Stop();
        _output.WriteLine($"[Search by Salt 'clavulanic'] Found: {bySalt.Count} items in {sw3.Elapsed.TotalMilliseconds:F2}ms");
        Assert.NotEmpty(bySalt);
        Assert.Equal("Augmentin 625 Duo Tablet", bySalt[0].Name);
        Assert.True(bySalt[0].IsPrescriptionRequired); // Schedule H1
    }

    [Fact]
    public async Task Scenario2_InteractiveBarcodeScanAndCartCheckout()
    {
        await SeedRealisticPharmacyInventoryAsync();

        // Step 1: Cashier scans Dolo barcode (8901234567890)
        var doloScanned = await _searchService.ScanBarcodeAsync("8901234567890", "wh-1");
        Assert.NotNull(doloScanned);
        Assert.Equal("Dolo 650mg Tablet", doloScanned!.ProductName);

        // Step 2: Cashier searches and selects Pan-D
        var pandResults = await _searchService.SearchAsync("pan-d", "wh-1");
        Assert.NotEmpty(pandResults);
        var pand = pandResults[0];

        // Step 3: Build and commit sale command (2x Dolo @ 30.00 + 1x Pan-D @ 195.00)
        var command = new CommitSaleCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            CounterId: "Counter 1",
            WarehouseId: "wh-1",
            UserId: "Cashier John",
            DeviceId: "POS-01",
            CustomerId: null,
            CustomerName: "Walk-in Customer",
            IsInterstate: false,
            PrescriptionRef: null,
            Items: new List<CartItemInput>
            {
                new CartItemInput(doloScanned.ProductId, doloScanned.ProductName, doloScanned.BatchId, doloScanned.BatchNumber, doloScanned.ExpiryDate, 2, 30.0m, 30.5m, 12.0m),
                new CartItemInput(pand.Id, pand.Name, pand.BatchId!, pand.BatchNumber!, pand.NearestExpiryDate!.Value, 1, 195.0m, 199.0m, 12.0m)
            },
            Payments: new List<SalePaymentInput>
            {
                new SalePaymentInput(PaymentMode.Cash, 286.00m)
            }
        );

        var sw = Stopwatch.StartNew();
        var result = await _posTransactionService.ProcessSaleAsync(command);
        sw.Stop();

        _output.WriteLine($"Sale committed in {sw.Elapsed.TotalMilliseconds:F2}ms. Invoice: {result.InvoiceNo}, Total: ₹{result.TotalAmount}");
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(286.00m, result.TotalAmount); // 60 + 7.20 GST + 195 + 23.40 GST = 285.60 -> Round-off: 286.00

        // Verify Database Stock Decrements
        using var conn = await _connectionFactory.CreateConnectionAsync();
        var doloStock = await conn.ExecuteScalarAsync<decimal>("SELECT quantity FROM stock_balances WHERE batch_id = 'b_dolo_1'");
        var pandStock = await conn.ExecuteScalarAsync<decimal>("SELECT quantity FROM stock_balances WHERE batch_id = 'b_pand_1'");

        Assert.Equal(148.0m, doloStock); // 150 - 2 = 148
        Assert.Equal(79.0m, pandStock);  // 80 - 1 = 79

        // Verify Outbox Event created for LAN / Cloud sync
        var pendingEvents = await _outboxRepo.GetPendingEventsAsync();
        Assert.Single(pendingEvents);
        Assert.Equal("SALE_COMMITTED", pendingEvents[0].EventType);

        // Verify Automatic Double-Entry Accounting Posting
        var accRepo = new SqliteAccountingRepository(_connectionFactory, _outboxRepo);
        var dayBook = await accRepo.GetDayBookAsync("org-1", "branch-1", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Contains(dayBook, v => v.VoucherNumber.StartsWith($"SAL-{result.InvoiceNo}") && v.VoucherType == Domain.Accounting.VoucherType.Sales);

        var cashAcc = await accRepo.GetAccountHeadByIdAsync("acc_cash");
        var salesAcc = await accRepo.GetAccountHeadByIdAsync("acc_sales");
        Assert.NotNull(cashAcc);
        Assert.NotNull(salesAcc);
        Assert.Equal(286.00m, cashAcc.CurrentBalance);
        Assert.True(salesAcc.CurrentBalance > 0);
    }


    [Fact]
    public async Task Scenario3_ConcurrentCheckout_PreventsOverbooking()
    {
        await SeedRealisticPharmacyInventoryAsync();

        // Insulin (HM24A) has exactly 25 vials in stock
        // Simulate two parallel counters trying to buy 15 vials simultaneously (15 + 15 = 30 > 25)
        var cmd1 = new CommitSaleCommand(
            OrgId: "org-1", BranchId: "branch-1", CounterId: "Counter 1", WarehouseId: "wh-1",
            UserId: "User1", DeviceId: "DEV1", CustomerId: null, CustomerName: "Patient A",
            IsInterstate: false, PrescriptionRef: "RX101",
            Items: new List<CartItemInput>
            {
                new CartItemInput("p_insul", "Human Mixtard", "b_insul_1", "HM24A", DateTime.UtcNow.AddDays(90), 15, 175.0m, 175.0m, 5.0m)
            },
            Payments: new List<SalePaymentInput> { new SalePaymentInput(PaymentMode.Cash, 2756.0m) }
        );

        var cmd2 = new CommitSaleCommand(
            OrgId: "org-1", BranchId: "branch-1", CounterId: "Counter 2", WarehouseId: "wh-1",
            UserId: "User2", DeviceId: "DEV2", CustomerId: null, CustomerName: "Patient B",
            IsInterstate: false, PrescriptionRef: "RX102",
            Items: new List<CartItemInput>
            {
                new CartItemInput("p_insul", "Human Mixtard", "b_insul_1", "HM24A", DateTime.UtcNow.AddDays(90), 15, 175.0m, 175.0m, 5.0m)
            },
            Payments: new List<SalePaymentInput> { new SalePaymentInput(PaymentMode.Cash, 2756.0m) }
        );

        var task1 = _posTransactionService.ProcessSaleAsync(cmd1);
        var task2 = _posTransactionService.ProcessSaleAsync(cmd2);

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);
        var failCount = results.Count(r => !r.IsSuccess);

        _output.WriteLine($"Concurrent Checkout: Success={successCount}, Fail={failCount}");
        Assert.Equal(1, successCount);
        Assert.Equal(1, failCount);

        using var conn = await _connectionFactory.CreateConnectionAsync();
        var finalInsulinStock = await conn.ExecuteScalarAsync<decimal>("SELECT quantity FROM stock_balances WHERE batch_id = 'b_insul_1'");
        Assert.Equal(10.0m, finalInsulinStock); // 25 - 15 = 10 (never negative)
    }
}
