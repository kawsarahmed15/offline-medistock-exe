using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Medistock.Contracts.Printing;

namespace Medistock.Infrastructure.Hardware.Printers;

public class BillDocumentGenerator : IBillDocumentGenerator
{
    private static readonly byte[] EscInit = { 0x1B, 0x40 };
    private static readonly byte[] EscAlignLeft = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] EscAlignCenter = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] EscAlignRight = { 0x1B, 0x61, 0x02 };
    private static readonly byte[] EscBoldOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] EscBoldOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] EscDoubleHeight = { 0x1B, 0x21, 0x10 };
    private static readonly byte[] EscNormalText = { 0x1B, 0x21, 0x00 };
    private static readonly byte[] EscFeedCut = { 0x1D, 0x56, 0x42, 0x00 };

    public Task<string> ConvertAmountToWordsInrAsync(decimal amount)
    {
        return Task.FromResult(IndianCurrencyWordsConverter.ConvertToWords(amount));
    }

    public async Task<string> GenerateHtmlBillAsync(SaleReceiptModel receipt, BillTemplateConfig config)
    {
        var sb = new StringBuilder();
        var style = config.Styling;
        var header = config.Header;
        var meta = config.Metadata;
        var footer = config.Footer;
        var tax = config.TaxSummary;
        var sig = config.Signatures;
        var visibleColumns = config.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayOrder).ToList();

        // Calculate max container width & page styling based on PaperSize
        string containerMaxWidth;
        string pageMarginRule;
        switch (config.PaperSize)
        {
            case PaperSize.A5_Landscape:
                containerMaxWidth = "800px";
                pageMarginRule = "@page { size: A5 landscape; margin: " + $"{style.MarginTopMm}mm {style.MarginRightMm}mm {style.MarginBottomMm}mm {style.MarginLeftMm}mm; " + "}";
                break;
            case PaperSize.A5_Portrait:
                containerMaxWidth = "560px";
                pageMarginRule = "@page { size: A5 portrait; margin: " + $"{style.MarginTopMm}mm {style.MarginRightMm}mm {style.MarginBottomMm}mm {style.MarginLeftMm}mm; " + "}";
                break;
            case PaperSize.Thermal_80mm:
                containerMaxWidth = "300px";
                pageMarginRule = "@page { size: 80mm auto; margin: 2mm; }";
                break;
            case PaperSize.Thermal_58mm:
                containerMaxWidth = "210px";
                pageMarginRule = "@page { size: 58mm auto; margin: 1mm; }";
                break;
            case PaperSize.A4_Landscape:
                containerMaxWidth = "1100px";
                pageMarginRule = "@page { size: A4 landscape; margin: " + $"{style.MarginTopMm}mm {style.MarginRightMm}mm {style.MarginBottomMm}mm {style.MarginLeftMm}mm; " + "}";
                break;
            case PaperSize.A4_Portrait:
            default:
                containerMaxWidth = "820px";
                pageMarginRule = "@page { size: A4 portrait; margin: " + $"{style.MarginTopMm}mm {style.MarginRightMm}mm {style.MarginBottomMm}mm {style.MarginLeftMm}mm; " + "}";
                break;
        }

        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<title>" + WebUtility.HtmlEncode(header.SaleInvoiceTitle) + " - " + WebUtility.HtmlEncode(receipt.InvoiceNo) + "</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(pageMarginRule);
        sb.AppendLine($"body {{ font-family: {style.FontFamily}; font-size: {style.BaseFontSizePt}pt; color: #111; margin: 0; padding: 12px; background: #fff; line-height: 1.35; }}");
        sb.AppendLine($".bill-container {{ max-width: {containerMaxWidth}; margin: 0 auto; {(config.PaperSize is PaperSize.Thermal_80mm or PaperSize.Thermal_58mm ? "padding: 4px;" : "border: 1px solid #ccc; padding: 18px; box-shadow: 0 2px 8px rgba(0,0,0,0.08);")} }}");
        
        // Header CSS
        var headerAlign = header.Alignment switch
        {
            TextAlignmentOption.Left => "left",
            TextAlignmentOption.Right => "right",
            _ => "center"
        };
        sb.AppendLine($".header {{ text-align: {headerAlign}; border-bottom: 2px solid {style.PrimaryColorHex}; padding-bottom: 8px; margin-bottom: 12px; }}");
        sb.AppendLine($".header h1 {{ margin: 0 0 4px 0; font-size: 1.6em; color: {style.PrimaryColorHex}; letter-spacing: 0.5px; text-transform: uppercase; }}");
        sb.AppendLine(".header .tagline { font-size: 0.85em; font-style: italic; color: #555; margin: 0 0 4px 0; }");
        sb.AppendLine(".header .address-line { font-size: 0.85em; margin: 2px 0; color: #333; }");
        sb.AppendLine(".header .licenses { font-size: 0.85em; margin: 4px 0 0 0; font-weight: 600; color: #222; }");
        sb.AppendLine($".doc-title {{ display: inline-block; padding: 3px 12px; font-weight: bold; font-size: 1em; border: 1.5px solid {style.PrimaryColorHex}; margin: 8px 0 0 0; border-radius: 3px; background: #fafafa; }}");

        // Metadata Grid CSS
        sb.AppendLine(".meta-grid { display: flex; justify-content: space-between; flex-wrap: wrap; margin-bottom: 12px; font-size: 0.9em; border-bottom: 1px dashed #bbb; padding-bottom: 8px; }");
        sb.AppendLine(".meta-col { flex: 1; min-width: 180px; margin: 2px 6px; }");
        sb.AppendLine(".meta-row { margin: 3px 0; }");
        sb.AppendLine(".meta-label { font-weight: 600; color: #444; }");

        // Table CSS
        sb.AppendLine("table.items-table { width: 100%; border-collapse: collapse; margin: 8px 0 12px 0; }");
        sb.AppendLine($"table.items-table th {{ background-color: #f3f6f4; color: #111; font-weight: bold; font-size: 0.9em; padding: 6px 8px; {(style.ShowTableBorders ? "border: 1px solid #ddd;" : "border-bottom: 2px solid #333;")} }}");
        sb.AppendLine($"table.items-table td {{ padding: 5px 8px; font-size: 0.88em; {(style.ShowTableBorders ? "border: 1px solid #eee;" : "border-bottom: 1px solid #f0f0f0;")} }}");
        if (style.AlternateRowColors)
            sb.AppendLine("table.items-table tbody tr:nth-child(even) { background-color: #fafbfc; }");

        // Totals & Footer CSS
        sb.AppendLine(".totals-section { display: flex; justify-content: space-between; margin-top: 8px; font-size: 0.9em; }");
        sb.AppendLine(".totals-left { flex: 1; margin-right: 16px; }");
        sb.AppendLine(".totals-right { width: 280px; }");
        sb.AppendLine(".totals-table { width: 100%; border-collapse: collapse; }");
        sb.AppendLine(".totals-table td { padding: 4px 6px; }");
        sb.AppendLine($".grand-total-row {{ font-size: 1.25em; font-weight: bold; background: #e8f5e9; color: {style.PrimaryColorHex}; border-top: 2px solid {style.PrimaryColorHex}; border-bottom: 2px solid {style.PrimaryColorHex}; }}");
        sb.AppendLine(".savings-box { background: #fff8e1; border: 1px dashed #f57f17; color: #e65100; font-weight: bold; padding: 6px 10px; margin: 6px 0; border-radius: 4px; text-align: center; }");
        sb.AppendLine(".amount-words { font-style: italic; font-size: 0.85em; color: #333; margin: 4px 0 8px 0; }");
        sb.AppendLine(".tax-table { width: 100%; border-collapse: collapse; margin-top: 8px; font-size: 0.8em; }");
        sb.AppendLine(".tax-table th, .tax-table td { border: 1px solid #ddd; padding: 3px 6px; text-align: right; }");
        sb.AppendLine(".tax-table th { background: #f5f5f5; text-align: center; }");

        // Signatures & Greetings
        sb.AppendLine(".signatures { display: flex; justify-content: space-between; margin-top: 24px; padding-top: 12px; }");
        sb.AppendLine(".sig-box { text-align: center; min-width: 140px; font-size: 0.85em; font-weight: 600; }");
        sb.AppendLine(".sig-line { border-top: 1px solid #444; margin-top: 35px; padding-top: 4px; }");
        sb.AppendLine(".terms-box { font-size: 0.78em; color: #666; margin-top: 14px; border-top: 1px dashed #ccc; padding-top: 6px; }");
        sb.AppendLine(".greeting-text { text-align: center; font-weight: bold; font-size: 0.9em; margin-top: 10px; letter-spacing: 0.5px; color: #333; }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<div class='bill-container'>");

        // 1. Header Section
        if (header.IsVisible)
        {
            sb.AppendLine("<div class='header'>");
            sb.AppendLine($"<h1>{WebUtility.HtmlEncode(header.StoreName)}</h1>");
            if (!string.IsNullOrWhiteSpace(header.Tagline))
                sb.AppendLine($"<div class='tagline'>{WebUtility.HtmlEncode(header.Tagline)}</div>");
            
            if (!string.IsNullOrWhiteSpace(header.AddressLine1))
                sb.AppendLine($"<div class='address-line'>{WebUtility.HtmlEncode(header.AddressLine1)}</div>");
            if (!string.IsNullOrWhiteSpace(header.AddressLine2))
                sb.AppendLine($"<div class='address-line'>{WebUtility.HtmlEncode(header.AddressLine2)}</div>");
            
            var contacts = new List<string>();
            if (!string.IsNullOrWhiteSpace(header.Phone)) contacts.Add($"Tel: {WebUtility.HtmlEncode(header.Phone)}");
            if (!string.IsNullOrWhiteSpace(header.Email)) contacts.Add($"Email: {WebUtility.HtmlEncode(header.Email)}");
            if (contacts.Any())
                sb.AppendLine($"<div class='address-line'>{string.Join(" | ", contacts)}</div>");

            var licList = new List<string>();
            if (header.ShowGstin && !string.IsNullOrWhiteSpace(header.Gstin)) licList.Add($"{header.GstinLabel}: {WebUtility.HtmlEncode(header.Gstin)}");
            if (header.ShowDlNumbers && !string.IsNullOrWhiteSpace(header.DlNumbers)) licList.Add($"{header.DlLabel}: {WebUtility.HtmlEncode(header.DlNumbers)}");
            if (header.ShowFssai && !string.IsNullOrWhiteSpace(header.Fssai)) licList.Add($"{header.FssaiLabel}: {WebUtility.HtmlEncode(header.Fssai)}");
            if (licList.Any())
                sb.AppendLine($"<div class='licenses'>{string.Join(" &nbsp;|&nbsp; ", licList)}</div>");

            sb.AppendLine($"<div class='doc-title'>{WebUtility.HtmlEncode(header.SaleInvoiceTitle)}</div>");
            if (header.ShowCompositionDeclaration && !string.IsNullOrWhiteSpace(header.CompositionDeclarationText))
            {
                sb.AppendLine($"<div style='font-size:0.8em; font-weight:600; color:#444; margin-top:4px; font-style:italic;'>{WebUtility.HtmlEncode(header.CompositionDeclarationText)}</div>");
            }
            sb.AppendLine("</div>");
        }

        // 2. Metadata / Patient & Doctor Section
        sb.AppendLine("<div class='meta-grid'>");
        
        // Left Column
        sb.AppendLine("<div class='meta-col'>");
        if (meta.ShowInvoiceNo)
            sb.AppendLine($"<div class='meta-row'><span class='meta-label'>{meta.InvoiceNoLabel}:</span> {WebUtility.HtmlEncode(receipt.InvoiceNo)}</div>");
        if (meta.ShowInvoiceDate)
            sb.AppendLine($"<div class='meta-row'><span class='meta-label'>{meta.InvoiceDateLabel}:</span> {receipt.InvoiceDate.ToString(meta.DateFormat)} {(meta.ShowInvoiceTime ? receipt.InvoiceDate.ToString(meta.TimeFormat) : "")}</div>");
        if (meta.ShowCounter)
            sb.AppendLine($"<div class='meta-row'><span class='meta-label'>{meta.CounterLabel}:</span> {WebUtility.HtmlEncode(receipt.CounterName)}</div>");
        if (meta.ShowCashier && !string.IsNullOrWhiteSpace(receipt.CashierName))
            sb.AppendLine($"<div class='meta-row'><span class='meta-label'>{meta.CashierLabel}:</span> {WebUtility.HtmlEncode(receipt.CashierName)}</div>");
        sb.AppendLine("</div>");

        // Right Column
        sb.AppendLine("<div class='meta-col'>");
        if (meta.ShowCustomerName && !string.IsNullOrWhiteSpace(receipt.CustomerName))
            sb.AppendLine($"<div class='meta-row'><span class='meta-label'>{meta.CustomerNameLabel}:</span> <strong>{WebUtility.HtmlEncode(receipt.CustomerName)}</strong></div>");
        if (meta.ShowDoctorName && !string.IsNullOrWhiteSpace(receipt.DoctorName))
            sb.AppendLine($"<div class='meta-row'><span class='meta-label'>{meta.DoctorNameLabel}:</span> {WebUtility.HtmlEncode(receipt.DoctorName)}</div>");
        if (meta.ShowPaymentMode && receipt.Payments.Any())
            sb.AppendLine($"<div class='meta-row'><span class='meta-label'>{meta.PaymentModeLabel}:</span> {string.Join(", ", receipt.Payments.Select(p => p.Mode))}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>"); // End meta-grid

        // 3. Dynamic Items Table
        sb.AppendLine("<table class='items-table'>");
        sb.AppendLine("<thead><tr>");
        foreach (var col in visibleColumns)
        {
            var alignStr = col.Alignment switch
            {
                TextAlignmentOption.Center => "center",
                TextAlignmentOption.Right => "right",
                _ => "left"
            };
            var widthAttr = col.WidthPercent > 0 ? $" style='width:{col.WidthPercent}%; text-align:{alignStr};'" : $" style='text-align:{alignStr};'";
            sb.AppendLine($"<th{widthAttr}>{WebUtility.HtmlEncode(col.HeaderTitle)}</th>");
        }
        sb.AppendLine("</tr></thead><tbody>");

        int srNo = 1;
        foreach (var item in receipt.Items)
        {
            sb.AppendLine("<tr>");
            foreach (var col in visibleColumns)
            {
                var alignStr = col.Alignment switch
                {
                    TextAlignmentOption.Center => "center",
                    TextAlignmentOption.Right => "right",
                    _ => "left"
                };
                var val = GetColumnValue(item, col.FieldType, srNo);
                sb.AppendLine($"<td style='text-align:{alignStr};'>{WebUtility.HtmlEncode(val)}</td>");
            }
            sb.AppendLine("</tr>");
            srNo++;
        }
        sb.AppendLine("</tbody></table>");

        // 4. Totals & Summary
        var words = await ConvertAmountToWordsInrAsync(receipt.GrandTotal);

        sb.AppendLine("<div class='totals-section'>");
        
        // Left Column (Words, Savings, Tax breakdown)
        sb.AppendLine("<div class='totals-left'>");
        if (footer.ShowAmountInWords)
        {
            sb.AppendLine($"<div class='amount-words'><strong>In Words:</strong> {WebUtility.HtmlEncode(words)}</div>");
        }

        // Savings Callout (Only if explicitly enabled, template string is provided, and genuine savings exist)
        if (footer.ShowSavingsCallout && !string.IsNullOrWhiteSpace(footer.SavingsTemplate))
        {
            decimal itemSavings = receipt.Items.Sum(i => Math.Max(0, (i.UnitPrice * i.Quantity) - i.NetAmount));
            if (itemSavings > 0)
            {
                var savingsStr = footer.SavingsTemplate.Replace("{SAVINGS_AMOUNT}", $"₹{itemSavings:F2}");
                sb.AppendLine($"<div class='savings-box'>{WebUtility.HtmlEncode(savingsStr)}</div>");
            }
        }

        // Tax Breakdown Table
        if (tax.ShowTaxTable)
        {
            sb.AppendLine("<table class='tax-table'>");
            sb.AppendLine("<thead><tr><th>Tax Rate</th><th>Taxable</th><th>CGST</th><th>SGST</th><th>IGST</th><th>Total Tax</th></tr></thead><tbody>");
            var totalTaxable = receipt.Subtotal;
            var totalTax = receipt.CgstAmount + receipt.SgstAmount + receipt.IgstAmount;
            sb.AppendLine($"<tr><td style='text-align:center;'>GST</td><td>₹{totalTaxable:F2}</td><td>₹{receipt.CgstAmount:F2}</td><td>₹{receipt.SgstAmount:F2}</td><td>₹{receipt.IgstAmount:F2}</td><td><strong>₹{totalTax:F2}</strong></td></tr>");
            sb.AppendLine("</tbody></table>");
        }

        if (footer.ShowBankDetails && !string.IsNullOrWhiteSpace(footer.BankDetailsText))
        {
            sb.AppendLine($"<div style='margin-top:8px; font-size:0.85em;'><strong>Bank Details:</strong> {WebUtility.HtmlEncode(footer.BankDetailsText)}</div>");
        }

        sb.AppendLine("</div>"); // End totals-left

        // Right Column (Subtotal, GST, RoundOff, Grand Total)
        sb.AppendLine("<div class='totals-right'>");
        sb.AppendLine("<table class='totals-table'>");
        
        if (footer.ShowTotalItems || footer.ShowTotalQuantity)
        {
            var totalQty = receipt.Items.Sum(i => i.Quantity);
            sb.AppendLine($"<tr><td>Total Items / Qty:</td><td style='text-align:right;'>{receipt.Items.Count} / {totalQty:0.#}</td></tr>");
        }

        if (footer.ShowSubtotal)
            sb.AppendLine($"<tr><td>Taxable Value:</td><td style='text-align:right;'>₹{receipt.Subtotal:F2}</td></tr>");

        if (receipt.CgstAmount > 0)
            sb.AppendLine($"<tr><td>CGST:</td><td style='text-align:right;'>₹{receipt.CgstAmount:F2}</td></tr>");
        if (receipt.SgstAmount > 0)
            sb.AppendLine($"<tr><td>SGST:</td><td style='text-align:right;'>₹{receipt.SgstAmount:F2}</td></tr>");
        if (receipt.IgstAmount > 0)
            sb.AppendLine($"<tr><td>IGST:</td><td style='text-align:right;'>₹{receipt.IgstAmount:F2}</td></tr>");

        if (footer.ShowRoundOff && receipt.RoundOff != 0)
            sb.AppendLine($"<tr><td>Round Off:</td><td style='text-align:right;'>₹{receipt.RoundOff:F2}</td></tr>");

        if (footer.ShowGrandTotal)
            sb.AppendLine($"<tr class='grand-total-row'><td><strong>GRAND TOTAL:</strong></td><td style='text-align:right;'><strong>₹{receipt.GrandTotal:F2}</strong></td></tr>");

        sb.AppendLine("</table></div>"); // End totals-right
        sb.AppendLine("</div>"); // End totals-section

        // 5. Signatures
        if (sig.ShowPharmacistSign || sig.ShowCustomerSign || sig.ShowAuthorizedSign)
        {
            sb.AppendLine("<div class='signatures'>");
            if (sig.ShowPharmacistSign)
            {
                sb.AppendLine($"<div class='sig-box'><div class='sig-line'>{WebUtility.HtmlEncode(sig.PharmacistSignTitle)}</div></div>");
            }
            if (sig.ShowCustomerSign)
            {
                sb.AppendLine($"<div class='sig-box'><div class='sig-line'>{WebUtility.HtmlEncode(sig.CustomerSignTitle)}</div></div>");
            }
            if (sig.ShowAuthorizedSign)
            {
                sb.AppendLine($"<div class='sig-box'><div class='sig-line'>{WebUtility.HtmlEncode(sig.AuthorizedSignTitle.Replace("\n", "<br/>"))}</div></div>");
            }
            sb.AppendLine("</div>");
        }

        // 6. Terms & Conditions and Greetings
        if (footer.ShowTermsAndConditions && !string.IsNullOrWhiteSpace(footer.TermsAndConditionsText))
        {
            sb.AppendLine("<div class='terms-box'>");
            sb.AppendLine($"<strong>Terms & Conditions:</strong><br/>{WebUtility.HtmlEncode(footer.TermsAndConditionsText).Replace("\n", "<br/>")}");
            sb.AppendLine("</div>");
        }

        if (footer.ShowGreeting && !string.IsNullOrWhiteSpace(footer.GreetingText))
        {
            sb.AppendLine($"<div class='greeting-text'>{WebUtility.HtmlEncode(footer.GreetingText)}</div>");
        }

        sb.AppendLine("</div></body></html>");

        return sb.ToString();
    }

    private static string GetColumnValue(ReceiptItemModel item, BillFieldType type, int srNo)
    {
        return type switch
        {
            BillFieldType.SrNo => srNo.ToString(),
            BillFieldType.ItemName => item.ProductName,
            BillFieldType.Packing => "10's",
            BillFieldType.Manufacturer => "",
            BillFieldType.BatchNumber => item.BatchNumber,
            BillFieldType.ExpiryDate => item.ExpiryDate.ToString("MM/yy"),
            BillFieldType.HsnCode => "3004",
            BillFieldType.Mrp => $"₹{item.UnitPrice:F2}",
            BillFieldType.UnitRate => $"₹{item.UnitPrice:F2}",
            BillFieldType.Quantity => $"{item.Quantity:0.#}",
            BillFieldType.FreeQuantity => "0",
            BillFieldType.DiscountPercent => "0%",
            BillFieldType.DiscountAmount => "₹0.00",
            BillFieldType.GstPercent => $"{item.GstPercent:0.#}%",
            BillFieldType.CgstAmount => $"₹{(item.NetAmount * (item.GstPercent / 200m)):F2}",
            BillFieldType.SgstAmount => $"₹{(item.NetAmount * (item.GstPercent / 200m)):F2}",
            BillFieldType.IgstAmount => "₹0.00",
            BillFieldType.TaxableAmount => $"₹{item.NetAmount:F2}",
            BillFieldType.TotalAmount => $"₹{item.NetAmount:F2}",
            _ => ""
        };
    }

    public Task<byte[]> GenerateEscPosBillAsync(SaleReceiptModel receipt, BillTemplateConfig config)
    {
        using var ms = new MemoryStream();
        var cols = config.PaperSize == PaperSize.Thermal_58mm ? 32 : 48;
        var enc = Encoding.UTF8;

        void WriteBytes(byte[] bytes) => ms.Write(bytes, 0, bytes.Length);
        void WriteString(string str) => WriteBytes(enc.GetBytes(str));
        void WriteLine(string str = "") => WriteString(str + "\n");
        void WriteDivider(char ch = '-') => WriteLine(new string(ch, cols));

        // 1. Initialize
        WriteBytes(EscInit);

        // 2. Store Header
        WriteBytes(EscAlignCenter);
        WriteBytes(EscDoubleHeight);
        WriteBytes(EscBoldOn);
        WriteLine(config.Header.StoreName.ToUpperInvariant());
        WriteBytes(EscNormalText);
        WriteBytes(EscBoldOff);

        if (!string.IsNullOrWhiteSpace(config.Header.AddressLine1))
            WriteLine(config.Header.AddressLine1);
        if (!string.IsNullOrWhiteSpace(config.Header.Phone))
            WriteLine($"Tel: {config.Header.Phone}");
        if (config.Header.ShowGstin && !string.IsNullOrWhiteSpace(config.Header.Gstin))
            WriteLine($"GSTIN: {config.Header.Gstin}");
        if (config.Header.ShowDlNumbers && !string.IsNullOrWhiteSpace(config.Header.DlNumbers))
            WriteLine($"DL: {config.Header.DlNumbers}");

        WriteDivider('=');

        // 3. Document details
        WriteBytes(EscAlignLeft);
        WriteLine($"{config.Header.SaleInvoiceTitle}: {receipt.InvoiceNo}");
        WriteLine($"Date: {receipt.InvoiceDate:dd-MMM-yyyy hh:mm tt}  Counter: {receipt.CounterName}");
        if (!string.IsNullOrWhiteSpace(receipt.CustomerName))
            WriteLine($"Patient: {receipt.CustomerName}");
        if (!string.IsNullOrWhiteSpace(receipt.DoctorName))
            WriteLine($"Doctor: {receipt.DoctorName}");

        WriteDivider('-');

        // 4. Line items
        var visibleCols = config.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayOrder).ToList();
        if (cols >= 48)
        {
            WriteLine($"{"ITEM".PadRight(22)} {"BATCH".PadRight(8)} {"EXP".PadRight(6)} {"QTY".PadLeft(3)} {"AMT".PadLeft(6)}");
        }
        else
        {
            WriteLine($"{"ITEM".PadRight(16)} {"QTY".PadLeft(5)} {"AMT".PadLeft(9)}");
        }
        WriteDivider('-');

        foreach (var item in receipt.Items)
        {
            var exp = item.ExpiryDate.ToString("MM/yy");
            if (cols >= 48)
            {
                var name = item.ProductName.Length > 20 ? item.ProductName.Substring(0, 20) : item.ProductName;
                var batch = item.BatchNumber.Length > 8 ? item.BatchNumber.Substring(0, 8) : item.BatchNumber;
                WriteLine($"{name.PadRight(22)} {batch.PadRight(8)} {exp.PadRight(6)} {item.Quantity,3:0.#} {item.NetAmount,6:F2}");
            }
            else
            {
                var name = item.ProductName.Length > 16 ? item.ProductName.Substring(0, 16) : item.ProductName;
                WriteLine($"{name.PadRight(16)} {item.Quantity,5:0.#} {item.NetAmount,9:F2}");
            }
        }

        WriteDivider('-');

        // 5. Totals
        WriteBytes(EscAlignRight);
        WriteLine($"Taxable Subtotal:  ₹{receipt.Subtotal:F2}");
        if (receipt.CgstAmount > 0) WriteLine($"CGST:  ₹{receipt.CgstAmount:F2}");
        if (receipt.SgstAmount > 0) WriteLine($"SGST:  ₹{receipt.SgstAmount:F2}");
        if (receipt.RoundOff != 0) WriteLine($"Round Off:  ₹{receipt.RoundOff:F2}");

        WriteBytes(EscBoldOn);
        WriteBytes(EscDoubleHeight);
        WriteLine($"TOTAL:  ₹{receipt.GrandTotal:F2}");
        WriteBytes(EscNormalText);
        WriteBytes(EscBoldOff);

        WriteDivider('-');

        // 6. Payments
        WriteBytes(EscAlignLeft);
        foreach (var p in receipt.Payments)
        {
            WriteLine($"Paid via {p.Mode}: ₹{p.Amount:F2}");
        }

        // 7. Greetings
        WriteBytes(EscAlignCenter);
        WriteLine();
        if (config.Footer.ShowGreeting && !string.IsNullOrWhiteSpace(config.Footer.GreetingText))
        {
            WriteLine(config.Footer.GreetingText);
        }
        WriteLine();
        WriteLine();

        WriteBytes(EscFeedCut);

        return Task.FromResult(ms.ToArray());
    }
}
