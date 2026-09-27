using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Sales.Commands;
using Medistock.Application.Sales.DTOs;
using Medistock.Application.Sales.Services;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class SaleReturnTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly DatabaseMigrator _migrator;
    private readonly SqliteStockRepository _stockRepo;
    private readonly SqliteSaleRepository _saleRepo;
    private readonly SqliteSaleReturnRepository _saleReturnRepo;
    private readonly SqliteOutboxRepository _outboxRepo;
    private readonly SqliteDocumentSequenceService _sequenceService;
    private readonly PosTransactionService _posService;
    private readonly SaleReturnService _saleReturnService;

    public SaleReturnTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_salereturn_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrator = new DatabaseMigrator(_connectionFactory);
        _outboxRepo = new SqliteOutboxRepository(_connectionFactory);
        _stockRepo = new SqliteStockRepository(_connectionFactory);
        _saleRepo = new SqliteSaleRepository(_connectionFactory, _stockRepo, _outboxRepo);
        _sequenceService = new SqliteDocumentSequenceService(_connectionFactory);
        _posService = new PosTransactionService(_saleRepo, _sequenceService);
        _saleReturnRepo = new SqliteSaleReturnRepository(_connectionFactory, _stockRepo, _outboxRepo);
        _saleReturnService = new SaleReturnService(_saleReturnRepo, _sequenceService);

        _migrator.MigrateAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    private async Task SeedDataAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        await connection.ExecuteAsync(@"
            INSERT INTO products (
                id, org_id, name, brand_name, generic_name, composition, strength,
                dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
                schedule, is_prescription_required, is_cold_chain, is_narcotic,
                is_active, primary_barcode, manufacturer_id, manufacturer_name,
                created_at, updated_at
            ) VALUES (
                'p_dolo', 'org-1', 'Dolo 650mg Tablet', 'Dolo 650', 'Paracetamol',
                'Paracetamol 650mg', '650mg', 0, 15, 'TAB', '3004', 12.0,
                0, 0, 0, 0, 1, '8901234567890', 'm1', 'Micro Labs Ltd',
                datetime('now'), datetime('now')
            );

            INSERT INTO batches (
                id, product_id, org_id, batch_number, expiry_date,
                mrp, purchase_rate, sale_rate, created_at
            ) VALUES (
                'b_dolo_1', 'p_dolo', 'org-1', 'DL2026', datetime('now', '+1 year'),
                35.0, 20.0, 30.0, datetime('now')
            );

            INSERT INTO stock_balances (
                id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at
            ) VALUES (
                'sb_dolo_1', 'b_dolo_1', 'p_dolo', 'wh-1', 100.0, 0.0, datetime('now')
            );
        ");
    }

    [Fact]
    public async Task ProcessSaleReturn_WithRestock_RestoresInventoryAndPostsCreditNote()
    {
        await SeedDataAsync();

        // 1. Commit Initial Sale (10 units @ ₹30 = ₹300 + 12% GST = ₹336)
        var saleCmd = new CommitSaleCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            CounterId: "counter-1",
            WarehouseId: "wh-1",
            UserId: "usr-1",
            DeviceId: "dev-1",
            CustomerId: "cust-1",
            CustomerName: "John Doe",
            IsInterstate: false,
            PrescriptionRef: null,
            Items: new List<CartItemInput>
            {
                new CartItemInput(
                    ProductId: "p_dolo",
                    ProductName: "Dolo 650mg Tablet",
                    BatchId: "b_dolo_1",
                    BatchNumber: "DL2026",
                    ExpiryDate: DateTime.UtcNow.AddDays(365),
                    Quantity: 10,
                    UnitPrice: 30.00m,
                    Mrp: 35.00m,
                    GstRatePercent: 12.0m,
                    DiscountPercent: 0)
            },
            Payments: new List<SalePaymentInput>
            {
                new SalePaymentInput(PaymentMode.Cash, 336.00m)
            }
        );

        var saleResult = await _posService.ProcessSaleAsync(saleCmd);
        Assert.True(saleResult.IsSuccess);

        // Verify stock after sale is 90
        using var conn = await _connectionFactory.CreateConnectionAsync();
        var stockAfterSale = await conn.ExecuteScalarAsync<double>(
            "SELECT quantity FROM stock_balances WHERE batch_id = 'b_dolo_1' AND warehouse_id = 'wh-1';");
        Assert.Equal(90.0, stockAfterSale);

        // 2. Fetch sale details
        var salesList = await _saleReturnService.GetSalesHistoryAsync("org-1", "branch-1");
        Assert.NotEmpty(salesList);
        var saleDetail = await _saleReturnService.GetSaleDetailsAsync(saleResult.SaleId!);
        Assert.NotNull(saleDetail);
        Assert.Single(saleDetail.Items);

        // 3. Process Return of 2 units with RestockToAvailable
        var returnCmd = new ProcessSaleReturnCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            CounterId: "counter-1",
            WarehouseId: "wh-1",
            OriginalSaleId: saleDetail.Id,
            OriginalInvoiceNo: saleDetail.InvoiceNo,
            CustomerId: saleDetail.CustomerId,
            CustomerName: saleDetail.CustomerName,
            UserId: "usr-1",
            DeviceId: "dev-1",
            Reason: "Doctor modified dose",
            RefundMode: PaymentMode.Cash,
            Items: new List<ProcessSaleReturnItemInput>
            {
                new ProcessSaleReturnItemInput(
                    SaleItemId: saleDetail.Items[0].Id,
                    ProductId: saleDetail.Items[0].ProductId,
                    ProductName: saleDetail.Items[0].ProductName,
                    BatchId: saleDetail.Items[0].BatchId,
                    BatchNumber: saleDetail.Items[0].BatchNumber,
                    Quantity: 2,
                    UnitPrice: 30.00m,
                    CgstRate: 6.0m,
                    SgstRate: 6.0m,
                    IgstRate: 0.0m,
                    RestockDecision: RestockDecision.RestockToAvailable,
                    Reason: "Doctor modified dose"
                )
            }
        );

        var returnResult = await _saleReturnService.ProcessReturnAsync(returnCmd);
        Assert.True(returnResult.IsSuccess, returnResult.ErrorMessage);
        Assert.NotNull(returnResult.CreditNoteNo);
        Assert.StartsWith("CN-", returnResult.CreditNoteNo!);
        Assert.Equal(67.00m, returnResult.TotalRefundAmount); // 60 taxable + 7.20 GST = 67.20 -> 67 round

        // 4. Verify Stock Restored to 92
        var stockAfterReturn = await conn.ExecuteScalarAsync<double>(
            "SELECT quantity FROM stock_balances WHERE batch_id = 'b_dolo_1' AND warehouse_id = 'wh-1';");
        Assert.Equal(92.0, stockAfterReturn);

        // 5. Verify Credit Note Double-Entry Voucher in Ledger
        var cnVoucher = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM journal_entries WHERE reference_id = @id AND voucher_type = 7;",
            new { id = returnResult.SaleReturnId });
        Assert.NotNull(cnVoucher);

        var lines = (await conn.QueryAsync<dynamic>(
            "SELECT * FROM journal_lines WHERE journal_entry_id = @jeId;",
            new { jeId = (string)cnVoucher!.id })).ToList();
        Assert.True(lines.Count >= 2);

        // Verify outbox event queued
        var outboxEvents = await _outboxRepo.GetPendingEventsAsync();
        Assert.Contains(outboxEvents, e => e.EventType == "SALE_RETURNED");
    }

    [Fact]
    public async Task ProcessSaleReturn_QuarantineDamaged_DoesNotRestockAvailable()
    {
        await SeedDataAsync();

        // Commit sale of 5 units
        var saleCmd = new CommitSaleCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            CounterId: "counter-1",
            WarehouseId: "wh-1",
            UserId: "usr-1",
            DeviceId: "dev-1",
            CustomerId: null,
            CustomerName: "Walk-in",
            IsInterstate: false,
            PrescriptionRef: null,
            Items: new List<CartItemInput>
            {
                new CartItemInput("p_dolo", "Dolo 650mg Tablet", "b_dolo_1", "DL2026", DateTime.UtcNow.AddDays(365), 5, 30.00m, 35.00m, 12.0m, 0)
            },
            Payments: new List<SalePaymentInput> { new SalePaymentInput(PaymentMode.Cash, 168.00m) }
        );

        var saleResult = await _posService.ProcessSaleAsync(saleCmd);
        Assert.True(saleResult.IsSuccess);

        var saleDetail = await _saleReturnService.GetSaleDetailsAsync(saleResult.SaleId!);
        Assert.NotNull(saleDetail);

        // Return 1 unit with QuarantineDamaged (broken strip)
        var returnCmd = new ProcessSaleReturnCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            CounterId: "counter-1",
            WarehouseId: "wh-1",
            OriginalSaleId: saleDetail.Id,
            OriginalInvoiceNo: saleDetail.InvoiceNo,
            CustomerId: null,
            CustomerName: "Walk-in",
            UserId: "usr-1",
            DeviceId: "dev-1",
            Reason: "Broken packaging",
            RefundMode: PaymentMode.Cash,
            Items: new List<ProcessSaleReturnItemInput>
            {
                new ProcessSaleReturnItemInput(
                    saleDetail.Items[0].Id,
                    saleDetail.Items[0].ProductId,
                    saleDetail.Items[0].ProductName,
                    saleDetail.Items[0].BatchId,
                    saleDetail.Items[0].BatchNumber,
                    1,
                    30.00m,
                    6.0m,
                    6.0m,
                    0.0m,
                    RestockDecision.QuarantineDamaged,
                    "Broken packaging"
                )
            }
        );

        var returnResult = await _saleReturnService.ProcessReturnAsync(returnCmd);
        Assert.True(returnResult.IsSuccess);

        // Verify available stock remains 95 (NOT incremented because quarantined)
        using var conn = await _connectionFactory.CreateConnectionAsync();
        var stockAfterQuarantine = await conn.ExecuteScalarAsync<double>(
            "SELECT quantity FROM stock_balances WHERE batch_id = 'b_dolo_1' AND warehouse_id = 'wh-1';");
        Assert.Equal(95.0, stockAfterQuarantine);

        // Verify movement recorded as Damage
        var movementType = await conn.ExecuteScalarAsync<int>(
            "SELECT movement_type FROM stock_movements WHERE reference_id = @cnNo;",
            new { cnNo = returnResult.CreditNoteNo });
        Assert.Equal((int)StockMovementType.Damage, movementType);
    }
}
