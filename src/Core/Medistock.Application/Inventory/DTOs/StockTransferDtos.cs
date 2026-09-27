using System;
using System.Collections.Generic;
using Medistock.Domain.Common;

namespace Medistock.Application.Inventory.DTOs;

public record StockTransferSummaryDto(
    string Id,
    string TransferNo,
    string SourceBranchId,
    string DestinationBranchId,
    StockTransferStatus Status,
    DateTime CreatedAt,
    DateTime? DispatchedAt,
    DateTime? ReceivedAt,
    int ItemsCount,
    decimal TotalRequestedQuantity,
    decimal TotalDispatchedQuantity,
    decimal TotalReceivedQuantity,
    string? Notes
);

public record StockTransferItemDto(
    string Id,
    string ProductId,
    string ProductName,
    string BatchId,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal RequestedQuantity,
    decimal DispatchedQuantity,
    decimal ReceivedQuantity,
    decimal DiscrepancyQuantity,
    decimal UnitCost
);

public record StockTransferDetailDto(
    string Id,
    string OrgId,
    string TransferNo,
    string SourceBranchId,
    string SourceWarehouseId,
    string DestinationBranchId,
    string DestinationWarehouseId,
    StockTransferStatus Status,
    string? Notes,
    string RequestedByUserId,
    string? DispatchedByUserId,
    string? ReceivedByUserId,
    DateTime CreatedAt,
    DateTime? DispatchedAt,
    DateTime? ReceivedAt,
    List<StockTransferItemDto> Items
);

public record CreateTransferRequestItemInput(
    string ProductId,
    string ProductName,
    string BatchId,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal RequestedQuantity,
    decimal UnitCost
);

public record CreateTransferRequestCommand(
    string OrgId,
    string SourceBranchId,
    string SourceWarehouseId,
    string DestinationBranchId,
    string DestinationWarehouseId,
    string RequestedByUserId,
    string? Notes,
    List<CreateTransferRequestItemInput> Items
);

public record DispatchTransferCommand(
    string TransferId,
    string DispatchedByUserId,
    string DeviceId
);

public record ReceiveTransferItemInput(
    string TransferItemId,
    decimal ReceivedQuantity
);

public record ReceiveTransferCommand(
    string TransferId,
    string ReceivedByUserId,
    string DeviceId,
    List<ReceiveTransferItemInput> Items
);

public record TransferOperationResult(
    bool IsSuccess,
    string? TransferId,
    string? TransferNo,
    string? ErrorMessage = null
);
