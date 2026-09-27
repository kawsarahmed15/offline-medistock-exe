using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Compliance.DTOs;

namespace Medistock.Application.Compliance.Services;

public interface IGstReportService
{
    Task<Gstr1ReportDto> GenerateGstr1Async(GstPeriodFilter filter, CancellationToken cancellationToken = default);
    Task<Gstr2ReportDto> GenerateGstr2Async(GstPeriodFilter filter, CancellationToken cancellationToken = default);
    Task<Gstr3bReportDto> GenerateGstr3bAsync(GstPeriodFilter filter, CancellationToken cancellationToken = default);
    Task<string> ExportGstr1JsonAsync(GstPeriodFilter filter, CancellationToken cancellationToken = default);
}

public class GstReportService : IGstReportService
{
    private readonly IGstReportRepository _gstRepository;

    public GstReportService(IGstReportRepository gstRepository)
    {
        _gstRepository = gstRepository;
    }

    public async Task<Gstr1ReportDto> GenerateGstr1Async(GstPeriodFilter filter, CancellationToken cancellationToken = default)
    {
        return await _gstRepository.GenerateGstr1Async(filter, cancellationToken);
    }

    public async Task<Gstr2ReportDto> GenerateGstr2Async(GstPeriodFilter filter, CancellationToken cancellationToken = default)
    {
        return await _gstRepository.GenerateGstr2Async(filter, cancellationToken);
    }

    public async Task<Gstr3bReportDto> GenerateGstr3bAsync(GstPeriodFilter filter, CancellationToken cancellationToken = default)
    {
        return await _gstRepository.GenerateGstr3bAsync(filter, cancellationToken);
    }

    public async Task<string> ExportGstr1JsonAsync(GstPeriodFilter filter, CancellationToken cancellationToken = default)
    {
        var gstr1 = await _gstRepository.GenerateGstr1Async(filter, cancellationToken);

        // Group B2B by Customer GSTIN according to GSTN schema
        var b2bSection = gstr1.B2bInvoices
            .GroupBy(i => i.CustomerGstin)
            .Select(g => new
            {
                ctin = g.Key,
                inv = g.Select(inv => new
                {
                    inum = inv.InvoiceNo,
                    idt = inv.InvoiceDate.ToString("dd-MM-yyyy"),
                    val = (double)inv.InvoiceValue,
                    pos = inv.PlaceOfSupply,
                    rchrg = inv.IsReverseCharge ? "Y" : "N",
                    inv_typ = "R",
                    itms = new[]
                    {
                        new
                        {
                            num = 1,
                            itm_det = new
                            {
                                rt = (double)inv.GstRate,
                                txval = (double)inv.TaxableValue,
                                iamt = (double)inv.IgstAmount,
                                camt = (double)inv.CgstAmount,
                                samt = (double)inv.SgstAmount,
                                csamt = 0.0
                            }
                        }
                    }
                }).ToArray()
            }).ToArray();

        // B2C Small Section
        var b2csSection = gstr1.B2cInvoices.Select(b => new
        {
            sply_ty = "INTRA",
            pos = b.PlaceOfSupply,
            typ = "OE",
            rt = (double)b.GstRate,
            txval = (double)b.TaxableValue,
            iamt = (double)b.IgstAmount,
            camt = (double)b.CgstAmount,
            samt = (double)b.SgstAmount,
            csamt = 0.0
        }).ToArray();

        // HSN Summary Section
        var hsnSection = new
        {
            data = gstr1.HsnSummary.Select((h, index) => new
            {
                num = index + 1,
                hsn_sc = h.HsnCode,
                desc = h.Description,
                uqc = h.Uqc,
                qty = (double)h.TotalQuantity,
                val = (double)h.TotalValue,
                txval = (double)h.TaxableValue,
                iamt = (double)h.IgstAmount,
                camt = (double)h.CgstAmount,
                samt = (double)h.SgstAmount,
                csamt = 0.0
            }).ToArray()
        };

        // Document Issue Section
        var docIssueSection = new
        {
            doc_det = gstr1.DocumentsSummary.Select((d, idx) => new
            {
                doc_num = idx + 1,
                doc_typ = d.DocType,
                docs = new[]
                {
                    new
                    {
                        num = 1,
                        from = d.FromSerialNo,
                        to = d.ToSerialNo,
                        totnum = d.TotalNumber,
                        canc = d.CancelledNumber,
                        net_issue = d.TotalNumber - d.CancelledNumber
                    }
                }
            }).ToArray()
        };

        var gstnPayload = new
        {
            gstin = gstr1.Gstin,
            fp = $"{gstr1.Month:D2}{gstr1.Year}",
            gt = (double)gstr1.TotalInvoiceValue,
            cur_gt = (double)gstr1.TotalInvoiceValue,
            b2b = b2bSection,
            b2cs = b2csSection,
            hsn = hsnSection,
            doc_issue = docIssueSection
        };

        return JsonSerializer.Serialize(gstnPayload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
