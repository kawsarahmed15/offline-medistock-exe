using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Inventory.DTOs;
using Medistock.Domain.Common;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteInventoryRepository : IInventoryRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteInventoryRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<StockSummaryItemDto>> GetStockSummaryAsync(
        string warehouseId,
        string? searchQuery = null,
        ExpiryBand? expiryBand = null,
        DrugSchedule? schedule = null,
        bool lowStockOnly = false,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var sb = new StringBuilder(@"
            SELECT 
                p.id AS ProductId,
                p.name AS ProductName,
                p.generic_name AS GenericName,
                p.composition AS SaltComposition,
                p.manufacturer_name AS Manufacturer,
                p.base_unit AS CategoryName,
                p.schedule AS Schedule,
                CAST(IFNULL(p.min_stock_alert, 10.0) AS REAL) AS MinStockAlert,
                b.id AS BatchId,
                b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDateStr,
                b.mrp AS Mrp,
                b.purchase_rate AS PurchaseRate,
                b.sale_rate AS SaleRate,
                sb.quantity AS TotalQuantity,
                sb.reserved_quantity AS ReservedQuantity
            FROM stock_balances sb
            INNER JOIN batches b ON b.id = sb.batch_id
            INNER JOIN products p ON p.id = sb.product_id
            WHERE (sb.warehouse_id = @warehouseId COLLATE NOCASE OR @warehouseId = '' OR @warehouseId = 'wh-1' OR @warehouseId = 'WH-MAIN')
        ");

        var parameters = new DynamicParameters();
        parameters.Add("warehouseId", warehouseId ?? "");
        parameters.Add("limit", limit);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            sb.Append(@" AND (
                p.name LIKE @query OR 
                p.brand_name LIKE @query OR 
                p.generic_name LIKE @query OR 
                p.composition LIKE @query OR 
                b.batch_number LIKE @query
            )");
            parameters.Add("query", $"%{searchQuery.Trim()}%");
        }

        if (schedule.HasValue)
        {
            sb.Append(" AND p.schedule = @schedule");
            parameters.Add("schedule", (int)schedule.Value);
        }

        if (lowStockOnly)
        {
            sb.Append(" AND (sb.quantity - sb.reserved_quantity) <= IFNULL(p.min_stock_alert, 10.0)");
        }

        sb.Append(" ORDER BY b.expiry_date ASC LIMIT @limit;");

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sb.ToString(), parameters, cancellationToken: cancellationToken));

        var now = DateTime.UtcNow.Date;
        var list = new List<StockSummaryItemDto>();

        foreach (var r in rows)
        {
            var expDate = DateTime.TryParse((string)r.ExpiryDateStr, out DateTime parsedExp) ? parsedExp : DateTime.MaxValue;
            var daysUntilExp = (expDate.Date - now).Days;

            ExpiryBand band = daysUntilExp switch
            {
                < 0 => ExpiryBand.Expired,
                <= 30 => ExpiryBand.Critical,
                <= 90 => ExpiryBand.Warning,
                _ => ExpiryBand.Good
            };

            if (expiryBand.HasValue && band != expiryBand.Value)
            {
                continue;
            }

            decimal totalQty = Convert.ToDecimal(r.TotalQuantity);
            decimal reservedQty = Convert.ToDecimal(r.ReservedQuantity);
            decimal availQty = Math.Max(0, totalQty - reservedQty);
            decimal mrp = Convert.ToDecimal(r.Mrp);
            decimal purchaseRate = Convert.ToDecimal(r.PurchaseRate);
            decimal saleRate = Convert.ToDecimal(r.SaleRate);
            decimal minStockAlert = r.MinStockAlert != null ? Convert.ToDecimal(r.MinStockAlert) : 10.0m;

            list.Add(new StockSummaryItemDto(
                ProductId: (string)r.ProductId,
                ProductName: (string)r.ProductName,
                GenericName: (string)r.GenericName,
                SaltComposition: (string)r.SaltComposition,
                Manufacturer: (string)r.Manufacturer,
                CategoryName: (string)r.CategoryName,
                Schedule: (DrugSchedule)(int)r.Schedule,
                BatchId: (string)r.BatchId,
                BatchNumber: (string)r.BatchNumber,
                ExpiryDate: expDate,
                DaysUntilExpiry: daysUntilExp,
                ExpiryStatus: band,
                AvailableQuantity: availQty,
                ReservedQuantity: reservedQty,
                TotalQuantity: totalQty,
                Mrp: mrp,
                PurchaseRate: purchaseRate,
                SaleRate: saleRate,
                StockValueAtMrp: Math.Round(availQty * mrp, 2),
                StockValueAtCost: Math.Round(availQty * purchaseRate, 2),
                MinStockAlert: minStockAlert
            ));
        }

        return list;
    }

    public async Task<ExpiryDashboardDto> GetExpiryDashboardAsync(
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        var allStocks = await GetStockSummaryAsync(warehouseId, limit: 10000, cancellationToken: cancellationToken);

        var expiredItems = allStocks.Where(s => s.ExpiryStatus == ExpiryBand.Expired).ToList();
        var criticalItems = allStocks.Where(s => s.ExpiryStatus == ExpiryBand.Critical).ToList();
        var warningItems = allStocks.Where(s => s.ExpiryStatus == ExpiryBand.Warning).ToList();
        var goodItems = allStocks.Where(s => s.ExpiryStatus == ExpiryBand.Good).ToList();

        var expiredBand = new ExpiryBandSummaryDto(
            Band: ExpiryBand.Expired,
            Title: "Expired Batches",
            BatchCount: expiredItems.Count,
            TotalQuantity: expiredItems.Sum(i => i.AvailableQuantity),
            TotalValueAtRisk: expiredItems.Sum(i => i.StockValueAtCost),
            TopBatches: expiredItems.Take(10).ToList()
        );

        var criticalBand = new ExpiryBandSummaryDto(
            Band: ExpiryBand.Critical,
            Title: "Critical (≤ 30 Days)",
            BatchCount: criticalItems.Count,
            TotalQuantity: criticalItems.Sum(i => i.AvailableQuantity),
            TotalValueAtRisk: criticalItems.Sum(i => i.StockValueAtCost),
            TopBatches: criticalItems.Take(10).ToList()
        );

        var warningBand = new ExpiryBandSummaryDto(
            Band: ExpiryBand.Warning,
            Title: "Warning (31 - 90 Days)",
            BatchCount: warningItems.Count,
            TotalQuantity: warningItems.Sum(i => i.AvailableQuantity),
            TotalValueAtRisk: warningItems.Sum(i => i.StockValueAtCost),
            TopBatches: warningItems.Take(10).ToList()
        );

        var goodBand = new ExpiryBandSummaryDto(
            Band: ExpiryBand.Good,
            Title: "Good (> 90 Days)",
            BatchCount: goodItems.Count,
            TotalQuantity: goodItems.Sum(i => i.AvailableQuantity),
            TotalValueAtRisk: goodItems.Sum(i => i.StockValueAtCost),
            TopBatches: goodItems.Take(10).ToList()
        );

        return new ExpiryDashboardDto(
            TotalActiveBatches: allStocks.Count,
            TotalStockQuantity: allStocks.Sum(i => i.AvailableQuantity),
            TotalInventoryValueCost: allStocks.Sum(i => i.StockValueAtCost),
            TotalInventoryValueMrp: allStocks.Sum(i => i.StockValueAtMrp),
            ExpiredBand: expiredBand,
            CriticalBand: criticalBand,
            WarningBand: warningBand,
            GoodBand: goodBand
        );
    }

    public async Task<StockAdjustmentResult> AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Check existing balance
            const string querySql = @"
                SELECT sb.quantity, b.purchase_rate, sb.warehouse_id
                FROM stock_balances sb
                INNER JOIN batches b ON b.id = sb.batch_id
                WHERE sb.batch_id = @BatchId;
            ";

            var balanceRow = await connection.QuerySingleOrDefaultAsync<dynamic>(
                new CommandDefinition(querySql, new { request.BatchId }, transaction, cancellationToken: cancellationToken));

            decimal currentQty = balanceRow != null ? Convert.ToDecimal(balanceRow.quantity) : 0;
            decimal purchaseRate = balanceRow != null ? Convert.ToDecimal(balanceRow.purchase_rate) : 0;

            decimal newQty;
            decimal movementQty;
            StockMovementType movementType;

            switch (request.AdjustmentType)
            {
                case StockAdjustmentType.Add:
                    newQty = currentQty + request.Quantity;
                    movementQty = request.Quantity;
                    movementType = StockMovementType.Adjustment;
                    break;

                case StockAdjustmentType.Reduce:
                    if (currentQty < request.Quantity)
                    {
                        transaction.Rollback();
                        return new StockAdjustmentResult(false, null, currentQty, $"Insufficient stock to reduce. Current: {currentQty}, Requested: {request.Quantity}");
                    }
                    newQty = currentQty - request.Quantity;
                    movementQty = -request.Quantity;
                    movementType = StockMovementType.Adjustment;
                    break;

                case StockAdjustmentType.QuarantineExpired:
                    if (currentQty <= 0)
                    {
                        transaction.Rollback();
                        return new StockAdjustmentResult(false, null, currentQty, "Batch already has zero stock.");
                    }
                    newQty = 0;
                    movementQty = -currentQty;
                    movementType = StockMovementType.ExpiryWriteOff;
                    break;

                case StockAdjustmentType.DamageWriteOff:
                    if (currentQty < request.Quantity)
                    {
                        transaction.Rollback();
                        return new StockAdjustmentResult(false, null, currentQty, $"Cannot write off more than available stock ({currentQty}).");
                    }
                    newQty = currentQty - request.Quantity;
                    movementQty = -request.Quantity;
                    movementType = StockMovementType.Damage;
                    break;

                default:
                    transaction.Rollback();
                    return new StockAdjustmentResult(false, null, currentQty, "Invalid adjustment type.");
            }

            // 2. Update stock balance
            const string updateSql = @"
                UPDATE stock_balances
                SET quantity = @newQty,
                    last_updated_at = @updatedAt
                WHERE batch_id = @BatchId;
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                updateSql,
                new { newQty = (double)newQty, updatedAt = DateTime.UtcNow.ToString("o"), request.BatchId },
                transaction,
                cancellationToken: cancellationToken));

            // 3. Write Stock Movement Ledger entry
            var movementId = Guid.NewGuid().ToString("N");
            const string insertMovementSql = @"
                INSERT INTO stock_movements (
                    id, org_id, branch_id, warehouse_id, batch_id, product_id,
                    movement_type, quantity, reference_type, reference_id,
                    unit_cost, user_id, device_id, created_at
                ) VALUES (
                    @Id, @OrgId, @BranchId, @WarehouseId, @BatchId, @ProductId,
                    @MovementType, @Quantity, @ReferenceType, @ReferenceId,
                    @UnitCost, @UserId, @DeviceId, @CreatedAt
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertMovementSql,
                new
                {
                    Id = movementId,
                    request.OrgId,
                    request.BranchId,
                    request.WarehouseId,
                    request.BatchId,
                    request.ProductId,
                    MovementType = (int)movementType,
                    Quantity = (double)movementQty,
                    ReferenceType = "ADJUSTMENT",
                    ReferenceId = request.Reason,
                    UnitCost = (double)purchaseRate,
                    request.UserId,
                    request.DeviceId,
                    CreatedAt = DateTime.UtcNow.ToString("o")
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();
            return new StockAdjustmentResult(true, movementId, newQty);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new StockAdjustmentResult(false, null, 0, ex.Message);
        }
    }
}
