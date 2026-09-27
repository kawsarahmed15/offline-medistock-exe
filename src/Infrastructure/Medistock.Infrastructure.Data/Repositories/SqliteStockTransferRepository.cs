using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Inventory.DTOs;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteStockTransferRepository : IStockTransferRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IStockRepository _stockRepository;
    private readonly IOutboxRepository _outboxRepository;

    public SqliteStockTransferRepository(
        ISqliteConnectionFactory connectionFactory,
        IStockRepository stockRepository,
        IOutboxRepository outboxRepository)
    {
        _connectionFactory = connectionFactory;
        _stockRepository = stockRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<IReadOnlyList<StockTransferSummaryDto>> GetTransfersAsync(
        string orgId,
        string? branchId = null,
        StockTransferStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var sql = @"
            SELECT 
                st.id AS Id,
                st.transfer_no AS TransferNo,
                st.source_branch_id AS SourceBranchId,
                st.destination_branch_id AS DestinationBranchId,
                st.status AS Status,
                st.created_at AS CreatedAtStr,
                st.dispatched_at AS DispatchedAtStr,
                st.received_at AS ReceivedAtStr,
                st.notes AS Notes,
                COUNT(sti.id) AS ItemsCount,
                IFNULL(SUM(sti.requested_quantity), 0.0) AS TotalRequested,
                IFNULL(SUM(sti.dispatched_quantity), 0.0) AS TotalDispatched,
                IFNULL(SUM(sti.received_quantity), 0.0) AS TotalReceived
            FROM stock_transfers st
            LEFT JOIN stock_transfer_items sti ON sti.transfer_id = st.id
            WHERE st.org_id = @orgId
        ";

        if (!string.IsNullOrEmpty(branchId))
        {
            sql += " AND (st.source_branch_id = @branchId OR st.destination_branch_id = @branchId) ";
        }

        if (status.HasValue)
        {
            sql += " AND st.status = @statusVal ";
        }

        sql += @"
            GROUP BY st.id
            ORDER BY st.created_at DESC;
        ";

        var rows = await connection.QueryAsync<dynamic>(new CommandDefinition(
            sql,
            new
            {
                orgId,
                branchId,
                statusVal = status.HasValue ? (int)status.Value : 0
            },
            cancellationToken: cancellationToken));

        var list = new List<StockTransferSummaryDto>();
        foreach (var r in rows)
        {
            DateTime.TryParse((string)r.CreatedAtStr, out DateTime crAt);
            DateTime? dispAt = DateTime.TryParse((string?)r.DispatchedAtStr, out var d) ? d : null;
            DateTime? recAt = DateTime.TryParse((string?)r.ReceivedAtStr, out var rc) ? rc : null;

            list.Add(new StockTransferSummaryDto(
                (string)r.Id,
                (string)r.TransferNo,
                (string)r.SourceBranchId,
                (string)r.DestinationBranchId,
                (StockTransferStatus)(int)r.Status,
                crAt,
                dispAt,
                recAt,
                (int)r.ItemsCount,
                Convert.ToDecimal(r.TotalRequested),
                Convert.ToDecimal(r.TotalDispatched),
                Convert.ToDecimal(r.TotalReceived),
                (string?)r.Notes
            ));
        }

        return list;
    }

    public async Task<StockTransferDetailDto?> GetTransferByIdAsync(
        string transferId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string headerSql = @"
            SELECT id, org_id, transfer_no, source_branch_id, source_warehouse_id,
                   destination_branch_id, destination_warehouse_id, status, notes,
                   requested_by_user_id, dispatched_by_user_id, received_by_user_id,
                   created_at, dispatched_at, received_at
            FROM stock_transfers
            WHERE id = @transferId;
        ";

        var h = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(
            headerSql, new { transferId }, cancellationToken: cancellationToken));

        if (h == null) return null;

        const string itemsSql = @"
            SELECT id, product_id, product_name, batch_id, batch_number, expiry_date,
                   requested_quantity, dispatched_quantity, received_quantity,
                   discrepancy_quantity, unit_cost
            FROM stock_transfer_items
            WHERE transfer_id = @transferId;
        ";

        var itemRows = await connection.QueryAsync<dynamic>(new CommandDefinition(
            itemsSql, new { transferId }, cancellationToken: cancellationToken));

        var items = new List<StockTransferItemDto>();
        foreach (var i in itemRows)
        {
            DateTime.TryParse((string)i.expiry_date, out DateTime expDate);
            items.Add(new StockTransferItemDto(
                (string)i.id,
                (string)i.product_id,
                (string)i.product_name,
                (string)i.batch_id,
                (string)i.batch_number,
                expDate,
                Convert.ToDecimal(i.requested_quantity),
                Convert.ToDecimal(i.dispatched_quantity),
                Convert.ToDecimal(i.received_quantity),
                Convert.ToDecimal(i.discrepancy_quantity),
                Convert.ToDecimal(i.unit_cost)
            ));
        }

        DateTime.TryParse((string)h.created_at, out DateTime crAt);
        DateTime? dispAt = DateTime.TryParse((string?)h.dispatched_at, out var d) ? d : null;
        DateTime? recAt = DateTime.TryParse((string?)h.received_at, out var rc) ? rc : null;

        return new StockTransferDetailDto(
            (string)h.id,
            (string)h.org_id,
            (string)h.transfer_no,
            (string)h.source_branch_id,
            (string)h.source_warehouse_id,
            (string)h.destination_branch_id,
            (string)h.destination_warehouse_id,
            (StockTransferStatus)(int)h.status,
            (string?)h.notes,
            (string)h.requested_by_user_id,
            (string?)h.dispatched_by_user_id,
            (string?)h.received_by_user_id,
            crAt,
            dispAt,
            recAt,
            items
        );
    }

    public async Task<TransferOperationResult> CreateTransferRequestAsync(
        StockTransfer transfer,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            const string insertHeaderSql = @"
                INSERT INTO stock_transfers (
                    id, org_id, transfer_no, source_branch_id, source_warehouse_id,
                    destination_branch_id, destination_warehouse_id, status, notes,
                    requested_by_user_id, created_at
                ) VALUES (
                    @Id, @OrgId, @TransferNo, @SourceBranchId, @SourceWarehouseId,
                    @DestinationBranchId, @DestinationWarehouseId, @Status, @Notes,
                    @RequestedByUserId, @CreatedAt
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertHeaderSql,
                new
                {
                    transfer.Id,
                    transfer.OrgId,
                    transfer.TransferNo,
                    transfer.SourceBranchId,
                    transfer.SourceWarehouseId,
                    transfer.DestinationBranchId,
                    transfer.DestinationWarehouseId,
                    Status = (int)transfer.Status,
                    transfer.Notes,
                    transfer.RequestedByUserId,
                    CreatedAt = transfer.CreatedAt.ToString("o")
                },
                transaction,
                cancellationToken: cancellationToken));

            const string insertItemSql = @"
                INSERT INTO stock_transfer_items (
                    id, transfer_id, product_id, product_name, batch_id, batch_number,
                    expiry_date, requested_quantity, dispatched_quantity, received_quantity,
                    discrepancy_quantity, unit_cost
                ) VALUES (
                    @Id, @TransferId, @ProductId, @ProductName, @BatchId, @BatchNumber,
                    @ExpiryDate, CAST(@RequestedQuantity AS REAL), CAST(@DispatchedQuantity AS REAL),
                    0.0, 0.0, CAST(@UnitCost AS REAL)
                );
            ";

            foreach (var item in transfer.Items)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    insertItemSql,
                    new
                    {
                        item.Id,
                        item.TransferId,
                        item.ProductId,
                        item.ProductName,
                        item.BatchId,
                        item.BatchNumber,
                        ExpiryDate = item.ExpiryDate.ToString("o"),
                        RequestedQuantity = (double)item.RequestedQuantity,
                        DispatchedQuantity = (double)item.DispatchedQuantity,
                        UnitCost = (double)item.UnitCost
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return new TransferOperationResult(true, transfer.Id, transfer.TransferNo);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new TransferOperationResult(false, null, null, ex.Message);
        }
    }

    public async Task<TransferOperationResult> DispatchTransferAtomicAsync(
        string transferId,
        string dispatchedByUserId,
        string deviceId,
        List<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Fetch Source Warehouse & Dispatched Quantities
            var transfer = await GetTransferByIdAsync(transferId, cancellationToken);
            if (transfer == null)
            {
                return new TransferOperationResult(false, null, null, "Transfer not found.");
            }

            // 2. Deduct inventory from source warehouse
            foreach (var item in transfer.Items)
            {
                var deducted = await _stockRepository.DeductStockAtomicAsync(
                    item.BatchId,
                    transfer.SourceWarehouseId,
                    item.DispatchedQuantity,
                    (System.Data.Common.DbTransaction)transaction,
                    cancellationToken);

                if (!deducted)
                {
                    transaction.Rollback();
                    return new TransferOperationResult(false, null, null, $"Insufficient stock to dispatch item: {item.ProductName} (Batch: {item.BatchNumber})");
                }
            }

            // 3. Update Transfer Header Status = InTransit
            const string updateHeaderSql = @"
                UPDATE stock_transfers 
                SET status = @status, dispatched_by_user_id = @dispatchedByUserId, dispatched_at = @dispatchedAt
                WHERE id = @transferId;
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                updateHeaderSql,
                new
                {
                    status = (int)StockTransferStatus.InTransit,
                    dispatchedByUserId,
                    dispatchedAt = DateTime.UtcNow.ToString("o"),
                    transferId
                },
                transaction,
                cancellationToken: cancellationToken));

            // 4. Record Stock Movements
            foreach (var m in stockMovements)
            {
                await _stockRepository.RecordStockMovementAsync(
                    m,
                    (System.Data.Common.DbTransaction)transaction,
                    cancellationToken);
            }

            // 5. Enqueue Outbox Event
            await _outboxRepository.EnqueueEventAsync(
                outboxEvent,
                (System.Data.Common.DbTransaction)transaction,
                cancellationToken);

            transaction.Commit();
            return new TransferOperationResult(true, transfer.Id, transfer.TransferNo);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new TransferOperationResult(false, null, null, ex.Message);
        }
    }

    public async Task<TransferOperationResult> ReceiveTransferAtomicAsync(
        string transferId,
        string receivedByUserId,
        string deviceId,
        Dictionary<string, decimal> receivedQuantities,
        List<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            var transfer = await GetTransferByIdAsync(transferId, cancellationToken);
            if (transfer == null)
            {
                return new TransferOperationResult(false, null, null, "Transfer not found.");
            }

            bool hasDiscrepancy = false;

            const string upsertStockSql = @"
                INSERT INTO stock_balances (
                    id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at
                ) VALUES (
                    @Id, @BatchId, @ProductId, @WarehouseId, CAST(@Quantity AS REAL), 0.0, @LastUpdatedAt
                )
                ON CONFLICT(batch_id, warehouse_id) DO UPDATE SET
                    quantity = quantity + CAST(@Quantity AS REAL),
                    last_updated_at = @LastUpdatedAt;
            ";

            const string updateItemSql = @"
                UPDATE stock_transfer_items
                SET received_quantity = CAST(@receivedQty AS REAL),
                    discrepancy_quantity = CAST(@discrepancyQty AS REAL)
                WHERE id = @itemId;
            ";

            foreach (var item in transfer.Items)
            {
                var qtyReceived = receivedQuantities.TryGetValue(item.Id, out var qty) ? qty : item.DispatchedQuantity;
                var discrepancy = qtyReceived - item.DispatchedQuantity;
                if (discrepancy != 0) hasDiscrepancy = true;

                // Update line item
                await connection.ExecuteAsync(new CommandDefinition(
                    updateItemSql,
                    new
                    {
                        receivedQty = (double)qtyReceived,
                        discrepancyQty = (double)discrepancy,
                        itemId = item.Id
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                // Restock received quantity into destination warehouse
                if (qtyReceived > 0)
                {
                    var sbId = $"sb_{item.BatchId}_{transfer.DestinationWarehouseId}";
                    await connection.ExecuteAsync(new CommandDefinition(
                        upsertStockSql,
                        new
                        {
                            Id = sbId,
                            BatchId = item.BatchId,
                            ProductId = item.ProductId,
                            WarehouseId = transfer.DestinationWarehouseId,
                            Quantity = (double)qtyReceived,
                            LastUpdatedAt = DateTime.UtcNow.ToString("o")
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }
            }

            // Update Header Status
            var finalStatus = hasDiscrepancy ? StockTransferStatus.Disputed : StockTransferStatus.ReceivedCompleted;

            const string updateHeaderSql = @"
                UPDATE stock_transfers 
                SET status = @status, received_by_user_id = @receivedByUserId, received_at = @receivedAt
                WHERE id = @transferId;
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                updateHeaderSql,
                new
                {
                    status = (int)finalStatus,
                    receivedByUserId,
                    receivedAt = DateTime.UtcNow.ToString("o"),
                    transferId
                },
                transaction,
                cancellationToken: cancellationToken));

            // Record Stock Movements
            foreach (var m in stockMovements)
            {
                await _stockRepository.RecordStockMovementAsync(
                    m,
                    (System.Data.Common.DbTransaction)transaction,
                    cancellationToken);
            }

            // Enqueue Outbox Event
            await _outboxRepository.EnqueueEventAsync(
                outboxEvent,
                (System.Data.Common.DbTransaction)transaction,
                cancellationToken);

            transaction.Commit();
            return new TransferOperationResult(true, transfer.Id, transfer.TransferNo);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new TransferOperationResult(false, null, null, ex.Message);
        }
    }
}
