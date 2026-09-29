using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Products.Queries;
using Medistock.Application.Purchases.DTOs;
using Medistock.Application.Purchases.Services;
using Medistock.Desktop.ViewModels;
using Medistock.Domain.Purchases;
using Medistock.Infrastructure.Hardware.Export;
using Xunit;

namespace Medistock.Desktop.Tests;

public class MockPurchaseService : IPurchaseService
{
    public List<SupplierDto> SuppliersList { get; } = new()
    {
        new SupplierDto("sup-1", "Apex Pharma Wholesalers", "07AABCU9603R1ZM", "DL-20B-112233", "+91 11 2345 6789", "orders@apex.in", "Okhla, New Delhi", 30, 25000m, true),
        new SupplierDto("sup-2", "Cipla Direct Depot", "29AAACC1234F1Z5", "DL-21B-445566", "+91 80 4151 7890", "depot@cipla.com", "Peenya, Bengaluru", 45, 12000m, true)
    };

    public List<PurchaseInvoiceSummaryDto> RecentPurchasesList { get; } = new()
    {
        new PurchaseInvoiceSummaryDto("inv-1", "Apex Pharma Wholesalers", "07AABCU9603R1ZM", "APEX/2026/001", DateTime.UtcNow.AddDays(-2), PurchaseInvoiceStatus.Posted, 20000m, 1200m, 1200m, 0m, 22400m, 5, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-2))
    };

    public Task<PurchasePostingResult> CreateAndPostPurchaseInvoiceAsync(CreatePurchaseInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PurchasePostingResult(true, "inv-new", command.Items.Count, command.Items.Count * 50, 25000m));
    }

    public Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(string orgId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SupplierDto>>(SuppliersList);
    }

    public Task<string> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        var id = $"sup-{Guid.NewGuid():N}";
        SuppliersList.Add(new SupplierDto(id, command.Name, command.Gstin, command.DlNumber, command.Phone, command.Email, command.Address, command.CreditDays, command.OpeningBalance, true));
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<PurchaseInvoiceSummaryDto>> GetRecentPurchasesAsync(string orgId, string branchId, int limit = 100, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PurchaseInvoiceSummaryDto>>(RecentPurchasesList);
    }

    public Task<PurchaseInvoiceDetailsDto?> GetPurchaseInvoiceDetailsAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var items = new List<PurchaseInvoiceItemDto>
        {
            new PurchaseInvoiceItemDto("pii-1", "p1", "Dolo 650", "30049099", "DL2601", DateTime.UtcNow.AddMonths(24), null, 100, 10, 110, 20m, 30.5m, 30m, 0, 0, 2000m, 12m, 120m, 120m, 0m, 2240m, 20.36m)
        };

        return Task.FromResult<PurchaseInvoiceDetailsDto?>(new PurchaseInvoiceDetailsDto(
            Id: invoiceId,
            OrgId: "org-1",
            BranchId: "br-1",
            WarehouseId: "wh-1",
            SupplierId: "sup-1",
            SupplierName: "Apex Pharma Wholesalers",
            SupplierGstin: "07AABCU9603R1ZM",
            SupplierInvoiceNo: "APEX/2026/001",
            SupplierInvoiceDate: DateTime.UtcNow.AddDays(-2),
            Status: PurchaseInvoiceStatus.Posted,
            IsInterstate: false,
            Subtotal: 2000m,
            DiscountAmount: 0m,
            TaxableAmount: 2000m,
            CgstAmount: 120m,
            SgstAmount: 120m,
            IgstAmount: 0m,
            RoundOff: 0m,
            GrandTotal: 2240m,
            Notes: "Test Notes",
            CreatedByUserId: "USER-01",
            CreatedAt: DateTime.UtcNow.AddDays(-2),
            PostedAt: DateTime.UtcNow.AddDays(-2),
            Items: items
        ));
    }

    public Task<PurchaseKpiSummaryDto> GetPurchaseKpiSummaryAsync(string orgId, string branchId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PurchaseKpiSummaryDto(
            TotalPurchaseAmount: 22400m,
            TotalInvoicesCount: 1,
            TotalSuppliersCount: 2,
            TotalOutstandingPayable: 37000m
        ));
    }

    public Task<bool> IsDuplicateInvoiceAsync(string orgId, string supplierId, string supplierInvoiceNo, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(supplierInvoiceNo == "EXISTING-INV-001");
    }

    public Task<PurchaseCancelResult> CancelPurchaseInvoiceAsync(string invoiceId, string cancelledByUserId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PurchaseCancelResult(true));
    }
}

