using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Products.Queries;
using Medistock.Domain.Common;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteProductSearchRepository : IProductSearchRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteProductSearchRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ProductSearchDto>> SearchProductsAsync(
        string query,
        string warehouseId,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ProductSearchDto>();
        }

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var trimmed = query.Trim();
        var cleaned = new string(trimmed.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());
        var terms = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return Array.Empty<ProductSearchDto>();
        }

        var ftsQuery = string.Join(" ", terms.Select(t => $"{t}*"));

        const string sql = @"
            SELECT 
                p.id AS Id,
                p.name AS Name,
                p.brand_name AS BrandName,
                p.generic_name AS GenericName,
                p.composition AS Composition,
                p.strength AS Strength,
                p.dosage_form AS DosageForm,
                (p.pack_units || ' ' || p.base_unit || '/Pack') AS PackSizeDescription,
                p.hsn_code AS HsnCode,
                CAST(p.gst_rate_percent AS REAL) AS GstRatePercent,
                p.schedule AS Schedule,
                p.is_prescription_required AS IsPrescriptionRequired,
                p.is_cold_chain AS IsColdChain,
                p.is_narcotic AS IsNarcotic,
                p.manufacturer_name AS ManufacturerName,
                p.primary_barcode AS Barcode,
                b.id AS BatchId,
                b.batch_number AS BatchNumber,
                b.expiry_date AS NearestExpiryDate,
                CAST(IFNULL(b.mrp, 0.0) AS REAL) AS Mrp,
                CAST(IFNULL(b.sale_rate, IFNULL(b.mrp, 0.0)) AS REAL) AS SaleRate,
                CAST(IFNULL(sb.quantity - sb.reserved_quantity, 0.0) AS REAL) AS AvailableQuantity
            FROM fts_products fts
            JOIN products p ON p.id = fts.product_id
            LEFT JOIN (
                SELECT b_in.id, b_in.product_id, b_in.batch_number, b_in.expiry_date, b_in.mrp, b_in.sale_rate
                FROM batches b_in
                WHERE date(b_in.expiry_date) >= date('now')
                GROUP BY b_in.product_id
                HAVING MIN(date(b_in.expiry_date))
            ) b ON b.product_id = p.id
            LEFT JOIN stock_balances sb ON sb.batch_id = b.id AND sb.warehouse_id = @warehouseId
            WHERE fts_products MATCH @ftsQuery AND p.is_active = 1
            ORDER BY rank
            LIMIT @limit;
        ";

        var command = new CommandDefinition(
            sql,
            new { ftsQuery, warehouseId, limit },
            cancellationToken: cancellationToken);

        var products = (await connection.QueryAsync<ProductSearchDto>(command)).ToList();

        if (products.Count > 0)
        {
            var productIds = products.Select(p => p.Id).ToList();
            const string batchSql = @"
                SELECT 
                    b.id AS Id,
                    b.product_id AS ProductId,
                    b.batch_number AS BatchNumber,
                    b.expiry_date AS ExpiryDate,
                    CAST(b.mrp AS REAL) AS Mrp,
                    CAST(b.purchase_rate AS REAL) AS PurchaseRate,
                    CAST(b.sale_rate AS REAL) AS SaleRate,
                    CAST(IFNULL(sb.quantity - sb.reserved_quantity, 0.0) AS REAL) AS AvailableQuantity
                FROM batches b
                LEFT JOIN stock_balances sb ON sb.batch_id = b.id AND sb.warehouse_id = @warehouseId
                WHERE b.product_id IN @productIds
                ORDER BY b.expiry_date ASC;
            ";

            var batches = (await connection.QueryAsync<ProductBatchDto>(
                new CommandDefinition(batchSql, new { productIds, warehouseId }, cancellationToken: cancellationToken)
            )).ToList();

            var batchMap = batches.GroupBy(b => b.ProductId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var p in products)
            {
                if (batchMap.TryGetValue(p.Id, out var pBatches))
                {
                    p.Batches = pBatches;
                }
            }
        }

        return products;
    }

    public async Task<IReadOnlyList<ProductBatchDto>> GetBatchesForProductAsync(
        string productId,
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return Array.Empty<ProductBatchDto>();
        }

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                b.id AS Id,
                b.product_id AS ProductId,
                b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDate,
                CAST(b.mrp AS REAL) AS Mrp,
                CAST(b.purchase_rate AS REAL) AS PurchaseRate,
                CAST(b.sale_rate AS REAL) AS SaleRate,
                CAST(IFNULL(sb.quantity - sb.reserved_quantity, 0.0) AS REAL) AS AvailableQuantity
            FROM batches b
            LEFT JOIN stock_balances sb ON sb.batch_id = b.id AND sb.warehouse_id = @warehouseId
            WHERE b.product_id = @productId
            ORDER BY b.expiry_date ASC;
        ";

        var command = new CommandDefinition(
            sql,
            new { productId, warehouseId },
            cancellationToken: cancellationToken);

        var batches = await connection.QueryAsync<ProductBatchDto>(command);
        return batches.ToList();
    }

    public async Task<BarcodeLookupDto?> LookupByBarcodeAsync(
        string barcode,
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                p.id AS ProductId,
                p.name AS ProductName,
                p.generic_name AS GenericName,
                b.id AS BatchId,
                b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDate,
                CAST(IFNULL(b.mrp, 0.0) AS REAL) AS Mrp,
                CAST(IFNULL(b.sale_rate, IFNULL(b.mrp, 0.0)) AS REAL) AS SaleRate,
                CAST(p.gst_rate_percent AS REAL) AS GstRatePercent,
                p.schedule AS Schedule,
                p.is_cold_chain AS IsColdChain,
                CAST(IFNULL(sb.quantity - sb.reserved_quantity, 0.0) AS REAL) AS AvailableQuantity,
                @barcode AS Barcode
            FROM products p
            LEFT JOIN product_barcodes pb ON pb.product_id = p.id
            LEFT JOIN (
                SELECT b_in.id, b_in.product_id, b_in.batch_number, b_in.expiry_date, b_in.mrp, b_in.sale_rate
                FROM batches b_in
                WHERE date(b_in.expiry_date) >= date('now')
                GROUP BY b_in.product_id
                HAVING MIN(date(b_in.expiry_date))
            ) b ON b.product_id = p.id
            LEFT JOIN stock_balances sb ON sb.batch_id = b.id AND sb.warehouse_id = @warehouseId
            WHERE (p.primary_barcode = @barcode OR pb.barcode = @barcode) AND p.is_active = 1
            LIMIT 1;
        ";

        var command = new CommandDefinition(
            sql,
            new { barcode = barcode.Trim(), warehouseId },
            cancellationToken: cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<BarcodeLookupDto>(command);
    }
}
