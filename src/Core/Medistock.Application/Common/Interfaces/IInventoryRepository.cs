using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Inventory.DTOs;
using Medistock.Domain.Common;

namespace Medistock.Application.Common.Interfaces;

public interface IInventoryRepository
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
}

public interface IScheduleDrugRepository
{
    Task RecordScheduleDrugEntriesAsync(
        IReadOnlyList<Medistock.Domain.Compliance.ScheduleDrugRegisterEntry> entries,
        System.Data.Common.DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleDrugRegisterDto>> GetScheduleRegisterAsync(
        ScheduleDrugFilter filter,
        CancellationToken cancellationToken = default);
}