public class MockProductSearchRepository : IProductSearchRepository
{
    public Task<IReadOnlyList<ProductSearchDto>> SearchProductsAsync(string query, string warehouseId, int limit = 20, CancellationToken cancellationToken = default)
    {
        var list = new List<ProductSearchDto>
        {
            new ProductSearchDto
            {
                Id = "p_dolo",
                Name = "Dolo 650mg Tablet",
                BrandName = "Dolo",
                GenericName = "Paracetamol",
                Composition = "Paracetamol 650mg",
                Strength = "650mg",
                DosageForm = Medistock.Domain.Common.DosageForm.Tablet,
                PackSizeDescription = "15 Tab/Strip",
                HsnCode = "30049099",
                GstRatePercent = 12.0m,
                Schedule = Medistock.Domain.Common.DrugSchedule.OTC,
                IsPrescriptionRequired = false,
                IsColdChain = false,
                IsNarcotic = false,
                MinStockAlert = 10.0m,
                ManufacturerName = "Micro Labs",
                Barcode = "8901234567890",
                BatchId = "b1",
                BatchNumber = "DL2601",
                NearestExpiryDate = DateTime.UtcNow.AddYears(2),
                Mrp = 30.50m,
                SaleRate = 30.00m,
                AvailableQuantity = 100
            }
        };
        var matches = list.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || p.GenericName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<ProductSearchDto>>(matches);
    }

    public Task<BarcodeLookupDto?> LookupByBarcodeAsync(string barcode, string warehouseId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<BarcodeLookupDto?>(null);
    }

    public Task<IReadOnlyList<ProductBatchDto>> GetBatchesForProductAsync(string productId, string warehouseId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProductBatchDto>>(new List<ProductBatchDto>());
    }
}

public class PurchaseViewModelAndExportTests
{
    [Fact]
    public void PurchaseItemRowViewModel_Calculates_Taxable_Net_Landed_And_Margin_Correctly()
    {
        var row = new PurchaseItemRowViewModel
        {
            Quantity = 100,
            FreeQuantity = 10,
            UnitPrice = 20.00m,
            Mrp = 35.00m,
            SaleRate = 34.00m,
            DiscountPct = 5.0m,
            GstRatePercent = 12.0m
        };
        row.Recalculate();

        // Gross = 100 * 20 = 2000
        // Disc = 2000 * 0.05 = 100
        // Taxable = 1900
        // GST = 1900 * 0.12 = 228
        // Net = 2128
        // Total Qty = 110
        // Landed Cost = 2128 / 110 = 19.35
        // Margin = ((35 - 19.35) / 35) * 100 = 44.7%

        Assert.Equal(2000.00m, row.GrossAmount);
        Assert.Equal(100.00m, row.DiscountAmount);
        Assert.Equal(1900.00m, row.TaxableAmount);
        Assert.Equal(228.00m, row.GstAmount);
        Assert.Equal(2128.00m, row.NetAmount);
        Assert.Equal(110.00m, row.TotalQuantity);
        Assert.Equal(19.35m, row.LandedCostPerUnit);
        Assert.True(row.MarginPercent > 40);
    }

    [Fact]
    public void PurchaseEntryViewModel_RecalculatesTotals_Handles_Intrastate_And_Interstate()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.LineItems.Clear();
        var row1 = new PurchaseItemRowViewModel
        {
            Quantity = 50,
            FreeQuantity = 0,
            UnitPrice = 20m,
            DiscountPct = 0,
            GstRatePercent = 12m
        };
        vm.LineItems.Add(row1);

        // Intra-state test
        vm.IsInterstate = false;
        vm.RecalculateTotals();

        Assert.Equal(1000m, vm.TaxableSubtotal);
        Assert.Equal(60m, vm.CgstTotal);
        Assert.Equal(60m, vm.SgstTotal);
        Assert.Equal(0m, vm.IgstTotal);
        Assert.Equal(1120m, vm.GrandTotal);

        // Interstate test
        vm.IsInterstate = true;
        vm.RecalculateTotals();

