using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Contracts.Printing;
using Medistock.Infrastructure.Hardware;
using Medistock.Infrastructure.Hardware.Printers;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class BillDocumentGeneratorTests
{
    private readonly BillDocumentGenerator _generator;

    public BillDocumentGeneratorTests()
    {
        _generator = new BillDocumentGenerator();
    }

    private SaleReceiptModel CreateSampleReceipt()
    {
        return new SaleReceiptModel(
            PharmacyName: "Apex Chemist & Surgical",
            PharmacyAddress: "42, Medical Square, MG Road, Mumbai",
            PharmacyPhone: "+91 98200 12345",
            Gstin: "27ABCDE1234F1Z5",
            DlNumbers: "MH-MZ2-998877 (20B/21B)",
            InvoiceNo: "INV-2026-0089",
            InvoiceDate: new DateTime(2026, 9, 27, 14, 30, 0),
            CounterName: "Counter 1",
            CashierName: "Rajesh Sharma",
            CustomerName: "Amitabh Kumar",
            DoctorName: "Dr. S. K. Gupta (MD Med)",
            Items: new List<ReceiptItemModel>
            {
                new("Dolo 650mg Tablet", "BT9921", new DateTime(2027, 12, 1), 2, 33.60m, 67.20m, 12.0m),
                new("Pan D Capsule 10s", "PD1029", new DateTime(2028, 5, 1), 1, 198.00m, 198.00m, 12.0m),
                new("Augmentin 625 Duo", "AUG881", new DateTime(2027, 8, 1), 1, 223.50m, 223.50m, 12.0m)
            },
            Subtotal: 436.79m,
            CgstAmount: 26.21m,
            SgstAmount: 26.20m,
            IgstAmount: 0m,
            RoundOff: 0.30m,
            GrandTotal: 489.50m,
            Payments: new List<ReceiptPaymentModel>
            {
                new("UPI / QR", 489.50m, "UPI/20260927/9988")
            }
        );
    }

    [Theory]
    [InlineData(0, "Zero Rupees Only")]
    [InlineData(489.50, "Four Hundred Eighty-Nine Rupees and Fifty Paise Only")]
    [InlineData(1250.00, "One Thousand Two Hundred Fifty Rupees Only")]
    [InlineData(105420.75, "One Lakh Five Thousand Four Hundred Twenty Rupees and Seventy-Five Paise Only")]
    [InlineData(15000000, "One Crore Fifty Lakh Rupees Only")]
    public async Task NumberToWords_ShouldFormatIndianCurrencyAccurately(decimal amount, string expected)
    {
        var result = await _generator.ConvertAmountToWordsInrAsync(amount);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GenerateHtmlBill_A4Standard_ShouldContainAllSelectedCustomColumnsAndSections()
    {
        var receipt = CreateSampleReceipt();
        var template = BillTemplatePresets.CreateA4StandardPreset();
        template.Header.StoreName = "APEX SPECIALITY PHARMACY";

        var html = await _generator.GenerateHtmlBillAsync(receipt, template);

        Assert.NotNull(html);
        Assert.Contains("APEX SPECIALITY PHARMACY", html);
        Assert.Contains("INV-2026-0089", html);
        Assert.Contains("Amitabh Kumar", html);
        Assert.Contains("Dr. S. K. Gupta", html);
        Assert.Contains("Dolo 650mg Tablet", html);
        Assert.Contains("Pan D Capsule 10s", html);
        Assert.Contains("Augmentin 625 Duo", html);
        Assert.Contains("₹489.50", html);
        Assert.Contains("Four Hundred Eighty-Nine Rupees and Fifty Paise Only", html);
        Assert.Contains("Registered Pharmacist", html);
    }

    [Fact]
    public async Task GenerateHtmlBill_WithCustomHiddenColumns_ShouldOmitThem()
    {
        var receipt = CreateSampleReceipt();
        var template = BillTemplatePresets.CreateA4StandardPreset();

        // Hide Batch and Expiry columns
        var batchCol = template.Columns.First(c => c.FieldType == BillFieldType.BatchNumber);
        var expCol = template.Columns.First(c => c.FieldType == BillFieldType.ExpiryDate);
        batchCol.IsVisible = false;
        expCol.IsVisible = false;

        var html = await _generator.GenerateHtmlBillAsync(receipt, template);

        Assert.DoesNotContain($"<th>{batchCol.HeaderTitle}</th>", html);
        Assert.DoesNotContain($"<th>{expCol.HeaderTitle}</th>", html);
    }

    [Fact]
    public async Task GenerateEscPosBill_80mm_ShouldGenerateValidBinaryStream()
    {
        var receipt = CreateSampleReceipt();
        var template = BillTemplatePresets.CreateThermal80mmPreset();

        var bytes = await _generator.GenerateEscPosBillAsync(receipt, template);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 50);
        // Should start with ESC @ (0x1B, 0x40)
        Assert.Equal(0x1B, bytes[0]);
        Assert.Equal(0x40, bytes[1]);
    }
}
