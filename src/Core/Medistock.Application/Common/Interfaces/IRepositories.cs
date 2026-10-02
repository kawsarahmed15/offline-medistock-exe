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

    Task<IReadOnlyList<ProductBatchDto>> GetBatchesForProductAsync(
        string productId,
        string warehouseId,
        CancellationToken cancellationToken = default);
}

public interface IProductRepository
{
    Task<string> CreateProductWithBatchAsync(
        Product product,
        Batch batch,
        decimal openingQuantity,
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
    Task<int> PeekNextSequenceNumberAsync(string orgId, string branchId, string prefix = "INV", CancellationToken cancellationToken = default);
}

public interface ISaleReturnRepository
{
    Task<IReadOnlyList<Medistock.Application.Sales.DTOs.SaleSummaryDto>> GetRecentSalesAsync(
        string orgId,
        string branchId,
        string? searchQuery = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<Medistock.Application.Sales.DTOs.SaleDetailDto?> GetSaleByIdAsync(
        string saleId,
        CancellationToken cancellationToken = default);

    Task<Medistock.Application.Sales.DTOs.SaleDetailDto?> GetSaleByInvoiceNoAsync(
        string orgId,
        string branchId,
        string invoiceNo,
        CancellationToken cancellationToken = default);

    Task<Medistock.Application.Sales.DTOs.SaleReturnResult> CommitSaleReturnAtomicAsync(
        SaleReturn saleReturn,
        List<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default);
}

public interface IGstReportRepository
{
    Task<Medistock.Application.Compliance.DTOs.Gstr1ReportDto> GenerateGstr1Async(
        Medistock.Application.Compliance.DTOs.GstPeriodFilter filter,
        CancellationToken cancellationToken = default);

    Task<Medistock.Application.Compliance.DTOs.Gstr2ReportDto> GenerateGstr2Async(
        Medistock.Application.Compliance.DTOs.GstPeriodFilter filter,
        CancellationToken cancellationToken = default);

    Task<Medistock.Application.Compliance.DTOs.Gstr3bReportDto> GenerateGstr3bAsync(
        Medistock.Application.Compliance.DTOs.GstPeriodFilter filter,
        CancellationToken cancellationToken = default);
}

public interface IStockTransferRepository
{
    Task<IReadOnlyList<Medistock.Application.Inventory.DTOs.StockTransferSummaryDto>> GetTransfersAsync(
        string orgId,
        string? branchId = null,
        StockTransferStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<Medistock.Application.Inventory.DTOs.StockTransferDetailDto?> GetTransferByIdAsync(
        string transferId,
        CancellationToken cancellationToken = default);

    Task<Medistock.Application.Inventory.DTOs.TransferOperationResult> CreateTransferRequestAsync(
        StockTransfer transfer,
        CancellationToken cancellationToken = default);

    Task<Medistock.Application.Inventory.DTOs.TransferOperationResult> DispatchTransferAtomicAsync(
        string transferId,
        string dispatchedByUserId,
        string deviceId,
        List<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default);

    Task<Medistock.Application.Inventory.DTOs.TransferOperationResult> ReceiveTransferAtomicAsync(
        string transferId,
        string receivedByUserId,
        string deviceId,
        Dictionary<string, decimal> receivedQuantities,
        List<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default);
}
