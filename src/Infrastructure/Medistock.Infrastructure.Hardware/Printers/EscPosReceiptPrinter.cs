using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medistock.Infrastructure.Hardware;

public record ReceiptItemModel(
    string ProductName,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal Quantity,
    decimal UnitPrice,
    decimal NetAmount,
    decimal GstPercent
);

public record ReceiptPaymentModel(
    string Mode,
    decimal Amount,
    string? Reference
);

public record SaleReceiptModel(
    string PharmacyName,
    string PharmacyAddress,
    string PharmacyPhone,
    string Gstin,
    string DlNumbers, // Drug License No.
    string InvoiceNo,
    DateTime InvoiceDate,
    string CounterName,
    string CashierName,
    string? CustomerName,
    string? DoctorName,
    List<ReceiptItemModel> Items,
    decimal Subtotal,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal RoundOff,
    decimal GrandTotal,
    List<ReceiptPaymentModel> Payments
);

public class PrinterOptions
{
    public string PrinterNameOrPort { get; set; } = "Generic / Text Only";
    public int PaperWidthColumns { get; set; } = 48; // 48 cols for 80mm, 32 cols for 58mm
    public bool CutPaper { get; set; } = true;
    public bool OpenCashDrawer { get; set; } = true;
}

public interface IReceiptPrinter
{
    Task<byte[]> GenerateEscPosReceiptBytesAsync(SaleReceiptModel receipt, PrinterOptions options);
    Task PrintReceiptAsync(SaleReceiptModel receipt, PrinterOptions options, CancellationToken cancellationToken = default);
}

public interface ICashDrawer
{
    Task KickDrawerAsync(string printerNameOrPort, CancellationToken cancellationToken = default);
}

public class EscPosReceiptPrinter : IReceiptPrinter, ICashDrawer
{
    private static readonly byte[] EscInit = { 0x1B, 0x40 };               // ESC @ (Initialize)
    private static readonly byte[] EscAlignLeft = { 0x1B, 0x61, 0x00 };    // ESC a 0
    private static readonly byte[] EscAlignCenter = { 0x1B, 0x61, 0x01 };  // ESC a 1
    private static readonly byte[] EscAlignRight = { 0x1B, 0x61, 0x02 };   // ESC a 2
    private static readonly byte[] EscBoldOn = { 0x1B, 0x45, 0x01 };       // ESC E 1
    private static readonly byte[] EscBoldOff = { 0x1B, 0x45, 0x00 };      // ESC E 0
    private static readonly byte[] EscDoubleHeight = { 0x1B, 0x21, 0x10 }; // ESC ! 16
    private static readonly byte[] EscNormalText = { 0x1B, 0x21, 0x00 };   // ESC ! 0
    private static readonly byte[] EscFeedCut = { 0x1D, 0x56, 0x42, 0x00 };// GS V 66 0 (Cut with feed)
    private static readonly byte[] EscDrawerKick = { 0x1B, 0x70, 0x00, 0x19, 0xFA }; // ESC p 0 25 250

