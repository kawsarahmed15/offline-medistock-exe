using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Compliance.DTOs;
using Medistock.Application.Compliance.Services;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class GstComplianceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly DatabaseMigrator _migrator;
    private readonly SqliteGstReportRepository _gstRepo;
    private readonly GstReportService _gstService;

    public GstComplianceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_gst_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrator = new DatabaseMigrator(_connectionFactory);
        _gstRepo = new SqliteGstReportRepository(_connectionFactory);
        _gstService = new GstReportService(_gstRepo);

        _migrator.MigrateAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    private async Task SeedGstDataAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // 1. Products
        await connection.ExecuteAsync(@"
            INSERT INTO products (
                id, org_id, name, brand_name, generic_name, composition, strength,
                dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
                schedule, is_prescription_required, is_cold_chain, is_narcotic,
                is_active, primary_barcode, manufacturer_id, manufacturer_name,
                created_at, updated_at
            ) VALUES (
                'p_parac', 'org-1', 'Paracetamol 500mg', 'Crocin', 'Paracetamol',
                'Paracetamol 500mg', '500mg', 0, 10, 'TAB', '3004', 12.0,
                0, 0, 0, 0, 1, '8901111111111', 'm1', 'GSK',
                datetime('now'), datetime('now')
            );
        ");

        // 2. Sales (Outward Taxable: ₹1,000 + 12% GST = ₹1,120)
        var nowStr = DateTime.UtcNow.ToString("o");
        await connection.ExecuteAsync(@"
            INSERT INTO sales (
                id, org_id, branch_id, counter_id, warehouse_id, user_id, device_id,
                invoice_no, invoice_date, status, customer_id, customer_name,
                subtotal, discount_amount, tax_amount, round_off, total,
                is_interstate, prescription_ref, notes, created_at, posted_at
            ) VALUES (
                's_1', 'org-1', 'branch-1', 'cnt-1', 'wh-1', 'usr-1', 'dev-1',
                'INV-2026-0001', @nowStr, 2, NULL, 'Walk-in Retail',
                1000.0, 0.0, 120.0, 0.0, 1120.0,
                0, NULL, NULL, @nowStr, @nowStr
            );

            INSERT INTO sale_items (
                id, sale_id, product_id, product_name, batch_id, batch_number, expiry_date,
                quantity, unit_price, mrp, discount_pct, discount_amount,
                taxable_amount, cgst_rate, cgst_amount, sgst_rate, sgst_amount,
                igst_rate, igst_amount, net_amount
            ) VALUES (
                'si_1', 's_1', 'p_parac', 'Paracetamol 500mg', 'b_1', 'B2026', datetime('now', '+1 year'),
                50.0, 20.0, 25.0, 0.0, 0.0,
                1000.0, 6.0, 60.0, 6.0, 60.0,
                0.0, 0.0, 1120.0
            );
        ", new { nowStr });

        // 3. Purchase Invoices (Inward Taxable: ₹600 + 12% GST = ₹672, Eligible ITC = ₹72)
        await connection.ExecuteAsync(@"
            INSERT INTO suppliers (
                id, org_id, name, gstin, dl_number, phone, email, address, credit_days, outstanding_balance, is_active, created_at
            ) VALUES (
                'sup_1', 'org-1', 'Apex Pharma Distributors', '27AAACA1234F1Z9', 'DL-12345', '9876543210', 'apex@pharma.com', 'Mumbai', 30, 672.0, 1, datetime('now')
            );

            INSERT INTO purchase_invoices (
                id, org_id, branch_id, warehouse_id, supplier_id, supplier_name,
                supplier_gstin, supplier_invoice_no, supplier_invoice_date, status,
                is_interstate, subtotal, discount_amount, taxable_amount,
                cgst_amount, sgst_amount, igst_amount, round_off, grand_total,
                notes, created_by_user_id, created_at, posted_at
            ) VALUES (
                'pur_1', 'org-1', 'branch-1', 'wh-1', 'sup_1', 'Apex Pharma Distributors',
                '27AAACA1234F1Z9', 'APX-9988', @nowStr, 1,
                0, 600.0, 0.0, 600.0,
                36.0, 36.0, 0.0, 0.0, 672.0,
                'Monthly stock restock', 'usr-1', @nowStr, @nowStr
            );
        ", new { nowStr });
    }

    [Fact]
    public async Task GenerateGstr1_ComputesTaxableOutwardAndHsnSummary()
    {
        await SeedGstDataAsync();

        var now = DateTime.UtcNow;
        var filter = new GstPeriodFilter(
            OrgId: "org-1",
            BranchId: "branch-1",
            Year: now.Year,
            Month: now.Month,
            FromDate: new DateTime(now.Year, now.Month, 1),
            ToDate: new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1)
        );

        var gstr1 = await _gstService.GenerateGstr1Async(filter);

        Assert.NotNull(gstr1);
        Assert.Equal(1000.00m, gstr1.TotalTaxableValue);
        Assert.Equal(60.00m, gstr1.TotalCgst);
        Assert.Equal(60.00m, gstr1.TotalSgst);
        Assert.Equal(0.00m, gstr1.TotalIgst);
        Assert.Equal(120.00m, gstr1.TotalOutputGst);
        Assert.Equal(1120.00m, gstr1.TotalInvoiceValue);

        // Verify HSN summary
        Assert.NotEmpty(gstr1.HsnSummary);
        var hsnItem = gstr1.HsnSummary[0];
        Assert.Equal("3004", hsnItem.HsnCode);
        Assert.Equal(50.0m, hsnItem.TotalQuantity);
        Assert.Equal(1000.00m, hsnItem.TaxableValue);

        // Verify B2C aggregation
        Assert.NotEmpty(gstr1.B2cInvoices);
        Assert.Equal(1000.00m, gstr1.B2cInvoices[0].TaxableValue);
    }

    [Fact]
    public async Task GenerateGstr2_ComputesInwardPurchasesAndItc()
    {
        await SeedGstDataAsync();

        var now = DateTime.UtcNow;
        var filter = new GstPeriodFilter(
            OrgId: "org-1",
            BranchId: "branch-1",
            Year: now.Year,
            Month: now.Month,
            FromDate: new DateTime(now.Year, now.Month, 1),
            ToDate: new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1)
        );

        var gstr2 = await _gstService.GenerateGstr2Async(filter);

        Assert.NotNull(gstr2);
        Assert.Equal(600.00m, gstr2.TotalTaxableValue);
        Assert.Equal(72.00m, gstr2.TotalEligibleItc);
        Assert.Equal(36.00m, gstr2.TotalCgstItc);
        Assert.Equal(36.00m, gstr2.TotalSgstItc);

        Assert.Single(gstr2.InwardInvoices);
        Assert.Equal("27AAACA1234F1Z9", gstr2.InwardInvoices[0].SupplierGstin);
    }

    [Fact]
    public async Task GenerateGstr3b_ComputesNetCashTaxLiability()
    {
        await SeedGstDataAsync();

        var now = DateTime.UtcNow;
        var filter = new GstPeriodFilter(
            OrgId: "org-1",
            BranchId: "branch-1",
            Year: now.Year,
            Month: now.Month,
            FromDate: new DateTime(now.Year, now.Month, 1),
            ToDate: new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1)
        );

        var gstr3b = await _gstService.GenerateGstr3bAsync(filter);

        Assert.NotNull(gstr3b);
        // Outward CGST = 60, ITC CGST = 36 -> Net CGST Payable = 24
        Assert.Equal(24.00m, gstr3b.NetCgstPayable);
        // Outward SGST = 60, ITC SGST = 36 -> Net SGST Payable = 24
        Assert.Equal(24.00m, gstr3b.NetSgstPayable);
        // Net Total Cash Payable = 48
        Assert.Equal(48.00m, gstr3b.NetTotalPayable);
    }

    [Fact]
    public async Task ExportGstr1Json_ProducesValidGstnFormat()
    {
        await SeedGstDataAsync();

        var now = DateTime.UtcNow;
        var filter = new GstPeriodFilter(
            OrgId: "org-1",
            BranchId: "branch-1",
            Year: now.Year,
            Month: now.Month,
            FromDate: new DateTime(now.Year, now.Month, 1),
            ToDate: new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1)
        );

        var json = await _gstService.ExportGstr1JsonAsync(filter);

        Assert.NotNull(json);
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc.RootElement.GetProperty("gstin").GetString());
        Assert.NotNull(doc.RootElement.GetProperty("fp").GetString());
        Assert.True(doc.RootElement.TryGetProperty("hsn", out var hsnProp));
        Assert.True(doc.RootElement.TryGetProperty("b2cs", out var b2csProp));
    }
}
