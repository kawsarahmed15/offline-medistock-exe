using System;
using System.Collections.Generic;

namespace Medistock.Application.Compliance.DTOs;

public record GstPeriodFilter(
    string OrgId,
    string BranchId,
    int Year,
    int Month,
    DateTime FromDate,
    DateTime ToDate
);

public record Gstr1B2bEntryDto(
    string CustomerGstin,
    string CustomerName,
    string InvoiceNo,
    DateTime InvoiceDate,
    decimal InvoiceValue,
    string PlaceOfSupply,
    bool IsReverseCharge,
    decimal TaxableValue,
    decimal GstRate,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount
);

public record Gstr1B2cEntryDto(
    string PlaceOfSupply,
    decimal GstRate,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    int InvoiceCount
);

public record Gstr1CreditNoteEntryDto(
    string? CustomerGstin,
    string? CustomerName,
    string CreditNoteNo,
    DateTime NoteDate,
    string OriginalInvoiceNo,
    decimal NoteValue,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount
);

public record Gstr1HsnSummaryItemDto(
    string HsnCode,
    string Description,
    string Uqc,
    decimal TotalQuantity,
    decimal TotalValue,
    decimal TaxableValue,
    decimal GstRate,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount
);

public record Gstr1DocIssuedSummaryDto(
    string DocType,
    string FromSerialNo,
    string ToSerialNo,
    int TotalNumber,
    int CancelledNumber
);

public record Gstr1ReportDto(
    string OrgId,
    string BranchId,
    string Gstin,
    int Year,
    int Month,
    decimal TotalTaxableValue,
    decimal TotalCgst,
    decimal TotalSgst,
    decimal TotalIgst,
    decimal TotalInvoiceValue,
    List<Gstr1B2bEntryDto> B2bInvoices,
    List<Gstr1B2cEntryDto> B2cInvoices,
    List<Gstr1CreditNoteEntryDto> CreditNotes,
    List<Gstr1HsnSummaryItemDto> HsnSummary,
    List<Gstr1DocIssuedSummaryDto> DocumentsSummary
)
{
    public decimal TotalOutputGst => TotalCgst + TotalSgst + TotalIgst;
}

public record Gstr2B2bEntryDto(
    string SupplierGstin,
    string SupplierName,
    string InvoiceNo,
    DateTime InvoiceDate,
    decimal InvoiceValue,
    string PlaceOfSupply,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    string ItcEligibility
)
{
    public decimal TotalGstAmount => CgstAmount + SgstAmount + IgstAmount;
};

public record Gstr2ReportDto(
    string OrgId,
    string BranchId,
    string Gstin,
    int Year,
    int Month,
    decimal TotalTaxableValue,
    decimal TotalEligibleItc,
    decimal TotalCgstItc,
    decimal TotalSgstItc,
    decimal TotalIgstItc,
    List<Gstr2B2bEntryDto> InwardInvoices
);

public record Gstr3bOutwardTaxDto(
    decimal TotalTaxableValue,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess
);

public record Gstr3bItcDto(
    decimal IntegratedTaxItc,
    decimal CentralTaxItc,
    decimal StateTaxItc,
    decimal CessItc
);

public record Gstr3bReportDto(
    string OrgId,
    string BranchId,
    string Gstin,
    int Year,
    int Month,
    Gstr3bOutwardTaxDto OutwardSupplies,
    Gstr3bItcDto EligibleItc,
    decimal NetCgstPayable,
    decimal NetSgstPayable,
    decimal NetIgstPayable,
    decimal NetTotalPayable
);
