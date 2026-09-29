using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Application.Inventory.DTOs;
using Medistock.Application.Inventory.Services;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class InventoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteInventoryRepository _inventoryRepository;
    private readonly InventoryService _inventoryService;
    private readonly DataSeeder _seeder;

    public InventoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_inv_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);

        // Run migrations & seed data
        var migrator = new DatabaseMigrator(_connectionFactory);
        migrator.MigrateAsync().GetAwaiter().GetResult();

        _seeder = new DataSeeder(_connectionFactory);
        _seeder.SeedIfEmptyAsync().GetAwaiter().GetResult();
        _seeder.SeedSampleProductsAsync().GetAwaiter().GetResult();

        _inventoryRepository = new SqliteInventoryRepository(_connectionFactory);
        _inventoryService = new InventoryService(_inventoryRepository);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task GetStockSummary_ReturnsBatches_WithAccurateFinancialValuation()
    {
        // Act
        var stocks = await _inventoryService.GetStockSummaryAsync(warehouseId: "WH-MAIN");

        // Assert
        Assert.NotEmpty(stocks);
        foreach (var item in stocks)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.ProductName));
            Assert.False(string.IsNullOrWhiteSpace(item.BatchNumber));
            Assert.True(item.AvailableQuantity >= 0);
            Assert.True(item.StockValueAtMrp >= 0);
            Assert.True(item.StockValueAtCost >= 0);
            Assert.Equal(Math.Round(item.AvailableQuantity * item.Mrp, 2), item.StockValueAtMrp);
            Assert.Equal(Math.Round(item.AvailableQuantity * item.PurchaseRate, 2), item.StockValueAtCost);
        }
    }

    [Fact]
    public async Task GetExpiryDashboard_CategorizesBandsAndComputesAtRiskValue()
    {
        // Act
        var dashboard = await _inventoryService.GetExpiryDashboardAsync(warehouseId: "WH-MAIN");

        // Assert
        Assert.NotNull(dashboard);
        Assert.True(dashboard.TotalActiveBatches > 0);
        Assert.True(dashboard.TotalStockQuantity > 0);
        Assert.True(dashboard.TotalInventoryValueCost > 0);

        Assert.NotNull(dashboard.ExpiredBand);
        Assert.NotNull(dashboard.CriticalBand);
        Assert.NotNull(dashboard.WarningBand);
        Assert.NotNull(dashboard.GoodBand);

        // Check that at least some batches fall into the Good or Warning band
        var totalCategorized = dashboard.ExpiredBand.BatchCount +
                               dashboard.CriticalBand.BatchCount +
                               dashboard.WarningBand.BatchCount +
                               dashboard.GoodBand.BatchCount;

        Assert.Equal(dashboard.TotalActiveBatches, totalCategorized);
    }

    [Fact]
    public async Task AdjustStock_Add_IncreasesQuantityAndWritesMovementLedger()
    {
        // Arrange
        var stocks = await _inventoryService.GetStockSummaryAsync(warehouseId: "WH-MAIN");
        var targetItem = stocks[0];
        var initialQty = targetItem.AvailableQuantity;

        var request = new StockAdjustmentRequest(
            OrgId: "ORG-01",
            BranchId: "BR-01",
            WarehouseId: "WH-MAIN",
            ProductId: targetItem.ProductId,
            BatchId: targetItem.BatchId,
            AdjustmentType: StockAdjustmentType.Add,
            Quantity: 25,
            Reason: "Physical Audit Found Extra Units",
            UserId: "USER-AUDIT",
            DeviceId: "POS-01"
        );

        // Act
        var result = await _inventoryService.AdjustStockAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.MovementId);
        Assert.Equal(initialQty + 25, result.NewAvailableQuantity);

        var updatedStocks = await _inventoryService.GetStockSummaryAsync(warehouseId: "WH-MAIN");
        var updatedItem = updatedStocks.FirstOrDefault(s => s.BatchId == targetItem.BatchId);
        Assert.NotNull(updatedItem);
        Assert.Equal(initialQty + 25, updatedItem.AvailableQuantity);
    }

    [Fact]
    public async Task AdjustStock_Reduce_DecreasesQuantity_AndRefusesNegativeStock()
    {
        // Arrange
        var stocks = await _inventoryService.GetStockSummaryAsync(warehouseId: "WH-MAIN");
        var targetItem = stocks[0];
        var initialQty = targetItem.AvailableQuantity;

        // 1. Valid reduction
        var validRequest = new StockAdjustmentRequest(
            OrgId: "ORG-01",
            BranchId: "BR-01",
            WarehouseId: "WH-MAIN",
            ProductId: targetItem.ProductId,
            BatchId: targetItem.BatchId,
            AdjustmentType: StockAdjustmentType.Reduce,
            Quantity: 5,
            Reason: "Damaged packaging write-off",
            UserId: "USER-AUDIT",
            DeviceId: "POS-01"
        );

        var validResult = await _inventoryService.AdjustStockAsync(validRequest);
        Assert.True(validResult.Success);
        Assert.Equal(initialQty - 5, validResult.NewAvailableQuantity);

        // 2. Invalid excessive reduction (exceeding stock)
        var excessiveRequest = new StockAdjustmentRequest(
            OrgId: "ORG-01",
            BranchId: "BR-01",
            WarehouseId: "WH-MAIN",
            ProductId: targetItem.ProductId,
            BatchId: targetItem.BatchId,
            AdjustmentType: StockAdjustmentType.Reduce,
            Quantity: 999999,
            Reason: "Impossible deduction",
            UserId: "USER-AUDIT",
            DeviceId: "POS-01"
        );

        var excessiveResult = await _inventoryService.AdjustStockAsync(excessiveRequest);
        Assert.False(excessiveResult.Success);
        Assert.Contains("Insufficient stock", excessiveResult.ErrorMessage);
    }

    [Fact]
    public async Task QuarantineExpiredBatch_ZeroesBalance_AndWritesExpiryWriteOffMovement()
    {
        // Arrange
        var stocks = await _inventoryService.GetStockSummaryAsync(warehouseId: "WH-MAIN");
        var targetItem = stocks[0];

        // Act
        var result = await _inventoryService.QuarantineExpiredBatchAsync(
            orgId: "ORG-01",
            branchId: "BR-01",
            warehouseId: "WH-MAIN",
            productId: targetItem.ProductId,
            batchId: targetItem.BatchId,
            currentQuantity: targetItem.AvailableQuantity,
            reason: "Batch expired quarantine protocol",
            userId: "PHARMACIST-01",
            deviceId: "POS-01"
        );

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.NewAvailableQuantity);

        var updatedStocks = await _inventoryService.GetStockSummaryAsync(warehouseId: "WH-MAIN");
        var updatedItem = updatedStocks.FirstOrDefault(s => s.BatchId == targetItem.BatchId);
        Assert.NotNull(updatedItem);
        Assert.Equal(0, updatedItem.AvailableQuantity);
    }
}
