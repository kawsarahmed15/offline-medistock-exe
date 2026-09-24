using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Sales.Commands;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;
using Medistock.Domain.Sales;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteSaleRepository : ISaleRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IStockRepository _stockRepository;
    private readonly IOutboxRepository _outboxRepository;

    public SqliteSaleRepository(
        ISqliteConnectionFactory connectionFactory,
        IStockRepository stockRepository,
        IOutboxRepository outboxRepository)
    {
        _connectionFactory = connectionFactory;
        _stockRepository = stockRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<CommitSaleResult> CommitSaleAtomicAsync(
        Sale sale,
        List<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Deduct stock for each line item atomically
            foreach (var item in sale.Items)
            {
                var deducted = await _stockRepository.DeductStockAtomicAsync(
                    item.BatchId,
                    sale.WarehouseId,
                    item.Quantity,
                    (System.Data.Common.DbTransaction)transaction,
                    cancellationToken);

                if (!deducted)
                {
                    transaction.Rollback();
                    return new CommitSaleResult(
                        false,
                        null,
                        null,
                        0,
                        0,
                        DateTime.UtcNow,
                        $"Insufficient stock for item: {item.ProductName} (Batch: {item.BatchNumber})");
                }
            }

            // 2. Insert Sale Record
            const string insertSaleSql = @"
                INSERT INTO sales (
                    id, org_id, branch_id, counter_id, warehouse_id, user_id, device_id,
                    invoice_no, invoice_date, status, customer_id, customer_name,
                    subtotal, discount_amount, tax_amount, round_off, total,
                    is_interstate, prescription_ref, notes, created_at, posted_at
                ) VALUES (
                    @Id, @OrgId, @BranchId, @CounterId, @WarehouseId, @UserId, @DeviceId,
                    @InvoiceNo, @InvoiceDate, @Status, @CustomerId, @CustomerName,
                    CAST(@Subtotal AS REAL), CAST(@DiscountAmount AS REAL), CAST(@TaxAmount AS REAL),
                    CAST(@RoundOff AS REAL), CAST(@Total AS REAL),
                    @IsInterstate, @PrescriptionRef, @Notes, @CreatedAt, @PostedAt
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertSaleSql,
                new
                {
                    sale.Id,
                    sale.OrgId,
                    sale.BranchId,
                    sale.CounterId,
                    sale.WarehouseId,
                    sale.UserId,
                    sale.DeviceId,
                    sale.InvoiceNo,
                    InvoiceDate = sale.InvoiceDate.ToString("o"),
                    Status = (int)sale.Status,
                    sale.CustomerId,
                    sale.CustomerName,
                    Subtotal = (double)sale.Subtotal,
                    DiscountAmount = (double)sale.DiscountAmount,
                    TaxAmount = (double)sale.TaxAmount,
                    RoundOff = (double)sale.RoundOff,
                    Total = (double)sale.Total,
                    IsInterstate = sale.IsInterstate ? 1 : 0,
                    sale.PrescriptionRef,
                    sale.Notes,
                    CreatedAt = sale.CreatedAt.ToString("o"),
                    PostedAt = sale.PostedAt?.ToString("o")
                },
                transaction,
                cancellationToken: cancellationToken));

            // 3. Insert Sale Items
            const string insertItemSql = @"
                INSERT INTO sale_items (
                    id, sale_id, product_id, product_name, batch_id, batch_number, expiry_date,
                    quantity, unit_price, mrp, discount_pct, discount_amount,
                    taxable_amount, cgst_rate, cgst_amount, sgst_rate, sgst_amount,
                    igst_rate, igst_amount, net_amount
                ) VALUES (
                    @Id, @SaleId, @ProductId, @ProductName, @BatchId, @BatchNumber, @ExpiryDate,
                    CAST(@Quantity AS REAL), CAST(@UnitPrice AS REAL), CAST(@Mrp AS REAL),
                    CAST(@DiscountPct AS REAL), CAST(@DiscountAmount AS REAL),
                    CAST(@TaxableAmount AS REAL), CAST(@CgstRate AS REAL), CAST(@CgstAmount AS REAL),
                    CAST(@SgstRate AS REAL), CAST(@SgstAmount AS REAL),
                    CAST(@IgstRate AS REAL), CAST(@IgstAmount AS REAL), CAST(@NetAmount AS REAL)
                );
            ";

            foreach (var item in sale.Items)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    insertItemSql,
                    new
                    {
                        item.Id,
                        item.SaleId,
                        item.ProductId,
                        item.ProductName,
                        item.BatchId,
                        item.BatchNumber,
                        ExpiryDate = item.ExpiryDate.ToString("o"),
                        Quantity = (double)item.Quantity,
                        UnitPrice = (double)item.UnitPrice,
                        Mrp = (double)item.Mrp,
                        DiscountPct = (double)item.DiscountPct,
                        DiscountAmount = (double)item.DiscountAmount,
                        TaxableAmount = (double)item.TaxableAmount,
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

            // 4. Insert Payments
            const string insertPaymentSql = @"
                INSERT INTO sale_payments (
                    id, sale_id, payment_mode, amount, reference, paid_at
                ) VALUES (
                    @Id, @SaleId, @PaymentMode, CAST(@Amount AS REAL), @Reference, @PaidAt
                );
            ";

            foreach (var payment in sale.Payments)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    insertPaymentSql,
                    new
                    {
                        payment.Id,
                        payment.SaleId,
                        PaymentMode = (int)payment.PaymentMode,
                        Amount = (double)payment.Amount,
                        payment.Reference,
                        PaidAt = payment.PaidAt.ToString("o")
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            // 5. Insert Stock Movements
            foreach (var movement in stockMovements)
            {
                await _stockRepository.RecordStockMovementAsync(
                    movement,
                    (System.Data.Common.DbTransaction)transaction,
                    cancellationToken);
            }

            // 6. Enqueue Outbox Event
            await _outboxRepository.EnqueueEventAsync(
                outboxEvent,
                (System.Data.Common.DbTransaction)transaction,
                cancellationToken);

            // 7. Commit everything atomically
            transaction.Commit();

            return new CommitSaleResult(
                true,
                sale.Id,
                sale.InvoiceNo,
                sale.Total,
                sale.RoundOff,
                sale.PostedAt ?? DateTime.UtcNow,
                null);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new CommitSaleResult(
                false,
                null,
                null,
                0,
                0,
                DateTime.UtcNow,
                $"Database commit failed: {ex.Message}");
        }
    }
}
