using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Medistock.Contracts.Printing;

namespace Medistock.Infrastructure.Hardware.Printers;

public record CreditNotePrintModel(
    string PharmacyName,
    string PharmacyAddress,
    string PharmacyPhone,
    string Gstin,
    string CreditNoteNo,
    string OriginalInvoiceNo,
    DateTime ReturnDate,
    string? CustomerName,
    List<CreditNoteItemPrintModel> Items,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalRefundAmount,
    string RefundMode,
    string? Reason
);

public record CreditNoteItemPrintModel(
    string ProductName,
    string BatchNumber,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal NetAmount,
    string RestockStatus
);

public interface IDocumentReportGenerator
{
    Task<string> GeneratePlainTextCreditNoteAsync(CreditNotePrintModel model, int columnWidth = 48);
    Task<byte[]> GenerateEscPosCreditNoteAsync(CreditNotePrintModel model, int columnWidth = 48);
    Task<string> GenerateHtmlTaxInvoiceAsync(SaleReceiptModel model);
}

public class DocumentReportGenerator : IDocumentReportGenerator
{
    private static readonly byte[] EscInit = { 0x1B, 0x40 };
    private static readonly byte[] EscAlignCenter = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] EscAlignLeft = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] EscAlignRight = { 0x1B, 0x61, 0x02 };
    private static readonly byte[] EscBoldOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] EscBoldOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] EscFeedCut = { 0x1D, 0x56, 0x42, 0x00 };

    public Task<string> GeneratePlainTextCreditNoteAsync(CreditNotePrintModel model, int columnWidth = 48)
    {
        var sb = new StringBuilder();
        var divider = new string('-', columnWidth);
        var doubleDivider = new string('=', columnWidth);

        sb.AppendLine(CenterText(model.PharmacyName.ToUpperInvariant(), columnWidth));
        sb.AppendLine(CenterText(model.PharmacyAddress, columnWidth));
        sb.AppendLine(CenterText($"GSTIN: {model.Gstin} | Tel: {model.PharmacyPhone}", columnWidth));
        sb.AppendLine(doubleDivider);

        sb.AppendLine(CenterText("** CREDIT NOTE / SALES RETURN VOUCHER **", columnWidth));
        sb.AppendLine($"Credit Note No : {model.CreditNoteNo}");
        sb.AppendLine($"Original Bill  : {model.OriginalInvoiceNo}");
        sb.AppendLine($"Date & Time    : {model.ReturnDate:dd-MMM-yyyy hh:mm tt}");
        if (!string.IsNullOrWhiteSpace(model.CustomerName))
            sb.AppendLine($"Customer Name  : {model.CustomerName}");
        if (!string.IsNullOrWhiteSpace(model.Reason))
            sb.AppendLine($"Return Reason  : {model.Reason}");
        sb.AppendLine(divider);

        // Header
        sb.AppendLine($"{"ITEM".PadRight(20)} {"BATCH".PadRight(8)} {"QTY".PadLeft(4)} {"AMOUNT".PadLeft(10)}");
        sb.AppendLine(divider);

        foreach (var item in model.Items)
        {
            var name = item.ProductName.Length > 19 ? item.ProductName.Substring(0, 19) : item.ProductName;
            var batch = item.BatchNumber.Length > 8 ? item.BatchNumber.Substring(0, 8) : item.BatchNumber;
            sb.AppendLine($"{name.PadRight(20)} {batch.PadRight(8)} {item.Quantity,4:0.#} {item.NetAmount,10:F2}");
        }

        sb.AppendLine(divider);
        sb.AppendLine($"{"Taxable Subtotal:".PadLeft(36)} {model.Subtotal,10:F2}");
        sb.AppendLine($"{"Reversed GST:".PadLeft(36)} {model.TaxAmount,10:F2}");
        sb.AppendLine($"{"TOTAL REFUND VALUE:".PadLeft(36)} {model.TotalRefundAmount,10:F2}");
        sb.AppendLine(divider);
        sb.AppendLine($"Refund Method: {model.RefundMode}");
        sb.AppendLine();
        sb.AppendLine(CenterText("Authorized Signature / Pharmacist Seal", columnWidth));
        sb.AppendLine();

        return Task.FromResult(sb.ToString());
    }

    public Task<byte[]> GenerateEscPosCreditNoteAsync(CreditNotePrintModel model, int columnWidth = 48)
    {
        using var ms = new MemoryStream();
        var enc = Encoding.UTF8;

        void WriteBytes(byte[] bytes) => ms.Write(bytes, 0, bytes.Length);
        void WriteString(string str) => WriteBytes(enc.GetBytes(str));
        void WriteLine(string str = "") => WriteString(str + "\n");
        void WriteDivider(char ch = '-') => WriteLine(new string(ch, columnWidth));

        WriteBytes(EscInit);
        WriteBytes(EscAlignCenter);
        WriteBytes(EscBoldOn);
        WriteLine(model.PharmacyName.ToUpperInvariant());
        WriteBytes(EscBoldOff);
        WriteLine(model.PharmacyAddress);
        WriteLine($"GSTIN: {model.Gstin} | Tel: {model.PharmacyPhone}");
        WriteDivider('=');

        WriteLine("** CREDIT NOTE / RETURN VOUCHER **");
        WriteBytes(EscAlignLeft);
        WriteLine($"CN No: {model.CreditNoteNo}  Orig Inv: {model.OriginalInvoiceNo}");
        WriteLine($"Date: {model.ReturnDate:dd-MMM-yyyy hh:mm tt}");
        if (!string.IsNullOrWhiteSpace(model.CustomerName))
            WriteLine($"Customer: {model.CustomerName}");
        WriteDivider('-');

        WriteLine($"{"ITEM".PadRight(20)} {"BATCH".PadRight(8)} {"QTY".PadLeft(4)} {"AMOUNT".PadLeft(10)}");
        WriteDivider('-');

        foreach (var item in model.Items)
        {
            var name = item.ProductName.Length > 19 ? item.ProductName.Substring(0, 19) : item.ProductName;
            var batch = item.BatchNumber.Length > 8 ? item.BatchNumber.Substring(0, 8) : item.BatchNumber;
            WriteLine($"{name.PadRight(20)} {batch.PadRight(8)} {item.Quantity,4:0.#} {item.NetAmount,10:F2}");
        }

        WriteDivider('-');
        WriteBytes(EscAlignRight);
        WriteLine($"Subtotal: ₹{model.Subtotal:F2}");
        WriteLine($"Reversed GST: ₹{model.TaxAmount:F2}");
        WriteBytes(EscBoldOn);
        WriteLine($"TOTAL REFUND: ₹{model.TotalRefundAmount:F2}");
        WriteBytes(EscBoldOff);

        WriteBytes(EscAlignCenter);
        WriteLine();
        WriteLine("Refund Disbursed & Inventory Reconciled");
        WriteLine("*** THANK YOU ***");
        WriteLine();
        WriteLine();

        WriteBytes(EscFeedCut);

        return Task.FromResult(ms.ToArray());
    }

    public Task<string> GenerateHtmlTaxInvoiceAsync(SaleReceiptModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Tax Invoice</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Tahoma, sans-serif; margin: 20px; font-size: 13px; color: #333; }");
        sb.AppendLine(".invoice-box { max-width: 800px; margin: auto; padding: 24px; border: 1px solid #ddd; box-shadow: 0 0 10px rgba(0,0,0,0.05); }");
        sb.AppendLine(".header { text-align: center; border-bottom: 2px solid #2e7d32; padding-bottom: 12px; margin-bottom: 16px; }");
        sb.AppendLine(".header h1 { margin: 0; color: #2e7d32; font-size: 24px; }");
        sb.AppendLine(".meta { display: flex; justify-content: space-between; margin-bottom: 16px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 10px; }");
        sb.AppendLine("th, td { border: 1px solid #eee; padding: 8px 12px; text-align: left; }");
        sb.AppendLine("th { background-color: #f8f9fa; font-weight: 600; }");
        sb.AppendLine(".total-row { font-weight: bold; background-color: #f1f8e9; }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<div class='invoice-box'>");
        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<h1>{model.PharmacyName}</h1>");
        sb.AppendLine($"<p>{model.PharmacyAddress} | Tel: {model.PharmacyPhone}</p>");
        sb.AppendLine($"<p><strong>GSTIN:</strong> {model.Gstin} | <strong>Drug License:</strong> {model.DlNumbers}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='meta'>");
        sb.AppendLine("<div>");
        sb.AppendLine($"<p><strong>Tax Invoice No:</strong> {model.InvoiceNo}</p>");
        sb.AppendLine($"<p><strong>Date & Time:</strong> {model.InvoiceDate:dd-MMM-yyyy hh:mm tt}</p>");
        sb.AppendLine($"<p><strong>Cashier / Counter:</strong> {model.CashierName} / {model.CounterName}</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div>");
        if (!string.IsNullOrWhiteSpace(model.CustomerName)) sb.AppendLine($"<p><strong>Patient / Customer:</strong> {model.CustomerName}</p>");
        if (!string.IsNullOrWhiteSpace(model.DoctorName)) sb.AppendLine($"<p><strong>Doctor:</strong> {model.DoctorName}</p>");
        sb.AppendLine("</div></div>");

        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine("<th>#</th><th>Medicine Description</th><th>Batch</th><th>Expiry</th><th>Qty</th><th>MRP</th><th>Rate</th><th>GST%</th><th>Amount (₹)</th>");
        sb.AppendLine("</tr></thead><tbody>");

        int index = 1;
        foreach (var item in model.Items)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{index++}</td><td>{item.ProductName}</td><td>{item.BatchNumber}</td><td>{item.ExpiryDate:MM/yy}</td>");
            sb.AppendLine($"<td>{item.Quantity:0.#}</td><td>₹{item.UnitPrice:F2}</td><td>₹{item.UnitPrice:F2}</td><td>{item.GstPercent}%</td><td>₹{item.NetAmount:F2}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody><tfoot>");
        sb.AppendLine($"<tr><td colspan='8' style='text-align:right'>Taxable Subtotal:</td><td>₹{model.Subtotal:F2}</td></tr>");
        if (model.CgstAmount > 0) sb.AppendLine($"<tr><td colspan='8' style='text-align:right'>CGST:</td><td>₹{model.CgstAmount:F2}</td></tr>");
        if (model.SgstAmount > 0) sb.AppendLine($"<tr><td colspan='8' style='text-align:right'>SGST:</td><td>₹{model.SgstAmount:F2}</td></tr>");
        if (model.IgstAmount > 0) sb.AppendLine($"<tr><td colspan='8' style='text-align:right'>IGST:</td><td>₹{model.IgstAmount:F2}</td></tr>");
        if (model.RoundOff != 0) sb.AppendLine($"<tr><td colspan='8' style='text-align:right'>Round Off:</td><td>₹{model.RoundOff:F2}</td></tr>");
        sb.AppendLine($"<tr class='total-row'><td colspan='8' style='text-align:right'>GRAND TOTAL:</td><td>₹{model.GrandTotal:F2}</td></tr>");
        sb.AppendLine("</tfoot></table>");

        sb.AppendLine("<div style='margin-top:20px; text-align:center; font-size:11px; color:#777;'>");
        sb.AppendLine("<p>GST Applicable on Medicines | No return without original tax bill | Consult physician before taking Schedule H/H1/X medicines.</p>");
        sb.AppendLine("<p><strong>*** GET WELL SOON ***</strong></p>");
        sb.AppendLine("</div></div></body></html>");

        return Task.FromResult(sb.ToString());
    }

    private static string CenterText(string text, int width)
    {
        if (text.Length >= width) return text;
        int pad = (width - text.Length) / 2;
        return text.PadLeft(pad + text.Length).PadRight(width);
    }
}
