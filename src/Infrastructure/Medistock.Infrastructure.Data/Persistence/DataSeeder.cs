using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;

namespace Medistock.Infrastructure.Data.Persistence;

public interface IDataSeeder
{
    Task SeedIfEmptyAsync(CancellationToken cancellationToken = default);
}

public class DataSeeder : IDataSeeder
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public DataSeeder(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM products;");
        if (count > 0) return;

        // Seed 12 popular Indian pharmaceutical products with batches & stock
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
            ("p_beta", "Betadine 10% Solution 100ml", "Betadine", "Povidone-Iodine", "Povidone-Iodine 10% w/v", "10%", 2, 1, "BTL", "30049099", 12.0, 0, 0, 0, "8908889990001", "Win-Medicare"),
            ("p_crocin", "Crocin Advance 500mg Tablet", "Crocin", "Paracetamol", "Paracetamol 500mg fast release", "500mg", 0, 20, "TAB", "30049099", 12.0, 0, 0, 0, "8901001001001", "GSK Consumer"),
            ("p_comb", "Combiflam Tablet", "Combiflam", "Ibuprofen and Paracetamol", "Ibuprofen 400mg + Paracetamol 325mg", "725mg", 0, 20, "TAB", "30049099", 12.0, 0, 0, 0, "8902002002002", "Sanofi India")
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

        var batches = new[]
        {
            ("b_dolo_1", "p_dolo", "DL24A", DateTime.UtcNow.AddDays(180), 30.50, 22.00, 30.00, 150.0),
            ("b_dolo_2", "p_dolo", "DL24B", DateTime.UtcNow.AddDays(400), 32.00, 23.50, 32.00, 200.0),
            ("b_aug_1", "p_aug", "AG24A", DateTime.UtcNow.AddDays(120), 205.00, 155.00, 200.00, 50.0),
            ("b_pand_1", "p_pand", "PD24A", DateTime.UtcNow.AddDays(300), 199.00, 140.00, 195.00, 80.0),
            ("b_azith_1", "p_azith", "AZ24A", DateTime.UtcNow.AddDays(240), 125.00, 90.00, 120.00, 60.0),
            ("b_telma_1", "p_telma", "TL24A", DateTime.UtcNow.AddDays(365), 240.00, 180.00, 235.00, 40.0),
            ("b_insul_1", "p_insul", "HM24A", DateTime.UtcNow.AddDays(90), 175.00, 130.00, 175.00, 25.0),
            ("b_glyc_1", "p_glyc", "GL24A", DateTime.UtcNow.AddDays(280), 110.00, 80.00, 105.00, 90.0),
            ("b_mont_1", "p_mont", "MT24A", DateTime.UtcNow.AddDays(320), 160.00, 115.00, 155.00, 75.0),
            ("b_alpra_1", "p_alpra", "AL24A", DateTime.UtcNow.AddDays(210), 45.00, 30.00, 45.00, 100.0),
            ("b_beta_1", "p_beta", "BT24A", DateTime.UtcNow.AddDays(500), 140.00, 98.00, 135.00, 30.0),
            ("b_crocin_1", "p_crocin", "CR24A", DateTime.UtcNow.AddDays(365), 35.00, 24.00, 35.00, 120.0),
            ("b_comb_1", "p_comb", "CB24A", DateTime.UtcNow.AddDays(400), 48.00, 32.00, 48.00, 140.0)
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
}
