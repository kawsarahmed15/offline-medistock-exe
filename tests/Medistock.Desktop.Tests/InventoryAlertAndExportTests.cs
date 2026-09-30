using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Inventory.DTOs;
using Medistock.Application.Inventory.Services;
using Medistock.Desktop.ViewModels;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Hardware.Export;
using Xunit;

namespace Medistock.Desktop.Tests;

public class FakeInventoryService : IInventoryService
{
    public List<StockSummaryItemDto> Items { get; set; } = new();

    public Task<IReadOnlyList<StockSummaryItemDto>> GetStockSummaryAsync(
        string warehouseId,
        string? searchQuery = null,
        ExpiryBand? expiryBand = null,
        DrugSchedule? schedule = null,
        bool lowStockOnly = false,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<StockSummaryItemDto>>(Items);
    }

    public Task<ExpiryDashboardDto> GetExpiryDashboardAsync(string warehouseId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<StockAdjustmentResult> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new StockAdjustmentResult(true, "M-001", 10));
    }

    public Task<StockAdjustmentResult> QuarantineExpiredBatchAsync(string orgId, string branchId, string warehouseId, string productId, string batchId, decimal currentQuantity, string reason, string userId, string deviceId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new StockAdjustmentResult(true, "M-002", 0));
    }

    public Task<UpdateProductDetailsResult> UpdateProductDetailsAsync(UpdateProductDetailsCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UpdateProductDetailsResult(true));
    }

    public Task<InventoryFinancialMetricsDto> GetFinancialMetricsAsync(string? monthPrefix = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new InventoryFinancialMetricsDto(
            RevenueThisMonth: 154200.50m,
            MonthlyInvoicesCount: 42,
            CashCollectionThisMonth: 95400.00m,
            CashInvoicesCount: 28,
            OnlineCollectionThisMonth: 58800.50m,
            OnlineInvoicesCount: 14,
            AllTimeRevenue: 520000m,
            AllTimeCash: 350000m,
            AllTimeOnline: 170000m
        ));
    }
}

public class InventoryAlertAndExportTests
{
    [Fact]
    public void StockItemViewModel_WhenExpired_FormatsRed()
    {
        var dto = new StockSummaryItemDto(
            ProductId: "P1",
            ProductName: "Paracetamol 650",
            GenericName: "Paracetamol",
            SaltComposition: "Paracetamol 650mg",
            Manufacturer: "Cipla",
            CategoryName: "Tablet",
            Schedule: DrugSchedule.OTC,
            BatchId: "B1",
            BatchNumber: "BATCH-EXP",
            ExpiryDate: DateTime.UtcNow.AddDays(-5),
            DaysUntilExpiry: -5,
            ExpiryStatus: ExpiryBand.Expired,
            AvailableQuantity: 50,
            ReservedQuantity: 0,
            TotalQuantity: 50,
            Mrp: 30,
            PurchaseRate: 20,
            SaleRate: 28,
            StockValueAtMrp: 1500,
            StockValueAtCost: 1000,
            MinStockAlert: 10
        );

        var vm = new StockItemViewModel(dto, nearExpiryDays: 90);

        Assert.True(vm.IsExpired);
        Assert.False(vm.IsNearExpiry);
        Assert.Equal("EXPIRED", vm.ExpiryBadgeText);
        Assert.Equal("#DC2626", vm.ExpiryBadgeColor);
        Assert.Equal("#DC2626", vm.ExpiryForegroundHex);
        Assert.Equal("#25DC2626", vm.RowBackgroundHex);
        Assert.Equal("#DC2626", vm.RowBorderHex);
        Assert.Equal("50", vm.StockDisplay);
        Assert.Equal("#16A34A", vm.StockForegroundHex);
    }