        Assert.Equal(1000m, vm.TaxableSubtotal);
        Assert.Equal(0m, vm.CgstTotal);
        Assert.Equal(0m, vm.SgstTotal);
        Assert.Equal(120m, vm.IgstTotal);
        Assert.Equal(1120m, vm.GrandTotal);
    }

    [Fact]
    public async Task PurchaseEntryViewModel_Initialize_Loads_Kpis_Suppliers_And_RecentPurchases()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        await vm.InitializeAsync();

        Assert.Equal(22400m, vm.TotalPurchasesAmount);
        Assert.Equal(1, vm.TotalInvoicesCount);
        Assert.Equal(2, vm.TotalSuppliersCount);
        Assert.Equal(37000m, vm.TotalOutstandingPayables);
        Assert.Equal(2, vm.Suppliers.Count);
        Assert.Equal(1, vm.RecentPurchases.Count);
    }

    [Fact]
    public async Task PurchaseExportService_Generates_Valid_Excel_Word_And_Pdf_Files()
    {
        var service = new PurchaseExportService();
        var meta = new PurchaseExportMetadata(
            PharmacyName: "City Care Pharmacy",
            StoreAddress: "Main Market, New Delhi",
            ContactPhone: "+91 11 2345 6789",
            Gstin: "07AABCU9603R1ZM",
            ExportDate: DateTime.Now,
            TotalPurchases: 250000m,
            TotalInvoicesCount: 15,
            TotalSuppliersCount: 6,
            TotalOutstandingPayables: 45000m
        );

        var rows = new List<PurchaseExportRow>
        {
            new PurchaseExportRow(1, "Apex Pharma Wholesalers", "APEX/2026/101", "07AABCU9603R1ZM", DateTime.Now.AddDays(-5), 10, 10000m, 600m, 600m, 0m, 11200m, "POSTED", DateTime.Now.AddDays(-5)),
            new PurchaseExportRow(2, "Cipla Direct Depot", "CIP/2026/882", "29AAACC1234F1Z5", DateTime.Now.AddDays(-2), 8, 15000m, 0m, 0m, 1800m, 16800m, "POSTED", DateTime.Now.AddDays(-2))
        };

        var tempDir = Path.Combine(Path.GetTempPath(), $"pur_exp_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var xlsxPath = Path.Combine(tempDir, "purchases.xlsx");
            var docxPath = Path.Combine(tempDir, "purchases.docx");
            var pdfPath = Path.Combine(tempDir, "purchases.html");

            var resXlsx = await service.ExportToExcelAsync(rows, meta, xlsxPath);
            var resDocx = await service.ExportToWordAsync(rows, meta, docxPath);
            var resPdf = await service.ExportToPdfHtmlAsync(rows, meta, pdfPath);

            Assert.True(File.Exists(resXlsx));
            Assert.True(new FileInfo(resXlsx).Length > 0);

            Assert.True(File.Exists(resDocx));
            Assert.True(new FileInfo(resDocx).Length > 0);

            Assert.True(File.Exists(resPdf));
            Assert.True(new FileInfo(resPdf).Length > 0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void AddBlankRow_DoesNotContainDemoData()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.LineItems.Clear();
        vm.AddBlankRow();

        var row = vm.LineItems[0];
        Assert.Equal(string.Empty, row.ProductName);
        Assert.Equal(string.Empty, row.BatchNumber);
        Assert.Equal(0m, row.UnitPrice);
        Assert.Equal(0m, row.Mrp);
        Assert.Equal(1m, row.Quantity);
    }

    [Fact]
    public void ActiveRowIndex_UpdatedWhenNavigatingRows()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.LineItems.Clear();
        vm.AddBlankRow();
        vm.AddBlankRow();

        vm.SetActiveRow(0);
        Assert.Equal(0, vm.ActiveRowIndex);
        Assert.Same(vm.LineItems[0], vm.ActiveRow);

        vm.SetActiveRow(1);
        Assert.Equal(1, vm.ActiveRowIndex);
        Assert.Same(vm.LineItems[1], vm.ActiveRow);
    }

    [Fact]
    public void ExpiryText_MMYY_ParsesCorrectExpiryDate()
    {
        var row = new PurchaseItemRowViewModel();
        row.ExpiryText = "06/27";
        Assert.Equal(2027, row.ExpiryDate.Year);
        Assert.Equal(6, row.ExpiryDate.Month);
    }

    [Fact]
    public void ExpiryText_MMYYYY_ParsesCorrectExpiryDate()
    {
        var row = new PurchaseItemRowViewModel();
        row.ExpiryText = "12/2028";
        Assert.Equal(2028, row.ExpiryDate.Year);
        Assert.Equal(12, row.ExpiryDate.Month);
    }

    [Fact]
    public async Task CancelInvoiceCommand_Flow_SuccessfullyCancels()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);
        await vm.InitializeAsync();

        var target = vm.RecentPurchases[0];
        vm.RequestCancelInvoice(target);
        Assert.True(vm.IsCancelConfirmOpen);
        Assert.Same(target, vm.InvoicePendingCancel);

        await vm.ConfirmCancelInvoiceAsync();
        Assert.False(vm.IsCancelConfirmOpen);
        Assert.Null(vm.InvoicePendingCancel);
        Assert.Contains("cancelled successfully", vm.StatusMessage);
    }

    [Fact]
    public void MrpChange_AutomaticallySetsSaleRate_WhenSaleRateNotExplicitlySet()
    {
        var row = new PurchaseItemRowViewModel();
        Assert.Equal(0m, row.Mrp);
        Assert.Equal(0m, row.SaleRate);

        row.Mrp = 50.00m;
        Assert.Equal(50.00m, row.SaleRate);

        row.MrpDouble = 75.50;
        Assert.Equal(75.50m, row.SaleRate);
        Assert.Equal(75.50, row.SaleRateDouble);
    }

    [Fact]
    public void MrpChange_AllowsCustomSaleRate_WhenUserModifiesSaleRate()
    {
        var row = new PurchaseItemRowViewModel();
        row.Mrp = 100.00m;
        Assert.Equal(100.00m, row.SaleRate);

        // User edits sale rate manually
        row.SaleRate = 95.00m;
        Assert.Equal(95.00m, row.SaleRate);
        Assert.Equal(100.00m, row.Mrp);
    }

    [Fact]
    public void DefaultRow_HasZeroDiscount_AndSettingsGstRate()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.LineItems.Clear();
        vm.AddBlankRow();

        var row = vm.LineItems[0];
        Assert.Equal(0m, row.DiscountPct);
        Assert.Equal(SettingsViewModel.GetDefaultGstRate(), row.GstRatePercent);
    }

    [Fact]
    public async Task SearchMedicinesAsync_PopulatesResultsAndOpensDropdown()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        await vm.SearchMedicinesAsync("dolo");

        Assert.True(vm.IsProductSearchOpen);
        Assert.NotEmpty(vm.ProductSearchResults);
        Assert.Equal(0, vm.SelectedProductSearchIndex);

        var row = vm.LineItems[0];
        vm.SelectHighlightedProduct(row);

        Assert.False(vm.IsProductSearchOpen);
        Assert.Equal("Dolo 650mg Tablet", row.ProductName);
        Assert.Equal("30049099", row.HsnCode);
        Assert.Equal(30.50m, row.Mrp);
        Assert.Equal(30.50m, row.SaleRate);
    }

    [Fact]
    public void CreateOrApplyCustomProduct_CreatesNewProductRow_WithPharmaDefaults()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        var row = vm.LineItems[0];
        vm.CreateOrApplyCustomProduct("Azithromycin 500mg", row);

        Assert.False(vm.IsProductSearchOpen);
        Assert.Equal("Azithromycin 500mg", row.ProductName);
        Assert.StartsWith("prod_", row.ProductId);
        Assert.Equal("30049099", row.HsnCode);
        Assert.Equal(SettingsViewModel.GetDefaultGstRate(), row.GstRatePercent);
        Assert.True(row.Quantity >= 1);
        Assert.True(row.ExpiryDate > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SearchRowProductAsync_WhenNoMatchFound_AutoCreatesCustomProduct()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        var row = vm.LineItems[0];
        await vm.SearchRowProductAsync("Unknown Medicine XYZ 100", row);

        Assert.Equal("Unknown Medicine XYZ 100", row.ProductName);
        Assert.StartsWith("prod_", row.ProductId);
        Assert.Equal("30049099", row.HsnCode);
    }

    [Fact]
    public void RealtimeCalculation_WhenRowInputsChange_UpdatesRowAndInvoiceTotalsAutomatically()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.LineItems.Clear();
        vm.AddBlankRow();
        var row = vm.LineItems[0];

        // Default discount is 0% and GST is default GST rate (e.g. 5%)
        Assert.Equal(0m, row.DiscountPct);
        Assert.Equal(SettingsViewModel.GetDefaultGstRate(), row.GstRatePercent);

        // Edit quantity and cost via double inputs (simulating UI NumberBox typing)
        row.QuantityDouble = 10;
        row.UnitPriceDouble = 100;
        row.MrpDouble = 150;
        row.DiscountPctDouble = 10; // 10% discount
        row.GstRatePercentDouble = 12; // 12% GST

        // Verify row calculations
        Assert.Equal(1000m, row.GrossAmount); // 10 * 100
        Assert.Equal(100m, row.DiscountAmount); // 10% of 1000
        Assert.Equal(900m, row.TaxableAmount); // 1000 - 100
        Assert.Equal(108m, row.GstAmount); // 12% of 900
        Assert.Equal(1008m, row.NetAmount); // 900 + 108
        Assert.Equal(150m, row.SaleRate); // MRP is Sale Price

        // Verify that parent ViewModel Grand Total and Taxable Subtotal automatically updated in real-time!
        Assert.Equal(900m, vm.TaxableSubtotal);
        Assert.Equal(1008m, vm.GrandTotal);
    }
}


