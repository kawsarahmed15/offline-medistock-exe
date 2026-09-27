using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Sales.DTOs;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;
using Medistock.Domain.Sales;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteSaleReturnRepository : ISaleReturnRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IStockRepository _stockRepository;
    private readonly IOutboxRepository _outboxRepository;

    public SqliteSaleReturnRepository(
        ISqliteConnectionFactory connectionFactory,
        IStockRepository stockRepository,
        IOutboxRepository outboxRepository)
    {
        _connectionFactory = connectionFactory;
        _stockRepository = stockRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<IReadOnlyList<SaleSummaryDto>> GetRecentSalesAsync(
        string orgId,
        string branchId,
        string? searchQuery = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var sql = @"
            SELECT 
                s.id AS Id,
                s.invoice_no AS InvoiceNo,
                s.invoice_date AS InvoiceDateStr,
                s.customer_name AS CustomerName,
                s.subtotal AS Subtotal,
                s.tax_amount AS TaxAmount,
                s.round_off AS RoundOff,
                s.total AS Total,
                s.status AS Status,
                COUNT(si.id) AS ItemsCount
            FROM sales s
            LEFT JOIN sale_items si ON si.sale_id = s.id
            WHERE s.org_id = @orgId AND s.branch_id = @branchId
        ";

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            sql += " AND (s.invoice_no LIKE @pattern OR s.customer_name LIKE @pattern) ";
        }

        sql += @"
            GROUP BY s.id
            ORDER BY s.invoice_date DESC
            LIMIT @limit;
        ";

        var pattern = $"%{searchQuery?.Trim()}%";

        var rows = await connection.QueryAsync<dynamic>(new CommandDefinition(
            sql,
            new { orgId, branchId, pattern, limit },
            cancellationToken: cancellationToken));

        var list = new List<SaleSummaryDto>();
        foreach (var r in rows)
        {
            DateTime.TryParse((string)r.InvoiceDateStr, out DateTime invDate);
            list.Add(new SaleSummaryDto(
                (string)r.Id,
                (string)r.InvoiceNo,
                invDate,
                (string?)r.CustomerName,
                Convert.ToDecimal(r.Subtotal),
                Convert.ToDecimal(r.TaxAmount),
                Convert.ToDecimal(r.RoundOff),
                Convert.ToDecimal(r.Total),
                (SaleStatus)(int)r.Status,
                (int)r.ItemsCount,
                "Cash/Electronic"
            ));
        }

        return list;
    }

    public async Task<SaleDetailDto?> GetSaleByIdAsync(
        string saleId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string saleSql = @"
            SELECT id, org_id, branch_id, counter_id, warehouse_id, invoice_no, invoice_date,
                   customer_id, customer_name, user_id, subtotal, discount_amount,
                   tax_amount, round_off, total, status, is_interstate
            FROM sales
            WHERE id = @saleId;
        ";

        var s = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(
            saleSql, new { saleId }, cancellationToken: cancellationToken));

        if (s == null) return null;

        const string itemsSql = @"
            SELECT 
                si.id, si.product_id, si.product_name, si.batch_id, si.batch_number,
                si.expiry_date, si.quantity, si.unit_price, si.mrp, si.discount_pct,
                si.taxable_amount, si.cgst_rate, si.cgst_amount, si.sgst_rate, si.sgst_amount,
                si.igst_rate, si.igst_amount, si.net_amount,
                IFNULL((SELECT SUM(sri.quantity) FROM sale_return_items sri WHERE sri.sale_item_id = si.id), 0.0) AS already_returned
            FROM sale_items si
            WHERE si.sale_id = @saleId;
        ";

        var itemRows = await connection.QueryAsync<dynamic>(new CommandDefinition(
            itemsSql, new { saleId }, cancellationToken: cancellationToken));

        var items = new List<SaleDetailItemDto>();
        foreach (var i in itemRows)
        {
            DateTime.TryParse((string)i.expiry_date, out DateTime expDate);
            items.Add(new SaleDetailItemDto(
                (string)i.id,
                (string)i.product_id,
                (string)i.product_name,
                (string)i.batch_id,
                (string)i.batch_number,
                expDate,
                Convert.ToDecimal(i.quantity),
                Convert.ToDecimal(i.unit_price),
                Convert.ToDecimal(i.mrp),
                Convert.ToDecimal(i.discount_pct),
                Convert.ToDecimal(i.taxable_amount),
                Convert.ToDecimal(i.cgst_rate),
                Convert.ToDecimal(i.cgst_amount),
                Convert.ToDecimal(i.sgst_rate),
                Convert.ToDecimal(i.sgst_amount),
                Convert.ToDecimal(i.igst_rate),
                Convert.ToDecimal(i.igst_amount),
                Convert.ToDecimal(i.net_amount),
                Convert.ToDecimal(i.already_returned)
            ));
        }

        DateTime.TryParse((string)s.invoice_date, out DateTime invDate);

        return new SaleDetailDto(
            (string)s.id,
            (string)s.org_id,
            (string)s.branch_id,
            (string)s.counter_id,
            (string)s.warehouse_id,
            (string)s.invoice_no,
            invDate,
            (string?)s.customer_id,
            (string?)s.customer_name,
            (string)s.user_id,
            Convert.ToDecimal(s.subtotal),
            Convert.ToDecimal(s.discount_amount),
            Convert.ToDecimal(s.tax_amount),
            Convert.ToDecimal(s.round_off),
            Convert.ToDecimal(s.total),
            (SaleStatus)(int)s.status,
            ((int)s.is_interstate) == 1,
            items
        );
    }

    public async Task<SaleDetailDto?> GetSaleByInvoiceNoAsync(
        string orgId,
        string branchId,
        string invoiceNo,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string saleIdSql = "SELECT id FROM sales WHERE org_id = @orgId AND branch_id = @branchId AND invoice_no = @invoiceNo;";
        var saleId = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            saleIdSql, new { orgId, branchId, invoiceNo }, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(saleId)) return null;

        return await GetSaleByIdAsync(saleId, cancellationToken);
    }

    public async Task<SaleReturnResult> CommitSaleReturnAtomicAsync(
        SaleReturn saleReturn,
        List<StockMovement> stockMovements,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Insert Return Header
            const string insertReturnSql = @"
                INSERT INTO sale_returns (
                    id, org_id, branch_id, counter_id, warehouse_id, original_sale_id,
                    original_invoice_no, credit_note_no, return_date, customer_id, customer_name,
                    user_id, device_id, status, reason, subtotal, tax_amount, round_off,
                    total_amount, refund_mode, created_at, posted_at
                ) VALUES (
                    @Id, @OrgId, @BranchId, @CounterId, @WarehouseId, @OriginalSaleId,
                    @OriginalInvoiceNo, @CreditNoteNo, @ReturnDate, @CustomerId, @CustomerName,
                    @UserId, @DeviceId, @Status, @Reason, CAST(@Subtotal AS REAL), CAST(@TaxAmount AS REAL),
                    CAST(@RoundOff AS REAL), CAST(@TotalAmount AS REAL), @RefundMode, @CreatedAt, @PostedAt
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertReturnSql,
                new
                {
                    saleReturn.Id,
                    saleReturn.OrgId,
                    saleReturn.BranchId,
                    saleReturn.CounterId,
                    saleReturn.WarehouseId,
                    saleReturn.OriginalSaleId,
                    saleReturn.OriginalInvoiceNo,
                    saleReturn.CreditNoteNo,
                    ReturnDate = saleReturn.ReturnDate.ToString("o"),
                    saleReturn.CustomerId,
                    saleReturn.CustomerName,
                    saleReturn.UserId,
                    saleReturn.DeviceId,
                    Status = (int)saleReturn.Status,
                    saleReturn.Reason,
                    Subtotal = (double)saleReturn.Subtotal,
                    TaxAmount = (double)saleReturn.TaxAmount,
                    RoundOff = (double)saleReturn.RoundOff,
                    TotalAmount = (double)saleReturn.TotalAmount,
                    RefundMode = (int)saleReturn.RefundMode,
                    CreatedAt = saleReturn.CreatedAt.ToString("o"),
                    PostedAt = saleReturn.PostedAt?.ToString("o")
                },
                transaction,
                cancellationToken: cancellationToken));

            // 2. Insert Return Items & Restock Available Quantity
            const string insertItemSql = @"
                INSERT INTO sale_return_items (
                    id, sale_return_id, sale_item_id, product_id, product_name,
                    batch_id, batch_number, quantity, unit_price, taxable_amount,
                    cgst_rate, cgst_amount, sgst_rate, sgst_amount, igst_rate, igst_amount,
                    net_amount, restock_decision, reason
                ) VALUES (
                    @Id, @SaleReturnId, @SaleItemId, @ProductId, @ProductName,
                    @BatchId, @BatchNumber, CAST(@Quantity AS REAL), CAST(@UnitPrice AS REAL), CAST(@TaxableAmount AS REAL),
                    CAST(@CgstRate AS REAL), CAST(@CgstAmount AS REAL), CAST(@SgstRate AS REAL), CAST(@SgstAmount AS REAL),
                    CAST(@IgstRate AS REAL), CAST(@IgstAmount AS REAL), CAST(@NetAmount AS REAL),
                    @RestockDecision, @Reason
                );
            ";

            const string restockSql = @"
                UPDATE stock_balances 
                SET quantity = quantity + @qty, last_updated_at = @now
                WHERE batch_id = @batchId AND warehouse_id = @warehouseId;
            ";

            foreach (var item in saleReturn.Items)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    insertItemSql,
                    new
                    {
                        item.Id,
                        item.SaleReturnId,
                        item.SaleItemId,
                        item.ProductId,
                        item.ProductName,
                        item.BatchId,
                        item.BatchNumber,
                        Quantity = (double)item.Quantity,
                        UnitPrice = (double)item.UnitPrice,
                        TaxableAmount = (double)item.TaxableAmount,
                        CgstRate = (double)item.CgstRate,
                        CgstAmount = (double)item.CgstAmount,
                        SgstRate = (double)item.SgstRate,
                        SgstAmount = (double)item.SgstAmount,
                        IgstRate = (double)item.IgstRate,
                        IgstAmount = (double)item.IgstAmount,
                        NetAmount = (double)item.NetAmount,
                        RestockDecision = (int)item.RestockDecision,
                        item.Reason
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                if (item.RestockDecision == RestockDecision.RestockToAvailable)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        restockSql,
                        new
                        {
                            qty = (double)item.Quantity,
                            now = DateTime.UtcNow.ToString("o"),
                            batchId = item.BatchId,
                            warehouseId = saleReturn.WarehouseId
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }
            }

            // 3. Record Stock Movements
            foreach (var m in stockMovements)
            {
                await _stockRepository.RecordStockMovementAsync(
                    m,
                    (System.Data.Common.DbTransaction)transaction,
                    cancellationToken);
            }

            // 4. Update Original Sale Status to Returned
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE sales SET status = @status WHERE id = @saleId;",
                new { status = (int)SaleStatus.Returned, saleId = saleReturn.OriginalSaleId },
                transaction,
                cancellationToken: cancellationToken));

            // 5. Automatic Double-Entry Credit Note Accounting Posting
            // Debit: Sales Return / Sales Revenue Reduction (acc_sales)
            // Debit: Output GST Reversal (acc_output_cgst, acc_output_sgst, acc_output_igst)
            // Credit: Cash on Hand (acc_cash) or Customer Debtors (acc_debtors)
            var journalEntryId = $"je_cn_{saleReturn.Id}";
            var voucherNo = $"CN-{saleReturn.CreditNoteNo}";
            var cnNarration = $"Credit Note {saleReturn.CreditNoteNo} (Ref Inv: {saleReturn.OriginalInvoiceNo}) - {saleReturn.CustomerName ?? "Customer"}";

            const string insertJournalSql = @"
                INSERT OR IGNORE INTO journal_entries (
                    id, org_id, branch_id, voucher_number, voucher_type, voucher_date,
                    narration, reference_id, reference_type, created_by_user_id, created_at
                ) VALUES (
                    @Id, @OrgId, @BranchId, @VoucherNumber, 7, @VoucherDate,
                    @Narration, @ReferenceId, 'CREDIT_NOTE', @CreatedByUserId, datetime('now')
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertJournalSql,
                new
                {
                    Id = journalEntryId,
                    saleReturn.OrgId,
                    saleReturn.BranchId,
                    VoucherNumber = voucherNo,
                    VoucherDate = saleReturn.ReturnDate.ToString("o"),
                    Narration = cnNarration,
                    ReferenceId = saleReturn.Id,
                    CreatedByUserId = saleReturn.UserId
                },
                transaction,
                cancellationToken: cancellationToken));

            const string insertLineSql = @"
                INSERT INTO journal_lines (id, journal_entry_id, account_id, account_name, debit_amount, credit_amount, narration)
                VALUES (@Id, @JournalEntryId, @AccountId, @AccountName, CAST(@DebitAmount AS REAL), CAST(@CreditAmount AS REAL), @Narration);
            ";

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
                        Narration = cnNarration
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

            var totalCgst = saleReturn.Items.Sum(i => i.CgstAmount);
            var totalSgst = saleReturn.Items.Sum(i => i.SgstAmount);
            var totalIgst = saleReturn.Items.Sum(i => i.IgstAmount);

            // Dr Sales Return / Sales A/c
            await PostLine("acc_sales", "Pharmacy Medicine Sales A/c", saleReturn.Subtotal, 0);

            // Dr Output GST Reversal
            if (totalCgst > 0) await PostLine("acc_output_cgst", "Output CGST A/c", totalCgst, 0);
            if (totalSgst > 0) await PostLine("acc_output_sgst", "Output SGST A/c", totalSgst, 0);
            if (totalIgst > 0) await PostLine("acc_output_igst", "Output IGST A/c", totalIgst, 0);

            // Dr Roundoff if positive, or Cr Roundoff if negative
            if (saleReturn.RoundOff > 0)
                await PostLine("acc_roundoff", "Round Off Expense / Income", saleReturn.RoundOff, 0);
            else if (saleReturn.RoundOff < 0)
                await PostLine("acc_roundoff", "Round Off Expense / Income", 0, Math.Abs(saleReturn.RoundOff));

            // Cr Cash / Bank / Customer Debtors
            var settlementAccount = saleReturn.RefundMode == PaymentMode.Credit ? "acc_debtors" : "acc_cash";
            var settlementAccountName = saleReturn.RefundMode == PaymentMode.Credit ? "Sundry Debtors (Customers)" : "Cash on Hand";
            await PostLine(settlementAccount, settlementAccountName, 0, saleReturn.TotalAmount);

            // 6. Enqueue Outbox Event
            await _outboxRepository.EnqueueEventAsync(
                outboxEvent,
                (System.Data.Common.DbTransaction)transaction,
                cancellationToken);

            transaction.Commit();

            return new SaleReturnResult(
                true,
                saleReturn.Id,
                saleReturn.CreditNoteNo,
                saleReturn.TotalAmount);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new SaleReturnResult(false, null, null, 0, ex.Message);
        }
    }
}
