using System;
using Medistock.Domain.Common;
using Medistock.Domain.Products;
using Medistock.Domain.Purchases;
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

    [Fact]
    public void Sale_WalkInCustomer_FullCashPayment_PostsSuccessfully()
    {
        var sale = Sale.Create(
            "sale-test",
            "org-1",
            "branch-1",
            "counter-1",
            "wh-1",
            "cashier-1",
            "device-1",
            "INV-9999",
            customerId: null,
            customerName: "WALK-IN CUSTOMER",
            isInterstate: false);

        var item1 = SaleItem.Create("i1", "sale-test", "p1", "Product 1", "b1", "BN1", DateTime.UtcNow.AddDays(100), 1, 900.0m, 900.0m, 12.0m, false, discountPct: 10m);
        var item2 = SaleItem.Create("i2", "sale-test", "p2", "Product 2", "b2", "BN2", DateTime.UtcNow.AddDays(100), 1, 900.0m, 900.0m, 12.0m, false, discountPct: 10m);

        sale.AddItem(item1);
        sale.AddItem(item2);

        Assert.Equal(1620.00m, sale.Total);

        sale.AddPayment(SalePayment.Create("pay-1", "sale-test", PaymentMode.Cash, 1620.00m));
        sale.PostSale();

        Assert.Equal(SaleStatus.Posted, sale.Status);
    }

    [Fact]
    public void SaleItem_UnitPrice_NeverExceedsMrp()
    {
        var item = SaleItem.Create(
            "i-clamp",
            "sale-clamp",
            "p-clamp",
            "Clamped Medicine",
            "b-clamp",
            "BATCH01",
            DateTime.UtcNow.AddMonths(12),
            quantity: 2,
            unitPrice: 150.00m,
            mrp: 100.00m,
            gstRatePercent: 12.0m,
            isInterstate: false,
            discountPct: 0);

        Assert.Equal(100.00m, item.UnitPrice);
        Assert.Equal(100.00m, item.Mrp);
        Assert.Equal(200.00m, item.NetAmount);
    }

    [Fact]
    public void PurchaseInvoiceItem_BuyingCost20_With12PercentGst_IncludesGstInNetAmount()
    {
        var item = PurchaseInvoiceItem.Create(
            id: "pi-item-1",
            purchaseInvoiceId: "pi-1",
            productId: "prod-1",
            productName: "Paracetamol 500mg",
            hsnCode: "3004",
            batchNumber: "PCM-2026",
            expiryDate: DateTime.UtcNow.AddYears(2),
            quantity: 1,
            freeQuantity: 0,
            unitPrice: 20.00m,
            mrp: 30.00m,
            saleRate: 30.00m,
            discountPct: 0,
            gstRatePercent: 12.0m,
            isInterstate: false
        );

        // Buying price 20 is inclusive of GST:
        // Net amount is exactly 20.00 (not 20 + GST = 22.40)
        Assert.Equal(20.00m, item.NetAmount);
        Assert.Equal(17.86m, item.TaxableAmount);
        Assert.Equal(1.07m, item.CgstAmount);
        Assert.Equal(1.07m, item.SgstAmount);
        Assert.Equal(0.00m, item.IgstAmount);
        Assert.Equal(20.00m, item.LandedCostPerUnit);
    }

    [Fact]
    public void PurchaseInvoiceItem_WithFreeQuantity_CalculatesLandedCostPerUnitCorrectly()
    {
        var item = PurchaseInvoiceItem.Create(
            id: "pi-item-2",
            purchaseInvoiceId: "pi-2",
            productId: "prod-2",
            productName: "Amoxicillin 500mg",
            hsnCode: "3004",
            batchNumber: "AMX-2026",
            expiryDate: DateTime.UtcNow.AddYears(2),
            quantity: 10,
            freeQuantity: 2,
            unitPrice: 20.00m,
            mrp: 35.00m,
            saleRate: 35.00m,
            discountPct: 0,
            gstRatePercent: 12.0m,
            isInterstate: false
        );

        // 10 purchased at 20 = 200 net total
        Assert.Equal(200.00m, item.NetAmount);
        Assert.Equal(12m, item.TotalQuantity); // 10 + 2
        // Landed cost per unit = 200 / 12 = 16.6667
        Assert.Equal(16.6667m, item.LandedCostPerUnit);
    }
}
