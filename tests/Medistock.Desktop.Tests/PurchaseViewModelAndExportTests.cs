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

    public Task<PurchaseKpiSummaryDto> GetPurchaseKpiSummaryAsync(string orgId, string branchId, string period = "All", CancellationToken cancellationToken = default)
    {
        decimal totalPur = period switch
        {
            "Today" => 5000m,
            "1 Month" => 15000m,
            _ => 22400m
        };

        return Task.FromResult(new PurchaseKpiSummaryDto(
            TotalPurchaseAmount: totalPur,
            TotalInvoicesCount: 1,
            TotalSuppliersCount: 2,
            TotalOutstandingPayable: 37000m,
            TotalStockValue: 154500m
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

    public Task<PurchasePostingResult> UpdatePurchaseInvoiceAsync(UpdatePurchaseInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PurchasePostingResult(true, command.InvoiceId, command.Items.Count, command.Items.Count * 50, 25000m));
    }

    public Task<PurchaseReturnResult> ProcessPurchaseReturnAsync(CreatePurchaseReturnCommand command, CancellationToken cancellationToken = default)
    {
        var totalQty = command.Items.Sum(i => i.ReturnQuantity);
        var totalAmt = command.Items.Sum(i => i.NetAmount);
        return Task.FromResult(new PurchaseReturnResult(true, "ret-1", "PR-0001", command.Items.Count, totalQty, totalAmt));
    }

    public Task<decimal> GetBatchAvailableStockAsync(string productId, string batchNumber, string warehouseId, string orgId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(50m);
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
        Assert.Equal(1696.43m, row.TaxableAmount);
        Assert.Equal(203.57m, row.GstAmount);
        Assert.Equal(1900.00m, row.NetAmount);
        Assert.Equal(110.00m, row.TotalQuantity);
        Assert.Equal(17.27m, row.LandedCostPerUnit);
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

        Assert.Equal(892.86m, vm.TaxableSubtotal);
        Assert.Equal(53.57m, vm.CgstTotal);
        Assert.Equal(53.57m, vm.SgstTotal);
        Assert.Equal(0m, vm.IgstTotal);
        Assert.Equal(1000m, vm.GrandTotal);

        // Interstate test
        vm.IsInterstate = true;
        vm.RecalculateTotals();

        Assert.Equal(892.86m, vm.TaxableSubtotal);
        Assert.Equal(0m, vm.CgstTotal);
        Assert.Equal(0m, vm.SgstTotal);
        Assert.Equal(107.14m, vm.IgstTotal);
        Assert.Equal(1000m, vm.GrandTotal);
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
    public async Task PurchaseEntryViewModel_SetPurchasePeriod_Updates_Period_And_KPIs()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        await vm.SetPurchasePeriodCommand.ExecuteAsync("Today");
        Assert.Equal("Today", vm.SelectedPurchasePeriod);
        Assert.Equal(5000m, vm.TotalPurchasesAmount);

        await vm.SetPurchasePeriodCommand.ExecuteAsync("1 Month");
        Assert.Equal("1 Month", vm.SelectedPurchasePeriod);
        Assert.Equal(15000m, vm.TotalPurchasesAmount);

        await vm.SetPurchasePeriodCommand.ExecuteAsync("All");
        Assert.Equal("All", vm.SelectedPurchasePeriod);
        Assert.Equal(22400m, vm.TotalPurchasesAmount);
    }

    [Fact]
    public void PurchaseEntryViewModel_RemoveRow_And_UndoRemoveRow_Restores_Row()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.LineItems.Clear();
        var row1 = new PurchaseItemRowViewModel { ProductName = "Medicine A", Quantity = 10, UnitPrice = 15m };
        var row2 = new PurchaseItemRowViewModel { ProductName = "Medicine B", Quantity = 5, UnitPrice = 25m };
        var row3 = new PurchaseItemRowViewModel { ProductName = "Medicine C", Quantity = 8, UnitPrice = 50m };
        vm.LineItems.Add(row1);
        vm.LineItems.Add(row2);
        vm.LineItems.Add(row3);
        vm.RecalculateTotals();

        Assert.Equal(3, vm.LineItems.Count);

        // Remove row 2 ("Medicine B")
        vm.RemoveRow(row2);
        Assert.Equal(2, vm.LineItems.Count);
        Assert.DoesNotContain(vm.LineItems, r => r.ProductName == "Medicine B");
        Assert.Equal(1, vm.UndoCount);

        // Undo removal -> Medicine B should be restored at its original index (1)
        vm.UndoRemoveRow();
        Assert.Equal(3, vm.LineItems.Count);
        Assert.Equal("Medicine B", vm.LineItems[1].ProductName);
        Assert.Equal(5, vm.LineItems[1].Quantity);
        Assert.Equal(25m, vm.LineItems[1].UnitPrice);
        Assert.Equal(0, vm.UndoCount);
    }

    [Fact]
    public void PurchaseEntryViewModel_MultiLevel_Undo_Restores_In_Reverse_Order()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.LineItems.Clear();
        var row1 = new PurchaseItemRowViewModel { ProductName = "Item 1", Quantity = 1 };
        var row2 = new PurchaseItemRowViewModel { ProductName = "Item 2", Quantity = 2 };
        var row3 = new PurchaseItemRowViewModel { ProductName = "Item 3", Quantity = 3 };
        vm.LineItems.Add(row1);
        vm.LineItems.Add(row2);
        vm.LineItems.Add(row3);

        // Remove Item 1, then Item 3
        vm.RemoveRow(row1);
        vm.RemoveRow(row3);
        Assert.Single(vm.LineItems);
        Assert.Equal("Item 2", vm.LineItems[0].ProductName);
        Assert.Equal(2, vm.UndoCount);

        // First Undo -> restores Item 3
        vm.UndoRemoveRow();
        Assert.Equal(2, vm.LineItems.Count);
        Assert.Contains(vm.LineItems, r => r.ProductName == "Item 3");

        // Second Undo -> restores Item 1
        vm.UndoRemoveRow();
        Assert.Equal(3, vm.LineItems.Count);
        Assert.Equal("Item 1", vm.LineItems[0].ProductName);
        Assert.Equal(0, vm.UndoCount);
    }

    [Fact]
    public void PurchaseEntryViewModel_WhenFirstRowIsActive_RemoveRow_DeletesFirstRow_NotSecondRow()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.LineItems.Clear();
        var row1 = new PurchaseItemRowViewModel { ProductName = "Medicine 1 (First)", Quantity = 10, UnitPrice = 10m };
        var row2 = new PurchaseItemRowViewModel { ProductName = "Medicine 2 (Second)", Quantity = 20, UnitPrice = 20m };
        vm.LineItems.Add(row1);
        vm.LineItems.Add(row2);
        vm.SetActiveRow(0); // First row is active/focused

        Assert.Equal(2, vm.LineItems.Count);
        Assert.Equal("Medicine 1 (First)", vm.ActiveRow?.ProductName);

        // Delete active row (first row)
        vm.RemoveRow(vm.ActiveRow);

        // Only first row was deleted, second row must still exist and be at index 0
        Assert.Single(vm.LineItems);
        Assert.Equal("Medicine 2 (Second)", vm.LineItems[0].ProductName);
        Assert.Equal(20, vm.LineItems[0].Quantity);

        // Undo restores only the first row
        vm.UndoRemoveRow();
        Assert.Equal(2, vm.LineItems.Count);
        Assert.Equal("Medicine 1 (First)", vm.LineItems[0].ProductName);
        Assert.Equal("Medicine 2 (Second)", vm.LineItems[1].ProductName);
    }

    [Fact]
    public void PurchaseEntryViewModel_UndoRemoveRow_WhenOnlyRowDeleted_RestoresOnlyDeletedRow_WithoutLeavingExtraBlankRow()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.LineItems.Clear();
        var row = new PurchaseItemRowViewModel { ProductName = "Solo Medicine", Quantity = 5, UnitPrice = 100m, BatchNumber = "B-001" };
        vm.LineItems.Add(row);
        vm.SetActiveRow(0);

        // Delete the only row -> table creates a blank placeholder
        vm.RemoveRow(row);
        Assert.Single(vm.LineItems);
        Assert.True(PurchaseEntryViewModel.IsBlankRow(vm.LineItems[0]));

        // Press Ctrl+Z (Undo) -> must restore ONLY "Solo Medicine", NO extra blank row should remain!
        vm.UndoRemoveRow();
        Assert.Single(vm.LineItems);
        Assert.Equal("Solo Medicine", vm.LineItems[0].ProductName);
        Assert.Equal(5, vm.LineItems[0].Quantity);
        Assert.Equal(100m, vm.LineItems[0].UnitPrice);
        Assert.Equal("B-001", vm.LineItems[0].BatchNumber);
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
        Assert.Equal(1m, row.StripCount);
        Assert.Equal(10m, row.PiecesPerStrip);
        Assert.Equal(10m, row.Quantity);
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
    public void PurchaseItemRowViewModel_Defaults_HaveEmptyExpiryAndHsn()
    {
        var row = new PurchaseItemRowViewModel();
        Assert.Equal(string.Empty, row.ExpiryText);
        Assert.Equal(default, row.ExpiryDate);
        Assert.Equal(string.Empty, row.HsnCode);
    }

    [Fact]
    public void ExpiryText_MMYY_ParsesCorrectExpiryDate_WithLastDayOfMonth()
    {
        var row = new PurchaseItemRowViewModel();
        // User enters "01/27" -> January 2027, last day is 31
        row.ExpiryText = "01/27";
        Assert.Equal(2027, row.ExpiryDate.Year);
        Assert.Equal(1, row.ExpiryDate.Month);
        Assert.Equal(31, row.ExpiryDate.Day);

        // "06/27" -> June 2027, last day is 30
        row.ExpiryText = "06/27";
        Assert.Equal(2027, row.ExpiryDate.Year);
        Assert.Equal(6, row.ExpiryDate.Month);
        Assert.Equal(30, row.ExpiryDate.Day);

        // "02/28" -> February 2028 (leap year), last day is 29
        row.ExpiryText = "02/28";
        Assert.Equal(2028, row.ExpiryDate.Year);
        Assert.Equal(2, row.ExpiryDate.Month);
        Assert.Equal(29, row.ExpiryDate.Day);
    }

    [Fact]
    public void ExpiryText_MMYYYY_ParsesCorrectExpiryDate()
    {
        var row = new PurchaseItemRowViewModel();
        row.ExpiryText = "12/2028";
        Assert.Equal(2028, row.ExpiryDate.Year);
        Assert.Equal(12, row.ExpiryDate.Month);
        Assert.Equal(31, row.ExpiryDate.Day);
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
        Assert.Equal(string.Empty, row.HsnCode);
        Assert.Equal(string.Empty, row.ExpiryText);
        Assert.Equal(default, row.ExpiryDate);
    }

    [Fact]
    public async Task SearchMedicinesAsync_PopulatesResultsAndOpensDropdown_AndSetsHsnForInStockProduct()
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
        // Existing in-stock product has HSN code populated by default
        Assert.Equal("30049099", row.HsnCode);
        Assert.Equal(30.50m, row.Mrp);
        Assert.Equal(30.50m, row.SaleRate);
        // Expiry starts empty for new purchase
        Assert.Equal(string.Empty, row.ExpiryText);
        Assert.Equal(default, row.ExpiryDate);
    }

    [Fact]
    public void CreateOrApplyCustomProduct_CreatesNewProductRow_WithoutHsnByDefault()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        var row = vm.LineItems[0];
        vm.CreateOrApplyCustomProduct("Azithromycin 500mg", row);

        Assert.False(vm.IsProductSearchOpen);
        Assert.Equal("Azithromycin 500mg", row.ProductName);
        Assert.StartsWith("prod_", row.ProductId);
        // New product entered to add in inventory does NOT have HSN code added by default
        Assert.Equal(string.Empty, row.HsnCode);
        Assert.Equal(SettingsViewModel.GetDefaultGstRate(), row.GstRatePercent);
        Assert.True(row.Quantity >= 1);
        // Expiry section is by default empty
        Assert.Equal(string.Empty, row.ExpiryText);
        Assert.Equal(default, row.ExpiryDate);
    }

    [Fact]
    public async Task SearchRowProductAsync_WhenNoMatchFound_OpensAddProductModal_AndSavesProduct()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        var row = vm.LineItems[0];
        await vm.SearchRowProductAsync("Unknown Medicine XYZ 100", row);

        // When product is not available in system/db, automatically opens Add Product modal
        Assert.True(vm.IsAddProductModalOpen);
        Assert.Equal("Unknown Medicine XYZ 100", vm.NewProductName);

        // Fill modal fields and save
        vm.NewProductManufacturer = "Cipla Ltd";
        vm.NewProductStockType = "Tablet (Tab)";
        vm.NewProductStripCount = 2;
        vm.NewProductPcsPerStrip = 10;
        vm.NewProductMrp = 120m;
        vm.NewProductBuyingPrice = 90m;
        vm.NewProductBatch = "b991";
        vm.NewProductExpiryText = "12/28";
        vm.NewProductHsnCode = "30049099";
        vm.NewProductTaxPercent = 12m;
        vm.NewProductIsPrescriptionRequired = true;

        // Auto-calculated stock qty is 2 strips * 10 pcs = 20
        Assert.Equal(20m, vm.NewProductInitialStockQty);

        await vm.SaveNewProductAsync();

        // Modal closes and populates the line item row
        Assert.False(vm.IsAddProductModalOpen);
        Assert.Equal("Unknown Medicine XYZ 100", row.ProductName);
        Assert.StartsWith("prod_", row.ProductId);
        Assert.Equal(string.Empty, row.GenericName);
        Assert.Equal("30049099", row.HsnCode);
        Assert.Equal("B991", row.BatchNumber);
        Assert.Equal("12/28", row.ExpiryText);
        Assert.Equal(90m, row.StripPrice);
        Assert.Equal(120m, row.StripMrp);
        Assert.Equal(9m, row.UnitPrice);
        Assert.Equal(12m, row.Mrp);
        // Selling price is considered as MRP
        Assert.Equal(12m, row.SaleRate);
        Assert.Equal(20m, row.Quantity);
        Assert.Equal(2m, row.StripCount);
        Assert.Equal(12m, row.GstRatePercent);
    }

    [Fact]
    public void AddProductModal_TabletCapsule_MultipliesStripAndPcs_CalculatesInitialStock()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.OpenAddProductModal("Paracetamol 650", vm.LineItems[0]);
        Assert.True(vm.IsAddProductModalOpen);
        Assert.True(vm.IsTabletOrCapsule);

        // If 2 strips and 15 pcs, calculate stock quantity as 30
        vm.NewProductStripCount = 2;
        vm.NewProductPcsPerStrip = 15;

        Assert.Equal(30m, vm.NewProductInitialStockQty);
        Assert.Equal("Available: 30 Pcs (2 Strips × 15 Pcs)", vm.NewProductStockBreakdownText);

        // Change strips to 4 -> 4 * 15 = 60
        vm.NewProductStripCount = 4;
        Assert.Equal(60m, vm.NewProductInitialStockQty);
        Assert.Equal("Available: 60 Pcs (4 Strips × 15 Pcs)", vm.NewProductStockBreakdownText);
    }

    [Fact]
    public void AddProductModal_Syrup_PackSizeMl_InitialStockManual()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.OpenAddProductModal("Cough Syrup", vm.LineItems[0]);
        vm.NewProductStockType = "Syrup (Syp)";

        Assert.False(vm.IsTabletOrCapsule);
        Assert.True(vm.IsVolumeMlType);
        Assert.Equal("100ml", vm.NewProductPackSizeText);

        // Initial stock qty is not autofilled by system; it is filled by user
        Assert.Equal(0m, vm.NewProductInitialStockQty);

        vm.NewProductPackSizeText = "200ml";
        vm.NewProductInitialStockQty = 5m; // 5 bottles entered by user
        Assert.Equal("Available: 5 Bottles (200ml)", vm.NewProductStockBreakdownText);
    }

    [Fact]
    public void AddProductModal_Injection_PackSizeMl_InitialStockManual()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.OpenAddProductModal("Insulin Inj", vm.LineItems[0]);
        vm.NewProductStockType = "Injection (Inj)";

        Assert.True(vm.IsVolumeMlType);
        Assert.Equal("2ml", vm.NewProductPackSizeText);
        Assert.Equal(0m, vm.NewProductInitialStockQty);

        vm.NewProductPackSizeText = "10ml";
        vm.NewProductInitialStockQty = 12m; // 12 vials entered by user
        Assert.Equal("Available: 12 Vials (10ml)", vm.NewProductStockBreakdownText);
    }

    [Fact]
    public void AddProductModal_Cream_PackSizeGm_InitialStockManual()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.OpenAddProductModal("Betnovate Cream", vm.LineItems[0]);
        vm.NewProductStockType = "Cream";

        Assert.True(vm.IsWeightGmType);
        Assert.Equal("20gm", vm.NewProductPackSizeText);
        Assert.Equal(0m, vm.NewProductInitialStockQty);

        vm.NewProductPackSizeText = "30gm";
        vm.NewProductInitialStockQty = 8m; // 8 tubes entered by user
        Assert.Equal("Available: 8 Tubes (30gm)", vm.NewProductStockBreakdownText);
    }

    [Fact]
    public async Task AddProductModal_SellingPrice_EqualsMrp()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.OpenAddProductModal("Test Med", vm.LineItems[0]);
        vm.NewProductMrp = 250m;

        // Selling price automatically tracks MRP
        Assert.Equal(250m, vm.NewProductSellingPrice);

        vm.NewProductBuyingPrice = 180m;
        await vm.SaveNewProductAsync();

        var row = vm.LineItems[0];
        Assert.Equal(250m, row.StripMrp);
        Assert.Equal(180m, row.StripPrice);
        // Default is Tablet with 10 pcs/strip, so piece price is 250 / 10 = 25, 180 / 10 = 18
        Assert.Equal(25m, row.Mrp);
        Assert.Equal(25m, row.SaleRate);
        Assert.Equal(18m, row.UnitPrice);
    }

    [Fact]
    public async Task AddProductModal_TabletCapsule_StripPriceCalculatesPiecePrice_Accurately()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.OpenAddProductModal("Test Strip Tablet", vm.LineItems[0]);
        vm.NewProductStockType = "Tablet (Tab)";
        vm.NewProductStripCount = 2;
        vm.NewProductPcsPerStrip = 10;
        vm.NewProductBuyingPrice = 10m; // Strip buying price = ₹10
        vm.NewProductMrp = 10m;         // Strip MRP = ₹10

        // Calculated piece price = 10 / 10 = 1
        Assert.Equal(1m, vm.NewProductPieceBuyingPrice);
        Assert.Equal(1m, vm.NewProductPieceMrp);
        Assert.Equal("Strip MRP (₹) *", vm.NewProductMrpHeader);
        Assert.Equal("Strip Buying Price (₹)", vm.NewProductBuyingPriceHeader);
        Assert.Contains("1 Pc MRP = ₹1.00", vm.NewProductPriceBreakdownText);
        Assert.Contains("1 Pc Buying = ₹1.00", vm.NewProductPriceBreakdownText);

        await vm.SaveNewProductAsync();

        var row = vm.LineItems[0];
        Assert.Equal(10m, row.StripPrice);
        Assert.Equal(10m, row.StripMrp);
        Assert.Equal(1m, row.UnitPrice);
        Assert.Equal(1m, row.Mrp);
        Assert.Equal(1m, row.SaleRate);
        Assert.Equal(20m, row.Quantity); // 2 strips * 10 pcs = 20
        Assert.Equal(20m, row.GrossAmount); // 20 * 1 = 20 (or 2 strips * 10 = 20)
    }

    [Fact]
    public void PurchaseItemRow_StripPriceCalculatesPiecePrice_Accurately()
    {
        var row = new PurchaseItemRowViewModel
        {
            StripCount = 2,
            PiecesPerStrip = 10,
            StripPrice = 10m,
            StripMrp = 10m
        };

        // If strip price is 10 and every strip has 10 tablets, then piece price is 1
        Assert.Equal(1m, row.UnitPrice);
        Assert.Equal(1m, row.Mrp);
        Assert.Equal(1m, row.SaleRate);
        Assert.Equal(20m, row.Quantity); // 2 strips * 10 = 20 pcs
        Assert.Equal(20m, row.GrossAmount); // 20 pcs * 1 = ₹20
        Assert.Equal("₹1.00/pc", row.UnitPricePieceDisplay);
        Assert.Equal("₹1.00/pc", row.MrpPieceDisplay);

        // If strip price changes to 20
        row.StripPrice = 20m;
        Assert.Equal(2m, row.UnitPrice); // 20 / 10 = 2
        Assert.Equal(40m, row.GrossAmount); // 20 * 2 = 40
        Assert.Equal("₹2.00/pc", row.UnitPricePieceDisplay);

        // If strip MRP changes to 25
        row.StripMrp = 25m;
        Assert.Equal(2.5m, row.Mrp); // 25 / 10 = 2.5
        Assert.Equal(2.5m, row.SaleRate);
        Assert.Equal("₹2.50/pc", row.MrpPieceDisplay);

        // If pieces per strip changes to 5 (with StripPrice 20 and StripMrp 25)
        row.PiecesPerStrip = 5;
        Assert.Equal(4m, row.UnitPrice); // 20 / 5 = 4
        Assert.Equal(5m, row.Mrp); // 25 / 5 = 5
        Assert.Equal(10m, row.Quantity); // 2 strips * 5 = 10 pcs
        Assert.Equal(40m, row.GrossAmount); // 10 * 4 = 40 (2 strips * 20 = 40)
    }

    [Fact]
    public void AddProductModal_StockType_UpdatesPackOptionsDefaults()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.OpenAddProductModal("Syrup Test", vm.LineItems[0]);
        Assert.True(vm.IsAddProductModalOpen);
        Assert.Equal("Tablet (Tab)", vm.NewProductStockType);
        Assert.Equal(10, vm.NewProductPackOptions);

        // Switch to Syrup (Syp) -> pack options becomes 1
        vm.NewProductStockType = "Syrup (Syp)";
        Assert.Equal(1, vm.NewProductPackOptions);

        // Switch to Capsule (Cap) -> pack options becomes 10
        vm.NewProductStockType = "Capsule (Cap)";
        Assert.Equal(10, vm.NewProductPackOptions);

        // Switch to Drop -> pack options becomes 1
        vm.NewProductStockType = "Drop";
        Assert.Equal(1, vm.NewProductPackOptions);
    }

    [Fact]
    public async Task AddProductModal_Validation_EnforcesMandatoryFields()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        vm.OpenAddProductModal(string.Empty, vm.LineItems[0]);

        // Attempting to save with empty name fails
        await vm.SaveNewProductAsync();
        Assert.True(vm.IsAddProductModalOpen);
        Assert.Contains("Product Name is required", vm.NewProductValidationMessage);

        // Attempting to save with MRP <= 0 fails
        vm.NewProductName = "Valid Medicine";
        vm.NewProductMrp = 0;
        await vm.SaveNewProductAsync();
        Assert.True(vm.IsAddProductModalOpen);
        Assert.Contains("MRP must be greater than zero", vm.NewProductValidationMessage);

        // Attempting to save with Buying Price > MRP fails
        vm.NewProductMrp = 100m;
        vm.NewProductBuyingPrice = 120m;
        await vm.SaveNewProductAsync();
        Assert.True(vm.IsAddProductModalOpen);
        Assert.Contains("Buying price cannot exceed MRP", vm.NewProductValidationMessage);

        // Valid values succeed
        vm.NewProductBuyingPrice = 80m;
        await vm.SaveNewProductAsync();
        Assert.False(vm.IsAddProductModalOpen);
        Assert.Empty(vm.NewProductValidationMessage);
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
        Assert.Equal(803.57m, row.TaxableAmount); // 900 / 1.12
        Assert.Equal(96.43m, row.GstAmount); // 900 - 803.57
        Assert.Equal(900m, row.NetAmount); // 1000 - 100
        Assert.Equal(150m, row.SaleRate); // MRP is Sale Price

        // Verify that parent ViewModel Grand Total and Taxable Subtotal automatically updated in real-time!
        Assert.Equal(803.57m, vm.TaxableSubtotal);
        Assert.Equal(900m, vm.GrandTotal);
    }

    [Fact]
    public void PurchaseItemRow_BatchNumber_IsAutomaticallyCapitalized()
    {
        var row = new PurchaseItemRowViewModel();
        row.BatchNumber = "abc123xyz";
        Assert.Equal("ABC123XYZ", row.BatchNumber);

        row.BatchNumber = "batch-test-99";
        Assert.Equal("BATCH-TEST-99", row.BatchNumber);
    }

    [Fact]
    public async Task PurchaseKpis_TotalStockValue_IsLoadedAndFormatted()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        await vm.LoadKpisAsync();

        Assert.Equal(154500m, vm.TotalStockValue);
        Assert.Equal($"₹{154500m:N2}", vm.TotalStockValueFormatted);
        Assert.Equal(22400m, vm.TotalPurchasesAmount);
        Assert.Equal(1, vm.TotalInvoicesCount);
        Assert.Equal(2, vm.TotalSuppliersCount);
    }

    [Fact]
    public async Task StatsCardsCommands_ViewPurchasesAndWholesalersAndRefreshStockValue()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        // Clicking Total Purchases or Invoices Recorded switches to History (Inward Invoices Ledger)
        await vm.ViewPurchasesLedgerCommand.ExecuteAsync(null);
        Assert.Equal("History", vm.SelectedTab);
        Assert.Contains("Inward Invoices Ledger", vm.StatusMessage);

        // Clicking Active Wholesalers switches to Suppliers tab (Wholesaler Directory)
        await vm.ViewWholesalersDirectoryCommand.ExecuteAsync(null);
        Assert.Equal("Suppliers", vm.SelectedTab);
        Assert.Contains("Wholesaler Directory", vm.StatusMessage);

        // Clicking Total Stock Value refreshes valuation
        await vm.RefreshStockValueCommand.ExecuteAsync(null);
        Assert.Equal(154500m, vm.TotalStockValue);
        Assert.Contains("Total Stock Valuation updated", vm.StatusMessage);
    }

    [Fact]
    public void SupplierSelection_SyncsSupplierSearchText()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        var testSupplier = new SupplierDto(
            Id: "sup-99",
            Name: "LifeCare Pharmaceuticals Ltd",
            Gstin: "27AABCL1234F1Z8",
            DlNumber: "DL-20B-9988",
            Phone: "9876543210",
            Email: "sales@lifecare.com",
            Address: "Mumbai, MH",
            CreditDays: 45,
            CurrentOutstandingBalance: 12000m,
            IsActive: true
        );

        vm.OnSupplierSelected(testSupplier);

        Assert.Equal("LifeCare Pharmaceuticals Ltd", vm.SelectedSupplierName);
        Assert.Equal("LifeCare Pharmaceuticals Ltd", vm.SupplierSearchText);
        Assert.Equal("27AABCL1234F1Z8", vm.SupplierGstin);
        Assert.Equal("DL-20B-9988", vm.SupplierDlNumber);
        Assert.Equal(45, vm.SupplierCreditDays);
        Assert.Equal(12000m, vm.SupplierOutstandingBalance);
    }

    [Fact]
    public void SupplierDto_FormattedPurchaseNoAndInvoiceNo_ReturnsCorrectDisplay()
    {
        var supplierWithoutBill = new SupplierDto("sup-1", "Test Wholesaler", "07AABCU1234F1Z1", "DL-1", "123", "a@b.com", "City", 30, 0m, true);
        Assert.Null(supplierWithoutBill.PurchaseNo);
        Assert.Null(supplierWithoutBill.InvoiceNo);
        Assert.Equal("—", supplierWithoutBill.FormattedPurchaseNo);
        Assert.Equal("—", supplierWithoutBill.FormattedInvoiceNo);

        var supplierWithBill = new SupplierDto("sup-2", "Apex Wholesalers", "07AABCU1234F1Z2", "DL-2", "456", "b@b.com", "City", 30, 0m, true, "PO-0001", "INV-2026-99");
        Assert.Equal("PO-0001", supplierWithBill.PurchaseNo);
        Assert.Equal("PO-0001", supplierWithBill.FormattedPurchaseNo);
        Assert.Equal("INV-2026-99", supplierWithBill.InvoiceNo);
        Assert.Equal("INV-2026-99", supplierWithBill.FormattedInvoiceNo);
    }

    [Fact]
    public async Task WholesalerDirectory_LoadsSuppliers_AndPopulatesPurchaseNoAndInvoiceNo()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        await vm.ViewWholesalersDirectoryCommand.ExecuteAsync(null);

        Assert.Equal("Suppliers", vm.SelectedTab);
        Assert.NotEmpty(vm.FilteredSuppliers);

        // Apex Pharma has recorded invoice APEX/2026/001 in RecentPurchasesList -> should get PO-0001 and APEX/2026/001
        var apexSupplier = vm.FilteredSuppliers.FirstOrDefault(s => s.Name == "Apex Pharma Wholesalers");
        Assert.NotNull(apexSupplier);
        Assert.Equal("PO-0001", apexSupplier.PurchaseNo);
        Assert.Equal("PO-0001", apexSupplier.FormattedPurchaseNo);
        Assert.Equal("APEX/2026/001", apexSupplier.InvoiceNo);
        Assert.Equal("APEX/2026/001", apexSupplier.FormattedInvoiceNo);

        // Cipla Direct Depot has no invoices recorded yet
        var ciplaSupplier = vm.FilteredSuppliers.FirstOrDefault(s => s.Name == "Cipla Direct Depot");
        Assert.NotNull(ciplaSupplier);
        Assert.Equal("—", ciplaSupplier.FormattedPurchaseNo);
        Assert.Equal("—", ciplaSupplier.FormattedInvoiceNo);
    }

    [Fact]
    public async Task PurchaseEntry_SupplierFields_EmptyByDefault()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        // Verification: On initial creation, supplier is empty
        Assert.Null(vm.SelectedSupplier);
        Assert.True(string.IsNullOrEmpty(vm.SelectedSupplierName));
        Assert.True(string.IsNullOrEmpty(vm.SupplierSearchText));
        Assert.True(string.IsNullOrEmpty(vm.SupplierGstin));
        Assert.True(string.IsNullOrEmpty(vm.SupplierDlNumber));
        Assert.Equal(0, vm.SupplierCreditDays);
        Assert.Equal(0m, vm.SupplierOutstandingBalance);

        // Verification: After loading suppliers, it does NOT auto-select Suppliers[0]
        await vm.LoadSuppliersCommand.ExecuteAsync(null);
        Assert.Null(vm.SelectedSupplier);
        Assert.True(string.IsNullOrEmpty(vm.SelectedSupplierName));
        Assert.True(string.IsNullOrEmpty(vm.SupplierSearchText));
    }

    [Fact]
    public async Task PurchaseEntry_EditInvoice_LoadsDataIntoEntryForm_AndUpdatesOnSave()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        var summary = new PurchaseInvoiceSummaryDto(
            Id: "inv-1",
            SupplierName: "Apex Pharma Wholesalers",
            SupplierGstin: "07AABCU9603R1ZM",
            SupplierInvoiceNo: "APEX/2026/001",
            SupplierInvoiceDate: DateTime.UtcNow.AddDays(-2),
            Status: PurchaseInvoiceStatus.Posted,
            TaxableAmount: 2000m,
            CgstAmount: 120m,
            SgstAmount: 120m,
            IgstAmount: 0m,
            GrandTotal: 2240m,
            ItemCount: 1,
            CreatedAt: DateTime.UtcNow.AddDays(-2),
            PostedAt: DateTime.UtcNow.AddDays(-2),
            PurchaseNo: "PO-0001"
        );

        // 1. Trigger Edit
        await vm.EditPurchaseInvoiceCommand.ExecuteAsync(summary);

        Assert.True(vm.IsEditingInvoice);
        Assert.Equal("inv-1", vm.EditingInvoiceId);
        Assert.Equal("PO-0001", vm.EditingInvoicePurchaseNo);
        Assert.Equal("APEX/2026/001", vm.SupplierInvoiceNo);
        Assert.Equal("Apex Pharma Wholesalers", vm.SelectedSupplierName);
        Assert.Equal("Entry", vm.SelectedTab);
        Assert.Equal("Save Changes [Ctrl+S]", vm.SaveButtonText);
        Assert.Contains("PO-0001", vm.FormHeaderTitle);
        Assert.Single(vm.LineItems);
        Assert.Equal("Dolo 650", vm.LineItems[0].ProductName);
        Assert.Equal(100m, vm.LineItems[0].Quantity);

        // 2. Add a new item to this same bill
        vm.LineItems.Add(new PurchaseItemRowViewModel
        {
            ProductId = "p2",
            ProductName = "Paracetamol 500",
            BatchNumber = "PCM2602",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            ExpiryText = "12/27",
            Quantity = 50,
            UnitPrice = 15m,
            Mrp = 25m,
            GstRatePercent = 12m
        });

        Assert.Equal(2, vm.LineItems.Count);

        // 3. Save updated bill
        await vm.PostPurchaseInvoiceCommand.ExecuteAsync(null);

        // Should exit edit mode and reset form
        Assert.False(vm.IsEditingInvoice);
        Assert.Null(vm.EditingInvoiceId);
        Assert.Contains("updated successfully", vm.StatusMessage);
    }

    [Fact]
    public async Task PurchaseEntry_PurchaseReturn_OpensModal_CalculatesNetValuation_AndGeneratesReturnBill()
    {
        var purchaseService = new MockPurchaseService();
        var searchRepo = new MockProductSearchRepository();
        var vm = new PurchaseEntryViewModel(purchaseService, searchRepo);

        var summary = new PurchaseInvoiceSummaryDto(
            Id: "inv-1",
            SupplierName: "Apex Pharma Wholesalers",
            SupplierGstin: "07AABCU9603R1ZM",
            SupplierInvoiceNo: "APEX/2026/001",
            SupplierInvoiceDate: DateTime.UtcNow.AddDays(-2),
            Status: PurchaseInvoiceStatus.Posted,
            TaxableAmount: 2000m,
            CgstAmount: 120m,
            SgstAmount: 120m,
            IgstAmount: 0m,
            GrandTotal: 2240m,
            ItemCount: 1,
            CreatedAt: DateTime.UtcNow.AddDays(-2),
            PostedAt: DateTime.UtcNow.AddDays(-2),
            PurchaseNo: "PO-0001"
        );

        // 1. Open Return Dialog
        await vm.OpenPurchaseReturnCommand.ExecuteAsync(summary);

        Assert.True(vm.IsPurchaseReturnModalOpen);
        Assert.NotNull(vm.ReturnSourceInvoice);
        Assert.Single(vm.ReturnItems);

        var returnItem = vm.ReturnItems[0];
        Assert.Equal("Dolo 650", returnItem.ProductName);
        Assert.Equal(110m, returnItem.InwardedQuantity); // Qty (100) + Free (10)
        Assert.Equal(50m, returnItem.AvailableStock);
        Assert.Equal(50m, returnItem.MaxReturnable); // min(110, 50)
        // Landed cost / net unit rate with GST
        Assert.True(returnItem.NetUnitPrice > 0);

        // 2. Select item for return with quantity 10
        returnItem.IsSelected = true;
        returnItem.ReturnQuantity = 10m;
        returnItem.Reason = "Expired Batch";

        Assert.Equal(1, vm.TotalReturnSelectedItems);
        Assert.Equal(10m, vm.TotalReturnQuantity);
        Assert.True(vm.TotalReturnAmount > 0);

        // 3. Confirm Return
        await vm.ConfirmPurchaseReturnCommand.ExecuteAsync(null);

        // Modal closes, Return Bill modal opens
        Assert.False(vm.IsPurchaseReturnModalOpen);
        Assert.True(vm.IsPurchaseReturnBillModalOpen);
        Assert.NotNull(vm.CurrentPurchaseReturnBill);
        Assert.Equal("PR-0001", vm.CurrentPurchaseReturnBill.ReturnNumber);
        Assert.Equal("PO-0001", vm.CurrentPurchaseReturnBill.OriginalPurchaseNo);
        Assert.Equal("APEX/2026/001", vm.CurrentPurchaseReturnBill.OriginalInvoiceNo);
        Assert.Equal("Apex Pharma Wholesalers", vm.CurrentPurchaseReturnBill.SupplierName);
        Assert.Contains("PR-0001", vm.StatusMessage);

        // 4. Close Return Bill
        vm.ClosePurchaseReturnBillModalCommand.Execute(null);
        Assert.False(vm.IsPurchaseReturnBillModalOpen);
        Assert.Null(vm.CurrentPurchaseReturnBill);
    }

    [Fact]
    public void PurchaseItemRow_StripAndPieceCalculation_WorksCorrectly()
    {
        var row = new PurchaseItemRowViewModel
        {
            StripCount = 5,
            PiecesPerStrip = 15
        };

        // 5 strips * 15 pieces = 75 total quantity
        Assert.Equal(75m, row.Quantity);
        Assert.Equal("5 Strip × 15 Pcs = 75 Total Qty", row.PackCalculationDisplay);

        // Changing StripCount updates Quantity
        row.StripCount = 10;
        Assert.Equal(150m, row.Quantity);
        Assert.Equal("10 Strip × 15 Pcs = 150 Total Qty", row.PackCalculationDisplay);

        // Changing PiecesPerStrip updates Quantity and PackUnits
        row.PiecesPerStrip = 10;
        Assert.Equal(100m, row.Quantity);
        Assert.Equal(10, row.PackUnits);

        // Changing Quantity updates StripCount (50 / 10 = 5)
        row.Quantity = 50;
        Assert.Equal(5m, row.StripCount);
    }

    [Fact]
    public void SupplierInvoiceDateText_SyncsWith_SupplierInvoiceDate_AndParsesCorrectly()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());

        // Default date text should be today's date formatted dd/MM/yyyy
        Assert.False(string.IsNullOrWhiteSpace(vm.SupplierInvoiceDateText));

        // Setting a custom date string parses to SupplierInvoiceDate
        vm.SupplierInvoiceDateText = "15/08/2026";
        Assert.Equal(15, vm.SupplierInvoiceDate.Day);
        Assert.Equal(8, vm.SupplierInvoiceDate.Month);
        Assert.Equal(2026, vm.SupplierInvoiceDate.Year);

        // Setting SupplierInvoiceDate updates SupplierInvoiceDateText
        vm.SupplierInvoiceDate = new DateTimeOffset(new DateTime(2028, 12, 25), TimeSpan.Zero);
        Assert.Equal("25/12/2028", vm.SupplierInvoiceDateText);
    }

    [Fact]
    public async Task SaveNewProductAsync_ClearsFormData_AfterSuccessfulSave()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());
        vm.OpenAddProductModal("Paracetamol 650mg", null);

        Assert.True(vm.IsAddProductModalOpen);
        Assert.Equal("Paracetamol 650mg", vm.NewProductName);

        vm.NewProductMrp = 30m;
        vm.NewProductBuyingPrice = 20m;
        vm.NewProductExpiryText = "12/28";

        await vm.SaveNewProductAsync();

        // Modal should be closed
        Assert.False(vm.IsAddProductModalOpen);
        // Form fields must be cleared/reset
        Assert.Equal(string.Empty, vm.NewProductName);
        Assert.Equal(0m, vm.NewProductMrp);
        Assert.Equal(0m, vm.NewProductBuyingPrice);

        // New product was added to line items
        Assert.Contains(vm.LineItems, r => r.ProductName == "Paracetamol 650mg");
    }

    [Fact]
    public void PurchaseEntryViewModel_DefaultSelectedTab_IsHistory()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());
        Assert.Equal("History", vm.SelectedTab);

        vm.OpenPurchaseEntry();
        Assert.Equal("Entry", vm.SelectedTab);
    }

    [Fact]
    public void PurchaseItemRowViewModel_UnitPackagingHoverText_FormatsCorrectly_PerStockType()
    {
        var row = new PurchaseItemRowViewModel
        {
            StripCount = 1,
            PiecesPerStrip = 10,
            Quantity = 10,
            StockType = "Tablet (Tab)"
        };
        Assert.Equal("1 Strip (10 Pcs/Strip) — Total 10 Pcs", row.UnitPackagingHoverText);

        row.StockType = "Syrup (Syp)";
        row.Unit = "BOTTLE";
        row.PiecesPerStrip = 1;
        row.PackUnits = 100;
        row.Quantity = 5;
        Assert.Equal("5 Units (100ml)", row.UnitPackagingHoverText);

        row.StockType = "Cream";
        row.Unit = "TUBE";
        row.PiecesPerStrip = 1;
        row.PackUnits = 30;
        row.Quantity = 2;
        Assert.Equal("2 Tubes (30gm)", row.UnitPackagingHoverText);
    }

    [Fact]
    public async Task PurchaseEntryViewModel_SearchProductsTopAsync_And_SelectTopProductSearch_AddsRow()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());
        vm.LineItems.Clear();

        await vm.SearchProductsTopAsync("dolo");
        Assert.True(vm.HasProductSearchResults);
        Assert.NotEmpty(vm.ProductSearchResults);

        var match = vm.ProductSearchResults[0];
        vm.SelectTopProductSearch(match);

        Assert.Single(vm.LineItems);
        Assert.Equal("Dolo 650mg Tablet", vm.LineItems[0].ProductName);
        Assert.Equal("30049099", vm.LineItems[0].HsnCode);
        Assert.Equal(string.Empty, vm.ProductSearchQuery);
        Assert.False(vm.HasProductSearchResults);
    }

    [Fact]
    public void PurchaseEntryViewModel_AddSearchedProductModal_OpensModalWithPrefilledName()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());
        vm.ProductSearchQuery = "New Drug 500mg";

        Assert.Equal("➕ Add \"New Drug 500mg\" [F3]", vm.AddNewProductButtonText);

        vm.AddSearchedProductModal();
        Assert.True(vm.IsAddProductModalOpen);
        Assert.Equal("New Drug 500mg", vm.NewProductName);
    }

    [Fact]
    public void PurchaseEntryViewModel_PureScreenNavigation_And_ExitConfirmationFlow()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());
        Assert.False(vm.IsPurchaseEntryScreenOpen);
        Assert.Equal("History", vm.SelectedTab);

        // Open pure purchase entry screen
        vm.OpenPurchaseEntry();
        Assert.True(vm.IsPurchaseEntryScreenOpen);
        Assert.Equal("Entry", vm.SelectedTab);

        // When no unsaved data, RequestClose closes immediately
        Assert.False(vm.HasUnsavedData);
        vm.RequestClosePurchaseEntry();
        Assert.False(vm.IsPurchaseEntryScreenOpen);
        Assert.Equal("History", vm.SelectedTab);
        Assert.False(vm.IsExitConfirmDialogOpen);

        // Re-open and add data
        vm.OpenPurchaseEntry();
        vm.SupplierInvoiceNo = "INV-999";
        Assert.True(vm.HasUnsavedData);

        // RequestClose when data entered opens Exit Confirm dialog
        vm.RequestClosePurchaseEntry();
        Assert.True(vm.IsExitConfirmDialogOpen);
        Assert.True(vm.IsPurchaseEntryScreenOpen);

        // Keep editing cancels dialog
        vm.CancelExitDialog();
        Assert.False(vm.IsExitConfirmDialogOpen);
        Assert.True(vm.IsPurchaseEntryScreenOpen);

        // Save draft exits screen but preserves data
        vm.SaveDraftAndExit();
        Assert.False(vm.IsPurchaseEntryScreenOpen);
        Assert.Equal("History", vm.SelectedTab);
        Assert.Equal("INV-999", vm.SupplierInvoiceNo);

        // Re-open and discard
        vm.OpenPurchaseEntry();
        vm.DiscardAndExit();
        Assert.False(vm.IsPurchaseEntryScreenOpen);
        Assert.Equal("History", vm.SelectedTab);
        Assert.Equal(string.Empty, vm.SupplierInvoiceNo);
    }

    [Fact]
    public void PurchaseEntryViewModel_SaveSummaryModal_And_StockTypeCycling()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());
        vm.OpenPurchaseEntry();

        // Requesting summary with no items shows validation error
        vm.LineItems.Clear();
        vm.RequestSaveSummary();
        Assert.False(vm.IsSaveSummaryModalOpen);
        Assert.Contains("Please add at least one medicine", vm.StatusMessage);

        // Add valid item and invoice no
        vm.AddBlankRow();
        vm.LineItems[0].ProductName = "Paracetamol 650mg";
        vm.LineItems[0].UnitPrice = 10m;
        vm.LineItems[0].Mrp = 15m;
        vm.LineItems[0].BatchNumber = "B101";
        vm.LineItems[0].ExpiryText = "12/28";
        vm.SupplierInvoiceNo = "APEX-5544";

        vm.RequestSaveSummary();
        Assert.True(vm.IsSaveSummaryModalOpen);

        vm.CancelSaveSummary();
        Assert.False(vm.IsSaveSummaryModalOpen);

        // Stock type navigation
        vm.NewProductStockType = "Tablet (Tab)";
        vm.SelectNextStockType();
        Assert.Equal("Capsule (Cap)", vm.NewProductStockType);

        vm.SelectPreviousStockType();
        Assert.Equal("Tablet (Tab)", vm.NewProductStockType);
    }

    [Fact]
    public async Task PurchaseEntryViewModel_InitialOpenHasNoRows_AndDynamicRowAddedOnSelectOrSave()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());
        vm.ResetForm();
        Assert.Empty(vm.LineItems);

        vm.OpenPurchaseEntry();
        Assert.True(vm.IsPurchaseEntryScreenOpen);
        Assert.Equal("Entry", vm.SelectedTab);
        Assert.Empty(vm.LineItems);

        // Dynamic row added upon product selection from top search
        await vm.SearchProductsTopAsync("dolo");
        Assert.NotEmpty(vm.ProductSearchResults);

        var match = vm.ProductSearchResults[0];
        var addedRow = vm.SelectTopProductSearch(match);

        Assert.Single(vm.LineItems);
        Assert.Same(addedRow, vm.LineItems[0]);
        Assert.Equal("Dolo 650mg Tablet", vm.LineItems[0].ProductName);
        Assert.Equal("30049099", vm.LineItems[0].HsnCode);
    }

    [Fact]
    public async Task PurchaseEntryViewModel_SequentialAdditions_DoNotOverwriteExistingRows()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());
        vm.ResetForm();
        Assert.Empty(vm.LineItems);

        // 1. Add custom product "kawsar" via modal
        vm.OpenAddProductModal("kawsar", null);
        vm.NewProductMrp = 50m;
        vm.NewProductBuyingPrice = 40m;
        await vm.SaveNewProductAsync();
        Assert.Single(vm.LineItems);
        Assert.Equal("kawsar", vm.LineItems[0].ProductName);

        // 2. Add product from search autocomplete "Dolo 650mg Tablet"
        await vm.SearchProductsTopAsync("dolo");
        vm.SelectTopProductSearch(vm.ProductSearchResults[0]);
        Assert.Equal(2, vm.LineItems.Count);
        Assert.Equal("kawsar", vm.LineItems[0].ProductName);
        Assert.Equal("Dolo 650mg Tablet", vm.LineItems[1].ProductName);

        // 3. Add second custom product "tamanna" via modal (targetRow is null)
        vm.OpenAddProductModal("tamanna", null);
        vm.NewProductMrp = 100m;
        vm.NewProductBuyingPrice = 80m;
        await vm.SaveNewProductAsync();

        // 4. Assert all 3 products are intact and nothing was overwritten!
        Assert.Equal(3, vm.LineItems.Count);
        Assert.Equal("kawsar", vm.LineItems[0].ProductName);
        Assert.Equal("Dolo 650mg Tablet", vm.LineItems[1].ProductName);
        Assert.Equal("tamanna", vm.LineItems[2].ProductName);
    }

    [Fact]
    public void PurchaseEntryViewModel_TaxAndPackSizeCycle_WorksCorrectly()
    {
        var vm = new PurchaseEntryViewModel(new MockPurchaseService(), new MockProductSearchRepository());
        vm.OpenAddProductModal("Test Drug", null);

        // Tax cycling
        vm.NewProductTaxPercent = 5m;
        vm.SelectNextTaxRate();
        Assert.Equal(12m, vm.NewProductTaxPercent);
        vm.SelectNextTaxRate();
        Assert.Equal(18m, vm.NewProductTaxPercent);
        vm.SelectPreviousTaxRate();
        Assert.Equal(12m, vm.NewProductTaxPercent);

        // Volume ML pack size cycling
        vm.NewProductStockType = "Syrup (Syp)";
        vm.NewProductPackSizeText = "100ml";
        vm.SelectNextPackSize();
        Assert.Equal("200ml", vm.NewProductPackSizeText);
        vm.SelectPreviousPackSize();
        Assert.Equal("100ml", vm.NewProductPackSizeText);
    }
}