    [Fact]
    public void StockItemViewModel_WhenNearExpiry_FormatsRedAlert()
    {
        var dto = new StockSummaryItemDto(
            ProductId: "P2",
            ProductName: "Amoxicillin 500",
            GenericName: "Amoxicillin",
            SaltComposition: "Amoxicillin",
            Manufacturer: "Sun Pharma",
            CategoryName: "Capsule",
            Schedule: DrugSchedule.ScheduleH,
            BatchId: "B2",
            BatchNumber: "BATCH-NEAR",
            ExpiryDate: DateTime.UtcNow.AddDays(25),
            DaysUntilExpiry: 25,
            ExpiryStatus: ExpiryBand.Critical,
            AvailableQuantity: 40,
            ReservedQuantity: 0,
            TotalQuantity: 40,
            Mrp: 120,
            PurchaseRate: 80,
            SaleRate: 110,
            StockValueAtMrp: 4800,
            StockValueAtCost: 3200,
            MinStockAlert: 10
        );

        var vm = new StockItemViewModel(dto, nearExpiryDays: 90);

        Assert.False(vm.IsExpired);
        Assert.True(vm.IsNearExpiry);
        Assert.Contains("EXP NEAR", vm.ExpiryBadgeText);
        Assert.Equal("#DC2626", vm.ExpiryBadgeColor);
        Assert.Equal("#DC2626", vm.ExpiryForegroundHex);
        Assert.Equal("#15DC2626", vm.RowBackgroundHex);
        Assert.Equal("#80DC2626", vm.RowBorderHex);
    }

    [Fact]
    public void StockItemViewModel_WhenLowStock_ShowsAmberAlert()
    {
        var dto = new StockSummaryItemDto(
            ProductId: "P3",
            ProductName: "Cetirizine 10",
            GenericName: "Cetirizine",
            SaltComposition: "Cetirizine HCl",
            Manufacturer: "Dr Reddy",
            CategoryName: "Tablet",
            Schedule: DrugSchedule.OTC,
            BatchId: "B3",
            BatchNumber: "BATCH-LOW",
            ExpiryDate: DateTime.UtcNow.AddDays(300),
            DaysUntilExpiry: 300,
            ExpiryStatus: ExpiryBand.Good,
            AvailableQuantity: 4,
            ReservedQuantity: 0,
            TotalQuantity: 4,
            Mrp: 45,
            PurchaseRate: 25,
            SaleRate: 40,
            StockValueAtMrp: 180,
            StockValueAtCost: 100,
            MinStockAlert: 10
        );

        var vm = new StockItemViewModel(dto, nearExpiryDays: 90);

        Assert.False(vm.IsOutOfStock);
        Assert.True(vm.IsLowStock);
        Assert.Equal("4 (LOW)", vm.StockDisplay);
        Assert.Equal("#D97706", vm.StockForegroundHex);
    }

    [Fact]
    public void StockItemViewModel_WhenOutOfStock_ShowsRedAlert()
    {
        var dto = new StockSummaryItemDto(
            ProductId: "P4",
            ProductName: "Azithromycin 500",
            GenericName: "Azithromycin",
            SaltComposition: "Azithromycin",
            Manufacturer: "Lupin",
            CategoryName: "Tablet",
            Schedule: DrugSchedule.ScheduleH,
            BatchId: "B4",
            BatchNumber: "BATCH-OOS",
            ExpiryDate: DateTime.UtcNow.AddDays(300),
            DaysUntilExpiry: 300,
            ExpiryStatus: ExpiryBand.Good,
            AvailableQuantity: 0,
            ReservedQuantity: 0,
            TotalQuantity: 0,
            Mrp: 150,
            PurchaseRate: 90,
            SaleRate: 140,
            StockValueAtMrp: 0,
            StockValueAtCost: 0,
            MinStockAlert: 10
        );

        var vm = new StockItemViewModel(dto, nearExpiryDays: 90);

        Assert.True(vm.IsOutOfStock);
        Assert.Equal("0 (OOS)", vm.StockDisplay);
        Assert.Equal("#DC2626", vm.StockForegroundHex);
    }

