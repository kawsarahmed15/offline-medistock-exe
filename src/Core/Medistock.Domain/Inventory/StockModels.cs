using System;
using Medistock.Domain.Common;

namespace Medistock.Domain.Inventory;

public class Batch : Entity<string>
{
    public string ProductId { get; private set; } = string.Empty;
    public string OrgId { get; private set; } = string.Empty;
    public string BatchNumber { get; private set; } = string.Empty;
    public DateTime ExpiryDate { get; private set; }
    public DateTime? ManufacturingDate { get; private set; }
    public decimal Mrp { get; private set; }
    public decimal PurchaseRate { get; private set; }
    public decimal SaleRate { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public bool IsExpired => ExpiryDate.Date < DateTime.UtcNow.Date;
    public int DaysUntilExpiry => (ExpiryDate.Date - DateTime.UtcNow.Date).Days;

    private Batch() { }

    public static Batch Create(
        string id,
        string productId,
        string orgId,
        string batchNumber,
        DateTime expiryDate,
        decimal mrp,
        decimal purchaseRate,
        decimal saleRate,
        DateTime? manufacturingDate = null)
    {
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new ArgumentException("Batch number cannot be empty.", nameof(batchNumber));
        if (mrp <= 0)
            throw new ArgumentOutOfRangeException(nameof(mrp), "MRP must be positive.");

        return new Batch
        {
            Id = id,
            ProductId = productId,
            OrgId = orgId,
            BatchNumber = batchNumber.Trim().ToUpperInvariant(),
            ExpiryDate = expiryDate,
            ManufacturingDate = manufacturingDate,
            Mrp = mrp,
            PurchaseRate = purchaseRate,
            SaleRate = saleRate > 0 ? saleRate : mrp,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public class StockBalance : Entity<string>
{
    public string BatchId { get; private set; } = string.Empty;
    public string ProductId { get; private set; } = string.Empty;
    public string WarehouseId { get; private set; } = string.Empty;
    public string? LocationId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal ReservedQuantity { get; private set; }
    public decimal AvailableQuantity => Math.Max(0, Quantity - ReservedQuantity);
    public DateTime LastUpdatedAt { get; private set; } = DateTime.UtcNow;

    private StockBalance() { }

    public static StockBalance Create(
        string id,
        string batchId,
        string productId,
        string warehouseId,
        decimal initialQuantity = 0,
        string? locationId = null)
    {
        return new StockBalance
        {
            Id = id,
            BatchId = batchId,
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            Quantity = Math.Max(0, initialQuantity),
            ReservedQuantity = 0,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    public bool TryDeductStock(decimal amount)
    {
        if (amount <= 0 || AvailableQuantity < amount) return false;
        Quantity -= amount;
        LastUpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void AddStock(decimal amount)
    {
        if (amount <= 0) return;
        Quantity += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }
}

public class StockMovement : Entity<string>
{
    public string OrgId { get; private set; } = string.Empty;
    public string BranchId { get; private set; } = string.Empty;
    public string WarehouseId { get; private set; } = string.Empty;
    public string BatchId { get; private set; } = string.Empty;
    public string ProductId { get; private set; } = string.Empty;
    public StockMovementType MovementType { get; private set; }
    public decimal Quantity { get; private set; } // Positive for addition, negative for deduction
    public string ReferenceType { get; private set; } = string.Empty; // e.g. "SALE", "PURCHASE"
    public string ReferenceId { get; private set; } = string.Empty;
    public decimal UnitCost { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string DeviceId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private StockMovement() { }

    public static StockMovement Record(
        string id,
        string orgId,
        string branchId,
        string warehouseId,
        string batchId,
        string productId,
        StockMovementType movementType,
        decimal quantity,
        string referenceType,
        string referenceId,
        decimal unitCost,
        string userId,
        string deviceId)
    {
        return new StockMovement
        {
            Id = id,
            OrgId = orgId,
            BranchId = branchId,
            WarehouseId = warehouseId,
            BatchId = batchId,
            ProductId = productId,
            MovementType = movementType,
            Quantity = quantity,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            UnitCost = unitCost,
            UserId = userId,
            DeviceId = deviceId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
