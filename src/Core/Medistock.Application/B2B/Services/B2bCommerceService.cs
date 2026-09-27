using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Purchases.DTOs;
using Medistock.Application.Purchases.Services;
using Medistock.Contracts.B2B;
using Medistock.Domain.B2B;
using Medistock.Domain.Common;

namespace Medistock.Application.B2B.Services;

public interface IB2bCommerceService
{
    Task<IReadOnlyList<Wholesaler>> GetWholesalersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WholesalerCatalogItemDto>> SearchCatalogAsync(string? wholesalerId = null, string? searchQuery = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<B2bOrderSummaryDto>> GetOrdersAsync(string orgId, string branchId, B2bOrderStatus? status = null, CancellationToken cancellationToken = default);
    Task<B2bOrder?> GetOrderByIdAsync(string orderId, CancellationToken cancellationToken = default);
    Task<B2bOrderSummaryDto> CreateAndSubmitOrderAsync(CreateB2bOrderRequest request, string orgId, string branchId, string userId, string deviceId, CancellationToken cancellationToken = default);
    Task UpdateOrderStatusAsync(string orderId, B2bOrderStatus newStatus, string? trackingNumber = null, CancellationToken cancellationToken = default);
    Task<PurchasePostingResult> ReceiveOrderAndConvertToPurchaseInvoiceAsync(string orderId, string warehouseId, string userId, string deviceId, string? customInvoiceNo = null, CancellationToken cancellationToken = default);
}

public class B2bCommerceService : IB2bCommerceService
{
    private readonly IB2bCommerceRepository _b2bRepository;
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierRepository _supplierRepository;

    public B2bCommerceService(
        IB2bCommerceRepository b2bRepository,
        IPurchaseService purchaseService,
        ISupplierRepository supplierRepository)
    {
        _b2bRepository = b2bRepository;
        _purchaseService = purchaseService;
        _supplierRepository = supplierRepository;
    }

    public Task<IReadOnlyList<Wholesaler>> GetWholesalersAsync(CancellationToken cancellationToken = default)
    {
        return _b2bRepository.GetWholesalersAsync(cancellationToken);
    }

    public Task<IReadOnlyList<WholesalerCatalogItemDto>> SearchCatalogAsync(
        string? wholesalerId = null,
        string? searchQuery = null,
        CancellationToken cancellationToken = default)
    {
        return _b2bRepository.SearchCatalogAsync(wholesalerId, searchQuery, limit: 100, cancellationToken);
    }

    public Task<IReadOnlyList<B2bOrderSummaryDto>> GetOrdersAsync(
        string orgId,
        string branchId,
        B2bOrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        return _b2bRepository.GetOrdersAsync(orgId, branchId, status, cancellationToken);
    }

    public Task<B2bOrder?> GetOrderByIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return _b2bRepository.GetOrderByIdAsync(orderId, cancellationToken);
    }

    public async Task<B2bOrderSummaryDto> CreateAndSubmitOrderAsync(
        CreateB2bOrderRequest request,
        string orgId,
        string branchId,
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new ArgumentException("B2B order must have at least one line item.");
        }

        var wholesaler = await _b2bRepository.GetWholesalerByIdAsync(request.WholesalerId, cancellationToken)
            ?? throw new InvalidOperationException($"Wholesaler not found with ID {request.WholesalerId}");

