using System;
using Medistock.Application.Products.Queries;
using Medistock.Desktop.ViewModels;
using Xunit;

namespace Medistock.Desktop.Tests;

public class PosAlertAndThresholdTests
{
    [Fact]
    public void ProductSearchItemViewModel_WhenExpired_FormatsRedAndShowsExpiredBadge()
    {
        var dto = new ProductSearchDto
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Paracetamol 500mg",
            BatchNumber = "BATCH-EXP",
            NearestExpiryDate = DateTime.UtcNow.AddDays(-10),
            AvailableQuantity = 20,
            Mrp = 50,
            MinStockAlert = 5
        };

        var vm = ProductSearchItemViewModel.FromDto(dto, nearExpiryDays: 90);

        Assert.True(vm.IsExpired);
        Assert.False(vm.IsNearExpiry);
        Assert.Equal("EXPIRED", vm.ExpiryBadge);
        Assert.True(vm.HasExpiryBadge);
        Assert.Equal("#DC2626", vm.ExpiryForegroundHex);
        Assert.Equal("#35DC2626", vm.RowBackgroundHex);
        Assert.Equal("#DC2626", vm.RowBorderHex);
        Assert.Equal("2 Strips", vm.StockDisplay);
        Assert.Equal("#16A34A", vm.StockForegroundHex); // healthy stock
    }

    [Fact]
    public void ProductSearchItemViewModel_WhenNearExpiry_FormatsRedAndShowsNearBadge()
    {
        var dto = new ProductSearchDto
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Amoxicillin 250mg",
            BatchNumber = "BATCH-NEAR",
            NearestExpiryDate = DateTime.UtcNow.AddDays(30),
            AvailableQuantity = 20,
            Mrp = 100,
            MinStockAlert = 5
        };

        var vm = ProductSearchItemViewModel.FromDto(dto, nearExpiryDays: 90);

        Assert.False(vm.IsExpired);
        Assert.True(vm.IsNearExpiry);
        Assert.Equal("EXP NEAR", vm.ExpiryBadge);
        Assert.True(vm.HasExpiryBadge);
        Assert.Equal("#DC2626", vm.ExpiryForegroundHex);
        Assert.Equal("#22DC2626", vm.RowBackgroundHex);
        Assert.Equal("#80DC2626", vm.RowBorderHex);
    }

    [Fact]
    public void ProductSearchItemViewModel_WhenStockZero_ShowsOutOfStockRed()
    {
        var dto = new ProductSearchDto
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Azithromycin 500mg",
            BatchNumber = "BATCH-OOS",
            NearestExpiryDate = DateTime.UtcNow.AddDays(365),
            AvailableQuantity = 0,
            Mrp = 120,
            MinStockAlert = 10
        };

        var vm = ProductSearchItemViewModel.FromDto(dto, nearExpiryDays: 90);

        Assert.True(vm.IsOutOfStock);
        Assert.Equal("0 (OOS)", vm.StockDisplay);
        Assert.Equal("#DC2626", vm.StockForegroundHex);
    }

    [Fact]
    public void ProductSearchItemViewModel_WhenLowStock_ShowsLowStockAmber()
    {
        var dto = new ProductSearchDto
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Cetirizine 10mg",
            BatchNumber = "BATCH-LOW",
            NearestExpiryDate = DateTime.UtcNow.AddDays(365),
            AvailableQuantity = 5,
            Mrp = 30,
            MinStockAlert = 10
        };

        var vm = ProductSearchItemViewModel.FromDto(dto, nearExpiryDays: 90);

        Assert.False(vm.IsOutOfStock);
        Assert.True(vm.IsLowStock);
        Assert.Equal("5 Pcs (LOW)", vm.StockDisplay);
        Assert.Equal("#D97706", vm.StockForegroundHex);
    }

    [Fact]
    public void ProductSearchItemViewModel_WhenNormal_HasCleanFormatting()
    {
        var dto = new ProductSearchDto
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Normal Med 100mg",
            BatchNumber = "BATCH-OK",
            NearestExpiryDate = DateTime.UtcNow.AddDays(200),
            AvailableQuantity = 50,
            Mrp = 75,
            MinStockAlert = 10
        };

        var vm = ProductSearchItemViewModel.FromDto(dto, nearExpiryDays: 90);

        Assert.False(vm.IsExpired);
        Assert.False(vm.IsNearExpiry);
        Assert.False(vm.IsOutOfStock);
        Assert.False(vm.IsLowStock);
        Assert.False(vm.HasExpiryBadge);
        Assert.Equal("5 Strips", vm.StockDisplay);
        Assert.Equal("#16A34A", vm.StockForegroundHex);
        Assert.Equal("#00000000", vm.RowBackgroundHex);
        Assert.Equal("#00000000", vm.RowBorderHex);
    }
}
