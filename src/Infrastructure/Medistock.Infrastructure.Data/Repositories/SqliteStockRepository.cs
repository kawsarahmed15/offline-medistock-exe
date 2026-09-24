using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Domain.Inventory;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteStockRepository : IStockRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteStockRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> DeductStockAtomicAsync(
        string batchId,
        string warehouseId,
        decimal quantity,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) return false;

        const string sql = @"
            UPDATE stock_balances
            SET quantity = quantity - CAST(@quantity AS REAL),
                last_updated_at = @updatedAt
            WHERE batch_id = @batchId 
              AND warehouse_id = @warehouseId 
              AND (quantity - reserved_quantity) >= CAST(@quantity AS REAL);
        ";

        var parameters = new
        {
            quantity = (double)quantity,
            batchId,
            warehouseId,
            updatedAt = DateTime.UtcNow.ToString("o")
        };

        if (transaction != null)
        {
            var affected = await transaction.Connection!.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            return affected > 0;
        }

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<Batch?> GetFefoBatchForProductAsync(
        string productId,
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                b.id AS Id,
                b.product_id AS ProductId,
                b.org_id AS OrgId,
                b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDate,
                b.manufacturing_date AS ManufacturingDate,
                b.mrp AS Mrp,
                b.purchase_rate AS PurchaseRate,
                b.sale_rate AS SaleRate,
                b.created_at AS CreatedAt
            FROM batches b
            JOIN stock_balances sb ON sb.batch_id = b.id AND sb.warehouse_id = @warehouseId
            WHERE b.product_id = @productId 
              AND date(b.expiry_date) >= date('now')
              AND (sb.quantity - sb.reserved_quantity) > 0
            ORDER BY date(b.expiry_date) ASC
            LIMIT 1;
        ";

        return await connection.QueryFirstOrDefaultAsync<Batch>(
            new CommandDefinition(sql, new { productId, warehouseId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Batch>> GetAvailableBatchesForProductAsync(
        string productId,
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                b.id AS Id,
                b.product_id AS ProductId,
                b.org_id AS OrgId,
                b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDate,
                b.manufacturing_date AS ManufacturingDate,
                b.mrp AS Mrp,
                b.purchase_rate AS PurchaseRate,
                b.sale_rate AS SaleRate,
                b.created_at AS CreatedAt
            FROM batches b
            JOIN stock_balances sb ON sb.batch_id = b.id AND sb.warehouse_id = @warehouseId
            WHERE b.product_id = @productId 
              AND date(b.expiry_date) >= date('now')
              AND (sb.quantity - sb.reserved_quantity) > 0
            ORDER BY date(b.expiry_date) ASC;
        ";

        var results = await connection.QueryAsync<Batch>(
            new CommandDefinition(sql, new { productId, warehouseId }, cancellationToken: cancellationToken));

        return results.ToList();
    }

    public async Task RecordStockMovementAsync(
        StockMovement movement,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO stock_movements (
                id, org_id, branch_id, warehouse_id, batch_id, product_id,
                movement_type, quantity, reference_type, reference_id,
                unit_cost, user_id, device_id, created_at
            ) VALUES (
                @Id, @OrgId, @BranchId, @WarehouseId, @BatchId, @ProductId,
                @MovementType, CAST(@Quantity AS REAL), @ReferenceType, @ReferenceId,
                CAST(@UnitCost AS REAL), @UserId, @DeviceId, @CreatedAt
            );
        ";

        var parameters = new
        {
            movement.Id,
            movement.OrgId,
            movement.BranchId,
            movement.WarehouseId,
            movement.BatchId,
            movement.ProductId,
            MovementType = (int)movement.MovementType,
            Quantity = (double)movement.Quantity,
            movement.ReferenceType,
            movement.ReferenceId,
            UnitCost = (double)movement.UnitCost,
            movement.UserId,
            movement.DeviceId,
            CreatedAt = movement.CreatedAt.ToString("o")
        };

        if (transaction != null)
        {
            await transaction.Connection!.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            return;
        }

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}