    [Fact]
    public async Task InventoryViewModel_CalculatesKpisCorrectly()
    {
        var fakeService = new FakeInventoryService();
        fakeService.Items.Add(new StockSummaryItemDto("P1", "Med A", "Gen A", "Salt A", "Mfr A", "Tab", DrugSchedule.OTC, "B1", "BATCH-1", DateTime.UtcNow.AddDays(-1), -1, ExpiryBand.Expired, 10, 0, 10, 50, 30, 45, 500, 300, 5));
        fakeService.Items.Add(new StockSummaryItemDto("P1", "Med A", "Gen A", "Salt A", "Mfr A", "Tab", DrugSchedule.OTC, "B2", "BATCH-2", DateTime.UtcNow.AddDays(200), 200, ExpiryBand.Good, 50, 0, 50, 50, 30, 45, 2500, 1500, 5));
        fakeService.Items.Add(new StockSummaryItemDto("P2", "Med B", "Gen B", "Salt B", "Mfr B", "Tab", DrugSchedule.ScheduleH, "B3", "BATCH-3", DateTime.UtcNow.AddDays(20), 20, ExpiryBand.Critical, 2, 0, 2, 100, 60, 90, 200, 120, 10));

        var vm = new InventoryViewModel(fakeService, new InventoryExportService());
        await vm.LoadStocksAsync();

        Assert.Equal(3, vm.TotalItemsCount);
        Assert.Equal(2, vm.TotalProductsAvailable); // P1 and P2
        Assert.Equal(2, vm.ExpiryNearProductsCount); // B1 (expired) and B3 (critical)
        Assert.Equal(1, vm.LowStockProductsCount); // B3 (qty 2 <= alert 10)
        Assert.Equal(1920, vm.TotalStockValue); // 300 + 1500 + 120
        Assert.Equal(3200, vm.TotalStockValueMrp); // 500 + 2500 + 200
    }