        var orderId = Guid.NewGuid().ToString("N");
        var orderNumber = $"PO-B2B-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var order = B2bOrder.Create(
            id: orderId,
            orgId: orgId,
            branchId: branchId,
            orderNumber: orderNumber,
            wholesalerId: request.WholesalerId,
            deliveryAddress: request.DeliveryAddress,
            notes: request.Notes
        );

        foreach (var item in request.Items)
        {
            var orderItem = B2bOrderItem.Create(
                id: Guid.NewGuid().ToString("N"),
                orderId: orderId,
                catalogId: item.CatalogId,
                productCode: item.ProductCode,
                productName: item.ProductName,
                orderQuantity: item.OrderQuantity,
                freeQuantity: item.FreeQuantity,
                unitWholesaleRate: item.UnitWholesaleRate,
                gstRate: item.GstRate
            );
            order.AddItem(orderItem);
        }

        order.SubmitOrder();

        // Enqueue Outbox event
        var outboxPayload = JsonSerializer.Serialize(new
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            WholesalerId = order.WholesalerId,
            WholesalerName = wholesaler.Name,
            TotalAmount = order.TotalAmount,
            ItemCount = order.Items.Count,
            OrderedAt = order.OrderedAt
        });

        var outboxEvent = OutboxEvent.Create(
            id: Guid.NewGuid().ToString("N"),
            aggregateType: "B2bOrder",
            aggregateId: order.Id,
            eventType: "B2bOrderSubmitted",
            payloadJson: outboxPayload,
            deviceId: deviceId,
            operationId: Guid.NewGuid().ToString("N")
        );

        await _b2bRepository.CreateOrderAtomicAsync(order, outboxEvent, cancellationToken);

        return new B2bOrderSummaryDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            WholesalerId = order.WholesalerId,
            WholesalerName = wholesaler.Name,
            Status = order.Status.ToString(),
            SubTotal = order.SubTotal,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount,
            TotalItems = order.Items.Count,
            OrderDateUtc = order.OrderedAt
        };
    }

    public async Task UpdateOrderStatusAsync(
        string orderId,
        B2bOrderStatus newStatus,
        string? trackingNumber = null,
        CancellationToken cancellationToken = default)
    {
        var order = await _b2bRepository.GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order not found with ID {orderId}");

        DateTime? deliveredAt = newStatus == B2bOrderStatus.Delivered ? DateTime.UtcNow : null;

        await _b2bRepository.UpdateOrderStatusAsync(
            orderId,
            newStatus,
            trackingNumber ?? order.DispatchTrackingNumber,
            deliveredAt,
            null,
            cancellationToken);
    }

    public async Task<PurchasePostingResult> ReceiveOrderAndConvertToPurchaseInvoiceAsync(
        string orderId,
        string warehouseId,
        string userId,
        string deviceId,
        string? customInvoiceNo = null,
        CancellationToken cancellationToken = default)
    {
        var order = await _b2bRepository.GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order not found with ID {orderId}");

        var wholesaler = await _b2bRepository.GetWholesalerByIdAsync(order.WholesalerId, cancellationToken)
            ?? throw new InvalidOperationException($"Wholesaler not found with ID {order.WholesalerId}");

        // Ensure wholesaler is registered as local supplier in suppliers table
        var localSupplier = await _supplierRepository.GetSupplierByIdAsync(wholesaler.Id, cancellationToken);
        if (localSupplier == null)
        {
            var newSupplier = Medistock.Domain.Purchases.Supplier.Create(
                id: wholesaler.Id,
                orgId: order.OrgId,
                name: wholesaler.Name,
                gstin: wholesaler.Gstin,
                dlNumber: wholesaler.DrugLicenseNo,
                phone: wholesaler.Phone,
                email: wholesaler.Email,
                address: $"{wholesaler.City}, {wholesaler.State}",
                creditDays: wholesaler.CreditDays,
                openingBalance: 0
            );
            await _supplierRepository.CreateSupplierAsync(newSupplier, cancellationToken);
        }

        var supplierInvoiceNo = customInvoiceNo ?? $"INV-{order.OrderNumber}";
        var today = DateTime.UtcNow;

        // Build CreatePurchaseInvoiceCommand items
        var purchaseItems = new List<PurchaseInvoiceItemInputDto>();
        foreach (var item in order.Items)
        {
            purchaseItems.Add(new PurchaseInvoiceItemInputDto(
                ProductId: item.ProductCode, // maps to product code / id
                ProductName: item.ProductName,
                HsnCode: "3004",
                BatchNumber: $"B2B-{DateTime.UtcNow:MMdd}-{Random.Shared.Next(100, 999)}",
                ExpiryDate: DateTime.UtcNow.AddMonths(18),
                ManufacturingDate: DateTime.UtcNow.AddMonths(-2),
                Quantity: item.OrderQuantity,
                FreeQuantity: item.FreeQuantity,
                UnitPrice: item.UnitWholesaleRate,
                Mrp: Math.Round(item.UnitWholesaleRate * 1.35m, 2),
                SaleRate: Math.Round(item.UnitWholesaleRate * 1.20m, 2),
                DiscountPct: 0,
                GstRatePercent: item.GstRate
            ));
        }

        var command = new CreatePurchaseInvoiceCommand(
            OrgId: order.OrgId,
            BranchId: order.BranchId,
            WarehouseId: warehouseId,
            SupplierId: wholesaler.Id,
            SupplierName: wholesaler.Name,
            SupplierGstin: wholesaler.Gstin,
            SupplierInvoiceNo: supplierInvoiceNo,
            SupplierInvoiceDate: today,
            IsInterstate: false,
            CreatedByUserId: userId,
            Notes: $"Auto-converted from B2B Purchase Order #{order.OrderNumber}",
            Items: purchaseItems
        );

        var result = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(command, cancellationToken);

        if (result.Success && result.PurchaseInvoiceId != null)
        {
            await _b2bRepository.UpdateOrderStatusAsync(
                orderId,
                B2bOrderStatus.Delivered,
                order.DispatchTrackingNumber,
                DateTime.UtcNow,
                result.PurchaseInvoiceId,
                cancellationToken);
        }

        return result;
    }
}
