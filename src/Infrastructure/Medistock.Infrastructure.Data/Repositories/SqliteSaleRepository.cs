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
            // 1. Ensure batches & stock balances exist, resolve actual batches, and deduct stock atomically
            var resolvedBatchMap = new Dictionary<string, string>(); // item.Id -> actualBatchId

            foreach (var item in sale.Items)
            {
                string actualBatchId = item.BatchId;
                var existingBatch = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT id, mrp, purchase_rate, sale_rate FROM batches WHERE id = @BatchId LIMIT 1;",
                    new { BatchId = item.BatchId }, transaction);

                if (existingBatch == null)
                {
                    // Check by product_id and batch_number
                    var matchByNo = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT id, mrp, purchase_rate, sale_rate FROM batches WHERE product_id = @ProductId AND batch_number = @BatchNumber LIMIT 1;",
                        new { item.ProductId, item.BatchNumber }, transaction);

                    if (matchByNo != null)
                    {
                        actualBatchId = (string)matchByNo.id;
                    }
                    else
                    {
                        // Check if any batch with available stock exists for this product in this warehouse
                        var matchByStock = await connection.QueryFirstOrDefaultAsync<dynamic>(
                            @"SELECT b.id FROM batches b 
                              JOIN stock_balances sb ON sb.batch_id = b.id 
                              WHERE b.product_id = @ProductId AND (sb.warehouse_id = @WarehouseId OR @WarehouseId = '') AND sb.quantity > 0 
                              ORDER BY b.expiry_date ASC LIMIT 1;",
                            new { item.ProductId, sale.WarehouseId }, transaction);

                        if (matchByStock != null)
                        {
                            actualBatchId = (string)matchByStock.id;
                        }
                        else
                        {
                            actualBatchId = string.IsNullOrWhiteSpace(item.BatchId) ? Guid.NewGuid().ToString("N") : item.BatchId;
                            const string ensureBatchSql = @"
                                INSERT OR IGNORE INTO batches (
                                    id, product_id, org_id, batch_number, expiry_date, mrp, purchase_rate, sale_rate, created_at
                                ) VALUES (
                                    @BatchId, @ProductId, @OrgId, @BatchNumber, @ExpiryDate, CAST(@Mrp AS REAL), CAST(@UnitPrice AS REAL) * 0.8, CAST(@UnitPrice AS REAL), @CreatedAt
                                );
                            ";
                            await connection.ExecuteAsync(new CommandDefinition(
                                ensureBatchSql,
                                new
                                {
                                    BatchId = actualBatchId,
                                    item.ProductId,
                                    sale.OrgId,
                                    item.BatchNumber,
                                    ExpiryDate = item.ExpiryDate.ToString("o"),
                                    Mrp = (double)item.Mrp,
                                    UnitPrice = (double)item.UnitPrice,
                                    CreatedAt = DateTime.UtcNow.ToString("o")
                                },
                                transaction,
                                cancellationToken: cancellationToken));
                        }
                    }
                }

                resolvedBatchMap[item.Id] = actualBatchId;

                // Deduct stock from stock_balances
                var existingStock = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT id, quantity, reserved_quantity FROM stock_balances WHERE batch_id = @BatchId AND (warehouse_id = @WarehouseId OR @WarehouseId = '' OR warehouse_id = 'wh-1' OR warehouse_id = 'WH-MAIN') LIMIT 1;",
                    new { BatchId = actualBatchId, sale.WarehouseId }, transaction);

                if (existingStock != null)
                {
                    string stockId = (string)existingStock.id;
                    var affected = await connection.ExecuteAsync(new CommandDefinition(
                        "UPDATE stock_balances SET quantity = quantity - @Quantity, last_updated_at = @UpdatedAt WHERE id = @Id AND (quantity - reserved_quantity) >= @Quantity;",
                        new { Id = stockId, Quantity = (double)item.Quantity, UpdatedAt = DateTime.UtcNow.ToString("o") },
                        transaction, cancellationToken: cancellationToken));

                    if (affected == 0)
                    {
                        transaction.Rollback();
                        return new CommitSaleResult(false, null, null, 0, 0, DateTime.UtcNow, $"Insufficient stock for batch {item.BatchNumber}. Requested: {item.Quantity}");
                    }
                }
                else
                {
                    // Check if stock exists for this product in warehouse under any batch
                    var prodStock = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT id, quantity FROM stock_balances WHERE product_id = @ProductId AND (warehouse_id = @WarehouseId OR @WarehouseId = '' OR warehouse_id = 'wh-1' OR warehouse_id = 'WH-MAIN') AND (quantity - reserved_quantity) >= @Quantity ORDER BY quantity DESC LIMIT 1;",
                        new { item.ProductId, sale.WarehouseId, Quantity = (double)item.Quantity }, transaction);

                    if (prodStock != null)
                    {
                        string stockId = (string)prodStock.id;
                        await connection.ExecuteAsync(new CommandDefinition(
                            "UPDATE stock_balances SET quantity = quantity - @Quantity, last_updated_at = @UpdatedAt WHERE id = @Id AND (quantity - reserved_quantity) >= @Quantity;",
                            new { Id = stockId, Quantity = (double)item.Quantity, UpdatedAt = DateTime.UtcNow.ToString("o") },
                            transaction, cancellationToken: cancellationToken));
                    }
                    else
                    {
                        // Ensure stock balance record exists at 0
                        const string ensureStockSql = @"
                            INSERT OR IGNORE INTO stock_balances (
                                id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at
                            ) VALUES (
                                @Id, @BatchId, @ProductId, @WarehouseId, 0.0, 0.0, @LastUpdatedAt
                            );
                        ";
                        await connection.ExecuteAsync(new CommandDefinition(
                            ensureStockSql,
                            new
                            {
                                Id = $"sb_{actualBatchId}_{sale.WarehouseId}",
                                BatchId = actualBatchId,
                                item.ProductId,
                                sale.WarehouseId,
                                LastUpdatedAt = DateTime.UtcNow.ToString("o")
                            },
                            transaction,
                            cancellationToken: cancellationToken));
                    }
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
                        BatchId = resolvedBatchMap.TryGetValue(item.Id, out var rId) ? rId : item.BatchId,
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

            // 6. Automatic Double-Entry Accounting Posting
            var journalEntryId = $"je_sal_{sale.Id}";
            var voucherNo = $"SAL-{sale.InvoiceNo}";
            var saleNarration = $"Tax Invoice {sale.InvoiceNo} - {sale.CustomerName}";

            const string insertJournalSql = @"
                INSERT OR IGNORE INTO journal_entries (
                    id, org_id, branch_id, voucher_number, voucher_type, voucher_date,
                    narration, reference_id, reference_type, created_by_user_id, created_at
                ) VALUES (
                    @Id, @OrgId, @BranchId, @VoucherNumber, 1, @VoucherDate,
                    @Narration, @ReferenceId, 'SALE', @CreatedByUserId, datetime('now')
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertJournalSql,
                new
                {
                    Id = journalEntryId,
                    sale.OrgId,
                    sale.BranchId,
                    VoucherNumber = voucherNo,
                    VoucherDate = sale.InvoiceDate.ToString("o"),
                    Narration = saleNarration,
                    ReferenceId = sale.Id,
                    CreatedByUserId = sale.UserId
                },
                transaction,
                cancellationToken: cancellationToken));

            const string insertLineSql = @"
                INSERT INTO journal_lines (id, journal_entry_id, account_id, account_name, debit_amount, credit_amount, narration)
                VALUES (@Id, @JournalEntryId, @AccountId, @AccountName, CAST(@DebitAmount AS REAL), CAST(@CreditAmount AS REAL), @Narration);
            ";

            // Helper local function to post a journal line and update account balance
            async Task PostLine(string accountId, string accountName, decimal debit, decimal credit)
            {
                if (debit == 0 && credit == 0) return;
                var lineId = $"jl_{Guid.NewGuid():N}";
                await connection.ExecuteAsync(new CommandDefinition(
                    insertLineSql,
                    new
                    {
                        Id = lineId,
                        JournalEntryId = journalEntryId,
                        AccountId = accountId,
                        AccountName = accountName,
                        DebitAmount = (double)debit,
                        CreditAmount = (double)credit,
                        Narration = saleNarration
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                var cat = await connection.ExecuteScalarAsync<int?>(
                    new CommandDefinition("SELECT category FROM account_heads WHERE id = @accountId;", new { accountId }, transaction, cancellationToken: cancellationToken));

                if (cat.HasValue)
                {
                    if (cat.Value == 1 || cat.Value == 5) // Asset or Expense
                    {
                        await connection.ExecuteAsync(new CommandDefinition(
                            "UPDATE account_heads SET current_balance = current_balance + (@debit - @credit) WHERE id = @accountId;",
                            new { debit = (double)debit, credit = (double)credit, accountId }, transaction, cancellationToken: cancellationToken));
                    }
                    else // Liability, Equity, Revenue
                    {
                        await connection.ExecuteAsync(new CommandDefinition(
                            "UPDATE account_heads SET current_balance = current_balance + (@credit - @debit) WHERE id = @accountId;",
                            new { debit = (double)debit, credit = (double)credit, accountId }, transaction, cancellationToken: cancellationToken));
                    }
                }
            }

            // Debit side: Payment receipts (Cash, Bank, Debtors)
            decimal paidAmount = 0m;
            if (sale.Payments != null && sale.Payments.Count > 0)
            {
                foreach (var p in sale.Payments)
                {
                    paidAmount += p.Amount;
                    if (p.PaymentMode == PaymentMode.Cash)
                    {
                        await PostLine("acc_cash", "Cash on Hand", p.Amount, 0m);
                    }
                    else if (p.PaymentMode == PaymentMode.Credit)
                    {
                        await PostLine("acc_debtors", "Sundry Debtors (Customers)", p.Amount, 0m);
                    }
                    else // Card, UPI, etc.
                    {
                        await PostLine("acc_bank_hdfc", "Bank Account (Primary)", p.Amount, 0m);
                    }
                }
            }

            if (sale.Total > paidAmount)
            {
                // Balance on credit
                await PostLine("acc_debtors", "Sundry Debtors (Customers)", sale.Total - paidAmount, 0m);
            }

            // Credit side: Sales Revenue
            await PostLine("acc_sales", "Pharmacy Medicine Sales A/c", 0m, sale.Subtotal);

            // Output GST
            decimal cgstTotal = 0m;
            decimal sgstTotal = 0m;
            decimal igstTotal = 0m;
            foreach (var item in sale.Items)
            {
                cgstTotal += item.CgstAmount;
                sgstTotal += item.SgstAmount;
                igstTotal += item.IgstAmount;
            }

            if (cgstTotal > 0) await PostLine("acc_output_cgst", "Output CGST A/c", 0m, cgstTotal);
            if (sgstTotal > 0) await PostLine("acc_output_sgst", "Output SGST A/c", 0m, sgstTotal);
            if (igstTotal > 0) await PostLine("acc_output_igst", "Output IGST A/c", 0m, igstTotal);

            // Round Off
            if (sale.RoundOff > 0)
            {
                await PostLine("acc_roundoff", "Round Off Expense / Income", 0m, sale.RoundOff);
            }
            else if (sale.RoundOff < 0)
            {
                await PostLine("acc_roundoff", "Round Off Expense / Income", Math.Abs(sale.RoundOff), 0m);
            }

            // 7. Enqueue Outbox Event
            await _outboxRepository.EnqueueEventAsync(
                outboxEvent,
                (System.Data.Common.DbTransaction)transaction,
                cancellationToken);

            // 8. Commit everything atomically
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
