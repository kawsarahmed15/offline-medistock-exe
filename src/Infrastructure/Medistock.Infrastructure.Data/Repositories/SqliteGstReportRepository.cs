using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Compliance.DTOs;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteGstReportRepository : IGstReportRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteGstReportRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Gstr1ReportDto> GenerateGstr1Async(GstPeriodFilter filter, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var fromStr = filter.FromDate.ToString("o");
        var toStr = filter.ToDate.ToString("o");

        // 1. Fetch Outward Invoices and Items
        const string salesSql = @"
            SELECT 
                s.id AS SaleId, s.invoice_no AS InvoiceNo, s.invoice_date AS InvoiceDateStr,
                s.customer_id AS CustomerId, s.customer_name AS CustomerName,
                s.subtotal AS Subtotal, s.tax_amount AS TaxAmount, s.total AS Total,
                s.is_interstate AS IsInterstate,
                si.product_name AS ProductName, si.quantity AS Quantity,
                si.taxable_amount AS TaxableAmount,
                (si.cgst_rate + si.sgst_rate + si.igst_rate) AS GstRate,
                si.cgst_amount AS CgstAmount, si.sgst_amount AS SgstAmount, si.igst_amount AS IgstAmount,
                p.hsn_code AS HsnCode, p.base_unit AS BaseUnit
            FROM sales s
            JOIN sale_items si ON si.sale_id = s.id
            LEFT JOIN products p ON p.id = si.product_id
            WHERE s.org_id = @OrgId 
              AND s.branch_id = @BranchId
              AND s.invoice_date >= @fromStr 
              AND s.invoice_date <= @toStr
            ORDER BY s.invoice_date ASC;
        ";

        var saleRows = (await connection.QueryAsync<dynamic>(new CommandDefinition(
            salesSql,
            new { filter.OrgId, filter.BranchId, fromStr, toStr },
            cancellationToken: cancellationToken))).ToList();

        var b2bInvoices = new List<Gstr1B2bEntryDto>();
        var b2cInvoices = new List<Gstr1B2cEntryDto>();
        var hsnMap = new Dictionary<string, (string Desc, string Uqc, decimal Qty, decimal Taxable, decimal GstRate, decimal Cgst, decimal Sgst, decimal Igst, decimal Total)>();

        decimal grandTaxable = 0;
        decimal grandCgst = 0;
        decimal grandSgst = 0;
        decimal grandIgst = 0;
        decimal grandTotal = 0;

        foreach (var r in saleRows)
        {
            DateTime.TryParse((string)r.InvoiceDateStr, out DateTime invDate);
            var taxable = Convert.ToDecimal(r.TaxableAmount);
            var cgst = Convert.ToDecimal(r.CgstAmount);
            var sgst = Convert.ToDecimal(r.SgstAmount);
            var igst = Convert.ToDecimal(r.IgstAmount);
            var rate = Convert.ToDecimal(r.GstRate);
            var total = Convert.ToDecimal(r.Total);
            var qty = Convert.ToDecimal(r.Quantity);
            var isInterstate = ((int)r.IsInterstate) == 1;
            var pos = isInterstate ? "INTERSTATE" : "27-MAHARASHTRA";
            var hsn = (string?)r.HsnCode ?? "3004";
            var prodName = (string?)r.ProductName ?? "Pharmaceuticals";
            var uqc = (string?)r.BaseUnit ?? "TAB";

            grandTaxable += taxable;
            grandCgst += cgst;
            grandSgst += sgst;
            grandIgst += igst;

            // In our system, retail sales are categorized as B2C unless customer has designated business entity/name
            var customerName = (string?)r.CustomerName;
            var isB2b = !string.IsNullOrWhiteSpace(customerName) && customerName.Contains("Distributor", StringComparison.OrdinalIgnoreCase);

            if (isB2b)
            {
                b2bInvoices.Add(new Gstr1B2bEntryDto(
                    CustomerGstin: "27AAAAA0000A1Z5",
                    CustomerName: customerName ?? "B2B Buyer",
                    InvoiceNo: (string)r.InvoiceNo,
                    InvoiceDate: invDate,
                    InvoiceValue: total,
                    PlaceOfSupply: pos,
                    IsReverseCharge: false,
                    TaxableValue: taxable,
                    GstRate: rate,
                    CgstAmount: cgst,
                    SgstAmount: sgst,
                    IgstAmount: igst
                ));
            }
            else
            {
                b2cInvoices.Add(new Gstr1B2cEntryDto(
                    PlaceOfSupply: pos,
                    GstRate: rate,
                    TaxableValue: taxable,
                    CgstAmount: cgst,
                    SgstAmount: sgst,
                    IgstAmount: igst,
                    InvoiceCount: 1
                ));
            }

            // HSN Aggregation
            if (!hsnMap.ContainsKey(hsn))
            {
                hsnMap[hsn] = (prodName, uqc, qty, taxable, rate, cgst, sgst, igst, taxable + cgst + sgst + igst);
            }
            else
            {
                var cur = hsnMap[hsn];
                hsnMap[hsn] = (
                    cur.Desc,
                    cur.Uqc,
                    cur.Qty + qty,
                    cur.Taxable + taxable,
                    rate,
                    cur.Cgst + cgst,
                    cur.Sgst + sgst,
                    cur.Igst + igst,
                    cur.Total + taxable + cgst + sgst + igst
                );
            }
        }

        grandTotal = grandTaxable + grandCgst + grandSgst + grandIgst;

        // Group B2C entries by POS and Rate
        var groupedB2c = b2cInvoices
            .GroupBy(b => new { b.PlaceOfSupply, b.GstRate })
            .Select(g => new Gstr1B2cEntryDto(
                g.Key.PlaceOfSupply,
                g.Key.GstRate,
                g.Sum(x => x.TaxableValue),
                g.Sum(x => x.CgstAmount),
                g.Sum(x => x.SgstAmount),
                g.Sum(x => x.IgstAmount),
                g.Count()
            )).ToList();

        // 2. Fetch Credit Notes
        const string returnsSql = @"
            SELECT 
                sr.credit_note_no AS CreditNoteNo, sr.return_date AS NoteDateStr,
                sr.original_invoice_no AS OriginalInvoiceNo, sr.customer_name AS CustomerName,
                sr.subtotal AS Subtotal, sr.tax_amount AS TaxAmount, sr.total_amount AS TotalAmount,
                sri.cgst_amount AS CgstAmount, sri.sgst_amount AS SgstAmount, sri.igst_amount AS IgstAmount
            FROM sale_returns sr
            JOIN sale_return_items sri ON sri.sale_return_id = sr.id
            WHERE sr.org_id = @OrgId AND sr.branch_id = @BranchId
              AND sr.return_date >= @fromStr AND sr.return_date <= @toStr;
        ";

        var returnRows = (await connection.QueryAsync<dynamic>(new CommandDefinition(
            returnsSql,
            new { filter.OrgId, filter.BranchId, fromStr, toStr },
            cancellationToken: cancellationToken))).ToList();

        var creditNotes = new List<Gstr1CreditNoteEntryDto>();
        foreach (var r in returnRows)
        {
            DateTime.TryParse((string)r.NoteDateStr, out DateTime dt);
            creditNotes.Add(new Gstr1CreditNoteEntryDto(
                CustomerGstin: null,
                CustomerName: (string?)r.CustomerName,
                CreditNoteNo: (string)r.CreditNoteNo,
                NoteDate: dt,
                OriginalInvoiceNo: (string)r.OriginalInvoiceNo,
                NoteValue: Convert.ToDecimal(r.TotalAmount),
                TaxableValue: Convert.ToDecimal(r.Subtotal),
                CgstAmount: Convert.ToDecimal(r.CgstAmount),
                SgstAmount: Convert.ToDecimal(r.SgstAmount),
                IgstAmount: Convert.ToDecimal(r.IgstAmount)
            ));
        }

        var hsnSummaryList = hsnMap.Select(kvp => new Gstr1HsnSummaryItemDto(
            HsnCode: kvp.Key,
            Description: kvp.Value.Desc,
            Uqc: kvp.Value.Uqc,
            TotalQuantity: kvp.Value.Qty,
            TotalValue: kvp.Value.Total,
            TaxableValue: kvp.Value.Taxable,
            GstRate: kvp.Value.GstRate,
            CgstAmount: kvp.Value.Cgst,
            SgstAmount: kvp.Value.Sgst,
            IgstAmount: kvp.Value.Igst
        )).ToList();

        // 3. Document Summary
        const string docSql = @"
            SELECT MIN(invoice_no) AS MinInv, MAX(invoice_no) AS MaxInv, COUNT(1) AS Cnt
            FROM sales
            WHERE org_id = @OrgId AND branch_id = @BranchId
              AND invoice_date >= @fromStr AND invoice_date <= @toStr;
        ";
        var docInfo = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(
            docSql, new { filter.OrgId, filter.BranchId, fromStr, toStr }, cancellationToken: cancellationToken));

        var docList = new List<Gstr1DocIssuedSummaryDto>
        {
            new Gstr1DocIssuedSummaryDto(
                DocType: "Invoices for outward supply",
                FromSerialNo: (string?)docInfo?.MinInv ?? "INV-0001",
                ToSerialNo: (string?)docInfo?.MaxInv ?? "INV-0001",
                TotalNumber: docInfo != null ? (int)docInfo.Cnt : 0,
                CancelledNumber: 0
            )
        };

        return new Gstr1ReportDto(
            OrgId: filter.OrgId,
            BranchId: filter.BranchId,
            Gstin: "27AACCM1234F1Z1",
            Year: filter.Year,
            Month: filter.Month,
            TotalTaxableValue: grandTaxable,
            TotalCgst: grandCgst,
            TotalSgst: grandSgst,
            TotalIgst: grandIgst,
            TotalInvoiceValue: grandTotal,
            B2bInvoices: b2bInvoices,
            B2cInvoices: groupedB2c,
            CreditNotes: creditNotes,
            HsnSummary: hsnSummaryList,
            DocumentsSummary: docList
        );
    }

    public async Task<Gstr2ReportDto> GenerateGstr2Async(GstPeriodFilter filter, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var fromStr = filter.FromDate.ToString("o");
        var toStr = filter.ToDate.ToString("o");

        const string purSql = @"
            SELECT 
                pi.id AS InvoiceId, pi.supplier_id AS SupplierId, pi.supplier_name AS SupplierName,
                pi.supplier_gstin AS SupplierGstin, pi.supplier_invoice_no AS SupplierInvoiceNo,
                pi.supplier_invoice_date AS InvoiceDateStr, pi.is_interstate AS IsInterstate,
                pi.taxable_amount AS TaxableAmount, pi.cgst_amount AS CgstAmount,
                pi.sgst_amount AS SgstAmount, pi.igst_amount AS IgstAmount,
                pi.grand_total AS GrandTotal
            FROM purchase_invoices pi
            WHERE pi.org_id = @OrgId AND pi.branch_id = @BranchId
              AND pi.supplier_invoice_date >= @fromStr AND pi.supplier_invoice_date <= @toStr
            ORDER BY pi.supplier_invoice_date ASC;
        ";

        var rows = (await connection.QueryAsync<dynamic>(new CommandDefinition(
            purSql,
            new { filter.OrgId, filter.BranchId, fromStr, toStr },
            cancellationToken: cancellationToken))).ToList();

        var inwardList = new List<Gstr2B2bEntryDto>();
        decimal grandTaxable = 0;
        decimal grandCgst = 0;
        decimal grandSgst = 0;
        decimal grandIgst = 0;

        foreach (var r in rows)
        {
            DateTime.TryParse((string)r.InvoiceDateStr, out DateTime invDate);
            var taxable = Convert.ToDecimal(r.TaxableAmount);
            var cgst = Convert.ToDecimal(r.CgstAmount);
            var sgst = Convert.ToDecimal(r.SgstAmount);
            var igst = Convert.ToDecimal(r.IgstAmount);
            var total = Convert.ToDecimal(r.GrandTotal);
            var isInterstate = ((int)r.IsInterstate) == 1;

            grandTaxable += taxable;
            grandCgst += cgst;
            grandSgst += sgst;
            grandIgst += igst;

            inwardList.Add(new Gstr2B2bEntryDto(
                SupplierGstin: (string?)r.SupplierGstin ?? "27AAAPL1234K1Z2",
                SupplierName: (string)r.SupplierName,
                InvoiceNo: (string)r.SupplierInvoiceNo,
                InvoiceDate: invDate,
                InvoiceValue: total,
                PlaceOfSupply: isInterstate ? "INTERSTATE" : "27-MAHARASHTRA",
                TaxableValue: taxable,
                CgstAmount: cgst,
                SgstAmount: sgst,
                IgstAmount: igst,
                ItcEligibility: "Inputs"
            ));
        }

        var totalItc = grandCgst + grandSgst + grandIgst;

        return new Gstr2ReportDto(
            OrgId: filter.OrgId,
            BranchId: filter.BranchId,
            Gstin: "27AACCM1234F1Z1",
            Year: filter.Year,
            Month: filter.Month,
            TotalTaxableValue: grandTaxable,
            TotalEligibleItc: totalItc,
            TotalCgstItc: grandCgst,
            TotalSgstItc: grandSgst,
            TotalIgstItc: grandIgst,
            InwardInvoices: inwardList
        );
    }

    public async Task<Gstr3bReportDto> GenerateGstr3bAsync(GstPeriodFilter filter, CancellationToken cancellationToken = default)
    {
        var gstr1 = await GenerateGstr1Async(filter, cancellationToken);
        var gstr2 = await GenerateGstr2Async(filter, cancellationToken);

        // Net Outward Supplies (Gross sales minus Credit Notes)
        var creditTaxable = gstr1.CreditNotes.Sum(c => c.TaxableValue);
        var creditCgst = gstr1.CreditNotes.Sum(c => c.CgstAmount);
        var creditSgst = gstr1.CreditNotes.Sum(c => c.SgstAmount);
        var creditIgst = gstr1.CreditNotes.Sum(c => c.IgstAmount);

        var netTaxable = Math.Max(0, gstr1.TotalTaxableValue - creditTaxable);
        var outwardCgst = Math.Max(0, gstr1.TotalCgst - creditCgst);
        var outwardSgst = Math.Max(0, gstr1.TotalSgst - creditSgst);
        var outwardIgst = Math.Max(0, gstr1.TotalIgst - creditIgst);

        var outwardSupplies = new Gstr3bOutwardTaxDto(
            TotalTaxableValue: netTaxable,
            IntegratedTax: outwardIgst,
            CentralTax: outwardCgst,
            StateTax: outwardSgst,
            Cess: 0
        );

        var eligibleItc = new Gstr3bItcDto(
            IntegratedTaxItc: gstr2.TotalIgstItc,
            CentralTaxItc: gstr2.TotalCgstItc,
            StateTaxItc: gstr2.TotalSgstItc,
            CessItc: 0
        );

        // Net Tax payable in cash = Outward Tax - Available ITC
        var netCgstPayable = Math.Max(0, outwardCgst - eligibleItc.CentralTaxItc);
        var netSgstPayable = Math.Max(0, outwardSgst - eligibleItc.StateTaxItc);
        var netIgstPayable = Math.Max(0, outwardIgst - eligibleItc.IntegratedTaxItc);
        var netTotal = netCgstPayable + netSgstPayable + netIgstPayable;

        return new Gstr3bReportDto(
            OrgId: filter.OrgId,
            BranchId: filter.BranchId,
            Gstin: gstr1.Gstin,
            Year: filter.Year,
            Month: filter.Month,
            OutwardSupplies: outwardSupplies,
            EligibleItc: eligibleItc,
            NetCgstPayable: netCgstPayable,
            NetSgstPayable: netSgstPayable,
            NetIgstPayable: netIgstPayable,
            NetTotalPayable: netTotal
        );
    }
}