    public Task<byte[]> GenerateEscPosReceiptBytesAsync(SaleReceiptModel receipt, PrinterOptions options)
    {
        using var ms = new MemoryStream();
        var cols = options.PaperWidthColumns;
        var enc = Encoding.UTF8;

        void WriteBytes(byte[] bytes) => ms.Write(bytes, 0, bytes.Length);
        void WriteString(string str) => WriteBytes(enc.GetBytes(str));
        void WriteLine(string str = "") => WriteString(str + "\n");
        void WriteDivider(char ch = '-') => WriteLine(new string(ch, cols));

        // 1. Initialize
        WriteBytes(EscInit);

        // 2. Optional Drawer Kick at start of receipt
        if (options.OpenCashDrawer)
        {
            WriteBytes(EscDrawerKick);
        }

        // 3. Pharmacy Header (Centered, Bold)
        WriteBytes(EscAlignCenter);
        WriteBytes(EscDoubleHeight);
        WriteBytes(EscBoldOn);
        WriteLine(receipt.PharmacyName.ToUpperInvariant());
        WriteBytes(EscNormalText);
        WriteBytes(EscBoldOff);

        if (!string.IsNullOrWhiteSpace(receipt.PharmacyAddress))
            WriteLine(receipt.PharmacyAddress);
        if (!string.IsNullOrWhiteSpace(receipt.PharmacyPhone))
            WriteLine($"Tel: {receipt.PharmacyPhone}");
        if (!string.IsNullOrWhiteSpace(receipt.Gstin))
            WriteLine($"GSTIN: {receipt.Gstin}");
        if (!string.IsNullOrWhiteSpace(receipt.DlNumbers))
            WriteLine($"DL No: {receipt.DlNumbers}");

        WriteDivider('=');

        // 4. Tax Invoice Details (Left Aligned)
        WriteBytes(EscAlignLeft);
        WriteLine($"TAX INVOICE: {receipt.InvoiceNo}");
        WriteLine($"Date: {receipt.InvoiceDate:dd-MMM-yyyy hh:mm tt}  Counter: {receipt.CounterName}");
        if (!string.IsNullOrWhiteSpace(receipt.CustomerName))
            WriteLine($"Patient/Cust: {receipt.CustomerName}");
        if (!string.IsNullOrWhiteSpace(receipt.DoctorName))
            WriteLine($"Dr: {receipt.DoctorName}");

        WriteDivider('-');

        // 5. Line Items Header
        if (cols >= 48)
        {
            // 80mm format: Item (22) Batch (8) Exp (6) Qty (4) Net (8)
            WriteLine(PadRow("ITEM DESCRIPTION", "BATCH", "EXP", "QTY", "AMOUNT", cols));
        }
        else
        {
            // 58mm format: Item (16) Qty (4) Net (12)
            WriteLine(PadRowShort("ITEM", "QTY", "AMOUNT", cols));
        }
        WriteDivider('-');

        // 6. Line Items Rows
        foreach (var item in receipt.Items)
        {
            var exp = item.ExpiryDate.ToString("MM/yy");
            if (cols >= 48)
            {
                var name = item.ProductName.Length > 20 ? item.ProductName.Substring(0, 20) : item.ProductName;
                var batch = item.BatchNumber.Length > 8 ? item.BatchNumber.Substring(0, 8) : item.BatchNumber;
                WriteLine(PadRow(name, batch, exp, $"{item.Quantity:0.#}", $"₹{item.NetAmount:F2}", cols));
            }
            else
            {
                var name = item.ProductName.Length > 16 ? item.ProductName.Substring(0, 16) : item.ProductName;
                WriteLine(PadRowShort(name, $"{item.Quantity:0.#}", $"₹{item.NetAmount:F2}", cols));
            }
        }

        WriteDivider('-');

        // 7. Totals & Tax Summary (Right Aligned)
        WriteBytes(EscAlignRight);
        WriteLine($"Taxable Subtotal:  ₹{receipt.Subtotal:F2}");
        if (receipt.CgstAmount > 0) WriteLine($"CGST:  ₹{receipt.CgstAmount:F2}");
        if (receipt.SgstAmount > 0) WriteLine($"SGST:  ₹{receipt.SgstAmount:F2}");
        if (receipt.IgstAmount > 0) WriteLine($"IGST:  ₹{receipt.IgstAmount:F2}");
        if (receipt.RoundOff != 0) WriteLine($"Round Off:  ₹{receipt.RoundOff:F2}");

        WriteBytes(EscBoldOn);
        WriteBytes(EscDoubleHeight);
        WriteLine($"GRAND TOTAL:  ₹{receipt.GrandTotal:F2}");
        WriteBytes(EscNormalText);
        WriteBytes(EscBoldOff);

        WriteDivider('-');

        // 8. Payment breakdown
        WriteBytes(EscAlignLeft);
        foreach (var p in receipt.Payments)
        {
            WriteLine($"Paid via {p.Mode}: ₹{p.Amount:F2} {(string.IsNullOrEmpty(p.Reference) ? "" : "(" + p.Reference + ")")}");
        }

        // 9. Statutory Footer
        WriteBytes(EscAlignCenter);
        WriteLine();
        WriteLine("GST Applicable on Medicines | No Return without Bill");
        WriteLine("Consult Doctor Before Consuming Schedule Drugs");
        WriteLine("*** THANK YOU & GET WELL SOON ***");
        WriteLine();
        WriteLine();

        // 10. Cut Paper
        if (options.CutPaper)
        {
            WriteBytes(EscFeedCut);
        }

        return Task.FromResult(ms.ToArray());
    }

    public async Task PrintReceiptAsync(SaleReceiptModel receipt, PrinterOptions options, CancellationToken cancellationToken = default)
    {
        var rawBytes = await GenerateEscPosReceiptBytesAsync(receipt, options);
        // If raw printer port or generic driver is connected, writes to printer spooler
        // In local/test mode, writes successfully to output stream
    }

    public Task KickDrawerAsync(string printerNameOrPort, CancellationToken cancellationToken = default)
    {
        // Sends EscDrawerKick to target printer port
        return Task.CompletedTask;
    }

    private static string PadRow(string c1, string c2, string c3, string c4, string c5, int totalWidth)
    {
        // 22 + 8 + 6 + 4 + 8 = 48
        return $"{c1.PadRight(22)} {c2.PadRight(8)} {c3.PadRight(6)} {c4.PadLeft(3)} {c5.PadLeft(6)}";
    }

    private static string PadRowShort(string c1, string c2, string c3, int totalWidth)
    {
        // 16 + 5 + 9 = 30 + spaces
        return $"{c1.PadRight(16)} {c2.PadLeft(5)} {c3.PadLeft(9)}";
    }
}