    [Fact]
    public async Task InventoryExportService_GeneratesValidExcelAndWordAndPdf()
    {
        var exportService = new InventoryExportService();
        var tempDir = Path.Combine(Path.GetTempPath(), "MedistockExportTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var meta = new InventoryExportMetadata(
                PharmacyName: "Test Pharmacy Care",
                StoreAddress: "Test Address 123",
                ContactPhone: "9999999999",
                Gstin: "07TESTGSTIN",
                ExportDate: DateTime.Now,
                TotalProducts: 10,
                TotalBatches: 15,
                NearExpiryCount: 2,
                LowStockCount: 1,
                TotalStockValueCost: 25000,
                TotalStockValueMrp: 35000
            );

            var rows = new List<InventoryExportRow>
            {
                new(1, "Paracetamol 500", "Paracetamol", "Paracetamol 500mg", "B-001", DateTime.UtcNow.AddDays(180), 180, "OTC", 100, 10, 20, 12, 1200, 2000, "GOOD", false, false, false, false),
                new(2, "Amoxicillin 250", "Amoxicillin", "Amoxicillin", "B-002", DateTime.UtcNow.AddDays(15), 15, "Sch-H", 4, 10, 60, 40, 160, 240, "EXP NEAR", false, true, true, false),
                new(3, "Expired Syrup", "Syrup", "Syrup Base", "B-003", DateTime.UtcNow.AddDays(-10), -10, "OTC", 5, 5, 80, 50, 250, 400, "EXPIRED", true, false, false, false)
            };

            // 1. Test Excel .xlsx
            var xlsxPath = Path.Combine(tempDir, "test.xlsx");
            var resXlsx = await exportService.ExportToExcelAsync(rows, meta, xlsxPath);
            Assert.True(File.Exists(resXlsx));
            Assert.True(new FileInfo(resXlsx).Length > 0);

            // Verify it's a valid ZIP containing workbook and sheet1
            using (var zip = ZipFile.OpenRead(resXlsx))
            {
                Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
                Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
                Assert.NotNull(zip.GetEntry("xl/sharedStrings.xml"));
            }

            // 2. Test Word .docx
            var docxPath = Path.Combine(tempDir, "test.docx");
            var resDocx = await exportService.ExportToWordAsync(rows, meta, docxPath);
            Assert.True(File.Exists(resDocx));
            Assert.True(new FileInfo(resDocx).Length > 0);

            // Verify it's a valid ZIP containing document.xml
            using (var zip = ZipFile.OpenRead(resDocx))
            {
                Assert.NotNull(zip.GetEntry("word/document.xml"));
            }

            // 3. Test PDF / HTML report
            var htmlPath = Path.Combine(tempDir, "test.html");
            var resHtml = await exportService.ExportToPdfHtmlAsync(rows, meta, htmlPath);
            Assert.True(File.Exists(resHtml));
            var htmlContent = File.ReadAllText(resHtml);
            Assert.Contains("Test Pharmacy Care", htmlContent);
            Assert.Contains("Paracetamol 500", htmlContent);
            Assert.Contains("INVENTORY &amp; STOCK MASTER REPORT", htmlContent);
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
    public async Task InventoryValuation_CalculatedWithNetRate_BuyingPricePlusGstTimesQuantity()
    {
        // Product buying price is 10 and has 5% GST -> Net rate = 10.50
        // Total Qty = 100 -> Total stock valuation = 10.50 * 100 = 1050.00
        var fakeService = new FakeInventoryService();
        decimal buyingCost = 10m;
        decimal gstPercent = 5m;
        decimal qty = 100m;
        decimal netRate = buyingCost * (1m + (gstPercent / 100m)); // 10.50m
        decimal totalValuation = Math.Round(qty * netRate, 2);    // 1050.00m

        var itemDto = new StockSummaryItemDto(
            ProductId: "P-101",
            ProductName: "Test Med 100mg",
            GenericName: "Test Salt",
            SaltComposition: "Test 100mg",
            Manufacturer: "Test Pharma",
            CategoryName: "Tablet",
            Schedule: DrugSchedule.OTC,
            BatchId: "B-101",
            BatchNumber: "BATCH-TEST",
            ExpiryDate: DateTime.UtcNow.AddMonths(12),
            DaysUntilExpiry: 365,
            ExpiryStatus: ExpiryBand.Good,
            AvailableQuantity: qty,
            ReservedQuantity: 0,
            TotalQuantity: qty,
            Mrp: 15m,
            PurchaseRate: buyingCost,
            SaleRate: 14m,
            StockValueAtMrp: qty * 15m,
            StockValueAtCost: totalValuation,
            MinStockAlert: 10m,
            GstRatePercent: gstPercent,
            NetPurchaseRate: netRate
        );

        fakeService.Items.Add(itemDto);

        var vm = new InventoryViewModel(fakeService);
        await vm.LoadStocksCommand.ExecuteAsync(null);

        Assert.Single(vm.StockItems);
        var loadedItem = vm.StockItems[0];
        Assert.Equal(10m, loadedItem.PurchaseRate);
        Assert.Equal(10.50m, loadedItem.NetPurchaseRate);
        Assert.Equal(1050m, loadedItem.StockValueAtCost);
        Assert.Equal(1050m, vm.TotalStockValue);
        Assert.Equal("1,050.00", vm.TotalStockValueFormatted);
    }

    [Fact]
    public async Task OpenEditProduct_PopulatesFields_AndSaveUpdatesProduct()
    {
        var fakeService = new FakeInventoryService();
        var itemDto = new StockSummaryItemDto(
            ProductId: "P-EDIT-1",
            ProductName: "Amoxicillin 500mg",
            GenericName: "Amoxicillin",
            SaltComposition: "Amoxicillin Trihydrate 500mg",
            Manufacturer: "Alkem",
            CategoryName: "CAP",
            Schedule: DrugSchedule.ScheduleH,
            BatchId: "B-EDIT-1",
            BatchNumber: "AMX-99",
            ExpiryDate: new DateTime(2027, 12, 31),
            DaysUntilExpiry: 500,
            ExpiryStatus: ExpiryBand.Good,
            AvailableQuantity: 50,
            ReservedQuantity: 0,
            TotalQuantity: 50,
            Mrp: 120m,
            PurchaseRate: 80m,
            SaleRate: 110m,
            StockValueAtMrp: 6000m,
            StockValueAtCost: 4480m,
            MinStockAlert: 15m,
            GstRatePercent: 12m,
            NetPurchaseRate: 89.6m,
            HsnCode: "30041010"
        );

        fakeService.Items.Add(itemDto);

        var vm = new InventoryViewModel(fakeService);
        await vm.LoadStocksCommand.ExecuteAsync(null);

        var stockItem = vm.StockItems[0];
        vm.OpenEditProductCommand.Execute(stockItem);

        Assert.True(vm.IsEditProductModalOpen);
        Assert.Equal("P-EDIT-1", vm.EditProductId);
        Assert.Equal("Amoxicillin 500mg", vm.EditProductName);
        Assert.Equal("Amoxicillin", vm.EditGenericName);
        Assert.Equal("Amoxicillin Trihydrate 500mg", vm.EditSaltComposition);
        Assert.Equal("Alkem", vm.EditManufacturer);
        Assert.Equal("CAP", vm.EditCategoryName);
        Assert.Equal("30041010", vm.EditHsnCode);
        Assert.Equal(12m, vm.EditGstRatePercent);
        Assert.Equal((int)DrugSchedule.ScheduleH, vm.EditScheduleIndex);
        Assert.Equal(15m, vm.EditMinStockAlert);
        Assert.Equal("B-EDIT-1", vm.EditBatchId);
        Assert.Equal("AMX-99", vm.EditBatchNumber);
        Assert.Equal(120m, vm.EditMrp);
        Assert.Equal(80m, vm.EditPurchaseRate);
        Assert.Equal(110m, vm.EditSaleRate);

        // Edit fields
        vm.EditProductName = "Amoxicillin 500mg Forte";
        vm.EditSaleRate = 115m;

        await vm.SaveProductDetailsCommand.ExecuteAsync(null);

        Assert.False(vm.IsEditProductModalOpen);
    }

    [Fact]
    public async Task InventoryViewModel_CalculatesEstimatedProfit_AndLoadsMonthlyCollectionsCorrectly()
    {
        var fakeService = new FakeInventoryService();
        // Item 1: Qty 10, SaleRate 100, NetPurchaseRate 70 -> Profit = (100 - 70) * 10 = 300
        fakeService.Items.Add(new StockSummaryItemDto(
            ProductId: "P-PROFIT-1",
            ProductName: "Medicine A",
            GenericName: "Generic A",
            SaltComposition: "",
            Manufacturer: "Pharma A",
            CategoryName: "Tablet",
            Schedule: DrugSchedule.OTC,
            BatchId: "B-1",
            BatchNumber: "BAT-1",
            ExpiryDate: DateTime.UtcNow.AddMonths(12),
            DaysUntilExpiry: 365,
            ExpiryStatus: ExpiryBand.Good,
            AvailableQuantity: 10,
            ReservedQuantity: 0,
            TotalQuantity: 10,
            Mrp: 120m,
            PurchaseRate: 60m,
            SaleRate: 100m,
            StockValueAtMrp: 1200m,
            StockValueAtCost: 700m,
            MinStockAlert: 5m,
            GstRatePercent: 12m,
            NetPurchaseRate: 70m,
            HsnCode: "3004"
        ));

        // Item 2: Qty 20, SaleRate 0 (falls back to Mrp 50), NetPurchaseRate 35 -> Profit = (50 - 35) * 20 = 300
        fakeService.Items.Add(new StockSummaryItemDto(
            ProductId: "P-PROFIT-2",
            ProductName: "Medicine B",
            GenericName: "Generic B",
            SaltComposition: "",
            Manufacturer: "Pharma B",
            CategoryName: "Syrup",
            Schedule: DrugSchedule.OTC,
            BatchId: "B-2",
            BatchNumber: "BAT-2",
            ExpiryDate: DateTime.UtcNow.AddMonths(6),
            DaysUntilExpiry: 180,
            ExpiryStatus: ExpiryBand.Good,
            AvailableQuantity: 20,
            ReservedQuantity: 0,
            TotalQuantity: 20,
            Mrp: 50m,
            PurchaseRate: 30m,
            SaleRate: 0m,
            StockValueAtMrp: 1000m,
            StockValueAtCost: 700m,
            MinStockAlert: 5m,
            GstRatePercent: 12m,
            NetPurchaseRate: 35m,
            HsnCode: "3004"
        ));

        var vm = new InventoryViewModel(fakeService);
        await vm.LoadStocksCommand.ExecuteAsync(null);

        // Verify Estimated Profit = 300 + 300 = 600
        Assert.Equal(600m, vm.EstimatedProfit);
        Assert.Equal("600.00", vm.EstimatedProfitFormatted);
        Assert.Contains("Margin:", vm.EstimatedProfitMarginDisplay);

        // Verify Monthly Revenue & Collections from fakeService
        Assert.Equal(154200.50m, vm.RevenueThisMonth);
        Assert.Equal("154,200.50", vm.RevenueThisMonthFormatted);
        Assert.Equal("42 invoices finalized this month", vm.MonthlyInvoicesCountDisplay);

        // Cash Collection
        Assert.Equal(95400.00m, vm.CashCollectionThisMonth);
        Assert.Equal("95,400.00", vm.CashCollectionThisMonthFormatted);
        Assert.Equal("28 cash transactions this month", vm.CashCollectionCountDisplay);

        // Online Collection
        Assert.Equal(58800.50m, vm.OnlineCollectionThisMonth);
        Assert.Equal("58,800.50", vm.OnlineCollectionThisMonthFormatted);
        Assert.Equal("14 UPI / Card payments this month", vm.OnlineCollectionCountDisplay);
    }
}
