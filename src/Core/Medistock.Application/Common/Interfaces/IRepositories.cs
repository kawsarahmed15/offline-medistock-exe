using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Products.Queries;
using Medistock.Application.Sales.Commands;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;
using Medistock.Domain.Products;
using Medistock.Domain.Sales;

namespace Medistock.Application.Common.Interfaces;

public interface ISqliteConnectionFactory
{
    DbConnection CreateConnection();
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

public interface IProductSearchRepository
{
    Task<IReadOnlyList<ProductSearchDto>> SearchProductsAsync(
        string query,
        string warehouseId,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<BarcodeLookupDto?> LookupByBarcodeAsync(
        string barcode,
        string warehouseId,
        CancellationToken cancellationToken = default);
}

public interface IStockRepository
{
    Task<bool> DeductStockAtomicAsync(
        string batchId,
        string warehouseId,
        decimal quantity,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<Batch?> GetFefoBatchForProductAsync(
        string productId,
        string warehouseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Batch>> GetAvailableBatchesForProductAsync(
        string productId,
        string warehouseId,
        CancellationToken cancellationToken = default);

    Task RecordStockMovementAsync(
        StockMovement movement,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}

public interface ISaleRepository
{
    Task<CommitSaleResult> CommitSaleAtomicAsync(
        Sale sale,
        List<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default);
}

public interface IOutboxRepository
{
    Task EnqueueEventAsync(OutboxEvent outboxEvent, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxEvent>> GetPendingEventsAsync(int batchSize = 50, CancellationToken cancellationToken = default);
    Task MarkEventSyncedAsync(string eventId, CancellationToken cancellationToken = default);
    Task RecordEventFailureAsync(string eventId, string errorMessage, CancellationToken cancellationToken = default);
}

public interface IDocumentSequenceService
{
    Task<string> GenerateInvoiceNumberAsync(string orgId, string branchId, string prefix = "INV", CancellationToken cancellationToken = default);
}
