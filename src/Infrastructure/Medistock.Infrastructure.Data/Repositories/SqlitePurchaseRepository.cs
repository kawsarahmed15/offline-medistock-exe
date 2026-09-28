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
        var isDuplicate = await IsDuplicateInvoiceAsync(
            invoice.OrgId, invoice.SupplierId, invoice.SupplierInvoiceNo, cancellationToken);
        if (isDuplicate)
        {
            return new PurchasePostingResult(false, null, 0, 0, 0,
                $"Duplicate invoice: '{invoice.SupplierInvoiceNo}' already exists for this supplier. Cancel the existing invoice first, then re-enter.");
        }

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
            // 2. Ensure products exist in products table and insert invoice items
            const string ensureProductSql = @"
                INSERT INTO products (
                    id, org_id, name, brand_name, generic_name, composition, strength,
                    dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
                    schedule, is_prescription_required, is_cold_chain, is_narcotic,
                    is_active, created_at
                ) VALUES (
                    @Id, @OrgId, @Name, @Name, @Name, '', '',
                    0, 10, 'TAB', @HsnCode, @GstRatePercent,
                    0, 0, 0, 0,
                    1, @CreatedAt
                )
                ON CONFLICT(id) DO UPDATE SET
                    name = excluded.name,
                    hsn_code = excluded.hsn_code,
                    gst_rate_percent = excluded.gst_rate_percent;
            ";

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
                    ensureProductSql,
                    new
                    {
                        Id = item.ProductId,
                        invoice.OrgId,
                        Name = item.ProductName,
                        item.HsnCode,
                        GstRatePercent = (double)item.GstRatePercent,
                        CreatedAt = DateTime.UtcNow.ToString("o")
                    },
                    transaction,
                    cancellationToken: cancellationToken));

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
            const string findBatchSql = @"
                SELECT id FROM batches 
                WHERE product_id = @ProductId AND batch_number = @BatchNumber AND org_id = @OrgId 
                LIMIT 1;
            ";

            const string insertBatchSql = @"
                INSERT INTO batches (
                    id, product_id, org_id, batch_number, expiry_date,
                    manufacturing_date, mrp, purchase_rate, sale_rate, created_at
                ) VALUES (
                    @Id, @ProductId, @OrgId, @BatchNumber, @ExpiryDate,
                    @ManufacturingDate, @Mrp, @PurchaseRate, @SaleRate, @CreatedAt
                );
            ";

            const string updateBatchSql = @"
                UPDATE batches SET
                    mrp = @Mrp,
                    purchase_rate = @PurchaseRate,
                    sale_rate = @SaleRate,
                    expiry_date = @ExpiryDate
                WHERE id = @Id;
            ";

            const string findStockSql = @"
                SELECT id FROM stock_balances 
                WHERE batch_id = @BatchId AND warehouse_id = @WarehouseId 
                LIMIT 1;
            ";

            const string insertStockSql = @"
                INSERT INTO stock_balances (
                    id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at
                ) VALUES (
                    @Id, @BatchId, @ProductId, @WarehouseId, @Quantity, 0.0, @LastUpdatedAt
                );
            ";

            const string updateStockSql = @"
                UPDATE stock_balances SET
                    quantity = quantity + @Quantity,
                    last_updated_at = @LastUpdatedAt
                WHERE id = @Id;
            ";

            for (int i = 0; i < batchesToUpsert.Count; i++)
            {
                var batch = batchesToUpsert[i];
                var movement = stockMovements[i];

                var existingBatchId = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                    findBatchSql,
                    new { batch.ProductId, batch.BatchNumber, batch.OrgId },
                    transaction,
                    cancellationToken: cancellationToken));

                var actualBatchId = !string.IsNullOrEmpty(existingBatchId) ? existingBatchId : batch.Id;

                if (!string.IsNullOrEmpty(existingBatchId))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        updateBatchSql,
                        new
                        {
                            Id = existingBatchId,
                            ExpiryDate = batch.ExpiryDate.ToString("o"),
                            Mrp = (double)batch.Mrp,
                            PurchaseRate = (double)batch.PurchaseRate,
                            SaleRate = (double)batch.SaleRate
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }
                else
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        insertBatchSql,
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
                }

                var existingStockId = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                    findStockSql,
                    new { BatchId = actualBatchId, invoice.WarehouseId },
                    transaction,
                    cancellationToken: cancellationToken));

                if (!string.IsNullOrEmpty(existingStockId))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        updateStockSql,
                        new
                        {
                            Id = existingStockId,
                            Quantity = (double)movement.Quantity,
                            LastUpdatedAt = DateTime.UtcNow.ToString("o")
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }
                else
                {
                    var sbId = "sb_" + actualBatchId;
                    await connection.ExecuteAsync(new CommandDefinition(
                        insertStockSql,
                        new
                        {
                            Id = sbId,
                            BatchId = actualBatchId,
                            batch.ProductId,
                            invoice.WarehouseId,
                            Quantity = (double)movement.Quantity,
                            LastUpdatedAt = DateTime.UtcNow.ToString("o")
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }
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

            // 6. Automatic Double-Entry Accounting Posting
            var journalEntryId = $"je_pur_{invoice.Id}";
            var voucherNo = $"PUR-{invoice.SupplierInvoiceNo}";
            var purchaseNarration = $"Purchase Invoice {invoice.SupplierInvoiceNo} from {invoice.SupplierName}";

            const string insertJournalSql = @"
                INSERT OR IGNORE INTO journal_entries (
                    id, org_id, branch_id, voucher_number, voucher_type, voucher_date,
                    narration, reference_id, reference_type, created_by_user_id, created_at
                ) VALUES (
                    @Id, @OrgId, @BranchId, @VoucherNumber, 2, @VoucherDate,
                    @Narration, @ReferenceId, 'PURCHASE_INVOICE', @CreatedByUserId, datetime('now')
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertJournalSql,
                new
                {
                    Id = journalEntryId,
                    invoice.OrgId,
                    invoice.BranchId,
                    VoucherNumber = voucherNo,
                    VoucherDate = invoice.SupplierInvoiceDate.ToString("o"),
                    Narration = purchaseNarration,
                    ReferenceId = invoice.Id,
                    CreatedByUserId = invoice.CreatedByUserId
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
                        Narration = purchaseNarration
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

            // Debit side: Purchases Account
            await PostLine("acc_purchases", "Pharmacy Medicine Purchases A/c", invoice.TaxableAmount, 0m);

            // Input GST
            if (invoice.CgstAmount > 0) await PostLine("acc_input_cgst", "Input CGST A/c", invoice.CgstAmount, 0m);
            if (invoice.SgstAmount > 0) await PostLine("acc_input_sgst", "Input SGST A/c", invoice.SgstAmount, 0m);
            if (invoice.IgstAmount > 0) await PostLine("acc_input_igst", "Input IGST A/c", invoice.IgstAmount, 0m);

            // Round Off
            if (invoice.RoundOff > 0)
            {
                await PostLine("acc_roundoff", "Round Off Expense / Income", invoice.RoundOff, 0m);
            }
            else if (invoice.RoundOff < 0)
            {
                await PostLine("acc_roundoff", "Round Off Expense / Income", 0m, Math.Abs(invoice.RoundOff));
            }

            // Credit side: Sundry Creditors
            await PostLine("acc_creditors", "Sundry Creditors (Suppliers)", 0m, invoice.GrandTotal);

            // 7. Enqueue Outbox Event
            await _outboxRepository.EnqueueEventAsync(
                outboxEvent, (System.Data.Common.DbTransaction)transaction, cancellationToken);

            // 8. Commit
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
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                pi.id AS Id,
                pi.supplier_name AS SupplierName,
                pi.supplier_gstin AS SupplierGstin,
                pi.supplier_invoice_no AS SupplierInvoiceNo,
                pi.supplier_invoice_date AS SupplierInvoiceDateStr,
                pi.status AS Status,
                pi.taxable_amount AS TaxableAmount,
                pi.cgst_amount AS CgstAmount,
                pi.sgst_amount AS SgstAmount,
                pi.igst_amount AS IgstAmount,
                pi.grand_total AS GrandTotal,
                COUNT(pii.id) AS ItemCount,
                pi.created_at AS CreatedAtStr,
                pi.posted_at AS PostedAtStr
            FROM purchase_invoices pi
            LEFT JOIN purchase_invoice_items pii ON pii.purchase_invoice_id = pi.id
            WHERE pi.org_id = @orgId AND pi.branch_id = @branchId
            GROUP BY pi.id
            ORDER BY pi.created_at DESC
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
                ItemCount: Convert.ToInt32(r.ItemCount),
                CreatedAt: crDate,
                PostedAt: postDate
            ));
        }

        return list;
    }

    public async Task<PurchaseInvoiceDetailsDto?> GetPurchaseInvoiceDetailsAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string invSql = @"
            SELECT 
                id AS Id,
                org_id AS OrgId,
                branch_id AS BranchId,
                warehouse_id AS WarehouseId,
                supplier_id AS SupplierId,
                supplier_name AS SupplierName,
                supplier_gstin AS SupplierGstin,
                supplier_invoice_no AS SupplierInvoiceNo,
                supplier_invoice_date AS SupplierInvoiceDateStr,
                status AS Status,
                is_interstate AS IsInterstate,
                subtotal AS Subtotal,
                discount_amount AS DiscountAmount,
                taxable_amount AS TaxableAmount,
                cgst_amount AS CgstAmount,
                sgst_amount AS SgstAmount,
                igst_amount AS IgstAmount,
                round_off AS RoundOff,
                grand_total AS GrandTotal,
                notes AS Notes,
                created_by_user_id AS CreatedByUserId,
                created_at AS CreatedAtStr,
                posted_at AS PostedAtStr
            FROM purchase_invoices
            WHERE id = @invoiceId;
        ";

        var invRow = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(invSql, new { invoiceId }, cancellationToken: cancellationToken));

        if (invRow == null) return null;

        const string itemsSql = @"
            SELECT 
                id AS Id,
                product_id AS ProductId,
                product_name AS ProductName,
                hsn_code AS HsnCode,
                batch_number AS BatchNumber,
                expiry_date AS ExpiryDateStr,
                manufacturing_date AS ManufacturingDateStr,
                quantity AS Quantity,
                free_quantity AS FreeQuantity,
                unit_price AS UnitPrice,
                mrp AS Mrp,
                sale_rate AS SaleRate,
                discount_pct AS DiscountPct,
                discount_amount AS DiscountAmount,
                taxable_amount AS TaxableAmount,
                gst_rate_percent AS GstRatePercent,
                cgst_amount AS CgstAmount,
                sgst_amount AS SgstAmount,
                igst_amount AS IgstAmount,
                net_amount AS NetAmount
            FROM purchase_invoice_items
            WHERE purchase_invoice_id = @invoiceId
            ORDER BY rowid ASC;
        ";

        var itemRows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(itemsSql, new { invoiceId }, cancellationToken: cancellationToken));

        var items = new List<PurchaseInvoiceItemDto>();
        foreach (var it in itemRows)
        {
            DateTime exp = DateTime.TryParse((string)it.ExpiryDateStr, out DateTime e1) ? e1 : DateTime.UtcNow;
            DateTime? mfg = !string.IsNullOrEmpty((string?)it.ManufacturingDateStr) && DateTime.TryParse((string)it.ManufacturingDateStr, out DateTime m1) ? m1 : null;

            decimal qty = Convert.ToDecimal(it.Quantity);
            decimal free = Convert.ToDecimal(it.FreeQuantity);
            decimal net = Convert.ToDecimal(it.NetAmount);
            decimal totalQty = qty + free;
            decimal landedCost = totalQty > 0 ? Math.Round(net / totalQty, 4) : 0;

            items.Add(new PurchaseInvoiceItemDto(
                Id: (string)it.Id,
                ProductId: (string)it.ProductId,
                ProductName: (string)it.ProductName,
                HsnCode: (string)it.HsnCode,
                BatchNumber: (string)it.BatchNumber,
                ExpiryDate: exp,
                ManufacturingDate: mfg,
                Quantity: qty,
                FreeQuantity: free,
                TotalQuantity: totalQty,
                UnitPrice: Convert.ToDecimal(it.UnitPrice),
                Mrp: Convert.ToDecimal(it.Mrp),
                SaleRate: Convert.ToDecimal(it.SaleRate),
                DiscountPct: Convert.ToDecimal(it.DiscountPct),
                DiscountAmount: Convert.ToDecimal(it.DiscountAmount),
                TaxableAmount: Convert.ToDecimal(it.TaxableAmount),
                GstRatePercent: Convert.ToDecimal(it.GstRatePercent),
                CgstAmount: Convert.ToDecimal(it.CgstAmount),
                SgstAmount: Convert.ToDecimal(it.SgstAmount),
                IgstAmount: Convert.ToDecimal(it.IgstAmount),
                NetAmount: net,
                LandedCostPerUnit: landedCost
            ));
        }

        DateTime invDate = DateTime.TryParse((string)invRow.SupplierInvoiceDateStr, out DateTime d1) ? d1 : DateTime.UtcNow;
        DateTime crDate = DateTime.TryParse((string)invRow.CreatedAtStr, out DateTime d2) ? d2 : DateTime.UtcNow;
        DateTime? postDate = !string.IsNullOrEmpty((string?)invRow.PostedAtStr) && DateTime.TryParse((string)invRow.PostedAtStr, out DateTime d3) ? d3 : null;

        return new PurchaseInvoiceDetailsDto(
            Id: (string)invRow.Id,
            OrgId: (string)invRow.OrgId,
            BranchId: (string)invRow.BranchId,
            WarehouseId: (string)invRow.WarehouseId,
            SupplierId: (string)invRow.SupplierId,
            SupplierName: (string)invRow.SupplierName,
            SupplierGstin: (string?)invRow.SupplierGstin,
            SupplierInvoiceNo: (string)invRow.SupplierInvoiceNo,
            SupplierInvoiceDate: invDate,
            Status: (PurchaseInvoiceStatus)(int)invRow.Status,
            IsInterstate: Convert.ToInt32(invRow.IsInterstate) == 1,
            Subtotal: Convert.ToDecimal(invRow.Subtotal),
            DiscountAmount: Convert.ToDecimal(invRow.DiscountAmount),
            TaxableAmount: Convert.ToDecimal(invRow.TaxableAmount),
            CgstAmount: Convert.ToDecimal(invRow.CgstAmount),
            SgstAmount: Convert.ToDecimal(invRow.SgstAmount),
            IgstAmount: Convert.ToDecimal(invRow.IgstAmount),
            RoundOff: Convert.ToDecimal(invRow.RoundOff),
            GrandTotal: Convert.ToDecimal(invRow.GrandTotal),
            Notes: (string?)invRow.Notes,
            CreatedByUserId: (string)invRow.CreatedByUserId,
            CreatedAt: crDate,
            PostedAt: postDate,
            Items: items
        );
    }

    public async Task<PurchaseInvoice?> GetPurchaseInvoiceByIdAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var details = await GetPurchaseInvoiceDetailsAsync(invoiceId, cancellationToken);
        if (details == null) return null;

        var invoice = PurchaseInvoice.Create(
            id: details.Id,
            orgId: details.OrgId,
            branchId: details.BranchId,
            warehouseId: details.WarehouseId,
            supplierId: details.SupplierId,
            supplierName: details.SupplierName,
            supplierGstin: details.SupplierGstin,
            supplierInvoiceNo: details.SupplierInvoiceNo,
            supplierInvoiceDate: details.SupplierInvoiceDate,
            isInterstate: details.IsInterstate,
            createdByUserId: details.CreatedByUserId,
            notes: details.Notes
        );

        foreach (var item in details.Items)
        {
            invoice.AddItem(PurchaseInvoiceItem.Create(
                id: item.Id,
                purchaseInvoiceId: details.Id,
                productId: item.ProductId,
                productName: item.ProductName,
                hsnCode: item.HsnCode,
                batchNumber: item.BatchNumber,
                expiryDate: item.ExpiryDate,
                quantity: item.Quantity,
                freeQuantity: item.FreeQuantity,
                unitPrice: item.UnitPrice,
                mrp: item.Mrp,
                saleRate: item.SaleRate,
                discountPct: item.DiscountPct,
                gstRatePercent: item.GstRatePercent,
                isInterstate: details.IsInterstate,
                manufacturingDate: item.ManufacturingDate
            ));
        }

        if (details.Status == PurchaseInvoiceStatus.Posted)
        {
            invoice.MarkPosted();
        }

        return invoice;
    }

    public async Task<PurchaseKpiSummaryDto> GetPurchaseKpiSummaryAsync(string orgId, string branchId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                CAST(IFNULL(SUM(grand_total), 0.0) AS REAL) AS TotalPurchaseAmount,
                COUNT(1) AS TotalInvoicesCount
            FROM purchase_invoices
            WHERE org_id = @orgId AND branch_id = @branchId;

            SELECT 
                COUNT(1) AS TotalSuppliersCount,
                CAST(IFNULL(SUM(outstanding_balance), 0.0) AS REAL) AS TotalOutstandingPayable
            FROM suppliers
            WHERE org_id = @orgId AND is_active = 1;
        ";

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { orgId, branchId }, cancellationToken: cancellationToken));

        var purRow = await multi.ReadSingleOrDefaultAsync<dynamic>();
        var supRow = await multi.ReadSingleOrDefaultAsync<dynamic>();

        decimal totalPur = purRow != null ? Convert.ToDecimal(purRow.TotalPurchaseAmount) : 0m;
        int totalInv = purRow != null ? Convert.ToInt32(purRow.TotalInvoicesCount) : 0;
        int totalSup = supRow != null ? Convert.ToInt32(supRow.TotalSuppliersCount) : 0;
        decimal totalPayable = supRow != null ? Convert.ToDecimal(supRow.TotalOutstandingPayable) : 0m;

        return new PurchaseKpiSummaryDto(
            TotalPurchaseAmount: totalPur,
            TotalInvoicesCount: totalInv,
            TotalSuppliersCount: totalSup,
            TotalOutstandingPayable: totalPayable
        );
    }

    public async Task<bool> IsDuplicateInvoiceAsync(
        string orgId,
        string supplierId,
        string supplierInvoiceNo,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM purchase_invoices WHERE org_id = @orgId AND supplier_id = @supplierId AND supplier_invoice_no = @supplierInvoiceNo AND status != 2;",
                new { orgId, supplierId, supplierInvoiceNo },
                cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<PurchaseCancelResult> CancelPurchaseInvoiceAsync(
        string invoiceId,
        string cancelledByUserId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Fetch invoice header — must exist and be in Posted state
            var invRow = await connection.QuerySingleOrDefaultAsync<dynamic>(
                new CommandDefinition(
                    "SELECT id, org_id, branch_id, warehouse_id, supplier_id, grand_total, status FROM purchase_invoices WHERE id = @invoiceId;",
                    new { invoiceId }, transaction, cancellationToken: cancellationToken));

            if (invRow == null)
                return new PurchaseCancelResult(false, "Invoice not found.");

            int status = (int)invRow.status;
            if (status == 2)
                return new PurchaseCancelResult(false, "Invoice is already cancelled.");
            if (status == 0)
                return new PurchaseCancelResult(false, "Draft invoice cannot be cancelled.");

            string orgId = (string)invRow.org_id;
            string branchId = (string)invRow.branch_id;
            string warehouseId = (string)invRow.warehouse_id;
            string supplierId = (string)invRow.supplier_id;
            decimal grandTotal = Convert.ToDecimal(invRow.grand_total);

            // 2. Fetch all line items to reverse stock
            var items = await connection.QueryAsync<dynamic>(
                new CommandDefinition(
                    "SELECT product_id, batch_number, quantity, free_quantity FROM purchase_invoice_items WHERE purchase_invoice_id = @invoiceId;",
                    new { invoiceId }, transaction, cancellationToken: cancellationToken));

            // 3. For each item, reverse stock_balances
            foreach (var item in items)
            {
                string productId = (string)item.product_id;
                string batchNumber = (string)item.batch_number;
                decimal qty = Convert.ToDecimal(item.quantity);
                decimal freeQty = Convert.ToDecimal(item.free_quantity);
                decimal totalQty = qty + freeQty;

                var batchId = await connection.ExecuteScalarAsync<string?>(
                    new CommandDefinition(
                        "SELECT id FROM batches WHERE product_id = @productId AND batch_number = @batchNumber AND org_id = @orgId LIMIT 1;",
                        new { productId, batchNumber, orgId }, transaction, cancellationToken: cancellationToken));

                if (batchId != null)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            "UPDATE stock_balances SET quantity = MAX(0, quantity - @qty), last_updated_at = @now WHERE batch_id = @batchId AND warehouse_id = @warehouseId;",
                            new { qty = (double)totalQty, now = DateTime.UtcNow.ToString("o"), batchId, warehouseId },
                            transaction, cancellationToken: cancellationToken));

                    // 4. Insert reversal stock movement
                    var movId = Guid.NewGuid().ToString("N");
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            @"INSERT INTO stock_movements (id, org_id, branch_id, warehouse_id, batch_id, product_id,
                                movement_type, quantity, reference_type, reference_id, unit_cost, user_id, device_id, created_at)
                              VALUES (@Id, @OrgId, @BranchId, @WarehouseId, @BatchId, @ProductId,
                                5, @Qty, 'PURCHASE_CANCEL', @InvoiceId, 0.0, @UserId, 'DESKTOP-01', @CreatedAt);",
                            new { Id = movId, OrgId = orgId, BranchId = branchId, WarehouseId = warehouseId,
                                  BatchId = batchId, ProductId = productId,
                                  Qty = (double)totalQty, InvoiceId = invoiceId,
                                  UserId = cancelledByUserId, CreatedAt = DateTime.UtcNow.ToString("o") },
                            transaction, cancellationToken: cancellationToken));
                }
            }

            // 5. Reverse supplier outstanding balance (subtract grandTotal)
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE suppliers SET outstanding_balance = MAX(0, outstanding_balance - @delta) WHERE id = @supplierId;",
                    new { delta = (double)grandTotal, supplierId },
                    transaction, cancellationToken: cancellationToken));

            // 6. Mark invoice CANCELLED and record who cancelled and when
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE purchase_invoices SET status = 2, notes = COALESCE(notes, '') || ' [CANCELLED by ' || @userId || ' at ' || @cancelledAt || ']' WHERE id = @invoiceId;",
                    new { userId = cancelledByUserId, cancelledAt = DateTime.UtcNow.ToString("o"), invoiceId },
                    transaction, cancellationToken: cancellationToken));

            // 7. Reversal journal entry
            var journalId = $"je_cancel_{invoiceId}";
            var prefix = invoiceId.Length >= 8 ? invoiceId[..8].ToUpperInvariant() : invoiceId.ToUpperInvariant();
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"INSERT OR IGNORE INTO journal_entries
                        (id, org_id, branch_id, voucher_number, voucher_type, voucher_date, narration, reference_id, reference_type, created_by_user_id, created_at)
                      VALUES (@Id, @OrgId, @BranchId, @VoucherNo, 2, datetime('now'), @Narration, @RefId, 'PURCHASE_CANCEL', @UserId, datetime('now'));",
                    new { Id = journalId, OrgId = orgId, BranchId = branchId,
                          VoucherNo = $"CANCEL-PUR-{prefix}",
                          Narration = $"Cancellation reversal of purchase invoice {invoiceId}",
                          RefId = invoiceId, UserId = cancelledByUserId },
                    transaction, cancellationToken: cancellationToken));

            // Reversal lines: Credit Purchases, Debit Creditors
            var lineId1 = $"jl_{Guid.NewGuid():N}";
            var lineId2 = $"jl_{Guid.NewGuid():N}";
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO journal_lines (id, journal_entry_id, account_id, account_name, debit_amount, credit_amount, narration) VALUES (@Id, @JeId, 'acc_purchases', 'Pharmacy Medicine Purchases A/c', 0.0, CAST(@Amt AS REAL), @Narr);",
                    new { Id = lineId1, JeId = journalId, Amt = (double)grandTotal, Narr = "Purchase reversal" },
                    transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO journal_lines (id, journal_entry_id, account_id, account_name, debit_amount, credit_amount, narration) VALUES (@Id, @JeId, 'acc_creditors', 'Sundry Creditors (Suppliers)', CAST(@Amt AS REAL), 0.0, @Narr);",
                    new { Id = lineId2, JeId = journalId, Amt = (double)grandTotal, Narr = "Purchase reversal" },
                    transaction, cancellationToken: cancellationToken));

            transaction.Commit();
            return new PurchaseCancelResult(true);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new PurchaseCancelResult(false, ex.Message);
        }
    }
}

