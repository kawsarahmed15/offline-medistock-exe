using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Sync;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class SyncTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteOutboxRepository _outboxRepository;

    public SyncTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_sync_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _outboxRepository = new SqliteOutboxRepository(_connectionFactory);

        // Run migrations
        var migrator = new DatabaseMigrator(_connectionFactory);
        migrator.MigrateAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task ConnectivityService_ReturnsFullStateByDefault()
    {
        var service = new ConnectivityService();
        var state = await service.CheckConnectivityAsync();

        Assert.Equal(ConnectivityState.FullA, state);
        Assert.Equal(ConnectivityState.FullA, service.CurrentState);
    }

    [Fact]
    public async Task OutboxSyncWorker_ProcessesAndMarksPendingEventsAsSynced()
    {
        // 1. Arrange - Enqueue sample outbox events
        var event1 = OutboxEvent.Create(
            id: Guid.NewGuid().ToString("N"),
            aggregateType: "Sale",
            aggregateId: "SALE-001",
            eventType: "SaleCreated",
            payloadJson: "{\"SaleId\":\"SALE-001\",\"GrandTotal\":500.00}",
            deviceId: "POS-01",
            operationId: Guid.NewGuid().ToString("N")
        );

        var event2 = OutboxEvent.Create(
            id: Guid.NewGuid().ToString("N"),
            aggregateType: "StockMovement",
            aggregateId: "MOV-001",
            eventType: "StockReduced",
            payloadJson: "{\"ProductId\":\"PROD-1\",\"Quantity\":5}",
            deviceId: "POS-01",
            operationId: Guid.NewGuid().ToString("N")
        );

        await _outboxRepository.EnqueueEventAsync(event1);
        await _outboxRepository.EnqueueEventAsync(event2);

        var pendingBefore = await _outboxRepository.GetPendingEventsAsync(10);
        Assert.Equal(2, pendingBefore.Count);

        // 2. Act - Run OutboxSyncWorker process cycle
        var connectivityService = new ConnectivityService();
        var worker = new OutboxSyncWorker(_outboxRepository, connectivityService);

        var syncedCount = await worker.ProcessPendingEventsAsync();

        // 3. Assert
        Assert.Equal(2, syncedCount);

        var pendingAfter = await _outboxRepository.GetPendingEventsAsync(10);
        Assert.Empty(pendingAfter);
    }

    [Fact]
    public async Task OutboxSyncWorker_RecordsFailureWhenRepositoryErrors()
    {
        // Arrange
        var mockFailingRepo = new FailingOutboxRepository();
        var connectivityService = new ConnectivityService();
        var worker = new OutboxSyncWorker(mockFailingRepo, connectivityService);

        // Act
        var syncedCount = await worker.ProcessPendingEventsAsync();

        // Assert
        Assert.Equal(0, syncedCount);
        Assert.Single(mockFailingRepo.FailedEventIds);
    }

    private class FailingOutboxRepository : IOutboxRepository
    {
        public List<string> FailedEventIds { get; } = new();

        public Task EnqueueEventAsync(OutboxEvent outboxEvent, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<OutboxEvent>> GetPendingEventsAsync(int batchSize = 50, CancellationToken cancellationToken = default)
        {
            var list = new List<OutboxEvent>
            {
                OutboxEvent.Create(
                    id: "FAIL-1",
                    aggregateType: "Sale",
                    aggregateId: "SALE-FAIL",
                    eventType: "SaleCreated",
                    payloadJson: "{}",
                    deviceId: "POS-01",
                    operationId: "OP-FAIL")
            };
            return Task.FromResult<IReadOnlyList<OutboxEvent>>(list);
        }

        public Task MarkEventSyncedAsync(string eventId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated network timeout");
        }

        public Task RecordEventFailureAsync(string eventId, string error, CancellationToken cancellationToken = default)
        {
            FailedEventIds.Add(eventId);
            return Task.CompletedTask;
        }
    }
}
