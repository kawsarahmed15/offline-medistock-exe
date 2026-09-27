using System;
using Medistock.Domain.Common;
using Medistock.Domain.Products;
using Medistock.Domain.Sales;
using Xunit;

namespace Medistock.Domain.Tests;

public class DomainTests
{
    [Fact]
    public void Money_AdditionAndMultiplication_CalculatesCorrectly()
    {
        var m1 = Money.From(100.50m);
        var m2 = Money.From(50.25m);

        var sum = m1 + m2;
        var multiplied = m1 * 2;

        Assert.Equal(150.75m, sum.Amount);
        Assert.Equal(201.00m, multiplied.Amount);
    }

    [Theory]
    [InlineData("27AAACM1234F1Z5", true)]
    [InlineData("29ABCDE1234F2Z5", true)]
    [InlineData("INVALID_GSTIN", false)]
    [InlineData("", false)]
    public void Gstin_Validation_DetectsValidAndInvalidFormats(string input, bool isValid)
    {
        var parsed = Gstin.TryParse(input, out var gstin);
        Assert.Equal(isValid, parsed);
        if (isValid)
        {
            Assert.NotNull(gstin);
            Assert.Equal(input.Substring(0, 2), gstin!.StateCode);
        }
    }

    [Fact]
    public void PackSize_Conversions_AreAccurate()
    {
        var pack = new PackSize(10, "TAB");

        Assert.Equal(100m, pack.ConvertToBaseUnits(10));
        Assert.Equal(5m, pack.ConvertToPacks(50));
    }

    [Fact]
    public void SaleItem_IntrastateTaxSplit_SplitsCgstAndSgstEqually()
    {
        var item = SaleItem.Create(
            "item-1",
            "sale-1",
            "prod-1",
            "Paracetamol 500mg",
            "batch-1",
            "PCM240923",
            DateTime.UtcNow.AddMonths(12),
            quantity: 2,
            unitPrice: 50.00m,
            mrp: 50.00m,
            gstRatePercent: 12.0m,
            isInterstate: false,
            discountPct: 0);

        Assert.Equal(89.29m, item.TaxableAmount);
        Assert.Equal(6.0m, item.CgstRate);
        Assert.Equal(5.36m, item.CgstAmount);
        Assert.Equal(6.0m, item.SgstRate);
        Assert.Equal(5.35m, item.SgstAmount);
        Assert.Equal(0.00m, item.IgstAmount);
        Assert.Equal(100.00m, item.NetAmount);
    }

    [Fact]
    public void SaleItem_InterstateTax_AppliesFullIgst()
    {
        var item = SaleItem.Create(
            "item-2",
            "sale-2",
            "prod-2",
            "Amoxicillin 500mg",
            "batch-2",
            "AMX240812",
            DateTime.UtcNow.AddMonths(6),
            quantity: 1,
            unitPrice: 100.00m,
            mrp: 100.00m,
            gstRatePercent: 18.0m,
            isInterstate: true,
            discountPct: 10);

        Assert.Equal(76.27m, item.TaxableAmount);
        Assert.Equal(10.00m, item.DiscountAmount);
        Assert.Equal(0.00m, item.CgstAmount);
        Assert.Equal(0.00m, item.SgstAmount);
        Assert.Equal(18.0m, item.IgstRate);
        Assert.Equal(13.73m, item.IgstAmount);
        Assert.Equal(90.00m, item.NetAmount);
    }

    [Fact]
    public void Product_ScheduleH_RequiresPrescription()
    {
        var product = Product.Create(
            "prod-h",
            "org-1",
            "Alprazolam 0.5mg",
            "Alprax",
            "Alprazolam",
            "Alprazolam 0.5mg",
            "0.5mg",
            DosageForm.Tablet,
            new PackSize(10, "TAB"),
            "3004",
            12.0m,
            DrugSchedule.ScheduleH);

        Assert.True(product.IsPrescriptionRequired);
        Assert.False(product.IsNarcotic);
    }
}
