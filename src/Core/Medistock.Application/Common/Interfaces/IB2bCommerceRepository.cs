using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.B2B;
using Medistock.Domain.B2B;
using Medistock.Domain.Common;

namespace Medistock.Application.Common.Interfaces;

public interface IB2bCommerceRepository
{
    Task<IReadOnlyList<Wholesaler>> GetWholesalersAsync(CancellationToken cancellationToken = default);
    Task<Wholesaler?> GetWholesalerByIdAsync(string wholesalerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WholesalerCatalogItemDto>> SearchCatalogAsync(
        string? wholesalerId = null,
        string? searchQuery = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<B2bOrderSummaryDto>> GetOrdersAsync(
        string orgId,
        string branchId,
        B2bOrderStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<B2bOrder?> GetOrderByIdAsync(string orderId, CancellationToken cancellationToken = default);

    Task CreateOrderAtomicAsync(
        B2bOrder order,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default);

    Task UpdateOrderStatusAsync(
        string orderId,
        B2bOrderStatus newStatus,
        string? trackingNo = null,
        DateTime? deliveredAt = null,
        string? convertedPurchaseInvoiceId = null,
        CancellationToken cancellationToken = default);
}
