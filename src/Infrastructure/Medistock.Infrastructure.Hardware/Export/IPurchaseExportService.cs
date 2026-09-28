using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Medistock.Infrastructure.Hardware.Export;

public record PurchaseExportMetadata(
    string PharmacyName,
    string StoreAddress,
    string ContactPhone,
    string Gstin,
    DateTime ExportDate,
    decimal TotalPurchases,
    int TotalInvoicesCount,
    int TotalSuppliersCount,
    decimal TotalOutstandingPayables
);

public record PurchaseExportRow(
    int Index,
    string SupplierName,
    string SupplierInvoiceNo,
    string SupplierGstin,
    DateTime SupplierInvoiceDate,
    int ItemCount,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal GrandTotal,
    string Status,
    DateTime CreatedAt
);

public interface IPurchaseExportService
{
    Task<string> ExportToExcelAsync(IReadOnlyList<PurchaseExportRow> rows, PurchaseExportMetadata meta, string? targetFilePath = null);
    Task<string> ExportToWordAsync(IReadOnlyList<PurchaseExportRow> rows, PurchaseExportMetadata meta, string? targetFilePath = null);
    Task<string> ExportToPdfHtmlAsync(IReadOnlyList<PurchaseExportRow> rows, PurchaseExportMetadata meta, string? targetFilePath = null);
}
