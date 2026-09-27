using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.Sync;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteSyncStore : ISyncStore
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteSyncStore(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> HasIdempotencyKeyAsync(string orgId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = @"
            SELECT COUNT(1) 
            FROM server_sync_events 
            WHERE org_id = @orgId AND idempotency_key = @idempotencyKey;";

        var count = await connection.ExecuteScalarAsync<int>(sql, new { orgId, idempotencyKey });
        return count > 0;
    }

    public async Task<long> RecordProcessedEventAsync(SyncEventDto syncEvent, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = @"
            INSERT INTO server_sync_events (
                event_id, org_id, branch_id, device_id, event_type, 
                aggregate_id, idempotency_key, payload_json, client_created_at
            ) VALUES (
                @EventId, @OrgId, @BranchId, @DeviceId, @EventType,
                @AggregateId, @IdempotencyKey, @PayloadJson, @ClientCreatedAt
            );
            SELECT last_insert_rowid();";

        var sequence = await connection.ExecuteScalarAsync<long>(sql, new
        {
            syncEvent.EventId,
            syncEvent.OrgId,
            syncEvent.BranchId,
            syncEvent.DeviceId,
            syncEvent.EventType,
            syncEvent.AggregateId,
            syncEvent.IdempotencyKey,
            syncEvent.PayloadJson,
            ClientCreatedAt = syncEvent.CreatedAtUtc.ToString("o")
        });

        return sequence;
    }

    public async Task<(IReadOnlyList<SyncEventDto> Events, long LatestSequence, bool HasMore)> GetEventsSinceSequenceAsync(
        string orgId,
        string branchId,
        long sinceSequence,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = @"
            SELECT 
                event_id AS EventId,
                org_id AS OrgId,
                branch_id AS BranchId,
                device_id AS DeviceId,
                event_type AS EventType,
                aggregate_id AS AggregateId,
                idempotency_key AS IdempotencyKey,
                payload_json AS PayloadJson,
                client_created_at AS ClientCreatedAtStr,
                server_sequence_number AS ClientSequenceNumber
            FROM server_sync_events
            WHERE org_id = @orgId 
              AND (branch_id = @branchId OR branch_id = 'ALL' OR branch_id = '')
              AND server_sequence_number > @sinceSequence
            ORDER BY server_sequence_number ASC
            LIMIT @limit;";

        var rows = (await connection.QueryAsync<dynamic>(sql, new
        {
            orgId,
            branchId,
            sinceSequence,
            limit = batchSize + 1
        })).ToList();

        bool hasMore = rows.Count > batchSize;
        var items = rows.Take(batchSize).Select<dynamic, SyncEventDto>(r => new SyncEventDto
        {
            EventId = (string)r.EventId,
            OrgId = (string)r.OrgId,
            BranchId = (string)r.BranchId,
            DeviceId = (string)r.DeviceId,
            EventType = (string)r.EventType,
            AggregateId = (string)r.AggregateId,
            IdempotencyKey = (string)r.IdempotencyKey,
            PayloadJson = (string)r.PayloadJson,
            CreatedAtUtc = DateTime.TryParse((string)r.ClientCreatedAtStr, out DateTime dt) ? dt : DateTime.UtcNow,
            ClientSequenceNumber = (long)r.ClientSequenceNumber
        }).ToList();

        long latestSeq = items.Count > 0 ? items.Max(i => i.ClientSequenceNumber) : sinceSequence;

        return (items, latestSeq, hasMore);
    }
}
