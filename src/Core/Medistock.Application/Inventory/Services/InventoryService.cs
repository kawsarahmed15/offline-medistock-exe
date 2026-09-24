using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Inventory.DTOs;
using Medistock.Domain.Common;

namespace Medistock.Application.Inventory.Services;

public interface IInventoryService
{
    Task<IReadOnlyList<StockSummaryItemDto>> GetStockSummaryAsync(
        string warehouseId,
        string? searchQuery = null,
        ExpiryBand? expiryBand = null,
        DrugSchedule? schedule = null,
        bool lowStockOnly = false,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<ExpiryDashboardDto> GetExpiryDashboardAsync(
        string warehouseId,
        CancellationToken cancellationToken = default);

    Task<StockAdjustmentResult> AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<StockAdjustmentResult> QuarantineExpiredBatchAsync(
        string orgId,
        string branchId,
        string warehouseId,
        string productId,
        string batchId,
        decimal currentQuantity,
        string reason,
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default);
}

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;

    public InventoryService(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public Task<IReadOnlyList<StockSummaryItemDto>> GetStockSummaryAsync(
        string warehouseId,
        string? searchQuery = null,
        ExpiryBand? expiryBand = null,
        DrugSchedule? schedule = null,
        bool lowStockOnly = false,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return _inventoryRepository.GetStockSummaryAsync(
            warehouseId, searchQuery, expiryBand, schedule, lowStockOnly, limit, cancellationToken);
    }

    public Task<ExpiryDashboardDto> GetExpiryDashboardAsync(
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        return _inventoryRepository.GetExpiryDashboardAsync(warehouseId, cancellationToken);
    }

    public Task<StockAdjustmentResult> AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Task.FromResult(new StockAdjustmentResult(false, null, 0, "Reason is mandatory for stock adjustment."));
        }

        if (request.Quantity <= 0)
        {
            return Task.FromResult(new StockAdjustmentResult(false, null, 0, "Adjustment quantity must be greater than zero."));
        }

        return _inventoryRepository.AdjustStockAsync(request, cancellationToken);
    }

    public Task<StockAdjustmentResult> QuarantineExpiredBatchAsync(
        string orgId,
        string branchId,
        string warehouseId,
        string productId,
        string batchId,
        decimal currentQuantity,
        string reason,
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var request = new StockAdjustmentRequest(
            OrgId: orgId,
            BranchId: branchId,
            WarehouseId: warehouseId,
            ProductId: productId,
            BatchId: batchId,
            AdjustmentType: StockAdjustmentType.QuarantineExpired,
            Quantity: currentQuantity,
            Reason: string.IsNullOrWhiteSpace(reason) ? "Quarantined expired batch" : reason,
            UserId: userId,
            DeviceId: deviceId
        );

        return _inventoryRepository.AdjustStockAsync(request, cancellationToken);
    }
}
