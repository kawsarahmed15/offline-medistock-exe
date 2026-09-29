using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Purchases.DTOs;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;
using Medistock.Domain.Purchases;

namespace Medistock.Application.Common.Interfaces;

public interface ISupplierRepository
{
    Task<IReadOnlyList<SupplierDto>> GetAllSuppliersAsync(string orgId, CancellationToken cancellationToken = default);
    Task<SupplierDto?> GetSupplierByIdAsync(string supplierId, CancellationToken cancellationToken = default);
    Task<string> CreateSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task UpdateOutstandingBalanceAsync(string supplierId, decimal delta, System.Data.Common.DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}

public interface IPurchaseRepository
{
    Task<PurchasePostingResult> PostPurchaseInvoiceAtomicAsync(
        PurchaseInvoice invoice,
        IReadOnlyList<Batch> batchesToUpsert,
        IReadOnlyList<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseInvoiceSummaryDto>> GetPurchaseInvoicesAsync(
        string orgId,
        string branchId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<PurchaseInvoice?> GetPurchaseInvoiceByIdAsync(
        string invoiceId,
        CancellationToken cancellationToken = default);

    Task<PurchaseInvoiceDetailsDto?> GetPurchaseInvoiceDetailsAsync(
        string invoiceId,
        CancellationToken cancellationToken = default);

    Task<PurchaseKpiSummaryDto> GetPurchaseKpiSummaryAsync(
        string orgId,
        string branchId,
        string period = "All",
        CancellationToken cancellationToken = default);

    Task<bool> IsDuplicateInvoiceAsync(
        string orgId,
        string supplierId,
        string supplierInvoiceNo,
        CancellationToken cancellationToken = default);

    Task<PurchaseCancelResult> CancelPurchaseInvoiceAsync(
        string invoiceId,
        string cancelledByUserId,
        CancellationToken cancellationToken = default);
}
