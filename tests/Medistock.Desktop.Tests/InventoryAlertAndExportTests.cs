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
}
