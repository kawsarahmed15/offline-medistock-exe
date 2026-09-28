using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Medistock.Infrastructure.Hardware.Export;

public class InventoryExportService : IInventoryExportService
{
    public Task<string> ExportToExcelAsync(IReadOnlyList<InventoryExportRow> rows, InventoryExportMetadata meta, string? targetFilePath = null)
    {
        var filePath = targetFilePath ?? GetDefaultExportPath("Inventory_Stock_Master", "xlsx");
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
        <sheet name=""Stock Master"" sheetId=""1"" r:id=""rId1""/>
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
    <fills count=""5"">
        <fill><patternFill patternType=""none""/></fill>
        <fill><patternFill patternType=""gray125""/></fill>
        <fill><patternFill patternType=""solid""><fgColor rgb=""FF0284C7""/></patternFill></fill>
        <fill><patternFill patternType=""solid""><fgColor rgb=""FFFEE2E2""/></patternFill></fill>
        <fill><patternFill patternType=""solid""><fgColor rgb=""FFFEF3C7""/></patternFill></fill>
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
    <cellXfs count=""6"">
        <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
        <xf numFmtId=""0"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0""/> <!-- Title -->
        <xf numFmtId=""0"" fontId=""2"" fillId=""2"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1""/> <!-- Header -->
        <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""1"" xfId=""0"" applyBorder=""1""/> <!-- Normal row -->
        <xf numFmtId=""0"" fontId=""0"" fillId=""3"" borderId=""1"" xfId=""0"" applyFill=""1"" applyBorder=""1""/> <!-- Expired/Near Exp row -->
        <xf numFmtId=""0"" fontId=""0"" fillId=""4"" borderId=""1"" xfId=""0"" applyFill=""1"" applyBorder=""1""/> <!-- Low Stock row -->
    </cellXfs>
</styleSheet>");

        // Collect shared strings
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

        // Build Sheet XML
        var sb = new StringBuilder();
        sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
    <cols>
        <col min=""1"" max=""1"" width=""6"" customWidth=""1""/>
        <col min=""2"" max=""2"" width=""28"" customWidth=""1""/>
        <col min=""3"" max=""3"" width=""24"" customWidth=""1""/>
        <col min=""4"" max=""4"" width=""14"" customWidth=""1""/>
        <col min=""5"" max=""5"" width=""14"" customWidth=""1""/>
        <col min=""6"" max=""6"" width=""12"" customWidth=""1""/>
        <col min=""7"" max=""7"" width=""12"" customWidth=""1""/>
        <col min=""8"" max=""8"" width=""14"" customWidth=""1""/>
        <col min=""9"" max=""9"" width=""14"" customWidth=""1""/>
        <col min=""10"" max=""10"" width=""14"" customWidth=""1""/>
        <col min=""11"" max=""11"" width=""14"" customWidth=""1""/>
        <col min=""12"" max=""12"" width=""16"" customWidth=""1""/>
        <col min=""13"" max=""13"" width=""18"" customWidth=""1""/>
    </cols>
    <sheetData>");

        int r = 1;
        // Title row
        sb.Append($@"<row r=""{r}"">");
        sb.Append($@"<c r=""A{r}"" s=""1"" t=""s""><v>{GetStringId(meta.PharmacyName.ToUpperInvariant() + " - INVENTORY & STOCK MASTER")}</v></c>");
        sb.Append("</row>");
        r++;

        // Metadata row
        sb.Append($@"<row r=""{r}"">");
        sb.Append($@"<c r=""A{r}"" t=""s""><v>{GetStringId($"GSTIN: {meta.Gstin} | Phone: {meta.ContactPhone} | Generated: {meta.ExportDate:dd-MMM-yyyy HH:mm}")}</v></c>");
        sb.Append("</row>");
        r++;

        // Summary row
        sb.Append($@"<row r=""{r}"">");
        sb.Append($@"<c r=""A{r}"" t=""s""><v>{GetStringId($"Total Products: {meta.TotalProducts} | Batches: {meta.TotalBatches} | Near Expiry: {meta.NearExpiryCount} | Low Stock: {meta.LowStockCount} | Total Valuation (Cost): ₹{meta.TotalStockValueCost:N2} | Valuation (MRP): ₹{meta.TotalStockValueMrp:N2}")}</v></c>");
        sb.Append("</row>");
        r += 2; // Blank row

        // Column Headers
        string[] headers = { "#", "Product Name", "Generic / Salt", "Batch No", "Expiry", "Days Left", "Schedule", "Avail Qty", "Min Alert", "MRP (₹)", "Cost (₹)", "Stock Value (₹)", "Status" };
        sb.Append($@"<row r=""{r}"">");
        for (int c = 0; c < headers.Length; c++)
        {
            string colLetter = GetColumnLetter(c + 1);
            sb.Append($@"<c r=""{colLetter}{r}"" s=""2"" t=""s""><v>{GetStringId(headers[c])}</v></c>");
        }
        sb.Append("</row>");
        r++;

        // Data Rows
        foreach (var item in rows)
        {
            int styleId = (item.IsExpired || item.IsNearExpiry) ? 4 : (item.IsLowStock || item.IsOutOfStock ? 5 : 3);

            sb.Append($@"<row r=""{r}"">");
            sb.Append($@"<c r=""A{r}"" s=""{styleId}""><v>{item.Index}</v></c>");
            sb.Append($@"<c r=""B{r}"" s=""{styleId}"" t=""s""><v>{GetStringId(item.ProductName)}</v></c>");
            sb.Append($@"<c r=""C{r}"" s=""{styleId}"" t=""s""><v>{GetStringId(item.GenericName)}</v></c>");
            sb.Append($@"<c r=""D{r}"" s=""{styleId}"" t=""s""><v>{GetStringId(item.BatchNumber)}</v></c>");
            sb.Append($@"<c r=""E{r}"" s=""{styleId}"" t=""s""><v>{GetStringId(item.ExpiryDate.ToString("MM/yyyy"))}</v></c>");
            sb.Append($@"<c r=""F{r}"" s=""{styleId}""><v>{item.DaysUntilExpiry}</v></c>");
            sb.Append($@"<c r=""G{r}"" s=""{styleId}"" t=""s""><v>{GetStringId(item.Schedule)}</v></c>");
            sb.Append($@"<c r=""H{r}"" s=""{styleId}""><v>{item.AvailableQuantity}</v></c>");
            sb.Append($@"<c r=""I{r}"" s=""{styleId}""><v>{item.MinStockAlert}</v></c>");
            sb.Append($@"<c r=""J{r}"" s=""{styleId}""><v>{item.Mrp}</v></c>");
            sb.Append($@"<c r=""K{r}"" s=""{styleId}""><v>{item.PurchaseRate}</v></c>");
            sb.Append($@"<c r=""L{r}"" s=""{styleId}""><v>{item.StockValueAtCost}</v></c>");
            sb.Append($@"<c r=""M{r}"" s=""{styleId}"" t=""s""><v>{GetStringId(item.StatusText)}</v></c>");
            sb.Append("</row>");
            r++;
        }

        sb.Append("</sheetData></worksheet>");
        AddZipEntry(zip, "xl/worksheets/sheet1.xml", sb.ToString());

        // 6. xl/sharedStrings.xml
        var ss = new StringBuilder();
        ss.Append($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""{sharedStrings.Count}"" uniqueCount=""{sharedStrings.Count}"">");
        foreach (var str in sharedStrings)
        {
            ss.Append($@"<si><t>{WebUtility.HtmlEncode(str)}</t></si>");
        }
        ss.Append("</sst>");
        AddZipEntry(zip, "xl/sharedStrings.xml", ss.ToString());

        return Task.FromResult(filePath);
    }

    public Task<string> ExportToWordAsync(IReadOnlyList<InventoryExportRow> rows, InventoryExportMetadata meta, string? targetFilePath = null)
    {
        var filePath = targetFilePath ?? GetDefaultExportPath("Inventory_Stock_Master", "docx");
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
    <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
    <Override PartName=""/word/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml""/>
</Types>");

        // 2. _rels/.rels
        AddZipEntry(zip, "_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
</Relationships>");

        // 3. word/_rels/document.xml.rels
        AddZipEntry(zip, "word/_rels/document.xml.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>");

        // 4. word/styles.xml
        AddZipEntry(zip, "word/styles.xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:styles xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
    <w:docDefaults>
        <w:rPrDefault>
            <w:rPr>
                <w:rFonts w:ascii=""Calibri"" w:hAnsi=""Calibri""/>
                <w:sz w:val=""22""/>
            </w:rPr>
        </w:rPrDefault>
    </w:docDefaults>
</w:styles>");

        // 5. word/document.xml
        var doc = new StringBuilder();
        doc.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
<w:body>");

        // Title
        doc.Append($@"<w:p>
            <w:pPr><w:jc w:val=""center""/></w:pPr>
            <w:r><w:rPr><w:b/><w:sz w:val=""32""/><w:color w:val=""0284C7""/></w:rPr><w:t>{WebUtility.HtmlEncode(meta.PharmacyName.ToUpperInvariant())}</w:t></w:r>
        </w:p>");

        // Subtitle & Header
        doc.Append($@"<w:p>
            <w:pPr><w:jc w:val=""center""/></w:pPr>
            <w:r><w:rPr><w:b/><w:sz w:val=""24""/><w:color w:val=""1E293B""/></w:rPr><w:t>INVENTORY &amp; STOCK MASTER REPORT</w:t></w:r>
        </w:p>");

        doc.Append($@"<w:p>
            <w:pPr><w:jc w:val=""center""/></w:pPr>
            <w:r><w:rPr><w:sz w:val=""18""/><w:color w:val=""64748B""/></w:rPr><w:t>{WebUtility.HtmlEncode(meta.StoreAddress)} | GSTIN: {WebUtility.HtmlEncode(meta.Gstin)} | Phone: {WebUtility.HtmlEncode(meta.ContactPhone)}</w:t></w:r>
        </w:p>");

        doc.Append($@"<w:p>
            <w:pPr><w:jc w:val=""center""/></w:pPr>
            <w:r><w:rPr><w:sz w:val=""18""/><w:color w:val=""64748B""/></w:rPr><w:t>Generated On: {meta.ExportDate:dd-MMM-yyyy hh:mm tt}</w:t></w:r>
        </w:p>");

        // KPI Summary Box
        doc.Append($@"<w:p>
            <w:pPr><w:pBdr><w:bottom w:val=""single"" w:sz=""12"" w:space=""4"" w:color=""0284C7""/></w:pBdr></w:pPr>
            <w:r><w:rPr><w:b/><w:sz w:val=""20""/><w:color w:val=""0284C7""/></w:rPr><w:t>EXECUTIVE INVENTORY SUMMARY</w:t></w:r>
        </w:p>");

        doc.Append($@"<w:p>
            <w:r><w:rPr><w:b/></w:rPr><w:t>• Total Available Products: </w:t></w:r>
            <w:r><w:t>{meta.TotalProducts} items ({meta.TotalBatches} total active batches)</w:t></w:r>
        </w:p>");
        doc.Append($@"<w:p>
            <w:r><w:rPr><w:b/><w:color w:val=""DC2626""/></w:rPr><w:t>• Near-Expiry / Expired Items: </w:t></w:r>
            <w:r><w:t>{meta.NearExpiryCount} batches</w:t></w:r>
        </w:p>");
        doc.Append($@"<w:p>
            <w:r><w:rPr><w:b/><w:color w:val=""D97706""/></w:rPr><w:t>• Low Stock / Out of Stock Items: </w:t></w:r>
            <w:r><w:t>{meta.LowStockCount} batches</w:t></w:r>
        </w:p>");
        doc.Append($@"<w:p>
            <w:r><w:rPr><w:b/><w:color w:val=""16A34A""/></w:rPr><w:t>• Total Inventory Valuation: </w:t></w:r>
            <w:r><w:t>₹{meta.TotalStockValueCost:N2} (Cost) | ₹{meta.TotalStockValueMrp:N2} (MRP)</w:t></w:r>
        </w:p>");

        doc.Append("<w:p/>"); // Space

        // Stock Master Table
        doc.Append(@"<w:tbl>
            <w:tblPr>
                <w:tblW w:w=""0"" w:type=""auto""/>
                <w:tblBorders>
                    <w:top w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""CBD5E1""/>
                    <w:bottom w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""CBD5E1""/>
                    <w:insideH w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""E2E8F0""/>
                    <w:insideV w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""E2E8F0""/>
                </w:tblBorders>
            </w:tblPr>");

        // Table Header
        string[] ths = { "#", "Product Name", "Batch", "Expiry", "Sch", "Qty", "Alert", "MRP", "Cost", "Value (₹)", "Status" };
        doc.Append("<w:tr><w:trPr><w:tblHeader/></w:trPr>");
        foreach (var th in ths)
        {
            doc.Append($@"<w:tc>
                <w:tcPr><w:shd w:val=""clear"" w:color=""auto"" w:fill=""0284C7""/><w:vAlign w:val=""center""/></w:tcPr>
                <w:p><w:pPr><w:jc w:val=""center""/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val=""18""/><w:color w:val=""FFFFFF""/></w:rPr><w:t>{th}</w:t></w:r></w:p>
            </w:tc>");
        }
        doc.Append("</w:tr>");

        // Table Rows
        foreach (var item in rows)
        {
            string rowFill = (item.IsExpired || item.IsNearExpiry) ? "FEE2E2" : (item.IsLowStock || item.IsOutOfStock ? "FEF3C7" : "FFFFFF");
            string statusColor = (item.IsExpired || item.IsNearExpiry) ? "DC2626" : (item.IsLowStock || item.IsOutOfStock ? "D97706" : "16A34A");

            doc.Append("<w:tr>");
            doc.Append(BuildWordCell(item.Index.ToString(), rowFill, "center"));
            doc.Append(BuildWordCell(item.ProductName, rowFill, "left", bold: true));
            doc.Append(BuildWordCell(item.BatchNumber, rowFill, "center"));
            doc.Append(BuildWordCell(item.ExpiryDate.ToString("MM/yy"), rowFill, "center"));
            doc.Append(BuildWordCell(item.Schedule, rowFill, "center"));
            doc.Append(BuildWordCell(item.AvailableQuantity.ToString("0.#"), rowFill, "right", bold: true));
            doc.Append(BuildWordCell(item.MinStockAlert.ToString("0.#"), rowFill, "right"));
            doc.Append(BuildWordCell(item.Mrp.ToString("F2"), rowFill, "right"));
            doc.Append(BuildWordCell(item.PurchaseRate.ToString("F2"), rowFill, "right"));
            doc.Append(BuildWordCell(item.StockValueAtCost.ToString("F2"), rowFill, "right", bold: true));
            doc.Append(BuildWordCell(item.StatusText, rowFill, "center", bold: true, colorHex: statusColor));
            doc.Append("</w:tr>");
        }

        doc.Append(@"</w:tbl>");
        doc.Append(@"<w:sectPr><w:pgSz w:w=""16838"" w:h=""11906"" w:orient=""landscape""/></w:sectPr>"); // Landscape A4
        doc.Append(@"</w:body></w:document>");

        AddZipEntry(zip, "word/document.xml", doc.ToString());

        return Task.FromResult(filePath);
    }

    public Task<string> ExportToPdfHtmlAsync(IReadOnlyList<InventoryExportRow> rows, InventoryExportMetadata meta, string? targetFilePath = null)
    {
        var filePath = targetFilePath ?? GetDefaultExportPath("Inventory_Stock_Master_Report", "html");
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var sb = new StringBuilder();
        sb.Append($@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Inventory &amp; Stock Master Report - {WebUtility.HtmlEncode(meta.PharmacyName)}</title>
    <style>
        @page {{
            size: A4 landscape;
            margin: 10mm;
        }}
        body {{
            font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif;
            color: #1e293b;
            background: #ffffff;
            margin: 0;
            padding: 12px;
            font-size: 11px;
        }}
        .header {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 2px solid #0284c7;
            padding-bottom: 8px;
            margin-bottom: 12px;
        }}
        .header h1 {{
            margin: 0 0 4px 0;
            font-size: 20px;
            color: #0284c7;
        }}
        .header p {{
            margin: 0;
            color: #64748b;
            font-size: 11px;
        }}
        .kpi-container {{
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 10px;
            margin-bottom: 14px;
        }}
        .kpi-card {{
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
            padding: 8px 12px;
        }}
        .kpi-title {{
            font-size: 10px;
            font-weight: 700;
            color: #64748b;
            text-transform: uppercase;
        }}
        .kpi-value {{
            font-size: 16px;
            font-weight: 800;
            color: #0f172a;
            margin-top: 2px;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
            font-size: 10.5px;
        }}
        th {{
            background: #0284c7;
            color: #ffffff;
            text-align: left;
            padding: 6px 8px;
            font-weight: 600;
            border: 1px solid #0284c7;
        }}
        th.right {{ text-align: right; }}
        th.center {{ text-align: center; }}
        td {{
            padding: 5px 8px;
            border-bottom: 1px solid #e2e8f0;
            vertical-align: middle;
        }}
        td.right {{ text-align: right; }}
        td.center {{ text-align: center; }}
        tr.expired {{ background-color: #fee2e2 !important; }}
        tr.near-expiry {{ background-color: #fef2f2 !important; }}
        tr.low-stock {{ background-color: #fef3c7 !important; }}
        .badge {{
            display: inline-block;
            padding: 2px 6px;
            border-radius: 4px;
            font-size: 9.5px;
            font-weight: 700;
        }}
        .badge-danger {{ background: #dc2626; color: white; }}
        .badge-warning {{ background: #d97706; color: white; }}
        .badge-success {{ background: #16a34a; color: white; }}
        .footer {{
            margin-top: 14px;
            display: flex;
            justify-content: space-between;
            color: #94a3b8;
            font-size: 9.5px;
        }}
        @media print {{
            body {{ padding: 0; }}
            .no-print {{ display: none; }}
        }}
    </style>
</head>
<body>
    <div class=""header"">
        <div>
            <h1>{WebUtility.HtmlEncode(meta.PharmacyName)}</h1>
            <p>{WebUtility.HtmlEncode(meta.StoreAddress)} | GSTIN: <b>{WebUtility.HtmlEncode(meta.Gstin)}</b> | Tel: {WebUtility.HtmlEncode(meta.ContactPhone)}</p>
        </div>
        <div style=""text-align: right;"">
            <h2 style=""margin:0; font-size:14px; color:#1e293b;"">INVENTORY &amp; STOCK MASTER REPORT</h2>
            <p>Generated: {meta.ExportDate:dd-MMM-yyyy HH:mm}</p>
        </div>
    </div>

    <div class=""kpi-container"">
        <div class=""kpi-card"">
            <div class=""kpi-title"">Total Products Available</div>
            <div class=""kpi-value"">{meta.TotalProducts} items <span style=""font-size:11px; font-weight:normal; color:#64748b;"">({meta.TotalBatches} batches)</span></div>
        </div>
        <div class=""kpi-card"" style=""border-left: 3px solid #dc2626;"">
            <div class=""kpi-title"">Near Expiry / Expired</div>
            <div class=""kpi-value"" style=""color: #dc2626;"">{meta.NearExpiryCount} batches</div>
        </div>
        <div class=""kpi-card"" style=""border-left: 3px solid #d97706;"">
            <div class=""kpi-title"">Low Stock / OOS</div>
            <div class=""kpi-value"" style=""color: #d97706;"">{meta.LowStockCount} batches</div>
        </div>
        <div class=""kpi-card"" style=""border-left: 3px solid #16a34a;"">
            <div class=""kpi-title"">Total Stock Valuation</div>
            <div class=""kpi-value"" style=""color: #16a34a;"">₹{meta.TotalStockValueCost:N2} <span style=""font-size:10px; font-weight:normal; color:#64748b;"">(MRP: ₹{meta.TotalStockValueMrp:N2})</span></div>
        </div>
    </div>

    <table>
        <thead>
            <tr>
                <th class=""center"">#</th>
                <th>Product Name &amp; Composition</th>
                <th class=""center"">Batch No</th>
                <th class=""center"">Expiry</th>
                <th class=""center"">Schedule</th>
                <th class=""right"">Avail Qty</th>
                <th class=""right"">Alert</th>
                <th class=""right"">MRP (₹)</th>
                <th class=""right"">Cost (₹)</th>
                <th class=""right"">Value (₹)</th>
                <th class=""center"">Status</th>
            </tr>
        </thead>
        <tbody>");

        foreach (var r in rows)
        {
            string rowClass = r.IsExpired ? "expired" : (r.IsNearExpiry ? "near-expiry" : (r.IsLowStock || r.IsOutOfStock ? "low-stock" : ""));
            string badgeClass = (r.IsExpired || r.IsNearExpiry) ? "badge-danger" : (r.IsLowStock || r.IsOutOfStock ? "badge-warning" : "badge-success");

            sb.Append($@"
            <tr class=""{rowClass}"">
                <td class=""center"">{r.Index}</td>
                <td><b>{WebUtility.HtmlEncode(r.ProductName)}</b><br><small style=""color:#64748b;"">{WebUtility.HtmlEncode(r.GenericName)}</small></td>
                <td class=""center"" style=""font-family:monospace; font-weight:bold;"">{WebUtility.HtmlEncode(r.BatchNumber)}</td>
                <td class=""center"">{r.ExpiryDate:MM/yyyy}</td>
                <td class=""center"">{WebUtility.HtmlEncode(r.Schedule)}</td>
                <td class=""right"" style=""font-weight:bold;"">{r.AvailableQuantity:0.#}</td>
                <td class=""right"">{r.MinStockAlert:0.#}</td>
                <td class=""right"">{r.Mrp:F2}</td>
                <td class=""right"">{r.PurchaseRate:F2}</td>
                <td class=""right"" style=""font-weight:bold;"">{r.StockValueAtCost:F2}</td>
                <td class=""center""><span class=""badge {badgeClass}"">{WebUtility.HtmlEncode(r.StatusText)}</span></td>
            </tr>");
        }

        sb.Append($@"
        </tbody>
    </table>

    <div class=""footer"">
        <span>MEDISTOCK OFFLINE-FIRST PHARMACY ERP</span>
        <span>CONFIDENTIAL &amp; PROPRIETARY INVENTORY RECORD</span>
        <span>Page 1 of 1</span>
    </div>
</body>
</html>");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return Task.FromResult(filePath);
    }

    private static string BuildWordCell(string text, string fillHex, string align, bool bold = false, string? colorHex = null)
    {
        string boldTag = bold ? "<w:b/>" : "";
        string colorTag = !string.IsNullOrEmpty(colorHex) ? $@"<w:color w:val=""{colorHex.TrimStart('#')}"" />" : "";
        return $@"<w:tc>
            <w:tcPr><w:shd w:val=""clear"" w:color=""auto"" w:fill=""{fillHex}""/><w:vAlign w:val=""center""/></w:tcPr>
            <w:p><w:pPr><w:jc w:val=""{align}""/></w:pPr><w:r><w:rPr>{boldTag}{colorTag}<w:sz w:val=""18""/></w:rPr><w:t>{WebUtility.HtmlEncode(text)}</w:t></w:r></w:p>
        </w:tc>";
    }

    private static void AddZipEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    private static string GetColumnLetter(int colNumber)
    {
        string result = string.Empty;
        while (colNumber > 0)
        {
            int mod = (colNumber - 1) % 26;
            result = (char)('A' + mod) + result;
            colNumber = (colNumber - mod) / 26;
        }
        return result;
    }

    private static string GetDefaultExportPath(string prefix, string extension)
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dir = Path.Combine(docs, "Medistock", "Reports");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");
    }
}
