using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Medistock.Infrastructure.Hardware;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class HardwareTests
{
    private SaleReceiptModel CreateSampleReceipt()
    {
        return new SaleReceiptModel(
            PharmacyName: "City Meds Pharmacy",
            PharmacyAddress: "123 Health Ave, MG Road, Bengaluru",
            PharmacyPhone: "080-12345678",
            Gstin: "29AAAAA0000A1Z5",
            DlNumbers: "KA-B1-123456 / KA-B2-123457",
            InvoiceNo: "INV-2026-00042",
            InvoiceDate: new DateTime(2026, 9, 24, 16, 30, 0),
            CounterName: "POS-01",
            CashierName: "Pharmacist John",
            CustomerName: "Rahul Sharma",
            DoctorName: "Dr. A. Verma (MBBS)",
            Items: new List<ReceiptItemModel>
            {
                new ReceiptItemModel("Dolo 650mg Tablet", "DOL2601", new DateTime(2027, 5, 1), 2, 30.50m, 61.00m, 12m),
                new ReceiptItemModel("Augmentin 625 Duo", "AUG2509", new DateTime(2026, 12, 1), 1, 201.71m, 201.71m, 12m)
            },
            Subtotal: 262.71m,
            CgstAmount: 15.76m,
            SgstAmount: 15.76m,
            IgstAmount: 0m,
            RoundOff: 0.23m,
            GrandTotal: 294.00m,
            Payments: new List<ReceiptPaymentModel>
            {
                new ReceiptPaymentModel("Cash", 200.00m, null),
                new ReceiptPaymentModel("UPI", 94.00m, "UPI/9876543210")
            }
        );
    }

    [Fact]
    public async Task GenerateEscPosReceiptBytes_80mm_GeneratesCorrectCommandsAndText()
    {
        // Arrange
        var printer = new EscPosReceiptPrinter();
        var receipt = CreateSampleReceipt();
        var options = new PrinterOptions
        {
            PaperWidthColumns = 48,
            OpenCashDrawer = true,
            CutPaper = true
        };

        // Act
        var bytes = await printer.GenerateEscPosReceiptBytesAsync(receipt, options);
        var receiptText = Encoding.UTF8.GetString(bytes);

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);

        // Check ESC/POS command sequences
        // ESC @ (Init: 0x1B, 0x40)
        Assert.Equal(0x1B, bytes[0]);
        Assert.Equal(0x40, bytes[1]);

        // Contains Drawer Kick (0x1B, 0x70, 0x00, 0x19, 0xFA)
        Assert.Contains((byte)0x19, bytes);
        Assert.Contains((byte)0xFA, bytes);

        // Contains Cut Paper (0x1D, 0x56, 0x42, 0x00)
        Assert.Contains((byte)0x1D, bytes);
        Assert.Contains((byte)0x56, bytes);
        Assert.Contains((byte)0x42, bytes);

        // Check Text Content
        Assert.Contains("CITY MEDS PHARMACY", receiptText);
        Assert.Contains("INV-2026-00042", receiptText);
        Assert.Contains("29AAAAA0000A1Z5", receiptText);
        Assert.Contains("Rahul Sharma", receiptText);
        Assert.Contains("Dr. A. Verma", receiptText);
        Assert.Contains("Dolo 650mg Tablet", receiptText);
        Assert.Contains("Augmentin 625 Duo", receiptText);
        Assert.Contains("294.00", receiptText);
        Assert.Contains("Paid via Cash: ₹200.00", receiptText);
        Assert.Contains("Paid via UPI: ₹94.00 (UPI/9876543210)", receiptText);
        Assert.Contains("GST Applicable on Medicines", receiptText);
    }

    [Fact]
    public async Task GenerateEscPosReceiptBytes_58mm_GeneratesNarrowFormat()
    {
        // Arrange
        var printer = new EscPosReceiptPrinter();
        var receipt = CreateSampleReceipt();
        var options = new PrinterOptions
        {
            PaperWidthColumns = 32,
            OpenCashDrawer = false,
            CutPaper = false
        };

        // Act
        var bytes = await printer.GenerateEscPosReceiptBytesAsync(receipt, options);
        var receiptText = Encoding.UTF8.GetString(bytes);

        // Assert
        Assert.NotNull(bytes);
        Assert.Contains("CITY MEDS PHARMACY", receiptText);
        Assert.Contains("294.00", receiptText);
    }

    [Fact]
    public async Task KickDrawer_CompletesSuccessfully()
    {
        var drawer = new EscPosReceiptPrinter();
        await drawer.KickDrawerAsync("POS-PRINTER-01");
    }
}
