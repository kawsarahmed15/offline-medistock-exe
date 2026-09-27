using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.B2B;
using Medistock.Domain.B2B;
using Medistock.Domain.Common;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteB2bCommerceRepository : IB2bCommerceRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteB2bCommerceRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Wholesaler>> GetWholesalersAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = @"
            SELECT 
                wholesaler_id AS Id,
                name AS Name,
                gstin AS Gstin,
                drug_license_no AS DrugLicenseNo,
                phone AS Phone,
                email AS Email,
                city AS City,
                state AS State,
                min_order_value AS MinOrderValue,
                credit_days AS CreditDays,
                is_verified AS IsVerified,
                rating AS Rating
            FROM b2b_wholesalers
            ORDER BY rating DESC;";

        var rows = await connection.QueryAsync<dynamic>(sql);
        var list = rows.Select<dynamic, Wholesaler>(r => Wholesaler.Create(
            id: (string)r.Id,
            name: (string)r.Name,
            gstin: (string)r.Gstin,
            drugLicenseNo: (string)r.DrugLicenseNo,
            phone: (string)r.Phone,
            email: (string)r.Email,
            city: (string)r.City,
            state: (string)r.State,
            minOrderValue: Convert.ToDecimal(r.MinOrderValue),
            creditDays: Convert.ToInt32(r.CreditDays),
            rating: Convert.ToDouble(r.Rating)
        )).ToList();

        return list;
    }

    public async Task<Wholesaler?> GetWholesalerByIdAsync(string wholesalerId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = @"
            SELECT 
                wholesaler_id AS Id,
                name AS Name,
                gstin AS Gstin,
                drug_license_no AS DrugLicenseNo,
                phone AS Phone,
                email AS Email,
                city AS City,
                state AS State,
                min_order_value AS MinOrderValue,
                credit_days AS CreditDays,
                is_verified AS IsVerified,
                rating AS Rating
            FROM b2b_wholesalers
            WHERE wholesaler_id = @wholesalerId;";

        var r = await connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { wholesalerId });
        if (r == null) return null;

        return Wholesaler.Create(
            id: (string)r.Id,
            name: (string)r.Name,
            gstin: (string)r.Gstin,
            drugLicenseNo: (string)r.DrugLicenseNo,
            phone: (string)r.Phone,
            email: (string)r.Email,
            city: (string)r.City,
            state: (string)r.State,
            minOrderValue: Convert.ToDecimal(r.MinOrderValue),
            creditDays: Convert.ToInt32(r.CreditDays),
            rating: Convert.ToDouble(r.Rating)
        );
    }

    public async Task<IReadOnlyList<WholesalerCatalogItemDto>> SearchCatalogAsync(
        string? wholesalerId = null,
        string? searchQuery = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var sql = @"
            SELECT 
                c.catalog_id AS CatalogId,
                c.wholesaler_id AS WholesalerId,
                w.name AS WholesalerName,
                c.product_code AS ProductCode,
                c.brand_name AS BrandName,
                c.generic_name AS GenericName,
                c.dosage_form AS DosageForm,
                c.strength AS Strength,
                c.manufacturer AS Manufacturer,
                c.hsn_code AS HsnCode,
                c.mrp AS Mrp,
                c.wholesale_rate AS WholesaleRate,
                c.gst_rate AS GstRate,
                c.available_stock AS AvailableStock,
                c.scheme_description AS SchemeDescription,
                c.min_order_qty AS MinimumOrderQuantity,
                c.free_ratio_buy AS FreeRatioBuy,
                c.free_ratio_get AS FreeRatioGet
            FROM b2b_wholesaler_catalogs c
            JOIN b2b_wholesalers w ON c.wholesaler_id = w.wholesaler_id
            WHERE (@wholesalerId IS NULL OR c.wholesaler_id = @wholesalerId)
              AND (@searchQuery IS NULL OR c.brand_name LIKE @pattern OR c.generic_name LIKE @pattern OR c.product_code LIKE @pattern)
            ORDER BY c.brand_name ASC
            LIMIT @limit;";

        var items = (await connection.QueryAsync<WholesalerCatalogItemDto>(sql, new
        {
            wholesalerId,
            searchQuery,
            pattern = $"%{searchQuery}%",
            limit
        })).ToList();

        return items;
    }

    public async Task<IReadOnlyList<B2bOrderSummaryDto>> GetOrdersAsync(
        string orgId,
        string branchId,
        B2bOrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var sql = @"
            SELECT 
                o.order_id AS OrderId,
                o.order_number AS OrderNumber,
                o.wholesaler_id AS WholesalerId,
                w.name AS WholesalerName,
                o.status AS Status,
                o.sub_total AS SubTotal,
                o.tax_amount AS TaxAmount,
                o.total_amount AS TotalAmount,
                (SELECT COUNT(1) FROM b2b_purchase_order_items i WHERE i.order_id = o.order_id) AS TotalItems,
                o.ordered_at AS OrderDateUtcStr,
                o.expected_delivery AS ExpectedDeliveryUtcStr,
                o.dispatch_tracking_no AS DispatchTrackingNumber
            FROM b2b_purchase_orders o
            JOIN b2b_wholesalers w ON o.wholesaler_id = w.wholesaler_id
            WHERE o.org_id = @orgId
              AND (o.branch_id = @branchId OR @branchId = '')
              AND (@statusStr IS NULL OR o.status = @statusStr)
            ORDER BY o.ordered_at DESC;";

        var rows = (await connection.QueryAsync<dynamic>(sql, new
        {
            orgId,
            branchId,
            statusStr = status?.ToString()
        })).ToList();

        var dtos = rows.Select<dynamic, B2bOrderSummaryDto>(r => new B2bOrderSummaryDto
        {
            OrderId = (string)r.OrderId,
            OrderNumber = (string)r.OrderNumber,
            WholesalerId = (string)r.WholesalerId,
            WholesalerName = (string)r.WholesalerName,
            Status = (string)r.Status,
            SubTotal = Convert.ToDecimal(r.SubTotal),
            TaxAmount = Convert.ToDecimal(r.TaxAmount),
            TotalAmount = Convert.ToDecimal(r.TotalAmount),
            TotalItems = Convert.ToInt32(r.TotalItems),
            OrderDateUtc = DateTime.TryParse((string)r.OrderDateUtcStr, out DateTime dt) ? dt : DateTime.UtcNow,
            ExpectedDeliveryUtc = DateTime.TryParse((string)r.ExpectedDeliveryUtcStr, out DateTime edt) ? edt : null,
            DispatchTrackingNumber = (string?)r.DispatchTrackingNumber
        }).ToList();

        return dtos;
    }

    public async Task<B2bOrder?> GetOrderByIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        const string orderSql = @"
            SELECT 
                order_id AS Id,
                org_id AS OrgId,
                branch_id AS BranchId,
                order_number AS OrderNumber,
                wholesaler_id AS WholesalerId,
                status AS StatusStr,
                sub_total AS SubTotal,
                tax_amount AS TaxAmount,
                total_amount AS TotalAmount,
                delivery_address AS DeliveryAddress,
                notes AS Notes,
                dispatch_tracking_no AS DispatchTrackingNumber,
                ordered_at AS OrderedAtStr,
                expected_delivery AS ExpectedDeliveryStr,
                delivered_at AS DeliveredAtStr,
                converted_purchase_invoice_id AS ConvertedPurchaseInvoiceId
            FROM b2b_purchase_orders
            WHERE order_id = @orderId;";

        var o = await connection.QuerySingleOrDefaultAsync<dynamic>(orderSql, new { orderId });
        if (o == null) return null;

        var order = B2bOrder.Create(
            id: (string)o.Id,
            orgId: (string)o.OrgId,
            branchId: (string)o.BranchId,
            orderNumber: (string)o.OrderNumber,
            wholesalerId: (string)o.WholesalerId,
            deliveryAddress: (string)o.DeliveryAddress,
            notes: (string?)o.Notes
        );

        const string itemsSql = @"
            SELECT 
                order_item_id AS Id,
                order_id AS OrderId,
                catalog_id AS CatalogId,
                product_code AS ProductCode,
                product_name AS ProductName,
                order_qty AS OrderQuantity,
                free_qty AS FreeQuantity,
                unit_wholesale_rate AS UnitWholesaleRate,
                gst_rate AS GstRate
            FROM b2b_purchase_order_items
            WHERE order_id = @orderId;";

        var items = (await connection.QueryAsync<dynamic>(itemsSql, new { orderId })).ToList();
        foreach (var i in items)
        {
            var item = B2bOrderItem.Create(
                id: (string)i.Id,
                orderId: (string)i.OrderId,
                catalogId: (string)i.CatalogId,
                productCode: (string)i.ProductCode,
                productName: (string)i.ProductName,
                orderQuantity: Convert.ToInt32(i.OrderQuantity),
                freeQuantity: Convert.ToInt32(i.FreeQuantity),
                unitWholesaleRate: Convert.ToDecimal(i.UnitWholesaleRate),
                gstRate: Convert.ToDecimal(i.GstRate)
            );
            order.AddItem(item);
        }

        if (Enum.TryParse<B2bOrderStatus>((string)o.StatusStr, out var status))
        {
            if (status == B2bOrderStatus.Submitted) order.SubmitOrder();
            else if (status == B2bOrderStatus.Confirmed) order.ConfirmOrder(DateTime.UtcNow.AddDays(2));
            else if (status == B2bOrderStatus.Dispatched) order.MarkDispatched((string?)o.DispatchTrackingNumber ?? "TRK-B2B");
            else if (status == B2bOrderStatus.Delivered) order.MarkDelivered((string?)o.ConvertedPurchaseInvoiceId);
            else if (status == B2bOrderStatus.Cancelled) order.CancelOrder("Cancelled");
        }

        return order;
    }

    public async Task CreateOrderAtomicAsync(
        B2bOrder order,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            const string orderSql = @"
                INSERT INTO b2b_purchase_orders (
                    order_id, org_id, branch_id, order_number, wholesaler_id,
                    status, sub_total, tax_amount, total_amount, delivery_address,
                    notes, ordered_at
                ) VALUES (
                    @Id, @OrgId, @BranchId, @OrderNumber, @WholesalerId,
                    @StatusStr, @SubTotal, @TaxAmount, @TotalAmount, @DeliveryAddress,
                    @Notes, @OrderedAt
                );";

            await connection.ExecuteAsync(orderSql, new
            {
                order.Id,
                order.OrgId,
                order.BranchId,
                order.OrderNumber,
                order.WholesalerId,
                StatusStr = order.Status.ToString(),
                order.SubTotal,
                order.TaxAmount,
                order.TotalAmount,
                order.DeliveryAddress,
                order.Notes,
                OrderedAt = order.OrderedAt.ToString("o")
            }, transaction);

            const string itemSql = @"
                INSERT INTO b2b_purchase_order_items (
                    order_item_id, order_id, catalog_id, product_code, product_name,
                    order_qty, free_qty, unit_wholesale_rate, gst_rate, tax_amount, total_amount
                ) VALUES (
                    @Id, @OrderId, @CatalogId, @ProductCode, @ProductName,
                    @OrderQuantity, @FreeQuantity, @UnitWholesaleRate, @GstRate, @TaxAmount, @TotalAmount
                );";

            foreach (var item in order.Items)
            {
                await connection.ExecuteAsync(itemSql, new
                {
                    item.Id,
                    item.OrderId,
                    item.CatalogId,
                    item.ProductCode,
                    item.ProductName,
                    item.OrderQuantity,
                    item.FreeQuantity,
                    item.UnitWholesaleRate,
                    item.GstRate,
                    item.TaxAmount,
                    item.TotalAmount
                }, transaction);
            }

            const string outboxSql = @"
                INSERT INTO outbox_events (
                    id, aggregate_type, aggregate_id, event_type, payload_json,
                    device_id, operation_id, created_at, status
                ) VALUES (
                    @Id, @AggregateType, @AggregateId, @EventType, @PayloadJson,
                    @DeviceId, @OperationId, @CreatedAt, @Status
                );";

            await connection.ExecuteAsync(outboxSql, new
            {
                outboxEvent.Id,
                outboxEvent.AggregateType,
                outboxEvent.AggregateId,
                outboxEvent.EventType,
                outboxEvent.PayloadJson,
                outboxEvent.DeviceId,
                outboxEvent.OperationId,
                CreatedAt = outboxEvent.CreatedAt.ToString("o"),
                Status = (int)outboxEvent.Status
            }, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateOrderStatusAsync(
        string orderId,
        B2bOrderStatus newStatus,
        string? trackingNo = null,
        DateTime? deliveredAt = null,
        string? convertedPurchaseInvoiceId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = @"
            UPDATE b2b_purchase_orders
            SET 
                status = @StatusStr,
                dispatch_tracking_no = COALESCE(@trackingNo, dispatch_tracking_no),
                delivered_at = COALESCE(@DeliveredAtStr, delivered_at),
                converted_purchase_invoice_id = COALESCE(@convertedPurchaseInvoiceId, converted_purchase_invoice_id)
            WHERE order_id = @orderId;";

        await connection.ExecuteAsync(sql, new
        {
            orderId,
            StatusStr = newStatus.ToString(),
            trackingNo,
            DeliveredAtStr = deliveredAt?.ToString("o"),
            convertedPurchaseInvoiceId
        });
    }
}
