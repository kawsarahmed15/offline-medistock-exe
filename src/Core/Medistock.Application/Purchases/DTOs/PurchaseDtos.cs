using System;
using System.Collections.Generic;
using Medistock.Domain.Purchases;

namespace Medistock.Application.Purchases.DTOs;

public record SupplierDto(
    string Id,
    string Name,
    string? Gstin,
    string? DlNumber,
    string? Phone,
    string? Email,
    string? Address,
    int CreditDays,
    decimal CurrentOutstandingBalance,
    bool IsActive,
    string? PurchaseNo = null,
    string? InvoiceNo = null
)
{
    public string FormattedPurchaseNo => string.IsNullOrWhiteSpace(PurchaseNo) ? "—" : PurchaseNo;
    public string FormattedInvoiceNo => string.IsNullOrWhiteSpace(InvoiceNo) ? "—" : InvoiceNo;

    // Backwards-compatible aliases
    public string? PurchaseBillNo => InvoiceNo ?? PurchaseNo;
    public string FormattedPurchaseBillNo => FormattedInvoiceNo != "—" ? FormattedInvoiceNo : FormattedPurchaseNo;
}

public record CreateSupplierCommand(
    string OrgId,
    string Name,
    string? Gstin,
    string? DlNumber,
    string? Phone,
    string? Email,
    string? Address,
    int CreditDays,
    decimal OpeningBalance
);

public record PurchaseInvoiceItemInputDto(
    string ProductId,
    string ProductName,
    string HsnCode,
    string BatchNumber,
    DateTime ExpiryDate,
    DateTime? ManufacturingDate,
    decimal Quantity,
    decimal FreeQuantity,
    decimal UnitPrice,
    decimal Mrp,
    decimal SaleRate,
    decimal DiscountPct,
    decimal GstRatePercent
);

public record CreatePurchaseInvoiceCommand(
    string OrgId,
    string BranchId,
    string WarehouseId,
    string SupplierId,
    string SupplierName,
    string? SupplierGstin,
    string SupplierInvoiceNo,
    DateTime SupplierInvoiceDate,
    bool IsInterstate,
    string CreatedByUserId,
    string? Notes,
    List<PurchaseInvoiceItemInputDto> Items
);

public record PurchasePostingResult(
    bool Success,
    string? PurchaseInvoiceId,
    int BatchesCreatedOrUpdated,
    decimal TotalStockAdded,
    decimal GrandTotal,
    string? ErrorMessage = null
);

public record PurchaseInvoiceSummaryDto(
    string Id,
    string SupplierName,
    string? SupplierGstin,
    string SupplierInvoiceNo,
    DateTime SupplierInvoiceDate,
    PurchaseInvoiceStatus Status,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal GrandTotal,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? PostedAt,
    string? PurchaseNo = null
)
{
    public string FormattedPurchaseNo => string.IsNullOrWhiteSpace(PurchaseNo) ? "—" : PurchaseNo;
}

public record PurchaseInvoiceItemDto(
    string Id,
    string ProductId,
    string ProductName,
    string HsnCode,
    string BatchNumber,
    DateTime ExpiryDate,
    DateTime? ManufacturingDate,
    decimal Quantity,
    decimal FreeQuantity,
    decimal TotalQuantity,
    decimal UnitPrice,
    decimal Mrp,
    decimal SaleRate,
    decimal DiscountPct,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal GstRatePercent,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal NetAmount,
    decimal LandedCostPerUnit
);

public record PurchaseInvoiceDetailsDto(
    string Id,
    string OrgId,
    string BranchId,
    string WarehouseId,
    string SupplierId,
    string SupplierName,
    string? SupplierGstin,
    string SupplierInvoiceNo,
    DateTime SupplierInvoiceDate,
    PurchaseInvoiceStatus Status,
    bool IsInterstate,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal RoundOff,
    decimal GrandTotal,
    string? Notes,
    string CreatedByUserId,
    DateTime CreatedAt,
    DateTime? PostedAt,
    IReadOnlyList<PurchaseInvoiceItemDto> Items
);

public record PurchaseKpiSummaryDto(
    decimal TotalPurchaseAmount,
    int TotalInvoicesCount,
    int TotalSuppliersCount,
    decimal TotalOutstandingPayable,
    decimal TotalStockValue = 0m
);

public record PurchaseCancelResult(
    bool Success,
    string? ErrorMessage = null
);

public record UpdatePurchaseInvoiceCommand(
    string InvoiceId,
    string OrgId,
    string BranchId,
    string WarehouseId,
    string SupplierId,
    string SupplierName,
    string? SupplierGstin,
    string SupplierInvoiceNo,
    DateTime SupplierInvoiceDate,
    bool IsInterstate,
    string UpdatedByUserId,
    string? Notes,
    List<PurchaseInvoiceItemInputDto> Items
);

public record PurchaseReturnItemInputDto(
    string ProductId,
    string ProductName,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal ReturnQuantity,
    decimal UnitPrice,
    decimal GstRatePercent,
    decimal NetUnitPrice,
    decimal NetAmount,
    string Reason
)
{
    public string FormattedExpiry => ExpiryDate != default ? ExpiryDate.ToString("MM/yy") : "—";
}

public record CreatePurchaseReturnCommand(
    string OrgId,
    string BranchId,
    string WarehouseId,
    string PurchaseInvoiceId,
    string SupplierId,
    string SupplierName,
    string? SupplierGstin,
    string OriginalInvoiceNo,
    string CreatedByUserId,
    string? Notes,
    List<PurchaseReturnItemInputDto> Items
);

public record PurchaseReturnResult(
    bool Success,
    string? ReturnId,
    string? ReturnNumber,
    int ItemsReturnedCount,
    decimal TotalQuantityReturned,
    decimal TotalReturnAmount,
    string? ErrorMessage = null
);

public record PurchaseReturnBillDto(
    string ReturnNumber,
    DateTime ReturnDate,
    string SupplierName,
    string? SupplierGstin,
    string OriginalInvoiceNo,
    int ItemsCount,
    decimal TotalQuantity,
    decimal TotalReturnAmount,
    IReadOnlyList<PurchaseReturnItemInputDto> Items,
    string? OriginalPurchaseNo = null
)
{
    public string FormattedVoucherNo => $"Voucher No: {ReturnNumber}";
    public string FormattedReturnDate => $"Date: {ReturnDate:dd/MM/yyyy HH:mm}";
    public string FormattedRefInvoice => $"Ref Supplier Inv: {OriginalInvoiceNo}";
    public string FormattedRefPo => !string.IsNullOrWhiteSpace(OriginalPurchaseNo) ? $"PO Ref: {OriginalPurchaseNo}" : "";
    public string FormattedGstin => !string.IsNullOrWhiteSpace(SupplierGstin) ? $"(GSTIN: {SupplierGstin})" : "";
    public string FormattedItemsCount => $"Returned Items: {ItemsCount}";
    public string FormattedTotalQuantity => $"Total Units: {TotalQuantity:G29}";
    public string FormattedTotalDeducted => $"Total Deducted: ₹{TotalReturnAmount:N2}";
}
