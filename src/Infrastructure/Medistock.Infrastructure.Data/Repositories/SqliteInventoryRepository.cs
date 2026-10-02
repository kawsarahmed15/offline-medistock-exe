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
                CAST(IFNULL(p.gst_rate_percent, 0.0) AS REAL) AS GstRatePercent,
                p.hsn_code AS HsnCode,
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
            decimal gstRatePercent = r.GstRatePercent != null ? Convert.ToDecimal(r.GstRatePercent) : 0.0m;
            decimal netRate = purchaseRate * (1m + (gstRatePercent / 100m));
            decimal stockValueAtCost = Math.Round(availQty * netRate, 2, MidpointRounding.AwayFromZero);

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
                StockValueAtCost: stockValueAtCost,
                MinStockAlert: minStockAlert,
                GstRatePercent: gstRatePercent,
                NetPurchaseRate: Math.Round(netRate, 2, MidpointRounding.AwayFromZero),
                HsnCode: (string)(r.HsnCode ?? "3004")
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

    public async Task<UpdateProductDetailsResult> UpdateProductDetailsAsync(
        UpdateProductDetailsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.ProductId))
            return new UpdateProductDetailsResult(false, "Product ID cannot be empty.");

        if (string.IsNullOrWhiteSpace(command.ProductName))
            return new UpdateProductDetailsResult(false, "Product name cannot be empty.");

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            const string updateProductSql = @"
                UPDATE products SET
                    name = @ProductName,
                    generic_name = @GenericName,
                    composition = @Composition,
                    manufacturer_name = @Manufacturer,
                    base_unit = @CategoryName,
                    hsn_code = @HsnCode,
                    gst_rate_percent = @GstRatePercent,
                    schedule = @Schedule,
                    min_stock_alert = @MinStockAlert
                WHERE id = @ProductId;
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                updateProductSql,
                new
                {
                    command.ProductId,
                    command.ProductName,
                    command.GenericName,
                    Composition = command.SaltComposition,
                    command.Manufacturer,
                    command.CategoryName,
                    command.HsnCode,
                    GstRatePercent = (double)command.GstRatePercent,
                    Schedule = (int)command.Schedule,
                    MinStockAlert = (double)command.MinStockAlert
                },
                transaction, cancellationToken: cancellationToken));

            if (!string.IsNullOrWhiteSpace(command.BatchId))
            {
                const string updateBatchSql = @"
                    UPDATE batches SET
                        batch_number = COALESCE(@BatchNumber, batch_number),
                        expiry_date = CASE WHEN @ExpiryDate IS NOT NULL THEN @ExpiryDate ELSE expiry_date END,
                        mrp = CASE WHEN @Mrp IS NOT NULL THEN @Mrp ELSE mrp END,
                        purchase_rate = CASE WHEN @PurchaseRate IS NOT NULL THEN @PurchaseRate ELSE purchase_rate END,
                        sale_rate = CASE WHEN @SaleRate IS NOT NULL THEN @SaleRate ELSE sale_rate END
                    WHERE id = @BatchId;
                ";

                await connection.ExecuteAsync(new CommandDefinition(
                    updateBatchSql,
                    new
                    {
                        command.BatchId,
                        command.BatchNumber,
                        ExpiryDate = command.ExpiryDate?.ToString("o"),
                        Mrp = command.Mrp.HasValue ? (double)command.Mrp.Value : (double?)null,
                        PurchaseRate = command.PurchaseRate.HasValue ? (double)command.PurchaseRate.Value : (double?)null,
                        SaleRate = command.SaleRate.HasValue ? (double)command.SaleRate.Value : (double?)null
                    },
                    transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return new UpdateProductDetailsResult(true);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new UpdateProductDetailsResult(false, ex.Message);
        }
    }

    public async Task<InventoryFinancialMetricsDto> GetFinancialMetricsAsync(
        string? monthPrefix = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var targetMonth = string.IsNullOrWhiteSpace(monthPrefix)
            ? DateTime.Now.ToString("yyyy-MM")
            : monthPrefix;

        // 1. Revenue this month & all-time revenue (excluding cancelled sales status = 3)
        const string revenueSql = @"
            SELECT
                COALESCE(SUM(CASE WHEN substr(s.invoice_date, 1, 7) = @targetMonth THEN s.total ELSE 0 END), 0) AS RevenueThisMonth,
                COUNT(DISTINCT CASE WHEN substr(s.invoice_date, 1, 7) = @targetMonth THEN s.id END) AS MonthlyInvoicesCount,
                COALESCE(SUM(s.total), 0) AS AllTimeRevenue
            FROM sales s
            WHERE s.status != 3;
        ";

        var revResult = await connection.QueryFirstOrDefaultAsync<dynamic>(
            new CommandDefinition(revenueSql, new { targetMonth }, cancellationToken: cancellationToken));

        decimal revMonth = 0m;
        int invCount = 0;
        decimal revAllTime = 0m;

        if (revResult != null)
        {
            revMonth = Convert.ToDecimal(revResult.RevenueThisMonth ?? 0.0);
            invCount = Convert.ToInt32(revResult.MonthlyInvoicesCount ?? 0);
            revAllTime = Convert.ToDecimal(revResult.AllTimeRevenue ?? 0.0);
        }

        // 2. Cash and Online Collections (Cash = 1, Card = 2, Upi = 3)
        // If sale_payments has no entry for a sale, default to Cash for backward compatibility
        const string paymentsSql = @"
            SELECT
                -- Monthly Cash Collection
                COALESCE(SUM(CASE 
                    WHEN substr(s.invoice_date, 1, 7) = @targetMonth AND (sp.payment_mode = 1 OR sp.id IS NULL) 
                    THEN COALESCE(sp.amount, s.total) 
                    ELSE 0 
                END), 0) AS CashCollectionThisMonth,
                COUNT(DISTINCT CASE 
                    WHEN substr(s.invoice_date, 1, 7) = @targetMonth AND (sp.payment_mode = 1 OR sp.id IS NULL) 
                    THEN s.id 
                END) AS CashInvoicesCount,

                -- Monthly Online Collection (Card = 2, UPI = 3)
                COALESCE(SUM(CASE 
                    WHEN substr(s.invoice_date, 1, 7) = @targetMonth AND sp.payment_mode IN (2, 3) 
                    THEN sp.amount 
                    ELSE 0 
                END), 0) AS OnlineCollectionThisMonth,
                COUNT(DISTINCT CASE 
                    WHEN substr(s.invoice_date, 1, 7) = @targetMonth AND sp.payment_mode IN (2, 3) 
                    THEN s.id 
                END) AS OnlineInvoicesCount,

                -- All-time Cash Collection
                COALESCE(SUM(CASE 
                    WHEN sp.payment_mode = 1 OR sp.id IS NULL 
                    THEN COALESCE(sp.amount, s.total) 
                    ELSE 0 
                END), 0) AS AllTimeCash,

                -- All-time Online Collection
                COALESCE(SUM(CASE 
                    WHEN sp.payment_mode IN (2, 3) 
                    THEN sp.amount 
                    ELSE 0 
                END), 0) AS AllTimeOnline
            FROM sales s
            LEFT JOIN sale_payments sp ON sp.sale_id = s.id
            WHERE s.status != 3;
        ";

        var payResult = await connection.QueryFirstOrDefaultAsync<dynamic>(
            new CommandDefinition(paymentsSql, new { targetMonth }, cancellationToken: cancellationToken));

        decimal cashMonth = 0m;
        int cashCount = 0;
        decimal onlineMonth = 0m;
        int onlineCount = 0;
        decimal cashAllTime = 0m;
        decimal onlineAllTime = 0m;

        if (payResult != null)
        {
            cashMonth = Convert.ToDecimal(payResult.CashCollectionThisMonth ?? 0.0);
            cashCount = Convert.ToInt32(payResult.CashInvoicesCount ?? 0);
            onlineMonth = Convert.ToDecimal(payResult.OnlineCollectionThisMonth ?? 0.0);
            onlineCount = Convert.ToInt32(payResult.OnlineInvoicesCount ?? 0);
            cashAllTime = Convert.ToDecimal(payResult.AllTimeCash ?? 0.0);
            onlineAllTime = Convert.ToDecimal(payResult.AllTimeOnline ?? 0.0);
        }

        // 3. Profit on completed sales (MRP - Buying Price) * Sold Quantity
        const string profitSql = @"
            SELECT
                -- Monthly Profit on sales
                COALESCE(SUM(CASE 
                    WHEN substr(s.invoice_date, 1, 7) = @targetMonth 
                    THEN (si.mrp - COALESCE(b.purchase_rate, 0)) * si.quantity 
                    ELSE 0 
                END), 0) AS EstimatedProfitThisMonth,

                -- All-time Profit on sales
                COALESCE(SUM((si.mrp - COALESCE(b.purchase_rate, 0)) * si.quantity), 0) AS AllTimeEstimatedProfit,

                -- Sold Items Count
                COUNT(DISTINCT CASE WHEN substr(s.invoice_date, 1, 7) = @targetMonth THEN si.id END) AS SoldItemsCountThisMonth,

                -- Monthly MRP valuation of sold items for Margin %
                COALESCE(SUM(CASE 
                    WHEN substr(s.invoice_date, 1, 7) = @targetMonth 
                    THEN si.mrp * si.quantity 
                    ELSE 0 
                END), 0) AS SoldMrpValueThisMonth,

                -- All-time MRP valuation of sold items
                COALESCE(SUM(si.mrp * si.quantity), 0) AS SoldMrpValueAllTime
            FROM sale_items si
            JOIN sales s ON s.id = si.sale_id
            LEFT JOIN batches b ON (b.id = si.batch_id OR (b.batch_number = si.batch_number AND b.product_id = si.product_id))
            WHERE s.status != 3;
        ";

        var profitResult = await connection.QueryFirstOrDefaultAsync<dynamic>(
            new CommandDefinition(profitSql, new { targetMonth }, cancellationToken: cancellationToken));

        decimal profitMonth = 0m;
        decimal profitAllTime = 0m;
        int soldItemsCount = 0;
        decimal soldMrpMonth = 0m;
        decimal soldMrpAllTime = 0m;

        if (profitResult != null)
        {
            profitMonth = Convert.ToDecimal(profitResult.EstimatedProfitThisMonth ?? 0.0);
            profitAllTime = Convert.ToDecimal(profitResult.AllTimeEstimatedProfit ?? 0.0);
            soldItemsCount = Convert.ToInt32(profitResult.SoldItemsCountThisMonth ?? 0);
            soldMrpMonth = Convert.ToDecimal(profitResult.SoldMrpValueThisMonth ?? 0.0);
            soldMrpAllTime = Convert.ToDecimal(profitResult.SoldMrpValueAllTime ?? 0.0);
        }

        decimal marginPct = 0m;
        if (profitMonth > 0 && soldMrpMonth > 0)
        {
            marginPct = Math.Round((profitMonth / soldMrpMonth) * 100m, 1);
        }
        else if (profitAllTime > 0 && soldMrpAllTime > 0)
        {
            marginPct = Math.Round((profitAllTime / soldMrpAllTime) * 100m, 1);
        }

        return new InventoryFinancialMetricsDto(
            RevenueThisMonth: revMonth,
            MonthlyInvoicesCount: invCount,
            CashCollectionThisMonth: cashMonth,
            CashInvoicesCount: cashCount,
            OnlineCollectionThisMonth: onlineMonth,
            OnlineInvoicesCount: onlineCount,
            AllTimeRevenue: revAllTime,
            AllTimeCash: cashAllTime,
            AllTimeOnline: onlineAllTime,
            EstimatedProfitThisMonth: profitMonth,
            AllTimeEstimatedProfit: profitAllTime,
            SoldItemsCount: soldItemsCount,
            ProfitMarginPercent: marginPct
        );
    }

    public async Task<IReadOnlyList<MetricDetailItemDto>> GetFinancialMetricDetailsAsync(
        string metricType,
        string? monthPrefix = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var targetMonth = !string.IsNullOrWhiteSpace(monthPrefix)
            ? monthPrefix
            : DateTime.UtcNow.ToString("yyyy-MM");

        var list = new List<MetricDetailItemDto>();

        if (metricType == "RevenueThisMonth")
        {
            var sql = @"
                SELECT 
                    s.invoice_no AS InvoiceNo,
                    s.invoice_date AS InvoiceDate,
                    COALESCE(s.customer_name, 'Walk-in Customer') AS CustomerName,
                    (SELECT COUNT(*) FROM sale_items si WHERE si.sale_id = s.id) AS ItemsCount,
                    (SELECT GROUP_CONCAT(DISTINCT 
                        CASE sp.payment_mode 
                            WHEN 1 THEN 'Cash' 
                            WHEN 2 THEN 'Card' 
                            WHEN 3 THEN 'UPI' 
                            ELSE 'Cash' 
                        END) FROM sale_payments sp WHERE sp.sale_id = s.id) AS PaymentModes,
                    s.subtotal AS Subtotal,
                    s.total AS Total
                FROM sales s
                WHERE s.status != 3 AND substr(s.invoice_date, 1, 7) = @targetMonth
                ORDER BY s.invoice_date DESC;
            ";

            var rows = await connection.QueryAsync<dynamic>(
                new CommandDefinition(sql, new { targetMonth }, cancellationToken: cancellationToken));

            foreach (var r in rows)
            {
                string invDate = r.InvoiceDate != null ? Convert.ToString(r.InvoiceDate) : "";
                if (DateTime.TryParse(invDate, out var dt)) invDate = dt.ToString("dd-MM-yyyy HH:mm");
                decimal subtotal = r.Subtotal != null ? Convert.ToDecimal(r.Subtotal) : 0m;
                decimal total = r.Total != null ? Convert.ToDecimal(r.Total) : 0m;
                string modes = r.PaymentModes != null ? Convert.ToString(r.PaymentModes) : "Cash";

                list.Add(new MetricDetailItemDto(
                    Col1: (string)r.InvoiceNo,
                    Col2: invDate,
                    Col3: (string)r.CustomerName,
                    Col4: $"{Convert.ToInt32(r.ItemsCount ?? 0)} items",
                    Col5: modes,
                    Col6: $"₹{subtotal:N2}",
                    Col7: $"₹{total:N2}",
                    BadgeText: "PAID",
                    BadgeColor: "#16A34A"
                ));
            }
        }
        else if (metricType == "CashCollection")
        {
            var sql = @"
                SELECT 
                    s.invoice_no AS InvoiceNo,
                    COALESCE(sp.paid_at, s.invoice_date) AS PaymentDate,
                    COALESCE(s.customer_name, 'Walk-in Customer') AS CustomerName,
                    'CASH' AS Mode,
                    s.total AS InvoiceTotal,
                    COALESCE(sp.amount, s.total) AS PaidAmount,
                    COALESCE(sp.reference, s.invoice_no) AS Reference
                FROM sales s
                LEFT JOIN sale_payments sp ON sp.sale_id = s.id
                WHERE s.status != 3 
                  AND substr(s.invoice_date, 1, 7) = @targetMonth
                  AND (sp.payment_mode = 1 OR sp.id IS NULL)
                ORDER BY s.invoice_date DESC;
            ";

            var rows = await connection.QueryAsync<dynamic>(
                new CommandDefinition(sql, new { targetMonth }, cancellationToken: cancellationToken));

            foreach (var r in rows)
            {
                string pDate = r.PaymentDate != null ? Convert.ToString(r.PaymentDate) : "";
                if (DateTime.TryParse(pDate, out var dt)) pDate = dt.ToString("dd-MM-yyyy HH:mm");
                decimal invTotal = r.InvoiceTotal != null ? Convert.ToDecimal(r.InvoiceTotal) : 0m;
                decimal paidAmt = r.PaidAmount != null ? Convert.ToDecimal(r.PaidAmount) : 0m;

                list.Add(new MetricDetailItemDto(
                    Col1: (string)r.InvoiceNo,
                    Col2: pDate,
                    Col3: (string)r.CustomerName,
                    Col4: "Cash Counter",
                    Col5: "CASH",
                    Col6: $"₹{invTotal:N2}",
                    Col7: $"₹{paidAmt:N2}",
                    BadgeText: "CASH",
                    BadgeColor: "#0D9488"
                ));
            }
        }
        else if (metricType == "OnlineCollection")
        {
            var sql = @"
                SELECT 
                    s.invoice_no AS InvoiceNo,
                    sp.paid_at AS PaymentDate,
                    COALESCE(s.customer_name, 'Walk-in Customer') AS CustomerName,
                    CASE sp.payment_mode WHEN 2 THEN 'Card' WHEN 3 THEN 'UPI' ELSE 'Digital' END AS Mode,
                    s.total AS InvoiceTotal,
                    sp.amount AS PaidAmount,
                    COALESCE(sp.reference, 'Digital QR/Card') AS Reference
                FROM sales s
                INNER JOIN sale_payments sp ON sp.sale_id = s.id
                WHERE s.status != 3 
                  AND substr(s.invoice_date, 1, 7) = @targetMonth
                  AND sp.payment_mode IN (2, 3)
                ORDER BY sp.paid_at DESC;
            ";

            var rows = await connection.QueryAsync<dynamic>(
                new CommandDefinition(sql, new { targetMonth }, cancellationToken: cancellationToken));

            foreach (var r in rows)
            {
                string pDate = r.PaymentDate != null ? Convert.ToString(r.PaymentDate) : "";
                if (DateTime.TryParse(pDate, out var dt)) pDate = dt.ToString("dd-MM-yyyy HH:mm");
                decimal invTotal = r.InvoiceTotal != null ? Convert.ToDecimal(r.InvoiceTotal) : 0m;
                decimal paidAmt = r.PaidAmount != null ? Convert.ToDecimal(r.PaidAmount) : 0m;
                string mode = Convert.ToString(r.Mode);

                list.Add(new MetricDetailItemDto(
                    Col1: (string)r.InvoiceNo,
                    Col2: pDate,
                    Col3: (string)r.CustomerName,
                    Col4: (string)r.Reference,
                    Col5: mode.ToUpperInvariant(),
                    Col6: $"₹{invTotal:N2}",
                    Col7: $"₹{paidAmt:N2}",
                    BadgeText: mode.ToUpperInvariant(),
                    BadgeColor: "#7C3AED"
                ));
            }
        }
        else if (metricType == "EstimatedProfit")
        {
            var sql = @"
                SELECT 
                    si.product_name AS ProductName,
                    si.batch_number AS BatchNumber,
                    SUM(si.quantity) AS SoldQty,
                    COALESCE(b.purchase_rate, 0) AS BuyingCost,
                    si.mrp AS Mrp,
                    (si.mrp - COALESCE(b.purchase_rate, 0)) AS UnitProfit,
                    ROUND(SUM((si.mrp - COALESCE(b.purchase_rate, 0)) * si.quantity), 2) AS TotalProfit
                FROM sale_items si
                JOIN sales s ON s.id = si.sale_id
                LEFT JOIN batches b ON (b.id = si.batch_id OR (b.batch_number = si.batch_number AND b.product_id = si.product_id))
                WHERE s.status != 3
                GROUP BY si.product_id, si.batch_number, si.mrp, b.purchase_rate
                ORDER BY TotalProfit DESC;
            ";

            var rows = await connection.QueryAsync<dynamic>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));

            foreach (var r in rows)
            {
                decimal soldQty = r.SoldQty != null ? Convert.ToDecimal(r.SoldQty) : 0m;
                decimal buyingCost = r.BuyingCost != null ? Convert.ToDecimal(r.BuyingCost) : 0m;
                decimal mrp = r.Mrp != null ? Convert.ToDecimal(r.Mrp) : 0m;
                decimal unitProfit = r.UnitProfit != null ? Convert.ToDecimal(r.UnitProfit) : 0m;
                decimal totalProfit = r.TotalProfit != null ? Convert.ToDecimal(r.TotalProfit) : 0m;
                decimal margin = mrp > 0 ? Math.Round((unitProfit / mrp) * 100m, 1) : 0m;

                list.Add(new MetricDetailItemDto(
                    Col1: (string)r.ProductName,
                    Col2: (string)r.BatchNumber,
                    Col3: $"{soldQty:0.##}",
                    Col4: $"₹{buyingCost:N2}",
                    Col5: $"₹{mrp:N2}",
                    Col6: $"₹{unitProfit:N2}",
                    Col7: $"₹{totalProfit:N2}",
                    BadgeText: $"{margin:F1}% Margin",
                    BadgeColor: margin >= 25 ? "#16A34A" : (margin >= 15 ? "#0284C7" : "#D97706")
                ));
            }
        }

        return list;
    }
}
