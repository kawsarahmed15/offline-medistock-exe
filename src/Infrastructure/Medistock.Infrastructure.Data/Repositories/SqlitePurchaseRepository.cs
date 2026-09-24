using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Purchases.DTOs;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;
using Medistock.Domain.Purchases;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqlitePurchaseRepository : IPurchaseRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISupplierRepository _supplierRepository;

    public SqlitePurchaseRepository(
        ISqliteConnectionFactory connectionFactory,
        IOutboxRepository outboxRepository,
        ISupplierRepository supplierRepository)
    {
        _connectionFactory = connectionFactory;
        _outboxRepository = outboxRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<PurchasePostingResult> PostPurchaseInvoiceAtomicAsync(
        PurchaseInvoice invoice,
        IReadOnlyList<Batch> batchesToUpsert,
        IReadOnlyList<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Insert purchase invoice
            const string insertInvoiceSql = @"
                INSERT INTO purchase_invoices (
                    id, org_id, branch_id, warehouse_id, supplier_id,
                    supplier_name, supplier_gstin, supplier_invoice_no, supplier_invoice_date,
                    status, is_interstate, subtotal, discount_amount, taxable_amount,
                    cgst_amount, sgst_amount, igst_amount, round_off, grand_total,
                    notes, created_by_user_id, created_at, posted_at
                ) VALUES (
                    @Id, @OrgId, @BranchId, @WarehouseId, @SupplierId,
                    @SupplierName, @SupplierGstin, @SupplierInvoiceNo, @SupplierInvoiceDate,
                    @Status, @IsInterstate, @Subtotal, @DiscountAmount, @TaxableAmount,
                    @CgstAmount, @SgstAmount, @IgstAmount, @RoundOff, @GrandTotal,
                    @Notes, @CreatedByUserId, @CreatedAt, @PostedAt
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertInvoiceSql,
                new
                {
                    invoice.Id,
                    invoice.OrgId,
                    invoice.BranchId,
                    invoice.WarehouseId,
                    invoice.SupplierId,
                    invoice.SupplierName,
                    invoice.SupplierGstin,
                    invoice.SupplierInvoiceNo,
                    SupplierInvoiceDate = invoice.SupplierInvoiceDate.ToString("o"),
                    Status = (int)invoice.Status,
                    IsInterstate = invoice.IsInterstate ? 1 : 0,
                    Subtotal = (double)invoice.Subtotal,
                    DiscountAmount = (double)invoice.DiscountAmount,
                    TaxableAmount = (double)invoice.TaxableAmount,
                    CgstAmount = (double)invoice.CgstAmount,
                    SgstAmount = (double)invoice.SgstAmount,
                    IgstAmount = (double)invoice.IgstAmount,
                    RoundOff = (double)invoice.RoundOff,
                    GrandTotal = (double)invoice.GrandTotal,
                    invoice.Notes,
                    invoice.CreatedByUserId,
                    CreatedAt = invoice.CreatedAt.ToString("o"),
                    PostedAt = invoice.PostedAt?.ToString("o")
                },
                transaction,
                cancellationToken: cancellationToken));

            // 2. Insert line items
            const string insertItemSql = @"
                INSERT INTO purchase_invoice_items (
                    id, purchase_invoice_id, product_id, product_name, hsn_code,
                    batch_number, expiry_date, manufacturing_date, quantity, free_quantity,
                    unit_price, mrp, sale_rate, discount_pct, discount_amount,
                    taxable_amount, gst_rate_percent, cgst_rate, cgst_amount,
                    sgst_rate, sgst_amount, igst_rate, igst_amount, net_amount
                ) VALUES (
                    @Id, @PurchaseInvoiceId, @ProductId, @ProductName, @HsnCode,
                    @BatchNumber, @ExpiryDate, @ManufacturingDate, @Quantity, @FreeQuantity,
                    @UnitPrice, @Mrp, @SaleRate, @DiscountPct, @DiscountAmount,
                    @TaxableAmount, @GstRatePercent, @CgstRate, @CgstAmount,
                    @SgstRate, @SgstAmount, @IgstRate, @IgstAmount, @NetAmount
                );
            ";

            foreach (var item in invoice.Items)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    insertItemSql,
                    new
                    {
                        item.Id,
                        item.PurchaseInvoiceId,
                        item.ProductId,
                        item.ProductName,
                        item.HsnCode,
                        item.BatchNumber,
                        ExpiryDate = item.ExpiryDate.ToString("o"),
                        ManufacturingDate = item.ManufacturingDate?.ToString("o"),
                        Quantity = (double)item.Quantity,
                        FreeQuantity = (double)item.FreeQuantity,
                        UnitPrice = (double)item.UnitPrice,
                        Mrp = (double)item.Mrp,
                        SaleRate = (double)item.SaleRate,
                        DiscountPct = (double)item.DiscountPct,
                        DiscountAmount = (double)item.DiscountAmount,
                        TaxableAmount = (double)item.TaxableAmount,
                        GstRatePercent = (double)item.GstRatePercent,
                        CgstRate = (double)item.CgstRate,
                        CgstAmount = (double)item.CgstAmount,
                        SgstRate = (double)item.SgstRate,
                        SgstAmount = (double)item.SgstAmount,
                        IgstRate = (double)item.IgstRate,
                        IgstAmount = (double)item.IgstAmount,
                        NetAmount = (double)item.NetAmount
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            // 3. Upsert Batches and Stock Balances
            const string upsertBatchSql = @"
                INSERT INTO batches (
                    id, product_id, org_id, batch_number, expiry_date,
                    manufacturing_date, mrp, purchase_rate, sale_rate, created_at
                ) VALUES (
                    @Id, @ProductId, @OrgId, @BatchNumber, @ExpiryDate,
                    @ManufacturingDate, @Mrp, @PurchaseRate, @SaleRate, @CreatedAt
                )
                ON CONFLICT(id) DO UPDATE SET
                    mrp = @Mrp,
                    purchase_rate = @PurchaseRate,
                    sale_rate = @SaleRate,
                    expiry_date = @ExpiryDate;
            ";

            const string upsertStockSql = @"
                INSERT INTO stock_balances (
                    id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at
                ) VALUES (
                    @Id, @BatchId, @ProductId, @WarehouseId, @Quantity, 0.0, @LastUpdatedAt
                )
                ON CONFLICT(batch_id, warehouse_id) DO UPDATE SET
                    quantity = quantity + @Quantity,
                    last_updated_at = @LastUpdatedAt;
            ";

            for (int i = 0; i < batchesToUpsert.Count; i++)
            {
                var batch = batchesToUpsert[i];
                var movement = stockMovements[i];

                await connection.ExecuteAsync(new CommandDefinition(
                    upsertBatchSql,
                    new
                    {
                        batch.Id,
                        batch.ProductId,
                        batch.OrgId,
                        batch.BatchNumber,
                        ExpiryDate = batch.ExpiryDate.ToString("o"),
                        ManufacturingDate = batch.ManufacturingDate?.ToString("o"),
                        Mrp = (double)batch.Mrp,
                        PurchaseRate = (double)batch.PurchaseRate,
                        SaleRate = (double)batch.SaleRate,
                        CreatedAt = batch.CreatedAt.ToString("o")
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                var sbId = "sb_" + batch.Id;
                await connection.ExecuteAsync(new CommandDefinition(
                    upsertStockSql,
                    new
                    {
                        Id = sbId,
                        BatchId = batch.Id,
                        batch.ProductId,
                        invoice.WarehouseId,
                        Quantity = (double)movement.Quantity,
                        LastUpdatedAt = DateTime.UtcNow.ToString("o")
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            // 4. Insert Stock Movements
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

            foreach (var m in stockMovements)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    insertMovementSql,
                    new
                    {
                        m.Id,
                        m.OrgId,
                        m.BranchId,
                        m.WarehouseId,
                        m.BatchId,
                        m.ProductId,
                        MovementType = (int)m.MovementType,
                        Quantity = (double)m.Quantity,
                        m.ReferenceType,
                        m.ReferenceId,
                        UnitCost = (double)m.UnitCost,
                        m.UserId,
                        m.DeviceId,
                        CreatedAt = m.CreatedAt.ToString("o")
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            // 5. Update Supplier Balance
            await _supplierRepository.UpdateOutstandingBalanceAsync(
                invoice.SupplierId, invoice.GrandTotal, (System.Data.Common.DbTransaction)transaction, cancellationToken);

            // 6. Enqueue Outbox Event
            await _outboxRepository.EnqueueEventAsync(
                outboxEvent, (System.Data.Common.DbTransaction)transaction, cancellationToken);

            transaction.Commit();

            return new PurchasePostingResult(
                Success: true,
                PurchaseInvoiceId: invoice.Id,
                BatchesCreatedOrUpdated: batchesToUpsert.Count,
                TotalStockAdded: stockMovements.Sum(m => m.Quantity),
                GrandTotal: invoice.GrandTotal
            );
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new PurchasePostingResult(false, null, 0, 0, 0, ex.Message);
        }
    }

    public async Task<IReadOnlyList<PurchaseInvoiceSummaryDto>> GetPurchaseInvoicesAsync(
        string orgId,
        string branchId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                id AS Id,
                supplier_name AS SupplierName,
                supplier_gstin AS SupplierGstin,
                supplier_invoice_no AS SupplierInvoiceNo,
                supplier_invoice_date AS SupplierInvoiceDateStr,
                status AS Status,
                taxable_amount AS TaxableAmount,
                cgst_amount AS CgstAmount,
                sgst_amount AS SgstAmount,
                igst_amount AS IgstAmount,
                grand_total AS GrandTotal,
                created_at AS CreatedAtStr,
                posted_at AS PostedAtStr
            FROM purchase_invoices
            WHERE org_id = @orgId AND branch_id = @branchId
            ORDER BY created_at DESC
            LIMIT @limit;
        ";

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { orgId, branchId, limit }, cancellationToken: cancellationToken));

        var list = new List<PurchaseInvoiceSummaryDto>();
        foreach (var r in rows)
        {
            DateTime invDate = DateTime.TryParse((string)r.SupplierInvoiceDateStr, out DateTime d1) ? d1 : DateTime.UtcNow;
            DateTime crDate = DateTime.TryParse((string)r.CreatedAtStr, out DateTime d2) ? d2 : DateTime.UtcNow;
            DateTime? postDate = !string.IsNullOrEmpty((string?)r.PostedAtStr) && DateTime.TryParse((string)r.PostedAtStr, out DateTime d3) ? d3 : null;

            list.Add(new PurchaseInvoiceSummaryDto(
                Id: (string)r.Id,
                SupplierName: (string)r.SupplierName,
                SupplierGstin: (string?)r.SupplierGstin,
                SupplierInvoiceNo: (string)r.SupplierInvoiceNo,
                SupplierInvoiceDate: invDate,
                Status: (PurchaseInvoiceStatus)(int)r.Status,
                TaxableAmount: Convert.ToDecimal(r.TaxableAmount),
                CgstAmount: Convert.ToDecimal(r.CgstAmount),
                SgstAmount: Convert.ToDecimal(r.SgstAmount),
                IgstAmount: Convert.ToDecimal(r.IgstAmount),
                GrandTotal: Convert.ToDecimal(r.GrandTotal),
                ItemCount: 0,
                CreatedAt: crDate,
                PostedAt: postDate
            ));
        }

        return list;
    }

    public Task<PurchaseInvoice?> GetPurchaseInvoiceByIdAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        // Cold-path retrieval for invoice viewing
        return Task.FromResult<PurchaseInvoice?>(null);
    }
}
