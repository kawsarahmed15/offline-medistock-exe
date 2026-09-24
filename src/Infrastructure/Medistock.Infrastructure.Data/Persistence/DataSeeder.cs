using System;
using System.Collections.Generic;
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
        if (count >= 200) return;

        var products = GetSeedProducts();

        using var tx = conn.BeginTransaction();

        foreach (var p in products)
        {
            await conn.ExecuteAsync(@"
                INSERT OR IGNORE INTO products (
                    id, org_id, name, brand_name, generic_name, composition, strength,
                    dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
                    schedule, is_prescription_required, is_cold_chain, is_narcotic,
                    is_active, primary_barcode, manufacturer_name, created_at
                ) VALUES (
                    @id, 'org-1', @name, @brand, @generic, @comp, @strength,
                    @form, @pack, @unit, @hsn, @gst,
                    @sch, @rx, @cold, @narc,
                    1, @barcode, @mfg, datetime('now')
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
                INSERT OR IGNORE INTO batches (id, product_id, org_id, batch_number, expiry_date, mrp, purchase_rate, sale_rate, created_at)
                VALUES (@id, @prodId, 'org-1', @batchNo, @expiry, @mrp, @prate, @srate, datetime('now'));

                INSERT OR IGNORE INTO stock_balances (id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at)
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

            // Optional secondary batch (e.g. for expiry testing or multi-batch tracking)
            if (!string.IsNullOrEmpty(p.BatchNo2))
            {
                var b2Id = $"b_{p.Id}_2";
                var expiry2 = DateTime.UtcNow.AddDays(p.ExpiryOffsetDays2);
                await conn.ExecuteAsync(@"
                    INSERT OR IGNORE INTO batches (id, product_id, org_id, batch_number, expiry_date, mrp, purchase_rate, sale_rate, created_at)
                    VALUES (@id, @prodId, 'org-1', @batchNo, @expiry, @mrp, @prate, @srate, datetime('now'));

                    INSERT OR IGNORE INTO stock_balances (id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at)
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
        }

        tx.Commit();
    }

    public record SeedItem(
        string Id,
        string Name,
        string BrandName,
        string GenericName,
        string Composition,
        string Strength,
        int DosageForm, // 0: TAB, 1: CAP, 2: SYR, 3: INJ, 4: OINT, 5: DROPS, 6: INH, 7: SUSP/POWDER/BOX
        int PackUnits,
        string BaseUnit,
        string HsnCode,
        double GstRatePercent,
        int Schedule, // 0: General/OTC, 1: Schedule H, 2: Schedule H1, 3: Schedule X, 4: Narcotic
        bool IsPrescriptionRequired,
        bool IsColdChain,
        bool IsNarcotic,
        string Barcode,
        string Manufacturer,
        string BatchNo1,
        int ExpiryOffsetDays1,
        double Mrp,
        double PurchaseRate,
        double SaleRate,
        double Quantity1,
        string? BatchNo2 = null,
        int ExpiryOffsetDays2 = 0,
        double Quantity2 = 0.0
    );

    public static List<SeedItem> GetSeedProducts()
    {
        var list = new List<SeedItem>(220)
        {
            // ==========================================
            // 1. ANALGESICS, ANTIPYRETICS & NSAIDs (1-20)
            // ==========================================
            new("p_dolo", "Dolo 650mg Tablet", "Dolo 650", "Paracetamol", "Paracetamol 650mg", "650mg", 0, 15, "TAB", "30049099", 12.0, 0, false, false, false, "8901234567890", "Micro Labs Ltd", "DL24A", 180, 30.50, 22.00, 30.00, 250, "DL24EXP", -15, 20),
            new("p_crocin", "Crocin Advance 500mg Tablet", "Crocin Advance", "Paracetamol", "Paracetamol 500mg Fast Release", "500mg", 0, 20, "TAB", "30049099", 12.0, 0, false, false, false, "8901001001001", "GSK Consumer", "CR24A", 365, 35.00, 24.00, 35.00, 150),
            new("p_calpol_650", "Calpol 650mg Tablet", "Calpol 650", "Paracetamol", "Paracetamol 650mg", "650mg", 0, 15, "TAB", "30049099", 12.0, 0, false, false, false, "8901001001002", "GSK Consumer", "CP24A", 240, 30.00, 21.50, 29.50, 200),
            new("p_calpol_500", "Calpol 500mg Tablet", "Calpol 500", "Paracetamol", "Paracetamol 500mg", "500mg", 0, 15, "TAB", "30049099", 12.0, 0, false, false, false, "8901001001003", "GSK Consumer", "CP24B", 300, 22.00, 15.00, 21.00, 180),
            new("p_comb", "Combiflam Tablet", "Combiflam", "Ibuprofen and Paracetamol", "Ibuprofen 400mg + Paracetamol 325mg", "725mg", 0, 20, "TAB", "30049099", 12.0, 0, false, false, false, "8902002002002", "Sanofi India", "CB24A", 400, 48.00, 32.00, 48.00, 180),
            new("p_zerodol_p", "Zerodol-P Tablet", "Zerodol-P", "Aceclofenac and Paracetamol", "Aceclofenac 100mg + Paracetamol 325mg", "425mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8903003003001", "Ipca Laboratories", "ZP24A", 280, 65.00, 46.00, 64.00, 120),
            new("p_zerodol_sp", "Zerodol-SP Tablet", "Zerodol-SP", "Aceclofenac, Paracetamol and Serratiopeptidase", "Aceclofenac 100mg + Paracetamol 325mg + Serratiopeptidase 15mg", "440mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8903003003002", "Ipca Laboratories", "ZSP24A", 200, 115.00, 80.00, 112.00, 100, "ZSP24CRIT", 22, 15),
            new("p_zerodol_th", "Zerodol-TH 4 Tablet", "Zerodol-TH 4", "Aceclofenac and Thiocolchicoside", "Aceclofenac 100mg + Thiocolchicoside 4mg", "104mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8903003003003", "Ipca Laboratories", "ZTH24A", 320, 210.00, 150.00, 205.00, 80),
            new("p_meftal_spas", "Meftal-Spas Tablet", "Meftal-Spas", "Mefenamic Acid and Dicyclomine", "Mefenamic Acid 250mg + Dicyclomine 10mg", "260mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8904004004001", "Blue Cross Labs", "MS24A", 350, 52.00, 36.00, 50.00, 140),
            new("p_meftal_forte", "Meftal Forte Tablet", "Meftal Forte", "Mefenamic Acid and Paracetamol", "Mefenamic Acid 500mg + Paracetamol 325mg", "825mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8904004004002", "Blue Cross Labs", "MF24A", 300, 60.00, 42.00, 58.00, 90),
            new("p_voveran_sr", "Voveran SR 100mg Tablet", "Voveran SR", "Diclofenac Sodium", "Diclofenac Sodium 100mg Sustained Release", "100mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8905005005001", "Novartis India", "VV24A", 365, 175.00, 125.00, 170.00, 75),
            new("p_voveran_50", "Voveran 50mg Tablet", "Voveran 50", "Diclofenac Sodium", "Diclofenac Sodium 50mg", "50mg", 0, 20, "TAB", "30049099", 12.0, 1, true, false, false, "8905005005003", "Novartis India", "VV24B", 340, 88.00, 60.00, 85.00, 110),
            new("p_dynapar_aq", "Dynapar AQ Injection 1ml", "Dynapar AQ", "Diclofenac Sodium", "Diclofenac 75mg/ml", "75mg/ml", 3, 1, "AMP", "30049099", 12.0, 1, true, false, false, "8905005005002", "Troikaa Pharma", "DN24A", 400, 32.00, 20.00, 30.00, 100),
            new("p_ultracet", "Ultracet Tablet", "Ultracet", "Tramadol and Paracetamol", "Tramadol 37.5mg + Paracetamol 325mg", "362.5mg", 0, 15, "TAB", "30049099", 12.0, 2, true, false, false, "8906006006001", "Janssen / J&J", "UC24A", 310, 260.00, 190.00, 255.00, 60),
            new("p_tramazac", "Tramazac 50mg Capsule", "Tramazac", "Tramadol Hydrochloride", "Tramadol HCl 50mg", "50mg", 1, 10, "CAP", "30049099", 12.0, 2, true, false, false, "8906006006002", "Zydus Healthcare", "TZ24A", 280, 85.00, 58.00, 82.00, 50),
            new("p_ketorol_dt", "Ketorol-DT Tablet", "Ketorol-DT", "Ketorolac Tromethamine", "Ketorolac Tromethamine 10mg Dispersible", "10mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8907007007001", "Dr. Reddy's", "KT24A", 330, 140.00, 95.00, 136.00, 70),
            new("p_disprin", "Disprin Regular 350mg Tablet", "Disprin", "Aspirin", "Acetylsalicylic Acid 350mg Soluble", "350mg", 0, 10, "TAB", "30049099", 12.0, 0, false, false, false, "8908008008001", "Reckitt Benckiser", "DP24A", 450, 14.00, 9.50, 14.00, 300),
            new("p_sumo", "Sumo Tablet", "Sumo", "Nimesulide and Paracetamol", "Nimesulide 100mg + Paracetamol 325mg", "425mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8909009009001", "Alkem Laboratories", "SM24A", 290, 130.00, 90.00, 125.00, 95),
            new("p_brufen_400", "Brufen 400mg Tablet", "Brufen 400", "Ibuprofen", "Ibuprofen 400mg", "400mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901011011011", "Abbott India", "BF24A", 360, 22.00, 15.00, 21.00, 110),
            new("p_flexon", "Flexon Tablet", "Flexon", "Ibuprofen and Paracetamol", "Ibuprofen 400mg + Paracetamol 325mg", "725mg", 0, 15, "TAB", "30049099", 12.0, 0, false, false, false, "8901021021021", "Aristo Pharma", "FX24A", 300, 32.00, 21.00, 31.00, 140),

            // ==========================================
            // 2. ANTIBIOTICS & ANTIMICROBIALS (21-45)
            // ==========================================
            new("p_aug", "Augmentin 625 Duo Tablet", "Augmentin", "Amoxicillin and Clavulanate", "Amoxicillin 500mg + Clavulanic Acid 125mg", "625mg", 0, 10, "TAB", "30041010", 12.0, 1, true, false, false, "8909876543210", "GSK Pharmaceuticals", "AG24A", 120, 205.00, 155.00, 200.00, 80, "AG24CRIT", 18, 15),
            new("p_aug_375", "Augmentin 375 Tablet", "Augmentin 375", "Amoxicillin and Clavulanate", "Amoxicillin 250mg + Clavulanic Acid 125mg", "375mg", 0, 10, "TAB", "30041010", 12.0, 1, true, false, false, "8909876543211", "GSK Pharmaceuticals", "AG24B", 240, 155.00, 110.00, 150.00, 60),
            new("p_clavam_625", "Clavam 625 Tablet", "Clavam 625", "Amoxicillin and Clavulanate", "Amoxicillin 500mg + Potassium Clavulanate 125mg", "625mg", 0, 10, "TAB", "30041010", 12.0, 1, true, false, false, "8901112223331", "Alkem Laboratories", "CV24A", 240, 204.00, 150.00, 198.00, 90),
            new("p_moxikind_cv", "Moxikind-CV 625 Tablet", "Moxikind-CV", "Amoxicillin and Clavulanic Acid", "Amoxicillin 500mg + Clavulanic Acid 125mg", "625mg", 0, 10, "TAB", "30041010", 12.0, 1, true, false, false, "8901112223332", "Mankind Pharma", "MK24A", 270, 185.00, 130.00, 180.00, 110),
            new("p_azith", "Azithral 500mg Tablet", "Azithral 500", "Azithromycin", "Azithromycin 500mg", "500mg", 0, 5, "TAB", "30042010", 12.0, 2, true, false, false, "8902223334445", "Alembic Pharmaceuticals", "AZ24A", 240, 125.00, 90.00, 120.00, 85),
            new("p_azithral_250", "Azithral 250mg Tablet", "Azithral 250", "Azithromycin", "Azithromycin 250mg", "250mg", 0, 6, "TAB", "30042010", 12.0, 2, true, false, false, "8902223334448", "Alembic Pharmaceuticals", "AZ24C", 290, 80.00, 55.00, 78.00, 70),
            new("p_azee_500", "Azee 500mg Tablet", "Azee 500", "Azithromycin", "Azithromycin 500mg", "500mg", 0, 5, "TAB", "30042010", 12.0, 2, true, false, false, "8902223334446", "Cipla Ltd", "AZ24B", 300, 124.00, 88.00, 119.00, 100),
            new("p_zithrox_500", "Zithrox 500mg Tablet", "Zithrox 500", "Azithromycin", "Azithromycin 500mg", "500mg", 0, 3, "TAB", "30042010", 12.0, 2, true, false, false, "8902223334447", "Macleods Pharma", "ZX24A", 330, 75.00, 52.00, 72.00, 70),
            new("p_cifran_500", "Cifran 500mg Tablet", "Cifran 500", "Ciprofloxacin", "Ciprofloxacin 500mg", "500mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8903334445551", "Sun Pharma", "CF24A", 360, 45.00, 30.00, 42.00, 80),
            new("p_ciplox_500", "Ciplox 500mg Tablet", "Ciplox 500", "Ciprofloxacin", "Ciprofloxacin 500mg", "500mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8903334445552", "Cipla Ltd", "CX24A", 310, 44.00, 29.00, 41.00, 95),
            new("p_oflox_200", "Oflox 200mg Tablet", "Oflox 200", "Ofloxacin", "Ofloxacin 200mg", "200mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8904445556661", "Cipla Ltd", "OF24A", 280, 78.00, 52.00, 75.00, 65),
            new("p_zanocin_200", "Zanocin 200mg Tablet", "Zanocin", "Ofloxacin", "Ofloxacin 200mg", "200mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8904445556662", "Sun Pharma", "ZN24A", 350, 82.00, 55.00, 79.00, 50),
            new("p_o2_tab", "O2 Tablet", "O2", "Ofloxacin and Ornidazole", "Ofloxacin 200mg + Ornidazole 500mg", "700mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8905556667771", "Medley Pharma", "O224A", 250, 155.00, 105.00, 150.00, 110),
            new("p_taxim_o_200", "Taxim-O 200mg Tablet", "Taxim-O 200", "Cefixime", "Cefixime 200mg", "200mg", 0, 10, "TAB", "30042099", 12.0, 2, true, false, false, "8906667778881", "Alkem Laboratories", "TX24A", 300, 178.00, 125.00, 172.00, 90),
            new("p_mahacef_200", "Mahacef 200mg Tablet", "Mahacef 200", "Cefixime", "Cefixime 200mg", "200mg", 0, 10, "TAB", "30042099", 12.0, 2, true, false, false, "8906667778882", "Mankind Pharma", "MC24A", 270, 165.00, 115.00, 160.00, 80),
            new("p_monocef_o", "Monocef-O 200mg Tablet", "Monocef-O", "Cefpodoxime Proxetil", "Cefpodoxime Proxetil 200mg", "200mg", 0, 10, "TAB", "30042099", 12.0, 2, true, false, false, "8907778889991", "Aristo Pharma", "MO24A", 320, 230.00, 165.00, 222.00, 70),
            new("p_gudcef_200", "Gudcef 200mg Tablet", "Gudcef 200", "Cefpodoxime Proxetil", "Cefpodoxime Proxetil 200mg", "200mg", 0, 10, "TAB", "30042099", 12.0, 2, true, false, false, "8907778889992", "Mankind Pharma", "GC24A", 260, 215.00, 150.00, 208.00, 60),
            new("p_ceftum_500", "Ceftum 500mg Tablet", "Ceftum 500", "Cefuroxime Axetil", "Cefuroxime Axetil 500mg", "500mg", 0, 4, "TAB", "30042099", 12.0, 2, true, false, false, "8907778889993", "GSK Pharma", "CT24C", 310, 390.00, 280.00, 375.00, 50),
            new("p_doxy_1", "Doxy-1 L-DR Forte Capsule", "Doxy-1 Forte", "Doxycycline and Lactobacillus", "Doxycycline 100mg + L. rhamnosus 5B Spores", "100mg", 1, 10, "CAP", "30049099", 12.0, 1, true, false, false, "8908889990002", "USV Private Limited", "DX24A", 365, 130.00, 90.00, 125.00, 85),
            new("p_norflox_tz", "Norflox-TZ Tablet", "Norflox-TZ", "Norfloxacin and Tinidazole", "Norfloxacin 400mg + Tinidazole 600mg", "1000mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8909990001111", "Cipla Ltd", "NF24A", 280, 105.00, 72.00, 100.00, 90),
            new("p_flagyl_400", "Flagyl 400mg Tablet", "Flagyl 400", "Metronidazole", "Metronidazole 400mg", "400mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901122334455", "Abbott India", "FL24A", 420, 25.00, 16.00, 24.00, 140),
            new("p_metrogyl_400", "Metrogyl 400mg Tablet", "Metrogyl 400", "Metronidazole", "Metronidazole 400mg", "400mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901122334456", "J.B. Chemicals", "MG24A", 380, 24.50, 15.50, 23.50, 160),
            new("p_bactrim_ds", "Bactrim DS Tablet", "Bactrim DS", "Sulfamethoxazole and Trimethoprim", "Sulfamethoxazole 800mg + Trimethoprim 160mg", "960mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8902233445566", "Abbott India", "BC24A", 350, 30.00, 20.00, 29.00, 75),
            new("p_levomac_500", "Levomac 500mg Tablet", "Levomac 500", "Levofloxacin", "Levofloxacin 500mg", "500mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8903344556677", "Macleods Pharma", "LM24A", 290, 110.00, 75.00, 105.00, 60),
            new("p_linid_600", "Linid 600mg Tablet", "Linid 600", "Linezolid", "Linezolid 600mg", "600mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8904455667788", "Cadila Pharma", "LN24A", 310, 380.00, 270.00, 365.00, 40),

            // ==========================================
            // 3. ANTACIDS, PPIs & GASTROINTESTINAL (46-75)
            // ==========================================
            new("p_pand", "Pan-D Capsule", "Pan-D", "Pantoprazole and Domperidone", "Pantoprazole 40mg + Domperidone 30mg SR", "70mg", 1, 15, "CAP", "30049099", 12.0, 1, true, false, false, "8901112223334", "Alkem Laboratories", "PD24A", 300, 199.00, 140.00, 195.00, 150, "PD24WARN", 55, 30),
            new("p_pan_40", "Pan 40mg Tablet", "Pan 40", "Pantoprazole", "Pantoprazole Sodium 40mg", "40mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901112223335", "Alkem Laboratories", "P424A", 365, 155.00, 110.00, 150.00, 140),
            new("p_pan_20", "Pan 20mg Tablet", "Pan 20", "Pantoprazole", "Pantoprazole 20mg", "20mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901112223338", "Alkem Laboratories", "P424B", 340, 95.00, 68.00, 92.00, 80),
            new("p_pantocid_40", "Pantocid 40mg Tablet", "Pantocid 40", "Pantoprazole", "Pantoprazole 40mg", "40mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901112223336", "Sun Pharma", "PC24A", 320, 160.00, 115.00, 155.00, 100),
            new("p_pantocid_dsr", "Pantocid DSR Capsule", "Pantocid DSR", "Pantoprazole and Domperidone SR", "Pantoprazole 40mg + Domperidone 30mg", "70mg", 1, 15, "CAP", "30049099", 12.0, 1, true, false, false, "8901112223337", "Sun Pharma", "PDS24A", 290, 210.00, 150.00, 205.00, 85),
            new("p_pantocid_it", "Pantocid-IT Capsule", "Pantocid-IT", "Pantoprazole and Itopride", "Pantoprazole 40mg + Itopride 150mg SR", "190mg", 1, 10, "CAP", "30049099", 12.0, 1, true, false, false, "8901112223339", "Sun Pharma", "PIT24A", 300, 175.00, 125.00, 170.00, 50),
            new("p_razo_d", "Razo-D Capsule", "Razo-D", "Rabeprazole and Domperidone", "Rabeprazole Sodium 20mg + Domperidone 30mg", "50mg", 1, 15, "CAP", "30049099", 12.0, 1, true, false, false, "8902223334448", "Dr. Reddy's", "RD24A", 340, 240.00, 175.00, 235.00, 75),
            new("p_razo_20", "Razo 20mg Tablet", "Razo 20", "Rabeprazole", "Rabeprazole Sodium 20mg", "20mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8902223334449", "Dr. Reddy's", "RZ24A", 310, 180.00, 130.00, 175.00, 60),
            new("p_rabicip_20", "Rabicip 20mg Tablet", "Rabicip 20", "Rabeprazole", "Rabeprazole Sodium 20mg", "20mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8902223334450", "Cipla Ltd", "RC24A", 365, 125.00, 88.00, 120.00, 90),
            new("p_omez_20", "Omez 20mg Capsule", "Omez 20", "Omeprazole", "Omeprazole 20mg", "20mg", 1, 20, "CAP", "30049099", 12.0, 1, true, false, false, "8903334445557", "Dr. Reddy's", "OM24A", 365, 120.00, 85.00, 115.00, 130),
            new("p_omez_d", "Omez-D Capsule", "Omez-D", "Omeprazole and Domperidone", "Omeprazole 20mg + Domperidone 10mg", "30mg", 1, 15, "CAP", "30049099", 12.0, 1, true, false, false, "8903334445558", "Dr. Reddy's", "OD24A", 300, 160.00, 115.00, 155.00, 90),
            new("p_sompraz_40", "Sompraz 40mg Tablet", "Sompraz 40", "Esomeprazole", "Esomeprazole Magnesium 40mg", "40mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8904445556668", "Sun Pharma", "SP24A", 350, 195.00, 140.00, 190.00, 70),
            new("p_sompraz_d", "Sompraz-D 40 Capsule", "Sompraz-D", "Esomeprazole and Domperidone", "Esomeprazole 40mg + Domperidone 30mg", "70mg", 1, 15, "CAP", "30049099", 12.0, 1, true, false, false, "8904445556669", "Sun Pharma", "SD24A", 280, 230.00, 165.00, 225.00, 65),
            new("p_nexpro_rd", "Nexpro-RD 40 Capsule", "Nexpro-RD", "Esomeprazole and Domperidone SR", "Esomeprazole 40mg + Domperidone 30mg SR", "70mg", 1, 15, "CAP", "30049099", 12.0, 1, true, false, false, "8905556667779", "Torrent Pharma", "NR24A", 330, 225.00, 160.00, 220.00, 75),
            new("p_aciloc_150", "Aciloc 150mg Tablet", "Aciloc 150", "Ranitidine", "Ranitidine Hydrochloride 150mg", "150mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8906667778890", "Cadila Pharma", "AC24A", 400, 42.00, 28.00, 40.00, 160),
            new("p_aciloc_300", "Aciloc 300mg Tablet", "Aciloc 300", "Ranitidine", "Ranitidine Hydrochloride 300mg", "300mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8906667778891", "Cadila Pharma", "AC24B", 380, 75.00, 50.00, 72.00, 80),
            new("p_rantac_150", "Rantac 150mg Tablet", "Rantac 150", "Ranitidine", "Ranitidine Hydrochloride 150mg", "150mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8907778889901", "J.B. Chemicals", "RT24A", 365, 45.00, 30.00, 44.00, 120),
            new("p_sucrafil_susp", "Sucrafil Oral Suspension 200ml", "Sucrafil", "Sucralfate", "Sucralfate 1000mg/10ml", "200ml", 2, 1, "BTL", "30049099", 12.0, 1, true, false, false, "8907778889902", "Fourrts Laboratories", "SF24B", 340, 225.00, 165.00, 220.00, 60),
            new("p_sucrafil_o", "Sucrafil-O Gel 200ml", "Sucrafil-O", "Sucralfate and Oxetacaine", "Sucralfate 1000mg + Oxetacaine 20mg / 10ml", "200ml", 2, 1, "BTL", "30049099", 12.0, 1, true, false, false, "8907778889903", "Fourrts Laboratories", "SFO24A", 320, 260.00, 190.00, 252.00, 55),
            new("p_digene_gel", "Digene Antacid Gel Mint 200ml", "Digene Gel", "Antacid Suspension", "Magnesium Hydroxide + Aluminium Hydroxide + Simethicone", "200ml", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8908889990012", "Abbott India", "DG24A", 450, 165.00, 120.00, 160.00, 80),
            new("p_gelusil_mps", "Gelusil MPS Antacid Liquid 200ml", "Gelusil MPS", "Antacid Suspension", "Aluminium Hydroxide + Dimethicone + Magnesium", "200ml", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8909990001123", "Pfizer", "GS24A", 400, 140.00, 100.00, 138.00, 65),
            new("p_mucaine_gel", "Mucaine Gel Mint 200ml", "Mucaine Gel", "Oxetacaine and Antacids", "Oxetacaine 10mg + Aluminium Hydroxide + Magnesium", "200ml", 2, 1, "BTL", "30049099", 12.0, 1, true, false, false, "8901113334455", "Pfizer", "MG24B", 310, 215.00, 160.00, 210.00, 50),
            new("p_cremaffin_plus", "Cremaffin Plus Syrup 225ml", "Cremaffin Plus", "Laxative Emulsion", "Liquid Paraffin + Milk of Magnesia + Sodium Picosulfate", "225ml", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8902224445566", "Abbott India", "CR24B", 365, 275.00, 205.00, 270.00, 55),
            new("p_duphalac_syr", "Duphalac Oral Solution 150ml", "Duphalac", "Lactulose", "Lactulose 3.335g/5ml", "150ml", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8903335556677", "Abbott India", "DH24A", 450, 310.00, 235.00, 305.00, 45),
            new("p_cyclopam", "Cyclopam Tablet", "Cyclopam", "Dicyclomine and Paracetamol", "Dicyclomine HCl 20mg + Paracetamol 500mg", "520mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8904446667788", "Indoco Remedies", "CY24A", 300, 58.00, 39.00, 56.00, 90),
            new("p_buscopan", "Buscopan 10mg Tablet", "Buscopan", "Hyoscine Butylbromide", "Hyoscine Butylbromide 10mg", "10mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8905557778899", "Sanofi India", "BP24A", 365, 45.00, 31.00, 43.00, 70),
            new("p_eldoper", "Eldoper 2mg Capsule", "Eldoper", "Loperamide", "Loperamide Hydrochloride 2mg", "2mg", 1, 10, "CAP", "30049099", 12.0, 1, true, false, false, "8906668889900", "Micro Labs Ltd", "EL24A", 400, 28.00, 18.00, 27.00, 120),
            new("p_ondem_4", "Ondem 4mg Tablet", "Ondem 4", "Ondansetron", "Ondansetron 4mg Fast Dissolving", "4mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8907779990011", "Alkem Laboratories", "ON24A", 350, 55.00, 37.00, 53.00, 130),
            new("p_ondem_md", "Ondem-MD 4 Tablet", "Ondem-MD", "Ondansetron MD", "Ondansetron 4mg Mouth Dissolving", "4mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8907779990012", "Alkem Laboratories", "OMD24A", 380, 58.00, 39.00, 56.00, 110),
            new("p_econorm", "Econorm Sachet 250mg", "Econorm", "Saccharomyces Boulardii", "Saccharomyces Boulardii 250mg Probiotic", "250mg", 7, 6, "SCT", "30049099", 5.0, 0, false, false, false, "8901114445566", "Dr. Reddy's", "EC24A", 280, 310.00, 230.00, 300.00, 60),

            // ==========================================
            // 4. CARDIOVASCULAR & HYPERTENSION (76-105)
            // ==========================================
            new("p_telma", "Telma 40mg Tablet", "Telma 40", "Telmisartan", "Telmisartan 40mg", "40mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8903334445556", "Glenmark Pharmaceuticals", "TL24A", 365, 240.00, 180.00, 235.00, 120),
            new("p_telma_20", "Telma 20mg Tablet", "Telma 20", "Telmisartan", "Telmisartan 20mg", "20mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8903334445561", "Glenmark Pharmaceuticals", "TL24B", 350, 140.00, 100.00, 136.00, 80),
            new("p_telma_80", "Telma 80mg Tablet", "Telma 80", "Telmisartan", "Telmisartan 80mg", "80mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8903334445562", "Glenmark Pharmaceuticals", "TL24C", 320, 395.00, 290.00, 385.00, 60),
            new("p_telma_h", "Telma-H Tablet", "Telma-H", "Telmisartan and Hydrochlorothiazide", "Telmisartan 40mg + Hydrochlorothiazide 12.5mg", "52.5mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8903334445559", "Glenmark Pharmaceuticals", "TH24A", 340, 320.00, 240.00, 310.00, 80),
            new("p_telma_am", "Telma-AM Tablet", "Telma-AM", "Telmisartan and Amlodipine", "Telmisartan 40mg + Amlodipine 5mg", "45mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8903334445560", "Glenmark Pharmaceuticals", "TAM24A", 300, 340.00, 255.00, 330.00, 75),
            new("p_telmikind_40", "Telmikind 40mg Tablet", "Telmikind 40", "Telmisartan", "Telmisartan 40mg", "40mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8904447778899", "Mankind Pharma", "TK24A", 350, 48.00, 32.00, 46.00, 100),
            new("p_cilacar_10", "Cilacar 10mg Tablet", "Cilacar 10", "Cilnidipine", "Cilnidipine 10mg", "10mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8905558889900", "J.B. Chemicals", "CL24A", 380, 165.00, 120.00, 160.00, 95),
            new("p_cilacar_20", "Cilacar 20mg Tablet", "Cilacar 20", "Cilnidipine", "Cilnidipine 20mg", "20mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8905558889902", "J.B. Chemicals", "CL24B", 330, 280.00, 210.00, 275.00, 50),
            new("p_cilacar_t", "Cilacar-T Tablet", "Cilacar-T", "Cilnidipine and Telmisartan", "Cilnidipine 10mg + Telmisartan 40mg", "50mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8905558889901", "J.B. Chemicals", "CT24A", 320, 245.00, 180.00, 238.00, 70),
            new("p_amlong_5", "Amlong 5mg Tablet", "Amlong 5", "Amlodipine", "Amlodipine Besylate 5mg", "5mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8906669990011", "Micro Labs Ltd", "AL24B", 365, 42.00, 28.00, 40.00, 110),
            new("p_amlong_at", "Amlong-A Tablet", "Amlong-A", "Amlodipine and Atenolol", "Amlodipine 5mg + Atenolol 50mg", "55mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8906669990012", "Micro Labs Ltd", "ALA24A", 340, 78.00, 52.00, 75.00, 85),
            new("p_stamlo_5", "Stamlo 5mg Tablet", "Stamlo 5", "S-Amlodipine", "S-Amlodipine Besylate 2.5mg / Amlodipine 5mg", "5mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8907770001122", "Dr. Reddy's", "ST24A", 300, 98.00, 70.00, 95.00, 60),
            new("p_aten_50", "Aten 50mg Tablet", "Aten 50", "Atenolol", "Atenolol 50mg", "50mg", 0, 14, "TAB", "30049099", 12.0, 1, true, false, false, "8908881112233", "Zydus Healthcare", "AT24A", 350, 48.00, 32.00, 46.00, 85),
            new("p_aten_25", "Aten 25mg Tablet", "Aten 25", "Atenolol", "Atenolol 25mg", "25mg", 0, 14, "TAB", "30049099", 12.0, 1, true, false, false, "8908881112235", "Zydus Healthcare", "AT24F", 365, 32.00, 21.00, 30.50, 70),
            new("p_metolar_xr_50", "Metolar-XR 50mg Tablet", "Metolar-XR 50", "Metoprolol Succinate", "Metoprolol Succinate ER 50mg", "50mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8909992223344", "Cipla Ltd", "MX24A", 310, 175.00, 125.00, 170.00, 90),
            new("p_metolar_xr_25", "Metolar-XR 25mg Tablet", "Metolar-XR 25", "Metoprolol Succinate", "Metoprolol Succinate ER 25mg", "25mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8909992223345", "Cipla Ltd", "MX24B", 330, 105.00, 72.00, 100.00, 75),
            new("p_betaloc_50", "Betaloc 50mg Tablet", "Betaloc 50", "Metoprolol Tartrate", "Metoprolol Tartrate 50mg", "50mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8901115556677", "AstraZeneca", "BL24A", 365, 185.00, 135.00, 180.00, 65),
            new("p_concor_5", "Concor 5mg Tablet", "Concor 5", "Bisoprolol Fumarate", "Bisoprolol Fumarate 5mg", "5mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8902226667788", "Merck Ltd", "CC24A", 340, 112.00, 80.00, 108.00, 70),
            new("p_cardivas_3", "Cardivas 3.125mg Tablet", "Cardivas 3.125", "Carvedilol", "Carvedilol 3.125mg", "3.125mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8903337778899", "Sun Pharma", "CD24A", 300, 65.00, 44.00, 62.00, 50),
            new("p_cardivas_6", "Cardivas 6.25mg Tablet", "Cardivas 6.25", "Carvedilol", "Carvedilol 6.25mg", "6.25mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8903337778900", "Sun Pharma", "CD24B", 310, 95.00, 65.00, 92.00, 45),
            new("p_envas_5", "Envas 5mg Tablet", "Envas 5", "Enalapril Maleate", "Enalapril Maleate 5mg", "5mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8904448889900", "Cadila Pharma", "EV24A", 365, 52.00, 36.00, 50.00, 80),
            new("p_cardace_5", "Cardace 5mg Tablet", "Cardace 5", "Ramipril", "Ramipril 5mg", "5mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8905559990011", "Sanofi India", "CR24C", 380, 175.00, 125.00, 170.00, 60),
            new("p_cardace_25", "Cardace 2.5mg Tablet", "Cardace 2.5", "Ramipril", "Ramipril 2.5mg", "2.5mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8905559990012", "Sanofi India", "CR24D", 365, 110.00, 78.00, 106.00, 50),
            new("p_losar_50", "Losar 50mg Tablet", "Losar 50", "Losartan Potassium", "Losartan Potassium 50mg", "50mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8906660001122", "Unichem Labs", "LS24A", 350, 140.00, 98.00, 135.00, 75),
            new("p_losar_h", "Losar-H Tablet", "Losar-H", "Losartan and Hydrochlorothiazide", "Losartan 50mg + Hydrochlorothiazide 12.5mg", "62.5mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8906660001123", "Unichem Labs", "LSH24A", 330, 185.00, 132.00, 180.00, 60),
            new("p_ecosprin_75", "Ecosprin 75mg Tablet", "Ecosprin 75", "Aspirin Enteric Coated", "Aspirin Gastro-resistant 75mg", "75mg", 0, 14, "TAB", "30049099", 12.0, 1, true, false, false, "8907771112233", "USV Private Limited", "EC24B", 450, 6.00, 3.80, 5.80, 400),
            new("p_ecosprin_150", "Ecosprin 150mg Tablet", "Ecosprin 150", "Aspirin Enteric Coated", "Aspirin Gastro-resistant 150mg", "150mg", 0, 14, "TAB", "30049099", 12.0, 1, true, false, false, "8907771112234", "USV Private Limited", "EC24C", 400, 9.50, 6.00, 9.20, 300),
            new("p_clavix_75", "Clavix 75mg Tablet", "Clavix 75", "Clopidogrel", "Clopidogrel Bisulfate 75mg", "75mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8908882223344", "Sun Pharma", "CX24B", 320, 185.00, 132.00, 180.00, 85),
            new("p_brilinta_90", "Brilinta 90mg Tablet", "Brilinta 90", "Ticagrelor", "Ticagrelor 90mg", "90mg", 0, 14, "TAB", "30049099", 12.0, 1, true, false, false, "8909993334455", "AstraZeneca", "BR24A", 280, 490.00, 380.00, 480.00, 40),
            new("p_atorva_10", "Atorva 10mg Tablet", "Atorva 10", "Atorvastatin", "Atorvastatin Calcium 10mg", "10mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901116667788", "Zydus Healthcare", "AT24B", 365, 110.00, 75.00, 105.00, 120),

            // ==========================================
            // 5. ANTIDIABETICS & ENDOCRINE (106-130)
            // ==========================================
            new("p_glycomet_500", "Glycomet 500mg Tablet", "Glycomet 500", "Metformin", "Metformin Hydrochloride 500mg", "500mg", 0, 20, "TAB", "30049099", 12.0, 1, true, false, false, "8904445556666", "USV Private Limited", "GM24A", 365, 38.00, 25.00, 36.00, 200),
            new("p_glycomet_850", "Glycomet 850mg Tablet", "Glycomet 850", "Metformin", "Metformin 850mg SR", "850mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8904445556671", "USV Private Limited", "GM24C", 340, 52.00, 36.00, 50.00, 120),
            new("p_glyc", "Glycomet-GP 2 Tablet", "Glycomet-GP 2", "Glimepiride and Metformin", "Glimepiride 2mg + Metformin 500mg SR", "502mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8904445556667", "USV Private Limited", "GL24A", 280, 110.00, 80.00, 105.00, 150),
            new("p_glycomet_gp1", "Glycomet-GP 1 Tablet", "Glycomet-GP 1", "Glimepiride and Metformin", "Glimepiride 1mg + Metformin 500mg SR", "501mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8904445556670", "USV Private Limited", "GL24B", 300, 95.00, 68.00, 92.00, 110),
            new("p_januvia_100", "Januvia 100mg Tablet", "Januvia 100", "Sitagliptin", "Sitagliptin Phosphate 100mg", "100mg", 0, 7, "TAB", "30049099", 12.0, 1, true, false, false, "8905550001122", "MSD Pharma", "JN24A", 350, 345.00, 260.00, 335.00, 50),
            new("p_janumet_50_500", "Janumet 50/500mg Tablet", "Janumet", "Sitagliptin and Metformin", "Sitagliptin 50mg + Metformin 500mg", "550mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8905550001123", "MSD Pharma", "JM24A", 320, 480.00, 370.00, 470.00, 45),
            new("p_galvus_50", "Galvus 50mg Tablet", "Galvus 50", "Vildagliptin", "Vildagliptin 50mg", "50mg", 0, 14, "TAB", "30049099", 12.0, 1, true, false, false, "8906661112233", "Novartis India", "GV24A", 365, 320.00, 240.00, 310.00, 60),
            new("p_galvus_met", "Galvus Met 50/500mg Tablet", "Galvus Met", "Vildagliptin and Metformin", "Vildagliptin 50mg + Metformin 500mg", "550mg", 0, 14, "TAB", "30049099", 12.0, 1, true, false, false, "8906661112234", "Novartis India", "GM24B", 300, 360.00, 275.00, 350.00, 55),
            new("p_forxiga_10", "Forxiga 10mg Tablet", "Forxiga 10", "Dapagliflozin", "Dapagliflozin Propanediol 10mg", "10mg", 0, 14, "TAB", "30049099", 12.0, 1, true, false, false, "8907772223344", "AstraZeneca", "FX24B", 340, 850.00, 660.00, 830.00, 35),
            new("p_jardiance_10", "Jardiance 10mg Tablet", "Jardiance 10", "Empagliflozin", "Empagliflozin 10mg", "10mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8908883334455", "Boehringer Ingelheim", "JD24A", 365, 590.00, 460.00, 580.00, 40),
            new("p_jardiance_25", "Jardiance 25mg Tablet", "Jardiance 25", "Empagliflozin", "Empagliflozin 25mg", "25mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8908883334456", "Boehringer Ingelheim", "JD24B", 310, 690.00, 540.00, 675.00, 30),
            new("p_amaryl_1", "Amaryl 1mg Tablet", "Amaryl 1", "Glimepiride", "Glimepiride 1mg", "1mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8909994445566", "Sanofi India", "AM24A", 365, 140.00, 100.00, 135.00, 70),
            new("p_amaryl_2", "Amaryl 2mg Tablet", "Amaryl 2", "Glimepiride", "Glimepiride 2mg", "2mg", 0, 30, "TAB", "30049099", 12.0, 1, true, false, false, "8909994445567", "Sanofi India", "AM24B", 330, 220.00, 160.00, 215.00, 65),
            new("p_thyronorm_50", "Thyronorm 50mcg Tablet", "Thyronorm 50", "Thyroxine Sodium", "Thyroxine Sodium 50mcg", "50mcg", 0, 120, "BTL", "30049099", 12.0, 1, true, false, false, "8901117778899", "Abbott India", "TN24A", 450, 175.00, 128.00, 170.00, 140),
            new("p_thyronorm_100", "Thyronorm 100mcg Tablet", "Thyronorm 100", "Thyroxine Sodium", "Thyroxine Sodium 100mcg", "100mcg", 0, 120, "BTL", "30049099", 12.0, 1, true, false, false, "8901117778800", "Abbott India", "TN24B", 400, 195.00, 142.00, 190.00, 120),
            new("p_thyronorm_25", "Thyronorm 25mcg Tablet", "Thyronorm 25", "Thyroxine Sodium", "Thyroxine Sodium 25mcg", "25mcg", 0, 120, "BTL", "30049099", 12.0, 1, true, false, false, "8901117778801", "Abbott India", "TN24C", 420, 160.00, 115.00, 155.00, 80),
            new("p_thyronorm_75", "Thyronorm 75mcg Tablet", "Thyronorm 75", "Thyroxine Sodium", "Thyroxine Sodium 75mcg", "75mcg", 0, 120, "BTL", "30049099", 12.0, 1, true, false, false, "8901117778802", "Abbott India", "TN24D", 390, 185.00, 135.00, 180.00, 75),
            new("p_euthyrox_50", "Euthyrox 50mcg Tablet", "Euthyrox 50", "Levothyroxine Sodium", "Levothyroxine Sodium 50mcg", "50mcg", 0, 100, "BTL", "30049099", 12.0, 1, true, false, false, "8902228889900", "Merck Ltd", "ET24A", 380, 160.00, 118.00, 155.00, 90),
            new("p_insul", "Human Mixtard 30/70 100IU/ml", "Mixtard 30/70", "Insulin Human", "Biphasic Isophane Insulin 100IU/ml", "100IU/ml", 3, 1, "VIAL", "30043110", 5.0, 1, true, true, false, "8907778889990", "Novo Nordisk", "HM24A", 75, 175.00, 130.00, 175.00, 80),
            new("p_lantus_100", "Lantus 100IU/ml Solostar Pen", "Lantus Solostar", "Insulin Glargine", "Insulin Glargine 100IU/ml", "100IU/ml", 3, 1, "PEN", "30043110", 5.0, 1, true, true, false, "8903339990011", "Sanofi India", "LT24A", 180, 720.00, 560.00, 700.00, 30),
            new("p_novorapid", "Novorapid 100IU/ml Flexpen", "Novorapid", "Insulin Aspart", "Insulin Aspart 100IU/ml Rapid Acting", "100IU/ml", 3, 1, "PEN", "30043110", 5.0, 1, true, true, false, "8904440001122", "Novo Nordisk", "NR24B", 150, 680.00, 530.00, 665.00, 25),
            new("p_teneligliptin", "Ziten 20mg Tablet", "Ziten 20", "Teneligliptin", "Teneligliptin Hydrobromide 20mg", "20mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8904440001123", "Glenmark Pharmaceuticals", "ZT24A", 330, 145.00, 102.00, 140.00, 60),
            new("p_voglibose_03", "Volibo 0.3mg Tablet", "Volibo 0.3", "Voglibose", "Voglibose 0.3mg Mouth Dissolving", "0.3mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8904440001124", "Sun Pharma", "VB24A", 360, 110.00, 78.00, 106.00, 50),
            new("p_trajenta_5", "Trajenta 5mg Tablet", "Trajenta", "Linagliptin", "Linagliptin 5mg", "5mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8904440001125", "Boehringer Ingelheim", "TJ24A", 350, 520.00, 410.00, 510.00, 35),
            new("p_glucobay_50", "Glucobay 50mg Tablet", "Glucobay", "Acarbose", "Acarbose 50mg", "50mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8904440001126", "Bayer India", "GB24B", 310, 135.00, 95.00, 130.00, 40),

            // ==========================================
            // 6. RESPIRATORY & ANTIALLERGICS (131-155)
            // ==========================================
            new("p_mont", "Montair-LC Tablet", "Montair-LC", "Montelukast and Levocetirizine", "Montelukast 10mg + Levocetirizine 5mg", "15mg", 0, 10, "TAB", "30049099", 12.0, 0, false, false, false, "8905556667778", "Cipla Ltd", "MT24A", 320, 160.00, 115.00, 155.00, 140),
            new("p_monticope", "Monticope Tablet", "Monticope", "Montelukast and Levocetirizine", "Montelukast 10mg + Levocetirizine 5mg", "15mg", 0, 10, "TAB", "30049099", 12.0, 0, false, false, false, "8905556667780", "Mankind Pharma", "MC24B", 300, 125.00, 88.00, 120.00, 110),
            new("p_allegra_120", "Allegra 120mg Tablet", "Allegra 120", "Fexofenadine Hydrochloride", "Fexofenadine HCl 120mg", "120mg", 0, 10, "TAB", "30049099", 12.0, 0, false, false, false, "8906662223344", "Sanofi India", "AG124A", 365, 195.00, 142.00, 190.00, 95),
            new("p_allegra_180", "Allegra 180mg Tablet", "Allegra 180", "Fexofenadine Hydrochloride", "Fexofenadine HCl 180mg", "180mg", 0, 10, "TAB", "30049099", 12.0, 0, false, false, false, "8906662223345", "Sanofi India", "AG124B", 350, 240.00, 175.00, 235.00, 75),
            new("p_cetzine", "Cetzine 10mg Tablet", "Cetzine 10", "Cetirizine Hydrochloride", "Cetirizine HCl 10mg", "10mg", 0, 15, "TAB", "30049099", 12.0, 0, false, false, false, "8907773334455", "Dr. Reddy's", "CZ24A", 400, 52.00, 35.00, 50.00, 150),
            new("p_okacet", "Okacet 10mg Tablet", "Okacet 10", "Cetirizine", "Cetirizine 10mg", "10mg", 0, 10, "TAB", "30049099", 12.0, 0, false, false, false, "8907773334456", "Cipla Ltd", "OK24A", 365, 30.00, 19.50, 29.00, 160),
            new("p_levocet_5", "Levocet 5mg Tablet", "Levocet 5", "Levocetirizine", "Levocetirizine 5mg", "5mg", 0, 10, "TAB", "30049099", 12.0, 0, false, false, false, "8908884445566", "Hetero Healthcare", "LC24A", 320, 45.00, 30.00, 43.00, 100),
            new("p_avil_25", "Avil 25mg Tablet", "Avil 25", "Pheniramine Maleate", "Pheniramine Maleate 25mg", "25mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8909995556677", "Sanofi India", "AV24A", 450, 18.00, 11.50, 17.50, 200),
            new("p_atarax_25", "Atarax 25mg Tablet", "Atarax 25", "Hydroxyzine", "Hydroxyzine Hydrochloride 25mg", "25mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901118889900", "Dr. Reddy's", "AT24D", 340, 92.00, 65.00, 89.00, 70),
            new("p_ascoril_ls", "Ascoril-LS Syrup 100ml", "Ascoril-LS", "Levosalbutamol, Ambroxol and Guaiphenesin", "Levosalbutamol 1mg + Ambroxol 30mg + Guaiphenesin 50mg", "100ml", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8902229990011", "Glenmark Pharmaceuticals", "AS24A", 365, 128.00, 92.00, 125.00, 120),
            new("p_ascoril_d", "Ascoril-D Plus Syrup 100ml", "Ascoril-D", "Dextromethorphan, Phenylephrine, Chlorpheniramine", "Dextromethorphan 10mg + CPM 2mg + Phenylephrine 5mg", "100ml", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8902229990012", "Glenmark Pharmaceuticals", "AD24A", 330, 135.00, 98.00, 132.00, 95),
            new("p_benadryl_syr", "Benadryl Cough Syrup 150ml", "Benadryl", "Diphenhydramine Compound", "Diphenhydramine 14.08mg + Ammonium Chloride", "150ml", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8903330001122", "Johnson & Johnson", "BN24A", 400, 145.00, 105.00, 142.00, 80),
            new("p_grilinctus", "Grilinctus Syrup 100ml", "Grilinctus", "Dextromethorphan, Chlorpheniramine, Guaiphenesin", "Dextromethorphan + CPM + Guaiphenesin + Ammonium Chloride", "100ml", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8904441112233", "Franco-Indian Pharma", "GR24A", 350, 120.00, 85.00, 118.00, 100),
            new("p_alex_syr", "Alex Syrup Sugar Free 100ml", "Alex Syrup", "Dextromethorphan, Phenylephrine, Chlorpheniramine", "Dextromethorphan + Phenylephrine + CPM Sugar Free", "100ml", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8905552223344", "Glenmark Pharmaceuticals", "AX24A", 320, 138.00, 100.00, 135.00, 85),
            new("p_asthalin_inh", "Asthalin 100mcg Inhaler 200 MDI", "Asthalin Inhaler", "Salbutamol", "Salbutamol 100mcg / puff", "100mcg", 6, 1, "INH", "30049099", 12.0, 1, true, false, false, "8906663334455", "Cipla Ltd", "AH24A", 450, 165.00, 122.00, 160.00, 90),
            new("p_budecort_200", "Budecort 200 Inhaler 200 MDI", "Budecort 200", "Budesonide", "Budesonide 200mcg / puff", "200mcg", 6, 1, "INH", "30049099", 12.0, 1, true, false, false, "8907774445566", "Cipla Ltd", "BD24A", 400, 390.00, 290.00, 380.00, 50),
            new("p_foracort_200", "Foracort 200 Synchrobreathe Inhaler", "Foracort 200", "Formoterol and Budesonide", "Formoterol 6mcg + Budesonide 200mcg", "206mcg", 6, 1, "INH", "30049099", 12.0, 1, true, false, false, "8908885556677", "Cipla Ltd", "FC24A", 365, 520.00, 395.00, 505.00, 45),
            new("p_duolin_resp", "Duolin Respules 2.5ml", "Duolin Respules", "Levosalbutamol and Ipratropium", "Levosalbutamol 1.25mg + Ipratropium 500mcg", "2.5ml", 6, 5, "PKT", "30049099", 12.0, 1, true, false, false, "8909996667788", "Cipla Ltd", "DL24C", 300, 180.00, 130.00, 175.00, 60),
            new("p_seroflo_250", "Seroflo 250 Synchrobreathe Inhaler", "Seroflo 250", "Salmeterol and Fluticasone", "Salmeterol 25mcg + Fluticasone 250mcg", "275mcg", 6, 1, "INH", "30049099", 12.0, 1, true, false, false, "8909996667789", "Cipla Ltd", "SF24C", 350, 680.00, 520.00, 665.00, 30),
            new("p_aerocort_inh", "Aerocort Inhaler", "Aerocort", "Levosalbutamol and Beclomethasone", "Levosalbutamol 50mcg + Beclomethasone 50mcg", "100mcg", 6, 1, "INH", "30049099", 12.0, 1, true, false, false, "8909996667790", "Cipla Ltd", "AC24D", 365, 295.00, 220.00, 288.00, 40),

            // ==========================================
            // 7. CNS, PSYCHIATRIC & SCHEDULED (156-180)
            // ==========================================
            new("p_alpra", "Alprax 0.5mg Tablet", "Alprax 0.5", "Alprazolam", "Alprazolam 0.5mg", "0.5mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8906667778889", "Torrent Pharmaceuticals", "AL24A", 210, 45.00, 30.00, 45.00, 110),
            new("p_alprax_025", "Alprax 0.25mg Tablet", "Alprax 0.25", "Alprazolam", "Alprazolam 0.25mg", "0.25mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8906667778892", "Torrent Pharmaceuticals", "AL24D", 280, 28.00, 18.00, 27.00, 90),
            new("p_restyl_05", "Restyl 0.5mg Tablet", "Restyl 0.5", "Alprazolam", "Alprazolam 0.5mg", "0.5mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901119990011", "Cipla Ltd", "RS24C", 310, 42.00, 28.00, 40.00, 80),
            new("p_clonafit_05", "Clonafit 0.5mg Tablet", "Clonafit 0.5", "Clonazepam", "Clonazepam 0.5mg", "0.5mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8902220001122", "Mankind Pharma", "CF24B", 350, 38.00, 25.00, 36.00, 85),
            new("p_lonazep_05", "Lonazep 0.5mg Tablet", "Lonazep 0.5", "Clonazepam", "Clonazepam 0.5mg", "0.5mg", 0, 15, "TAB", "30049099", 12.0, 2, true, false, false, "8903331112233", "Sun Pharma", "LZ24A", 300, 68.00, 46.00, 65.00, 95),
            new("p_petril_05", "Petril 0.5mg Tablet", "Petril 0.5", "Clonazepam", "Clonazepam 0.5mg Fast Dissolve", "0.5mg", 0, 15, "TAB", "30049099", 12.0, 2, true, false, false, "8904442223344", "Micro Labs Ltd", "PT24A", 320, 65.00, 44.00, 62.00, 70),
            new("p_frisium_10", "Frisium 10mg Tablet", "Frisium 10", "Clobazam", "Clobazam 10mg", "10mg", 0, 15, "TAB", "30049099", 12.0, 2, true, false, false, "8905553334455", "Sanofi India", "FR24A", 365, 145.00, 100.00, 140.00, 60),
            new("p_valium_5", "Valium 5mg Tablet", "Valium 5", "Diazepam", "Diazepam 5mg", "5mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8906664445566", "Abbott India", "VL24A", 340, 35.00, 23.00, 34.00, 75),
            new("p_calmpose_5", "Calmpose 5mg Tablet", "Calmpose 5", "Diazepam", "Diazepam 5mg", "5mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8907775556677", "Sun Pharma", "CP24B", 310, 32.00, 21.00, 30.00, 50),
            new("p_ativan_2", "Ativan 2mg Tablet", "Ativan 2", "Lorazepam", "Lorazepam 2mg", "2mg", 0, 30, "TAB", "30049099", 12.0, 2, true, false, false, "8908886667788", "Pfizer", "AT24E", 360, 95.00, 65.00, 90.00, 80),
            new("p_lopez_2", "Lopez 2mg Tablet", "Lopez 2", "Lorazepam", "Lorazepam 2mg", "2mg", 0, 20, "TAB", "30049099", 12.0, 2, true, false, false, "8909997778899", "Intas Pharma", "LP24A", 300, 62.00, 42.00, 60.00, 70),
            new("p_nexito_10", "Nexito 10mg Tablet", "Nexito 10", "Escitalopram", "Escitalopram Oxalate 10mg", "10mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8901110002233", "Sun Pharma", "NX24A", 365, 110.00, 75.00, 105.00, 100),
            new("p_nexito_plus", "Nexito Plus Tablet", "Nexito Plus", "Escitalopram and Clonazepam", "Escitalopram 10mg + Clonazepam 0.5mg", "10.5mg", 0, 10, "TAB", "30049099", 12.0, 2, true, false, false, "8901110002234", "Sun Pharma", "NXP24A", 320, 140.00, 98.00, 135.00, 90),
            new("p_flunil_20", "Flunil 20mg Capsule", "Flunil 20", "Fluoxetine", "Fluoxetine Hydrochloride 20mg", "20mg", 1, 15, "CAP", "30049099", 12.0, 1, true, false, false, "8902221113344", "Intas Pharma", "FL24B", 350, 70.00, 48.00, 68.00, 60),
            new("p_sertima_50", "Sertima 50mg Tablet", "Sertima 50", "Sertraline", "Sertraline Hydrochloride 50mg", "50mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8903332224455", "Intas Pharma", "SR24A", 300, 135.00, 95.00, 130.00, 55),
            new("p_pari_cr_12", "Pari CR 12.5mg Tablet", "Pari CR 12.5", "Paroxetine Controlled Release", "Paroxetine CR 12.5mg", "12.5mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8904443335566", "Ipca Laboratories", "PR24A", 330, 210.00, 150.00, 205.00, 45),
            new("p_oleanz_5", "Oleanz 5mg Tablet", "Oleanz 5", "Olanzapine", "Olanzapine 5mg", "5mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8905554446677", "Sun Pharma", "OL24A", 365, 65.00, 44.00, 62.00, 50),
            new("p_sizodon_2", "Sizodon 2mg Tablet", "Sizodon 2", "Risperidone", "Risperidone 2mg", "2mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8906665557788", "Sun Pharma", "SZ24A", 320, 52.00, 35.00, 50.00, 40),
            new("p_qutan_50", "Qutan 50mg Tablet", "Qutan 50", "Quetiapine", "Quetiapine Fumarate 50mg", "50mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8907776668899", "Intas Pharma", "QT24A", 350, 85.00, 58.00, 82.00, 45),
            new("p_gardenal_30", "Gardenal 30mg Tablet", "Gardenal 30", "Phenobarbital (Phenobarbitone)", "Phenobarbital 30mg", "30mg", 0, 20, "TAB", "30049099", 12.0, 3, true, false, false, "8908887779900", "Abbott India", "GD24A", 400, 24.00, 15.00, 23.00, 60),
            new("p_tegretol_200", "Tegretol 200mg Tablet", "Tegretol 200", "Carbamazepine", "Carbamazepine 200mg", "200mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8909998880011", "Novartis India", "TG24A", 365, 26.00, 17.00, 25.00, 100),
            new("p_encorate_500", "Encorate Chrono 500 Tablet", "Encorate Chrono 500", "Sodium Valproate and Valproic Acid", "Sodium Valproate 333mg + Valproic Acid 145mg CR", "500mg", 0, 15, "TAB", "30049099", 12.0, 1, true, false, false, "8901111112244", "Sun Pharma", "EC24D", 340, 230.00, 165.00, 222.00, 60),
            new("p_eptoin_100", "Eptoin 100mg Tablet", "Eptoin 100", "Phenytoin Sodium", "Phenytoin Sodium 100mg", "100mg", 0, 120, "BTL", "30049099", 12.0, 1, true, false, false, "8902222223355", "Abbott India", "EP24A", 420, 215.00, 155.00, 210.00, 50),
            new("p_levera_500", "Levera 500mg Tablet", "Levera 500", "Levetiracetam", "Levetiracetam 500mg", "500mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8903333334466", "Intas Pharma", "LV24A", 310, 160.00, 115.00, 155.00, 70),
            new("p_gabapin_nt", "Gabapin-NT Tablet", "Gabapin-NT", "Gabapentin and Nortriptyline", "Gabapentin 400mg + Nortriptyline 10mg", "410mg", 0, 10, "TAB", "30049099", 12.0, 1, true, false, false, "8904444445577", "Intas Pharma", "GB24A", 300, 280.00, 205.00, 272.00, 55),
            new("p_pregalin_m75", "Pregalin-M 75 Capsule", "Pregalin-M 75", "Pregabalin and Methylcobalamin", "Pregabalin 75mg + Methylcobalamin 750mcg", "75mg", 1, 15, "CAP", "30049099", 12.0, 1, true, false, false, "8905555556688", "Torrent Pharma", "PG24A", 320, 295.00, 215.00, 288.00, 65),

            // ==========================================
            // 8. DERMATOLOGY, TOPICAL & ANTIFUNGALS (181-205)
            // ==========================================
            new("p_beta", "Betadine 10% Solution 100ml", "Betadine Solution", "Povidone-Iodine", "Povidone-Iodine 10% w/v Antiseptic", "10%", 2, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8908889990001", "Win-Medicare", "BT24A", 500, 140.00, 98.00, 135.00, 80),
            new("p_betadine_oint", "Betadine 10% Ointment 20g", "Betadine Ointment", "Povidone-Iodine", "Povidone-Iodine 10% w/w", "10%", 4, 1, "TUBE", "30049099", 12.0, 0, false, false, false, "8908889990003", "Win-Medicare", "BT24B", 450, 75.00, 52.00, 72.00, 90),
            new("p_soframycin", "Soframycin Skin Cream 30g", "Soframycin", "Framycetin Sulphate", "Framycetin Sulphate 1% w/w", "1%", 4, 1, "TUBE", "30049099", 12.0, 0, false, false, false, "8906666667799", "Sanofi India", "SF24A", 400, 65.00, 45.00, 63.00, 120),
            new("p_silverex", "Silverex Ionic Gel 20g", "Silverex Ionic", "Silver Nitrate and Chlorhexidine", "Silver Nitrate + Chlorhexidine Gluconate", "20g", 4, 1, "TUBE", "30049099", 12.0, 0, false, false, false, "8907777778800", "Ranbaxy / Sun", "SI24A", 365, 110.00, 78.00, 105.00, 60),
            new("p_tbact_oint", "T-Bact 2% Ointment 5g", "T-Bact", "Mupirocin", "Mupirocin 2% w/w", "2%", 4, 1, "TUBE", "30049099", 12.0, 1, true, false, false, "8908888889911", "GSK Pharma", "TB24A", 330, 155.00, 112.00, 150.00, 70),
            new("p_candid_dust", "Candid Dusting Powder 100g", "Candid Powder", "Clotrimazole", "Clotrimazole 1% w/w Dusting Powder", "1%", 7, 1, "BTL", "30049099", 12.0, 0, false, false, false, "8909999990022", "Glenmark Pharmaceuticals", "CD24B", 450, 160.00, 115.00, 155.00, 110),
            new("p_candid_b", "Candid-B Cream 20g", "Candid-B", "Clotrimazole and Beclomethasone", "Clotrimazole 1% + Beclomethasone 0.025%", "20g", 4, 1, "TUBE", "30049099", 12.0, 1, true, false, false, "8909999990023", "Glenmark Pharmaceuticals", "CB24B", 350, 145.00, 102.00, 140.00, 85),
            new("p_canesten", "Canesten 1% Cream 30g", "Canesten", "Clotrimazole", "Clotrimazole 1% w/w", "1%", 4, 1, "TUBE", "30049099", 12.0, 0, false, false, false, "8901112224455", "Bayer India", "CN24A", 400, 115.00, 82.00, 110.00, 65),
            new("p_itrasys_200", "Itrasys 200mg Capsule", "Itrasys 200", "Itraconazole", "Itraconazole 200mg Pellets", "200mg", 1, 10, "CAP", "30049099", 12.0, 1, true, false, false, "8902223335566", "Systopic Labs", "IT24A", 300, 275.00, 195.00, 268.00, 80),
            new("p_canditral_200", "Canditral 200mg Capsule", "Canditral 200", "Itraconazole", "Itraconazole 200mg", "200mg", 1, 10, "CAP", "30049099", 12.0, 1, true, false, false, "8902223335567", "Glenmark Pharmaceuticals", "CT24B", 320, 290.00, 210.00, 282.00, 65),
            new("p_forcan_150", "Forcan 150mg Tablet", "Forcan 150", "Fluconazole", "Fluconazole 150mg", "150mg", 0, 1, "TAB", "30049099", 12.0, 1, true, false, false, "8903334446677", "Cipla Ltd", "FC24B", 420, 18.00, 11.00, 17.50, 150),
            new("p_terbinaforce", "Terbinaforce 250mg Tablet", "Terbinaforce 250", "Terbinafine", "Terbinafine Hydrochloride 250mg", "250mg", 0, 7, "TAB", "30049099", 12.0, 1, true, false, false, "8904445557788", "Mankind Pharma", "TF24A", 310, 115.00, 80.00, 110.00, 75),
            new("p_betnovate_c", "Betnovate-C Cream 30g", "Betnovate-C", "Betamethasone and Clioquinol", "Betamethasone Valerate 0.1% + Clioquinol 3%", "30g", 4, 1, "TUBE", "30049099", 12.0, 1, true, false, false, "8905556668899", "GSK Pharma", "BV24A", 365, 68.00, 48.00, 66.00, 130),
            new("p_betnovate_n", "Betnovate-N Cream 20g", "Betnovate-N", "Betamethasone and Neomycin", "Betamethasone 0.1% + Neomycin 0.5%", "20g", 4, 1, "TUBE", "30049099", 12.0, 1, true, false, false, "8905556668800", "GSK Pharma", "BV24B", 350, 52.00, 36.00, 50.00, 140),
            new("p_omnigel", "Omnigel Pain Relief Gel 50g", "Omnigel", "Diclofenac, Linseed Oil, Menthol", "Diclofenac Diethylamine + Virgin Linseed Oil + Menthol", "50g", 4, 1, "TUBE", "30049099", 12.0, 0, false, false, false, "8906667779911", "Cipla Ltd", "OG24A", 450, 145.00, 102.00, 140.00, 90),
            new("p_volini_gel", "Volini Pain Relief Gel 75g", "Volini Gel", "Diclofenac Gel", "Diclofenac Diethylamine + Methyl Salicylate + Menthol", "75g", 4, 1, "TUBE", "30049099", 12.0, 0, false, false, false, "8907778880022", "Sun Pharma", "VL24B", 420, 220.00, 158.00, 212.00, 80),
            new("p_moov_oint", "Moov Pain Relief Ointment 50g", "Moov", "Ayurvedic Pain Balm", "Oil of Wintergreen + Mint Extract + Turpentine Oil", "50g", 4, 1, "TUBE", "30049011", 12.0, 0, false, false, false, "8908889991133", "Reckitt Benckiser", "MV24A", 500, 175.00, 126.00, 170.00, 100),
            new("p_burnol", "Burnol Antiseptic Cream 25g", "Burnol", "Aminacrine and Cetrimide", "Aminacrine HCl + Cetrimide Antiseptic", "25g", 4, 1, "TUBE", "30049099", 12.0, 0, false, false, false, "8909990002244", "Dr. Morepen", "BN24B", 450, 85.00, 58.00, 82.00, 75),
            new("p_lulican_cream", "Lulican 1% Cream 30g", "Lulican", "Luliconazole", "Luliconazole 1% w/w", "1%", 4, 1, "TUBE", "30049099", 12.0, 1, true, false, false, "8909990002245", "Glenmark Pharmaceuticals", "LL24A", 365, 340.00, 250.00, 330.00, 55),
            new("p_nizral_shampoo", "Nizral 2% Anti-Dandruff Shampoo 100ml", "Nizral", "Ketoconazole", "Ketoconazole 2% w/v Shampoo", "2%", 2, 1, "BTL", "33051090", 18.0, 0, false, false, false, "8909990002246", "Johnson & Johnson", "NZ24A", 400, 310.00, 230.00, 300.00, 60),

            // ==========================================
            // 9. NUTRITIONAL, VITAMINS & WELLNESS (206-218)
            // ==========================================
            new("p_becosules_z", "Becosules Z Capsule", "Becosules Z", "B-Complex Forte with Zinc and Vitamin C", "Thiamine + Riboflavin + Niacinamide + Zinc + Vit C", "Forte", 1, 20, "CAP", "30045020", 12.0, 0, false, false, false, "8901113335577", "Pfizer", "BZ24A", 400, 52.00, 36.00, 50.00, 220),
            new("p_neurobion_forte", "Neurobion Forte Tablet", "Neurobion Forte", "Vitamin B-Complex with B12", "Vit B1 + B2 + B3 + B5 + B6 + B12 (Cyanocobalamin)", "Forte", 0, 30, "TAB", "30045020", 12.0, 0, false, false, false, "8902224446688", "Procter & Gamble", "NF24B", 420, 42.00, 29.00, 40.00, 250),
            new("p_shelcal_500", "Shelcal 500mg Tablet", "Shelcal 500", "Calcium and Vitamin D3", "Calcium Carbonate 1250mg eq to Calcium 500mg + Vit D3 250IU", "500mg", 0, 15, "TAB", "30045020", 12.0, 0, false, false, false, "8903335557799", "Torrent Pharma", "SC24A", 365, 135.00, 96.00, 130.00, 180),
            new("p_shelcal_hd", "Shelcal-HD Tablet", "Shelcal-HD", "Calcium and Vitamin D3 High Dose", "Calcium 500mg + Vitamin D3 500IU", "500mg", 0, 15, "TAB", "30045020", 12.0, 0, false, false, false, "8903335557800", "Torrent Pharma", "SHD24A", 340, 155.00, 110.00, 150.00, 110),
            new("p_gemcal", "Gemcal Capsule", "Gemcal", "Calcium, Calcitriol and Zinc", "Calcitriol 0.25mcg + Calcium Carbonate 500mg + Zinc 7.5mg", "Multi", 1, 15, "CAP", "30045020", 12.0, 1, true, false, false, "8904446668800", "Alkem Laboratories", "GC24B", 310, 310.00, 230.00, 300.00, 85),
            new("p_cipcal_500", "Cipcal 500mg Tablet", "Cipcal 500", "Calcium and Vitamin D3", "Elemental Calcium 500mg + Vit D3 250IU", "500mg", 0, 15, "TAB", "30045020", 12.0, 0, false, false, false, "8905557779911", "Cipla Ltd", "CC24B", 380, 95.00, 68.00, 92.00, 130),
            new("p_livogen_z", "Livogen-Z Tablet", "Livogen-Z", "Iron, Folic Acid and Zinc", "Ferrous Fumarate 152mg + Folic Acid 750mcg + Zinc", "Multi", 0, 15, "TAB", "30045020", 12.0, 0, false, false, false, "8906668880022", "Merck Ltd", "LG24A", 365, 85.00, 60.00, 82.00, 120),
            new("p_orofer_xt", "Orofer-XT Tablet", "Orofer-XT", "Ferrous Ascorbate and Folic Acid", "Ferrous Ascorbate 100mg + Folic Acid 1.5mg", "100mg", 0, 10, "TAB", "30045020", 12.0, 0, false, false, false, "8907779991133", "Emcure Pharma", "OF24B", 350, 165.00, 120.00, 160.00, 95),
            new("p_supradyn", "Supradyn Daily Multivitamin Tablet", "Supradyn Daily", "Multivitamins and Minerals with Minerals", "Essential Multivitamins + Zinc + Trace Elements", "Daily", 0, 15, "TAB", "30045020", 12.0, 0, false, false, false, "8908880002244", "Bayer India", "SD24B", 420, 60.00, 42.00, 58.00, 200),
            new("p_revital_h", "Revital H Daily Health Capsule", "Revital H", "Ginseng with Multivitamins and Minerals", "Ginseng 42.5mg + 10 Vitamins + 9 Minerals", "Daily", 1, 30, "BTL", "30045020", 12.0, 0, false, false, false, "8909991113355", "Sun Pharma", "RH24A", 450, 340.00, 250.00, 330.00, 110),
            new("p_limcee_500", "Limcee 500mg Chewable Tablet Orange", "Limcee 500", "Vitamin C (Ascorbic Acid)", "Ascorbic Acid 500mg Chewable", "500mg", 0, 15, "TAB", "30045020", 12.0, 0, false, false, false, "8901112224466", "Abbott India", "LC24B", 450, 24.50, 16.00, 23.50, 250),
            new("p_celin_500", "Celin 500mg Tablet", "Celin 500", "Vitamin C", "Ascorbic Acid 500mg", "500mg", 0, 25, "TAB", "30045020", 12.0, 0, false, false, false, "8902223335577", "GlaxoSmithKline", "CL24C", 400, 40.00, 27.00, 38.00, 180),
            new("p_zincovit_tab", "Zincovit Tablet", "Zincovit", "Multivitamins with Zinc and Grape Seed Extract", "Vitamins A, B, C, D, E + Zinc + Grape Seed Extract", "Multi", 0, 15, "TAB", "30045020", 12.0, 0, false, false, false, "8903334446688", "Apex Labs", "ZV24A", 365, 115.00, 82.00, 110.00, 160)
        };

        return list;
    }
}
