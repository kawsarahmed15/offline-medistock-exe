using System;
using System.Collections.Generic;
using Medistock.Domain.Common;

namespace Medistock.Application.Sales.Commands;

public record CartItemInput(
    string ProductId,
    string ProductName,
    string BatchId,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal Quantity,
    decimal UnitPrice,
    decimal Mrp,
    decimal GstRatePercent,
    decimal DiscountPercent = 0
);

public record SalePaymentInput(
    PaymentMode PaymentMode,
    decimal Amount,
    string? Reference = null
);

public record CommitSaleCommand(
    string OrgId,
    string BranchId,
    string CounterId,
    string WarehouseId,
    string UserId,
    string DeviceId,
    string? CustomerId,
    string? CustomerName,
    bool IsInterstate,
    string? PrescriptionRef,
    List<CartItemInput> Items,
    List<SalePaymentInput> Payments
);

public record CommitSaleResult(
    bool IsSuccess,
    string? SaleId,
    string? InvoiceNo,
    decimal TotalAmount,
    decimal RoundOff,
    DateTime Timestamp,
    string? ErrorMessage
);
