using System;
using System.Collections.Generic;
using System.Linq;
using Medistock.Domain.Common;

namespace Medistock.Domain.Inventory;

public class StockTransfer : AggregateRoot<string>
{
    public string OrgId { get; private set; } = string.Empty;
    public string TransferNo { get; private set; } = string.Empty;
    public string SourceBranchId { get; private set; } = string.Empty;
    public string SourceWarehouseId { get; private set; } = string.Empty;
    public string DestinationBranchId { get; private set; } = string.Empty;
    public string DestinationWarehouseId { get; private set; } = string.Empty;
    public StockTransferStatus Status { get; private set; } = StockTransferStatus.Draft;
    public string? Notes { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public string? DispatchedByUserId { get; private set; }
    public string? ReceivedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DispatchedAt { get; private set; }
    public DateTime? ReceivedAt { get; private set; }

    private readonly List<StockTransferItem> _items = new();
    public IReadOnlyCollection<StockTransferItem> Items => _items.AsReadOnly();

    private StockTransfer() { }

    public static StockTransfer Create(
        string id,
        string orgId,
        string transferNo,
        string sourceBranchId,
        string sourceWarehouseId,
        string destinationBranchId,
        string destinationWarehouseId,
        string requestedByUserId,
        string? notes = null)
    {
        return new StockTransfer
        {
            Id = id,
            OrgId = orgId,
            TransferNo = transferNo,
            SourceBranchId = sourceBranchId,
            SourceWarehouseId = sourceWarehouseId,
            DestinationBranchId = destinationBranchId,
            DestinationWarehouseId = destinationWarehouseId,
            RequestedByUserId = requestedByUserId,
            Notes = notes,
            Status = StockTransferStatus.Requested,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddItem(StockTransferItem item)
    {
        if (Status != StockTransferStatus.Draft && Status != StockTransferStatus.Requested)
            throw new InvalidOperationException("Cannot modify items once transfer is dispatched.");

        _items.Add(item);
    }

    public void Dispatch(string dispatchedByUserId)
    {
        if (Status != StockTransferStatus.Requested && Status != StockTransferStatus.Draft)
            throw new InvalidOperationException("Transfer cannot be dispatched in its current state.");

        if (!_items.Any())
            throw new InvalidOperationException("Cannot dispatch a transfer with zero items.");

        Status = StockTransferStatus.InTransit;
        DispatchedByUserId = dispatchedByUserId;
        DispatchedAt = DateTime.UtcNow;
    }

    public void Receive(string receivedByUserId, Dictionary<string, decimal> receivedQuantities)
    {
        if (Status != StockTransferStatus.InTransit)
            throw new InvalidOperationException("Only in-transit transfers can be received.");

        bool hasDiscrepancy = false;

        foreach (var item in _items)
        {
            if (receivedQuantities.TryGetValue(item.Id, out decimal qtyReceived))
            {
                item.SetReceivedQuantity(qtyReceived);
                if (item.DiscrepancyQuantity != 0)
                {
                    hasDiscrepancy = true;
                }
            }
            else
            {
                // Default to dispatched quantity if not specified
                item.SetReceivedQuantity(item.DispatchedQuantity);
            }
        }

        Status = hasDiscrepancy ? StockTransferStatus.Disputed : StockTransferStatus.ReceivedCompleted;
        ReceivedByUserId = receivedByUserId;
        ReceivedAt = DateTime.UtcNow;
    }
}

public class StockTransferItem : Entity<string>
{
    public string TransferId { get; private set; } = string.Empty;
    public string ProductId { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string BatchId { get; private set; } = string.Empty;
    public string BatchNumber { get; private set; } = string.Empty;
    public DateTime ExpiryDate { get; private set; }
    public decimal RequestedQuantity { get; private set; }
    public decimal DispatchedQuantity { get; private set; }
    public decimal ReceivedQuantity { get; private set; }
    public decimal DiscrepancyQuantity => ReceivedQuantity - DispatchedQuantity;
    public decimal UnitCost { get; private set; }

    private StockTransferItem() { }

    public static StockTransferItem Create(
        string id,
        string transferId,
        string productId,
        string productName,
        string batchId,
        string batchNumber,
        DateTime expiryDate,
        decimal requestedQuantity,
        decimal dispatchedQuantity,
        decimal unitCost)
    {
        if (requestedQuantity <= 0)
            throw new ArgumentException("Requested quantity must be greater than zero.", nameof(requestedQuantity));

        return new StockTransferItem
        {
            Id = id,
            TransferId = transferId,
            ProductId = productId,
            ProductName = productName,
            BatchId = batchId,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            RequestedQuantity = requestedQuantity,
            DispatchedQuantity = dispatchedQuantity > 0 ? dispatchedQuantity : requestedQuantity,
            ReceivedQuantity = 0,
            UnitCost = unitCost
        };
    }

    public void SetReceivedQuantity(decimal quantity)
    {
        if (quantity < 0)
            throw new ArgumentException("Received quantity cannot be negative.", nameof(quantity));

        ReceivedQuantity = quantity;
    }
}
