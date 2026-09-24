using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Sales.Commands;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class SqliteIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseMigrator _migrator;
    private readonly SqliteProductSearchRepository _searchRepo;
    private readonly SqliteStockRepository _stockRepo;
    private readonly SqliteSaleRepository _saleRepo;
    private readonly SqliteOutboxRepository _outboxRepo;
    private readonly SqliteDocumentSequenceService _sequenceService;
    private readonly PosTransactionService _posService;

    public SqliteIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrator = new DatabaseMigrator(_connectionFactory);
        _searchRepo = new SqliteProductSearchRepository(_connectionFactory);
        _stockRepo = new SqliteStockRepository(_connectionFactory);
        _outboxRepo = new SqliteOutboxRepository(_connectionFactory);
        _sequenceService = new SqliteDocumentSequenceService(_connectionFactory);
        _saleRepo = new SqliteSaleRepository(_connectionFactory, _stockRepo, _outboxRepo);
        _posService = new PosTransactionService(_saleRepo, _sequenceService);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            var wal = _dbPath + "-wal";
            var shm = _dbPath + "-shm";
            if (File.Exists(wal)) File.Delete(wal);
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }

    private async Task SeedDataAsync()
    {
        await _migrator.MigrateAsync();

        using var conn = await _connectionFactory.CreateConnectionAsync();

        // 1. Insert Products
        await conn.ExecuteAsync(@"
            INSERT INTO products (
                id, org_id, name, brand_name, generic_name, composition, strength,
                dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
                schedule, is_prescription_required, is_cold_chain, is_narcotic,
                is_active, primary_barcode, manufacturer_name, created_at
            ) VALUES (
                'p1', 'org-1', 'Dolo 650mg Tablet', 'Dolo 650', 'Paracetamol', 'Paracetamol 650mg', '650mg',
                0, 15, 'TAB', '3004', 12.0,
                0, 0, 0, 0,
                1, '8901234567890', 'Micro Labs Ltd', datetime('now')
            ), (
                'p2', 'org-1', 'Augmentin 625 Duo Tablet', 'Augmentin', 'Amoxicillin and Potassium Clavulanate', 'Amoxicillin 500mg + Clavulanic Acid 125mg', '625mg',
                0, 10, 'TAB', '3004', 12.0,
                1, 1, 0, 0,
                1, '8909876543210', 'GSK Pharmaceuticals', datetime('now')
            );
        ");

        // 2. Insert Batches
        await conn.ExecuteAsync(@"
            INSERT INTO batches (id, product_id, org_id, batch_number, expiry_date, mrp, purchase_rate, sale_rate, created_at)
            VALUES 
            ('b1', 'p1', 'org-1', 'DL2409', datetime('now', '+365 days'), 30.50, 22.00, 30.00, datetime('now')),
            ('b2', 'p1', 'org-1', 'DL2410', datetime('now', '+500 days'), 32.00, 23.00, 32.00, datetime('now')),
            ('b3', 'p2', 'org-1', 'AUG2401', datetime('now', '+180 days'), 205.00, 150.00, 200.00, datetime('now'));
        ");

        // 3. Insert Stock Balances
        await conn.ExecuteAsync(@"
            INSERT INTO stock_balances (id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at)
            VALUES 
            ('sb1', 'b1', 'p1', 'wh-1', 100.0, 0.0, datetime('now')),
            ('sb2', 'b2', 'p1', 'wh-1', 50.0, 0.0, datetime('now')),
            ('sb3', 'b3', 'p2', 'wh-1', 10.0, 0.0, datetime('now'));
        ");
    }

    [Fact]
    public async Task Fts5Search_ReturnsResults_Under100msLatency()
    {
        await SeedDataAsync();

        // Warm up JIT
        await _searchRepo.SearchProductsAsync("dolo", "wh-1", 1);

        var sw = Stopwatch.StartNew();
        var results = await _searchRepo.SearchProductsAsync("dolo", "wh-1", 10);
        sw.Stop();

        Assert.NotEmpty(results);
        Assert.Equal("Dolo 650mg Tablet", results[0].Name);
        Assert.Equal("DL2409", results[0].BatchNumber); // FEFO nearest batch
        Assert.Equal(100.0m, results[0].AvailableQuantity);
        Assert.True(sw.ElapsedMilliseconds < 100, $"Search took {sw.ElapsedMilliseconds}ms, exceeding budget");
    }

    [Fact]
    public async Task BarcodeLookup_ResolvesProductAndNearestBatch()
    {
        await SeedDataAsync();

        var result = await _searchRepo.LookupByBarcodeAsync("8901234567890", "wh-1");

        Assert.NotNull(result);
        Assert.Equal("Dolo 650mg Tablet", result!.ProductName);
        Assert.Equal("DL2409", result.BatchNumber);
        Assert.Equal(30.50m, result.Mrp);
    }

    [Fact]
    public async Task AtomicStockDeduction_PreventsNegativeStock()
    {
        await SeedDataAsync();

        // 1. Deduct within available stock (10 available)
        var success1 = await _stockRepo.DeductStockAtomicAsync("b3", "wh-1", 6);
        Assert.True(success1);

        // 2. Try to deduct more than remaining stock (4 remaining, request 5)
        var success2 = await _stockRepo.DeductStockAtomicAsync("b3", "wh-1", 5);
        Assert.False(success2);

        // 3. Deduct exact remaining stock (4 remaining)
        var success3 = await _stockRepo.DeductStockAtomicAsync("b3", "wh-1", 4);
        Assert.True(success3);
    }

    [Fact]
    public async Task PosTransactionService_CommitsSaleAndGeneratesOutboxEvent()
    {
        await SeedDataAsync();

        var command = new CommitSaleCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            CounterId: "counter-1",
            WarehouseId: "wh-1",
            UserId: "user-1",
            DeviceId: "device-pos-1",
            CustomerId: null,
            CustomerName: "Walk-in",
            IsInterstate: false,
            PrescriptionRef: null,
            Items: new List<CartItemInput>
            {
                new CartItemInput(
                    ProductId: "p1",
                    ProductName: "Dolo 650mg Tablet",
                    BatchId: "b1",
                    BatchNumber: "DL2409",
                    ExpiryDate: DateTime.UtcNow.AddDays(365),
                    Quantity: 2,
                    UnitPrice: 30.00m,
                    Mrp: 30.50m,
                    GstRatePercent: 12.0m,
                    DiscountPercent: 0)
            },
            Payments: new List<SalePaymentInput>
            {
                new SalePaymentInput(PaymentMode.Cash, 67.00m)
            }
        );

        // Execute hot path commit
        var sw = Stopwatch.StartNew();
        var result = await _posService.ProcessSaleAsync(command);
        sw.Stop();

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.InvoiceNo);
        Assert.StartsWith("INV-", result.InvoiceNo!);
        Assert.Equal(67.00m, result.TotalAmount); // 60 subtotal + 7.20 GST = 67.20 -> 67 round
        Assert.True(sw.ElapsedMilliseconds < 200, $"Sale commit took {sw.ElapsedMilliseconds}ms, exceeding budget");

        // Verify outbox queue
        var pendingEvents = await _outboxRepo.GetPendingEventsAsync();
        Assert.NotEmpty(pendingEvents);
        Assert.Equal("SALE_COMMITTED", pendingEvents[0].EventType);
    }
}
