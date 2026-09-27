using System;
using System.Collections.Generic;
using Medistock.Domain.Common;

namespace Medistock.Application.Sales.DTOs;

public record SaleSummaryDto(
    string Id,
    string InvoiceNo,
    DateTime InvoiceDate,
    string? CustomerName,
    decimal Subtotal,
    decimal TaxAmount,
    decimal RoundOff,
    decimal Total,
    SaleStatus Status,
    int ItemsCount,
    string PaymentModesSummary
);

public record SaleDetailItemDto(
    string Id,
    string ProductId,
    string ProductName,
    string BatchId,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal Quantity,
    decimal UnitPrice,
    decimal Mrp,
    decimal DiscountPct,
    decimal TaxableAmount,
    decimal CgstRate,
    decimal CgstAmount,
    decimal SgstRate,
    decimal SgstAmount,
    decimal IgstRate,
    decimal IgstAmount,
    decimal NetAmount,
    decimal AlreadyReturnedQuantity
)
{
    public decimal TotalGstRate => CgstRate + SgstRate + IgstRate;
}

public record SaleDetailDto(
    string Id,
    string OrgId,
    string BranchId,
    string CounterId,
    string WarehouseId,
    string InvoiceNo,
    DateTime InvoiceDate,
    string? CustomerId,
    string? CustomerName,
    string UserId,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal RoundOff,
    decimal Total,
    SaleStatus Status,
    bool IsInterstate,
    List<SaleDetailItemDto> Items
);

public record ProcessSaleReturnItemInput(
    string SaleItemId,
    string ProductId,
    string ProductName,
    string BatchId,
    string BatchNumber,
    decimal Quantity,
    decimal UnitPrice,
    decimal CgstRate,
    decimal SgstRate,
    decimal IgstRate,
    RestockDecision RestockDecision,
    string Reason
);

public record ProcessSaleReturnCommand(
    string OrgId,
    string BranchId,
    string CounterId,
    string WarehouseId,
    string OriginalSaleId,
    string OriginalInvoiceNo,
    string? CustomerId,
    string? CustomerName,
    string UserId,
    string DeviceId,
    string Reason,
    PaymentMode RefundMode,
    List<ProcessSaleReturnItemInput> Items
);

public record SaleReturnResult(
    bool IsSuccess,
    string? SaleReturnId,
    string? CreditNoteNo,
    decimal TotalRefundAmount,
    string? ErrorMessage = null
);
