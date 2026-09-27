using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Medistock.Application.Sync;
using Medistock.Contracts.Sync;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class SyncEngineAndHubTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseMigrator _migrator;
    private readonly SqliteSyncStore _syncStore;
    private readonly SyncEngineService _syncEngine;

    public SyncEngineAndHubTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_sync_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrator = new DatabaseMigrator(_connectionFactory);
        _migrator.MigrateAsync().GetAwaiter().GetResult();

        _syncStore = new SqliteSyncStore(_connectionFactory);
        _syncEngine = new SyncEngineService(_syncStore);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task ProcessPushBatch_WithValidEvents_ShouldReturnSuccessWithSequenceNumbers()
    {
        // Arrange
        var req = new SyncPushRequest
        {
            OrgId = "ORG-001",
            BranchId = "BR-01",
            DeviceId = "POS-01",
            Events = new List<SyncEventDto>
            {
                new()
                {
                    EventId = "EVT-101",
                    OrgId = "ORG-001",
                    BranchId = "BR-01",
                    DeviceId = "POS-01",
                    EventType = "SaleCommitted",
                    AggregateId = "SALE-001",
                    IdempotencyKey = "POS-01:OP-001",
                    PayloadJson = "{\"total\": 500}",
                    CreatedAtUtc = DateTime.UtcNow,
                    ClientSequenceNumber = 1
                },
                new()
                {
                    EventId = "EVT-102",
                    OrgId = "ORG-001",
                    BranchId = "BR-01",
                    DeviceId = "POS-01",
                    EventType = "SaleCommitted",
                    AggregateId = "SALE-002",
                    IdempotencyKey = "POS-01:OP-002",
                    PayloadJson = "{\"total\": 750}",
                    CreatedAtUtc = DateTime.UtcNow,
                    ClientSequenceNumber = 2
                }
            }
        };

        // Act
        var response = await _syncEngine.ProcessPushBatchAsync(req, authenticatedOrgId: "ORG-001");

        // Assert
        Assert.True(response.Success);
        Assert.Equal(2, response.ProcessedCount);
        Assert.Equal(2, response.SuccessCount);
        Assert.Equal(0, response.DuplicateCount);
        Assert.Equal(0, response.FailedCount);
        Assert.All(response.Results, r => Assert.Equal(SyncItemStatus.Success, r.Status));
        Assert.All(response.Results, r => Assert.True(r.ServerSequenceNumber > 0));
    }

    [Fact]
    public async Task ProcessPushBatch_WithDuplicateIdempotencyKey_ShouldFlagAsDuplicate()
    {
        // Arrange
        var evt = new SyncEventDto
        {
            EventId = "EVT-201",
            OrgId = "ORG-001",
            BranchId = "BR-01",
            DeviceId = "POS-01",
            EventType = "SaleCommitted",
            AggregateId = "SALE-201",
            IdempotencyKey = "POS-01:OP-IDEMPOTENT-TEST",
            PayloadJson = "{\"total\": 100}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var req1 = new SyncPushRequest
        {
            OrgId = "ORG-001",
            BranchId = "BR-01",
            DeviceId = "POS-01",
            Events = new List<SyncEventDto> { evt }
        };

        // First push
        var res1 = await _syncEngine.ProcessPushBatchAsync(req1, "ORG-001");
        Assert.True(res1.Success);
        Assert.Equal(SyncItemStatus.Success, res1.Results[0].Status);

        // Second push with same idempotency key
        var res2 = await _syncEngine.ProcessPushBatchAsync(req1, "ORG-001");

        // Assert
        Assert.True(res2.Success);
        Assert.Equal(1, res2.DuplicateCount);
        Assert.Equal(0, res2.SuccessCount);
        Assert.Equal(SyncItemStatus.Duplicate, res2.Results[0].Status);
    }

    [Fact]
    public async Task ProcessPushBatch_WithTenantMismatch_ShouldFailTenantIsolation()
    {
        // Arrange
        var req = new SyncPushRequest
        {
            OrgId = "ORG-VIOLATOR",
            BranchId = "BR-01",
            DeviceId = "POS-01",
            Events = new List<SyncEventDto>
            {
                new()
                {
                    EventId = "EVT-999",
                    OrgId = "ORG-VIOLATOR",
                    BranchId = "BR-01",
                    DeviceId = "POS-01",
                    EventType = "SaleCommitted",
                    AggregateId = "SALE-999",
                    IdempotencyKey = "POS-01:OP-TENANT-LEAK",
                    PayloadJson = "{}",
                    CreatedAtUtc = DateTime.UtcNow
                }
            }
        };

        // Act
        var response = await _syncEngine.ProcessPushBatchAsync(req, authenticatedOrgId: "ORG-LEGIT");

        // Assert
        Assert.False(response.Success);
        Assert.Equal(1, response.FailedCount);
        Assert.Equal(SyncItemStatus.Failed, response.Results[0].Status);
        Assert.Contains("Tenant isolation", response.Results[0].ErrorMessage);
    }

    [Fact]
    public async Task ProcessPullBatch_ShouldReturnSequentialEvents()
    {
        // Arrange: Push 3 events
        var pushReq = new SyncPushRequest
        {
            OrgId = "ORG-001",
            BranchId = "BR-01",
            DeviceId = "POS-01",
            Events = new List<SyncEventDto>
            {
                new()
                {
                    EventId = "EVT-301",
                    OrgId = "ORG-001",
                    BranchId = "BR-01",
                    DeviceId = "POS-01",
                    EventType = "ProductUpdated",
                    AggregateId = "PROD-01",
                    IdempotencyKey = "POS-01:OP-301",
                    PayloadJson = "{\"price\": 10}",
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    EventId = "EVT-302",
                    OrgId = "ORG-001",
                    BranchId = "BR-01",
                    DeviceId = "POS-01",
                    EventType = "ProductUpdated",
                    AggregateId = "PROD-02",
                    IdempotencyKey = "POS-01:OP-302",
                    PayloadJson = "{\"price\": 20}",
                    CreatedAtUtc = DateTime.UtcNow
                }
            }
        };

        await _syncEngine.ProcessPushBatchAsync(pushReq, "ORG-001");

        // Act: Pull since sequence 0
        var pullReq = new SyncPullRequest
        {
            OrgId = "ORG-001",
            BranchId = "BR-01",
            DeviceId = "POS-02",
            SinceSequenceNumber = 0,
            BatchSize = 10
        };

        var pullRes = await _syncEngine.ProcessPullBatchAsync(pullReq, "ORG-001");

        // Assert
        Assert.Equal("ORG-001", pullRes.OrgId);
        Assert.Equal(2, pullRes.Events.Count);
        Assert.True(pullRes.LatestSequenceNumber >= 2);
        Assert.False(pullRes.HasMore);
    }
}
