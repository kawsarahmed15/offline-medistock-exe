using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Medistock.Infrastructure.Hardware.Export;

public class PurchaseExportService : IPurchaseExportService
{
    public Task<string> ExportToExcelAsync(IReadOnlyList<PurchaseExportRow> rows, PurchaseExportMetadata meta, string? targetFilePath = null)
    {
        var filePath = targetFilePath ?? GetDefaultExportPath("Purchase_Inward_Ledger", "xlsx");
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);

        // 1. [Content_Types].xml
        AddZipEntry(zip, "[Content_Types].xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
    <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
    <Default Extension=""xml"" ContentType=""application/xml""/>
    <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
    <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
    <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
    <Override PartName=""/xl/sharedStrings.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml""/>
</Types>");

        // 2. _rels/.rels
        AddZipEntry(zip, "_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");

        // 3. xl/_rels/workbook.xml.rels
        AddZipEntry(zip, "xl/_rels/workbook.xml.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
    <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
    <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"" Target=""sharedStrings.xml""/>
</Relationships>");

        // 4. xl/workbook.xml
        AddZipEntry(zip, "xl/workbook.xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
    <sheets>
        <sheet name=""Purchase Invoices"" sheetId=""1"" r:id=""rId1""/>
    </sheets>
</workbook>");

        // 5. xl/styles.xml
        AddZipEntry(zip, "xl/styles.xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
    <fonts count=""4"">
        <font><sz val=""11""/><name val=""Calibri""/></font>
        <font><b/><sz val=""14""/><name val=""Calibri""/><color rgb=""FF1E293B""/></font>
        <font><b/><sz val=""11""/><name val=""Calibri""/><color rgb=""FFFFFFFF""/></font>
        <font><b/><sz val=""11""/><name val=""Calibri""/><color rgb=""FF0F172A""/></font>
    </fonts>
    <fills count=""4"">
        <fill><patternFill patternType=""none""/></fill>
        <fill><patternFill patternType=""gray125""/></fill>
        <fill><patternFill patternType=""solid""><fgColor rgb=""FF059669""/></patternFill></fill>
        <fill><patternFill patternType=""solid""><fgColor rgb=""FFF1F5F9""/></patternFill></fill>
    </fills>
    <borders count=""2"">
        <border><left/><right/><top/><bottom/></border>
        <border>
            <left style=""thin""><color rgb=""FFCBD5E1""/></left>
            <right style=""thin""><color rgb=""FFCBD5E1""/></right>
            <top style=""thin""><color rgb=""FFCBD5E1""/></top>
            <bottom style=""thin""><color rgb=""FFCBD5E1""/></bottom>
        </border>
    </borders>
    <cellStyleXfs count=""1"">
        <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/>
    </cellStyleXfs>
    <cellXfs count=""5"">
        <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
        <xf numFmtId=""0"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0""/> <!-- Title -->
        <xf numFmtId=""0"" fontId=""2"" fillId=""2"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1""/> <!-- Header -->
        <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""1"" xfId=""0"" applyBorder=""1""/> <!-- Normal row -->
        <xf numFmtId=""0"" fontId=""3"" fillId=""3"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1""/> <!-- Summary row -->
    </cellXfs>
</styleSheet>");

        var sharedStrings = new List<string>();
        var stringMap = new Dictionary<string, int>();

        int GetStringId(string val)
        {
            if (stringMap.TryGetValue(val, out int idx)) return idx;
            int newIdx = sharedStrings.Count;
            sharedStrings.Add(val);
            stringMap[val] = newIdx;
            return newIdx;
        }

        var sb = new StringBuilder();
        sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
    <cols>
        <col min=""1"" max=""1"" width=""6"" customWidth=""1""/>
        <col min=""2"" max=""2"" width=""32"" customWidth=""1""/>
        <col min=""3"" max=""3"" width=""18"" customWidth=""1""/>
        <col min=""4"" max=""4"" width=""18"" customWidth=""1""/>
        <col min=""5"" max=""5"" width=""14"" customWidth=""1""/>
        <col min=""6"" max=""6"" width=""10"" customWidth=""1""/>
        <col min=""7"" max=""7"" width=""14"" customWidth=""1""/>
        <col min=""8"" max=""8"" width=""12"" customWidth=""1""/>
        <col min=""9"" max=""9"" width=""12"" customWidth=""1""/>
        <col min=""10"" max=""10"" width=""12"" customWidth=""1""/>
        <col min=""11"" max=""11"" width=""16"" customWidth=""1""/>
        <col min=""12"" max=""12"" width=""12"" customWidth=""1""/>
    </cols>
    <sheetData>");

        int rNum = 1;
        // Title Row
        sb.Append($@"<row r=""{rNum}""><c r=""A{rNum}"" s=""1"" t=""s""><v>{GetStringId($"{meta.PharmacyName} — Purchase Inward Register")}</v></c></row>");
        rNum++;
        // Meta Row
        sb.Append($@"<row r=""{rNum}""><c r=""A{rNum}"" t=""s""><v>{GetStringId($"GSTIN: {meta.Gstin} | Generated: {meta.ExportDate:dd-MMM-yyyy HH:mm} | Total Purchases: ₹{meta.TotalPurchases:N2} | Invoices: {meta.TotalInvoicesCount}")}</v></c></row>");
        rNum += 2;

        // Header Row
        var headers = new[] { "#", "Wholesaler / Supplier", "Invoice No.", "Supplier GSTIN", "Invoice Date", "Items", "Taxable (₹)", "CGST (₹)", "SGST (₹)", "IGST (₹)", "Grand Total (₹)", "Status" };
        sb.Append($@"<row r=""{rNum}"">");
        for (int c = 0; c < headers.Length; c++)
        {
            var colLetter = GetColumnLetter(c + 1);
            sb.Append($@"<c r=""{colLetter}{rNum}"" s=""2"" t=""s""><v>{GetStringId(headers[c])}</v></c>");
        }
        sb.Append("</row>");
        rNum++;

        // Data Rows
        foreach (var row in rows)
        {
            sb.Append($@"<row r=""{rNum}"">");
            sb.Append($@"<c r=""A{rNum}"" s=""3""><v>{row.Index}</v></c>");
            sb.Append($@"<c r=""B{rNum}"" s=""3"" t=""s""><v>{GetStringId(row.SupplierName)}</v></c>");
            sb.Append($@"<c r=""C{rNum}"" s=""3"" t=""s""><v>{GetStringId(row.SupplierInvoiceNo)}</v></c>");
            sb.Append($@"<c r=""D{rNum}"" s=""3"" t=""s""><v>{GetStringId(row.SupplierGstin)}</v></c>");
            sb.Append($@"<c r=""E{rNum}"" s=""3"" t=""s""><v>{GetStringId(row.SupplierInvoiceDate.ToString("dd-MM-yyyy"))}</v></c>");
            sb.Append($@"<c r=""F{rNum}"" s=""3""><v>{row.ItemCount}</v></c>");
            sb.Append($@"<c r=""G{rNum}"" s=""3""><v>{row.TaxableAmount:F2}</v></c>");
            sb.Append($@"<c r=""H{rNum}"" s=""3""><v>{row.CgstAmount:F2}</v></c>");
            sb.Append($@"<c r=""I{rNum}"" s=""3""><v>{row.SgstAmount:F2}</v></c>");
            sb.Append($@"<c r=""J{rNum}"" s=""3""><v>{row.IgstAmount:F2}</v></c>");
            sb.Append($@"<c r=""K{rNum}"" s=""3""><v>{row.GrandTotal:F2}</v></c>");
            sb.Append($@"<c r=""L{rNum}"" s=""3"" t=""s""><v>{GetStringId(row.Status)}</v></c>");
            sb.Append("</row>");
            rNum++;
        }

        // Summary Row
        sb.Append($@"<row r=""{rNum}"">");
        sb.Append($@"<c r=""A{rNum}"" s=""4"" t=""s""><v>{GetStringId("TOTAL")}</v></c>");
        sb.Append($@"<c r=""B{rNum}"" s=""4"" t=""s""><v>{GetStringId($"{rows.Count} Invoices")}</v></c>");
        sb.Append($@"<c r=""C{rNum}"" s=""4""/>");
        sb.Append($@"<c r=""D{rNum}"" s=""4""/>");
        sb.Append($@"<c r=""E{rNum}"" s=""4""/>");
        sb.Append($@"<c r=""F{rNum}"" s=""4""/>");
        sb.Append($@"<c r=""G{rNum}"" s=""4""><v>{meta.TotalPurchases:F2}</v></c>");
        sb.Append($@"<c r=""H{rNum}"" s=""4""/>");
        sb.Append($@"<c r=""I{rNum}"" s=""4""/>");
        sb.Append($@"<c r=""J{rNum}"" s=""4""/>");
        sb.Append($@"<c r=""K{rNum}"" s=""4""><v>{meta.TotalPurchases:F2}</v></c>");
        sb.Append($@"<c r=""L{rNum}"" s=""4""/>");
        sb.Append("</row>");

        sb.Append(@"</sheetData></worksheet>");
        AddZipEntry(zip, "xl/worksheets/sheet1.xml", sb.ToString());

        // Shared Strings XML
        var ssXml = new StringBuilder();
        ssXml.Append($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""{sharedStrings.Count}"" uniqueCount=""{sharedStrings.Count}"">");
        foreach (var s in sharedStrings)
        {
            var escaped = WebUtility.HtmlEncode(s);
            ssXml.Append($@"<si><t>{escaped}</t></si>");
        }
        ssXml.Append("</sst>");
        AddZipEntry(zip, "xl/sharedStrings.xml", ssXml.ToString());

        return Task.FromResult(filePath);
    }

    public Task<string> ExportToWordAsync(IReadOnlyList<PurchaseExportRow> rows, PurchaseExportMetadata meta, string? targetFilePath = null)
    {
        var filePath = targetFilePath ?? GetDefaultExportPath("Purchase_Inward_Report", "docx");
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);

        AddZipEntry(zip, "[Content_Types].xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
    <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
    <Default Extension=""xml"" ContentType=""application/xml""/>
    <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
</Types>");

        AddZipEntry(zip, "_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
</Relationships>");

        var sb = new StringBuilder();
        sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
    <w:body>
        <w:p>
            <w:pPr><w:jc w:val=""center""/></w:pPr>
            <w:r><w:rPr><w:b/><w:sz w:val=""36""/><w:color w:val=""059669""/></w:rPr><w:t>" + WebUtility.HtmlEncode(meta.PharmacyName) + @"</w:t></w:r>
        </w:p>
        <w:p>
            <w:pPr><w:jc w:val=""center""/></w:pPr>
            <w:r><w:rPr><w:sz w:val=""20""/><w:color w:val=""64748B""/></w:rPr><w:t>" + WebUtility.HtmlEncode($"{meta.StoreAddress} | Ph: {meta.ContactPhone} | GSTIN: {meta.Gstin}") + @"</w:t></w:r>
        </w:p>
        <w:p>
            <w:pPr><w:jc w:val=""center""/></w:pPr>
            <w:r><w:rPr><w:b/><w:sz w:val=""24""/><w:color w:val=""0F172A""/></w:rPr><w:t>PURCHASE INWARD REGISTER &amp; GOODS RECEIPT SUMMARY</w:t></w:r>
        </w:p>
        <w:p><w:r><w:t> </w:t></w:r></w:p>");

        // KPI Summary Table
        sb.Append(@"<w:tbl>
            <w:tblPr><w:tblW w:w=""5000"" w:type=""pct""/><w:tblBorders><w:top w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""CBD5E1""/><w:bottom w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""CBD5E1""/><w:insideH w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""E2E8F0""/></w:tblBorders></w:tblPr>
            <w:tr>
                <w:tc><w:p><w:r><w:rPr><w:b/></w:rPr><w:t>Total Purchases (Gross)</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>₹ " + meta.TotalPurchases.ToString("N2") + @"</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/></w:rPr><w:t>Total Invoices</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>" + meta.TotalInvoicesCount + @"</w:t></w:r></w:p></w:tc>
            </w:tr>
            <w:tr>
                <w:tc><w:p><w:r><w:rPr><w:b/></w:rPr><w:t>Active Suppliers</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>" + meta.TotalSuppliersCount + @"</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/></w:rPr><w:t>Total Outstanding Payables</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:color w:val=""DC2626""/></w:rPr><w:t>₹ " + meta.TotalOutstandingPayables.ToString("N2") + @"</w:t></w:r></w:p></w:tc>
            </w:tr>
        </w:tbl>
        <w:p><w:r><w:t> </w:t></w:r></w:p>");

        // Main Purchases Table
        sb.Append(@"<w:tbl>
            <w:tblPr><w:tblW w:w=""5000"" w:type=""pct""/><w:tblBorders><w:top w:val=""single"" w:sz=""6"" w:space=""0"" w:color=""059669""/><w:bottom w:val=""single"" w:sz=""6"" w:space=""0"" w:color=""059669""/><w:insideH w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""CBD5E1""/></w:tblBorders></w:tblPr>
            <w:tr>
                <w:tc><w:p><w:r><w:rPr><w:b/><w:color w:val=""059669""/></w:rPr><w:t>#</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/><w:color w:val=""059669""/></w:rPr><w:t>Wholesaler / Supplier</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/><w:color w:val=""059669""/></w:rPr><w:t>Invoice No.</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/><w:color w:val=""059669""/></w:rPr><w:t>Invoice Date</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/><w:color w:val=""059669""/></w:rPr><w:t>Items</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/><w:color w:val=""059669""/></w:rPr><w:t>Taxable (₹)</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/><w:color w:val=""059669""/></w:rPr><w:t>Grand Total (₹)</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/><w:color w:val=""059669""/></w:rPr><w:t>Status</w:t></w:r></w:p></w:tc>
            </w:tr>");

        foreach (var r in rows)
        {
            sb.Append($@"<w:tr>
                <w:tc><w:p><w:r><w:t>{r.Index}</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/></w:rPr><w:t>{WebUtility.HtmlEncode(r.SupplierName)}</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>{WebUtility.HtmlEncode(r.SupplierInvoiceNo)}</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>{r.SupplierInvoiceDate:dd-MM-yyyy}</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>{r.ItemCount}</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>{r.TaxableAmount:N2}</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:rPr><w:b/></w:rPr><w:t>{r.GrandTotal:N2}</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>{WebUtility.HtmlEncode(r.Status)}</w:t></w:r></w:p></w:tc>
            </w:tr>");
        }

        sb.Append(@"</w:tbl>
        <w:p><w:pPr><w:jc w:val=""right""/></w:pPr><w:r><w:rPr><w:sz w:val=""18""/><w:color w:val=""64748B""/></w:rPr><w:t>Generated via Medistock Pharmacy ERP</w:t></w:r></w:p>
    </w:body>
</w:document>");

        AddZipEntry(zip, "word/document.xml", sb.ToString());

        return Task.FromResult(filePath);
    }

    public Task<string> ExportToPdfHtmlAsync(IReadOnlyList<PurchaseExportRow> rows, PurchaseExportMetadata meta, string? targetFilePath = null)
    {
        var filePath = targetFilePath ?? GetDefaultExportPath("Purchase_Inward_Report", "html");
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var sb = new StringBuilder();
        sb.Append(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Purchase Inward Register - " + WebUtility.HtmlEncode(meta.PharmacyName) + @"</title>
    <style>
        @page { size: A4 landscape; margin: 12mm; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; font-size: 11px; color: #1e293b; margin: 0; padding: 10px; }
        .header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #059669; padding-bottom: 8px; margin-bottom: 12px; }
        .pharmacy-title { font-size: 18px; font-weight: bold; color: #059669; }
        .pharmacy-meta { font-size: 11px; color: #64748b; margin-top: 2px; }
        .kpi-cards { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; margin-bottom: 14px; }
        .kpi-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 8px 12px; }
        .kpi-title { font-size: 9px; font-weight: bold; color: #64748b; text-transform: uppercase; }
        .kpi-val { font-size: 16px; font-weight: bold; color: #0f172a; margin-top: 2px; }
        table { width: 100%; border-collapse: collapse; font-size: 10.5px; }
        th { background: #059669; color: white; text-align: left; padding: 6px 8px; font-weight: 600; font-size: 10px; text-transform: uppercase; }
        td { padding: 5px 8px; border-bottom: 1px solid #e2e8f0; }
        tr:nth-child(even) { background: #f8fafc; }
        .num { text-align: right; }
        .badge { display: inline-block; padding: 2px 6px; border-radius: 3px; font-weight: bold; font-size: 9px; background: #d1fae5; color: #059669; }
    </style>
</head>
<body>
    <div class=""header"">
        <div>
            <div class=""pharmacy-title"">" + WebUtility.HtmlEncode(meta.PharmacyName) + @"</div>
            <div class=""pharmacy-meta"">" + WebUtility.HtmlEncode($"{meta.StoreAddress} | Ph: {meta.ContactPhone} | GSTIN: {meta.Gstin}") + @"</div>
        </div>
        <div style=""text-align: right;"">
            <div style=""font-size: 14px; font-weight: bold; color: #0f172a;"">PURCHASE INWARD REGISTER</div>
            <div class=""pharmacy-meta"">Generated: " + meta.ExportDate.ToString("dd-MMM-yyyy HH:mm") + @"</div>
        </div>
    </div>

    <div class=""kpi-cards"">
        <div class=""kpi-card""><div class=""kpi-title"">Total Purchases (Gross)</div><div class=""kpi-val"">₹ " + meta.TotalPurchases.ToString("N2") + @"</div></div>
        <div class=""kpi-card""><div class=""kpi-title"">Invoices Recorded</div><div class=""kpi-val"">" + meta.TotalInvoicesCount + @"</div></div>
        <div class=""kpi-card""><div class=""kpi-title"">Active Wholesalers</div><div class=""kpi-val"">" + meta.TotalSuppliersCount + @"</div></div>
        <div class=""kpi-card""><div class=""kpi-title"">Total Outstanding Payables</div><div class=""kpi-val"" style=""color: #dc2626;"">₹ " + meta.TotalOutstandingPayables.ToString("N2") + @"</div></div>
    </div>

    <table>
        <thead>
            <tr>
                <th style=""width: 30px;"">#</th>
                <th>Wholesaler / Supplier</th>
                <th>Invoice No.</th>
                <th>Supplier GSTIN</th>
                <th>Date</th>
                <th class=""num"">Items</th>
                <th class=""num"">Taxable (₹)</th>
                <th class=""num"">CGST (₹)</th>
                <th class=""num"">SGST (₹)</th>
                <th class=""num"">IGST (₹)</th>
                <th class=""num"">Grand Total (₹)</th>
                <th style=""text-align: center;"">Status</th>
            </tr>
        </thead>
        <tbody>");

        foreach (var r in rows)
        {
            sb.Append($@"<tr>
                <td>{r.Index}</td>
                <td style=""font-weight: 600;"">{WebUtility.HtmlEncode(r.SupplierName)}</td>
                <td style=""font-family: monospace;"">{WebUtility.HtmlEncode(r.SupplierInvoiceNo)}</td>
                <td style=""color: #64748b;"">{WebUtility.HtmlEncode(r.SupplierGstin)}</td>
                <td>{r.SupplierInvoiceDate:dd-MM-yyyy}</td>
                <td class=""num"">{r.ItemCount}</td>
                <td class=""num"">{r.TaxableAmount:N2}</td>
                <td class=""num"">{r.CgstAmount:N2}</td>
                <td class=""num"">{r.SgstAmount:N2}</td>
                <td class=""num"">{r.IgstAmount:N2}</td>
                <td class=""num"" style=""font-weight: bold; color: #059669;"">{r.GrandTotal:N2}</td>
                <td style=""text-align: center;""><span class=""badge"">{WebUtility.HtmlEncode(r.Status)}</span></td>
            </tr>");
        }

        sb.Append($@"</tbody>
    </table>
</body>
</html>");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return Task.FromResult(filePath);
    }

    private static void AddZipEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    private static string GetDefaultExportPath(string prefix, string extension)
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloads))
        {
            downloads = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        return Path.Combine(downloads, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");
    }

    private static string GetColumnLetter(int colIndex)
    {
        string result = "";
        while (colIndex > 0)
        {
            int rem = (colIndex - 1) % 26;
            result = (char)('A' + rem) + result;
            colIndex = (colIndex - 1) / 26;
        }
        return result;
    }
}
