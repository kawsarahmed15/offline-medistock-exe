using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Inventory.DTOs;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;

namespace Medistock.Application.Inventory.Services;

public interface IStockTransferService
{
    Task<IReadOnlyList<StockTransferSummaryDto>> GetTransfersAsync(
        string orgId,
        string? branchId = null,
        StockTransferStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<StockTransferDetailDto?> GetTransferDetailAsync(
        string transferId,
        CancellationToken cancellationToken = default);

    Task<TransferOperationResult> CreateTransferRequestAsync(
        CreateTransferRequestCommand command,
        CancellationToken cancellationToken = default);

    Task<TransferOperationResult> DispatchTransferAsync(
        DispatchTransferCommand command,
        CancellationToken cancellationToken = default);

    Task<TransferOperationResult> ReceiveTransferAsync(
        ReceiveTransferCommand command,
        CancellationToken cancellationToken = default);
}

public class StockTransferService : IStockTransferService
{
    private readonly IStockTransferRepository _transferRepository;
    private readonly IDocumentSequenceService _sequenceService;

    public StockTransferService(
        IStockTransferRepository transferRepository,
        IDocumentSequenceService sequenceService)
    {
        _transferRepository = transferRepository;
        _sequenceService = sequenceService;
    }

    public async Task<IReadOnlyList<StockTransferSummaryDto>> GetTransfersAsync(
        string orgId,
        string? branchId = null,
        StockTransferStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        return await _transferRepository.GetTransfersAsync(orgId, branchId, status, cancellationToken);
    }

    public async Task<StockTransferDetailDto?> GetTransferDetailAsync(
        string transferId,
        CancellationToken cancellationToken = default)
    {
        return await _transferRepository.GetTransferByIdAsync(transferId, cancellationToken);
    }

    public async Task<TransferOperationResult> CreateTransferRequestAsync(
        CreateTransferRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Items == null || command.Items.Count == 0)
        {
            return new TransferOperationResult(false, null, null, "No items specified in transfer request.");
        }

        var transferId = $"st_{Ulid.NewUlid()}";
        var transferNo = await _sequenceService.GenerateInvoiceNumberAsync(command.OrgId, command.SourceBranchId, "ST", cancellationToken);

        var transfer = StockTransfer.Create(
            transferId,
            command.OrgId,
            transferNo,
            command.SourceBranchId,
            command.SourceWarehouseId,
            command.DestinationBranchId,
            command.DestinationWarehouseId,
            command.RequestedByUserId,
            command.Notes);

        foreach (var item in command.Items)
        {
            var itemId = $"sti_{Ulid.NewUlid()}";
            var transferItem = StockTransferItem.Create(
                itemId,
                transferId,
                item.ProductId,
                item.ProductName,
                item.BatchId,
                item.BatchNumber,
                item.ExpiryDate,
                item.RequestedQuantity,
                item.RequestedQuantity,
                item.UnitCost);

            transfer.AddItem(transferItem);
        }

        return await _transferRepository.CreateTransferRequestAsync(transfer, cancellationToken);
    }

