using System;
using Medistock.Desktop.ViewModels;
using Medistock.Domain.Products;
using Xunit;

namespace Medistock.Desktop.Tests;

public class PosCalculationAndPackagingTests
{
    [Fact]
    public void PackagingHelper_SingleStripOrTabCap_DefaultsTo10Units()
    {
        var resultStrip = PackagingHelper.Parse("1 Strip", "CAP");
        Assert.Equal(10, resultStrip.TabsPerStrip);
        Assert.Equal(1, resultStrip.StripsPerBox);

        var resultTab = PackagingHelper.Parse("1", "TAB");
        Assert.Equal(10, resultTab.TabsPerStrip);

        var result10 = PackagingHelper.Parse("10 CAP/Pack", "CAP");
        Assert.Equal(10, result10.TabsPerStrip);
    }

    [Fact]
    public void CartItemViewModel_OneStripPlusOnePc_CalculatesAccurately()
    {
        var item = new CartItemViewModel
        {
            ProductName = "Test Amoxicillin",
            Mrp = 20.00m,
            UnitPrice = 20.00m,
            TabsPerStrip = 10,
            StripQuantity = 1,
            TabQuantity = 1,
            DiscountPercent = 0
        };

        // 1 strip * 20 = 20. 1 tab * (20 / 10) = 2. Total = 22.
        Assert.Equal(22.00m, item.GrossAmount);
        Assert.Equal(22.00m, item.NetAmount);
    }

    [Fact]
    public void CartItemViewModel_ZeroStripAndZeroTab_ReturnsZeroAmount()
    {
        var item = new CartItemViewModel
        {
            ProductName = "Test Amoxicillin",
            Mrp = 20.00m,
            UnitPrice = 20.00m,
            TabsPerStrip = 10,
            StripQuantity = 0,
            TabQuantity = 0,
            DiscountPercent = 0
        };

        Assert.Equal(0.00m, item.GrossAmount);
        Assert.Equal(0.00m, item.NetAmount);
    }

    [Fact]
    public void CartItemViewModel_NaNInput_SanitizesToZero()
    {
        var item = new CartItemViewModel
        {
            ProductName = "Test Paracetamol",
            Mrp = 50.00m,
            UnitPrice = 50.00m,
            TabsPerStrip = 10,
            StripQuantityDouble = 1,
            TabQuantityDouble = 0
        };

        Assert.Equal(50.00m, item.GrossAmount);

        // Simulate user clearing the input box (NumberBox sets double.NaN)
        item.StripQuantityDouble = double.NaN;
        item.TabQuantityDouble = double.NaN;

        Assert.Equal(0m, item.StripQuantity);
        Assert.Equal(0m, item.TabQuantity);
        Assert.Equal(0.00m, item.GrossAmount);
        Assert.Equal(0.00m, item.NetAmount);
    }
}
