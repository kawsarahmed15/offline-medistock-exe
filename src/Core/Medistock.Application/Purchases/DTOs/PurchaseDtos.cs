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
    bool IsActive
);

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
    DateTime? PostedAt
);

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
