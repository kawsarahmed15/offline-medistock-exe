using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Inventory.DTOs;
using Medistock.Application.Inventory.Services;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class StockTransferTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly DatabaseMigrator _migrator;
    private readonly SqliteStockRepository _stockRepo;
    private readonly SqliteStockTransferRepository _transferRepo;
    private readonly SqliteOutboxRepository _outboxRepo;
    private readonly SqliteDocumentSequenceService _sequenceService;
    private readonly StockTransferService _transferService;

    public StockTransferTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_transfer_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrator = new DatabaseMigrator(_connectionFactory);
        _outboxRepo = new SqliteOutboxRepository(_connectionFactory);
        _stockRepo = new SqliteStockRepository(_connectionFactory);
        _sequenceService = new SqliteDocumentSequenceService(_connectionFactory);
        _transferRepo = new SqliteStockTransferRepository(_connectionFactory, _stockRepo, _outboxRepo);
        _transferService = new StockTransferService(_transferRepo, _sequenceService);

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
                'p_amox', 'org-1', 'Amoxicillin 500mg Capsule', 'Mox 500', 'Amoxicillin',
                'Amoxicillin 500mg', '500mg', 1, 10, 'CAP', '3004', 12.0,
                0, 0, 0, 0, 1, '8902222222222', 'm1', 'Ranbaxy',
                datetime('now'), datetime('now')
            );

            INSERT INTO batches (
                id, product_id, org_id, batch_number, expiry_date,
                mrp, purchase_rate, sale_rate, created_at
            ) VALUES (
                'b_amox_1', 'p_amox', 'org-1', 'MX2026', datetime('now', '+1 year'),
                50.0, 30.0, 45.0, datetime('now')
            );

            -- Initial stock: 100 units at Main Branch (wh-1)
            INSERT INTO stock_balances (
                id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at
            ) VALUES (
                'sb_amox_wh1', 'b_amox_1', 'p_amox', 'wh-1', 100.0, 0.0, datetime('now')
            );
        ");
    }

    [Fact]
    public async Task TransferLifecycle_CreateDispatchReceive_UpdatesMultiBranchStock()
    {
        await SeedDataAsync();

        // 1. Create Transfer Request (25 units from wh-1 to wh-2)
        var createCmd = new CreateTransferRequestCommand(
            OrgId: "org-1",
            SourceBranchId: "branch-1",
            SourceWarehouseId: "wh-1",
            DestinationBranchId: "branch-2",
            DestinationWarehouseId: "wh-2",
            RequestedByUserId: "usr-branch2-mgr",
            Notes: "Urgent antibiotic rebalance",
            Items: new List<CreateTransferRequestItemInput>
            {
                new CreateTransferRequestItemInput(
                    ProductId: "p_amox",
                    ProductName: "Amoxicillin 500mg Capsule",
                    BatchId: "b_amox_1",
                    BatchNumber: "MX2026",
                    ExpiryDate: DateTime.UtcNow.AddDays(365),
                    RequestedQuantity: 25,
                    UnitCost: 30.00m)
            }
        );

        var createResult = await _transferService.CreateTransferRequestAsync(createCmd);
        Assert.True(createResult.IsSuccess, createResult.ErrorMessage);
        Assert.NotNull(createResult.TransferId);
        Assert.NotNull(createResult.TransferNo);
        Assert.StartsWith("ST-", createResult.TransferNo!);

        // 2. Query Detail
        var detail = await _transferService.GetTransferDetailAsync(createResult.TransferId!);
        Assert.NotNull(detail);
        Assert.Equal(StockTransferStatus.Requested, detail.Status);
        Assert.Single(detail.Items);
        Assert.Equal(25.0m, detail.Items[0].RequestedQuantity);

        // 3. Dispatch Transfer
        var dispatchCmd = new DispatchTransferCommand(
            TransferId: createResult.TransferId!,
            DispatchedByUserId: "usr-wh1-mgr",
            DeviceId: "dev-pos-1"
        );

        var dispatchResult = await _transferService.DispatchTransferAsync(dispatchCmd);
        Assert.True(dispatchResult.IsSuccess, dispatchResult.ErrorMessage);

        // Verify source stock is deducted (100 -> 75)
        using var conn = await _connectionFactory.CreateConnectionAsync();
        var stockWh1 = await conn.ExecuteScalarAsync<double>(
            "SELECT quantity FROM stock_balances WHERE batch_id = 'b_amox_1' AND warehouse_id = 'wh-1';");
        Assert.Equal(75.0, stockWh1);

        // Verify status is InTransit
        var detailInTransit = await _transferService.GetTransferDetailAsync(createResult.TransferId!);
        Assert.NotNull(detailInTransit);
        Assert.Equal(StockTransferStatus.InTransit, detailInTransit.Status);

        // 4. Receive Transfer at Branch 2 (wh-2)
        var receiveCmd = new ReceiveTransferCommand(
            TransferId: createResult.TransferId!,
            ReceivedByUserId: "usr-branch2-mgr",
            DeviceId: "dev-pos-2",
            Items: new List<ReceiveTransferItemInput>
            {
                new ReceiveTransferItemInput(detailInTransit.Items[0].Id, 25)
            }
        );

        var receiveResult = await _transferService.ReceiveTransferAsync(receiveCmd);
        Assert.True(receiveResult.IsSuccess, receiveResult.ErrorMessage);

        // Verify destination stock incremented to 25
        var stockWh2 = await conn.ExecuteScalarAsync<double>(
            "SELECT quantity FROM stock_balances WHERE batch_id = 'b_amox_1' AND warehouse_id = 'wh-2';");
        Assert.Equal(25.0, stockWh2);

        // Verify completed status
        var finalDetail = await _transferService.GetTransferDetailAsync(createResult.TransferId!);
        Assert.NotNull(finalDetail);
        Assert.Equal(StockTransferStatus.ReceivedCompleted, finalDetail.Status);

        // Verify Outbox Events (DISPATCHED and RECEIVED)
        var pendingEvents = await _outboxRepo.GetPendingEventsAsync();
        Assert.Contains(pendingEvents, e => e.EventType == "TRANSFER_DISPATCHED");
        Assert.Contains(pendingEvents, e => e.EventType == "TRANSFER_RECEIVED");
    }

    [Fact]
    public async Task Transfer_WithDiscrepancy_RecordsTransitLossAndDisputedStatus()
    {
        await SeedDataAsync();

        // 1. Create & Dispatch 10 units
        var createCmd = new CreateTransferRequestCommand(
            OrgId: "org-1",
            SourceBranchId: "branch-1",
            SourceWarehouseId: "wh-1",
            DestinationBranchId: "branch-2",
            DestinationWarehouseId: "wh-2",
            RequestedByUserId: "usr-b2",
            Notes: null,
            Items: new List<CreateTransferRequestItemInput>
            {
                new CreateTransferRequestItemInput("p_amox", "Amoxicillin 500mg", "b_amox_1", "MX2026", DateTime.UtcNow.AddDays(365), 10, 30.00m)
            }
        );

        var createResult = await _transferService.CreateTransferRequestAsync(createCmd);
        await _transferService.DispatchTransferAsync(new DispatchTransferCommand(createResult.TransferId!, "usr-wh1", "dev-1"));

        var inTransit = await _transferService.GetTransferDetailAsync(createResult.TransferId!);
        Assert.NotNull(inTransit);

        // 2. Receive only 8 units (2 lost/damaged in transit)
        var receiveCmd = new ReceiveTransferCommand(
            TransferId: createResult.TransferId!,
            ReceivedByUserId: "usr-b2",
            DeviceId: "dev-2",
            Items: new List<ReceiveTransferItemInput>
            {
                new ReceiveTransferItemInput(inTransit.Items[0].Id, 8)
            }
        );

        var receiveResult = await _transferService.ReceiveTransferAsync(receiveCmd);
        Assert.True(receiveResult.IsSuccess);

        // Verify Destination received 8
        using var conn = await _connectionFactory.CreateConnectionAsync();
        var stockWh2 = await conn.ExecuteScalarAsync<double>(
            "SELECT quantity FROM stock_balances WHERE batch_id = 'b_amox_1' AND warehouse_id = 'wh-2';");
        Assert.Equal(8.0, stockWh2);

        // Verify Transfer Status is Disputed
        var finalDetail = await _transferService.GetTransferDetailAsync(createResult.TransferId!);
        Assert.NotNull(finalDetail);
        Assert.Equal(StockTransferStatus.Disputed, finalDetail.Status);
        Assert.Equal(-2.0m, finalDetail.Items[0].DiscrepancyQuantity);

        // Verify transit loss movement recorded
        var lossMovement = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM stock_movements WHERE reference_type = 'TRANSFER_TRANSIT_LOSS';");
        Assert.NotNull(lossMovement);
        Assert.Equal(-2.0, (double)lossMovement.quantity);
    }
}