    public async Task<TransferOperationResult> DispatchTransferAsync(
        DispatchTransferCommand command,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.GetTransferByIdAsync(command.TransferId, cancellationToken);
        if (transfer == null)
        {
            return new TransferOperationResult(false, null, null, "Transfer request not found.");
        }

        var stockMovements = new List<StockMovement>();

        foreach (var item in transfer.Items)
        {
            var movementId = $"sm_tout_{Ulid.NewUlid()}";
            var movement = StockMovement.Record(
                movementId,
                transfer.OrgId,
                transfer.SourceBranchId,
                transfer.SourceWarehouseId,
                item.BatchId,
                item.ProductId,
                StockMovementType.StockTransferOut,
                -item.DispatchedQuantity, // Deduct from source warehouse
                "STOCK_TRANSFER_OUT",
                transfer.TransferNo,
                item.UnitCost,
                command.DispatchedByUserId,
                command.DeviceId);

            stockMovements.Add(movement);
        }

        var outboxPayload = JsonSerializer.Serialize(new
        {
            TransferId = transfer.Id,
            TransferNo = transfer.TransferNo,
            SourceBranchId = transfer.SourceBranchId,
            DestinationBranchId = transfer.DestinationBranchId,
            Status = "InTransit",
            DispatchedBy = command.DispatchedByUserId,
            Timestamp = DateTime.UtcNow
        });

        var outboxEvent = OutboxEvent.Create(
            $"evt_{Ulid.NewUlid()}",
            "StockTransfer",
            transfer.Id,
            "TRANSFER_DISPATCHED",
            outboxPayload,
            command.DeviceId,
            $"op_{Ulid.NewUlid()}");

        return await _transferRepository.DispatchTransferAtomicAsync(
            command.TransferId,
            command.DispatchedByUserId,
            command.DeviceId,
            stockMovements,
            outboxEvent,
            cancellationToken);
    }

    public async Task<TransferOperationResult> ReceiveTransferAsync(
        ReceiveTransferCommand command,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.GetTransferByIdAsync(command.TransferId, cancellationToken);
        if (transfer == null)
        {
            return new TransferOperationResult(false, null, null, "Transfer request not found.");
        }

        var receivedMap = command.Items.ToDictionary(i => i.TransferItemId, i => i.ReceivedQuantity);
        var stockMovements = new List<StockMovement>();

        foreach (var item in transfer.Items)
        {
            var qtyReceived = receivedMap.TryGetValue(item.Id, out var qty) ? qty : item.DispatchedQuantity;

            if (qtyReceived > 0)
            {
                var movementId = $"sm_tin_{Ulid.NewUlid()}";
                var movement = StockMovement.Record(
                    movementId,
                    transfer.OrgId,
                    transfer.DestinationBranchId,
                    transfer.DestinationWarehouseId,
                    item.BatchId,
                    item.ProductId,
                    StockMovementType.StockTransferIn,
                    qtyReceived, // Add to destination warehouse
                    "STOCK_TRANSFER_IN",
                    transfer.TransferNo,
                    item.UnitCost,
                    command.ReceivedByUserId,
                    command.DeviceId);

                stockMovements.Add(movement);
            }

            var discrepancy = qtyReceived - item.DispatchedQuantity;
            if (discrepancy < 0)
            {
                // Record transit damage/loss
                var lossMovementId = $"sm_tloss_{Ulid.NewUlid()}";
                var lossMovement = StockMovement.Record(
                    lossMovementId,
                    transfer.OrgId,
                    transfer.DestinationBranchId,
                    transfer.DestinationWarehouseId,
                    item.BatchId,
                    item.ProductId,
                    StockMovementType.Damage,
                    discrepancy, // Negative discrepancy
                    "TRANSFER_TRANSIT_LOSS",
                    transfer.TransferNo,
                    item.UnitCost,
                    command.ReceivedByUserId,
                    command.DeviceId);

                stockMovements.Add(lossMovement);
            }
        }

        var outboxPayload = JsonSerializer.Serialize(new
        {
            TransferId = transfer.Id,
            TransferNo = transfer.TransferNo,
            SourceBranchId = transfer.SourceBranchId,
            DestinationBranchId = transfer.DestinationBranchId,
            Status = "ReceivedCompleted",
            ReceivedBy = command.ReceivedByUserId,
            Timestamp = DateTime.UtcNow
        });

        var outboxEvent = OutboxEvent.Create(
            $"evt_{Ulid.NewUlid()}",
            "StockTransfer",
            transfer.Id,
            "TRANSFER_RECEIVED",
            outboxPayload,
            command.DeviceId,
            $"op_{Ulid.NewUlid()}");

        return await _transferRepository.ReceiveTransferAtomicAsync(
            command.TransferId,
            command.ReceivedByUserId,
            command.DeviceId,
            receivedMap,
            stockMovements,
            outboxEvent,
            cancellationToken);
    }
}
