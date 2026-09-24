using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Domain.Common;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteOutboxRepository : IOutboxRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteOutboxRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnqueueEventAsync(
        OutboxEvent outboxEvent,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO outbox_events (
                id, aggregate_type, aggregate_id, event_type, payload_json,
                device_id, operation_id, created_at, status, retry_count
            ) VALUES (
                @Id, @AggregateType, @AggregateId, @EventType, @PayloadJson,
                @DeviceId, @OperationId, @CreatedAt, @Status, @RetryCount
            );
        ";

        var parameters = new
        {
            outboxEvent.Id,
            outboxEvent.AggregateType,
            outboxEvent.AggregateId,
            outboxEvent.EventType,
            outboxEvent.PayloadJson,
            outboxEvent.DeviceId,
            outboxEvent.OperationId,
            CreatedAt = outboxEvent.CreatedAt.ToString("o"),
            Status = (int)outboxEvent.Status,
            outboxEvent.RetryCount
        };

        if (transaction != null)
        {
            await transaction.Connection!.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            return;
        }

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<OutboxEvent>> GetPendingEventsAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                id AS Id,
                aggregate_type AS AggregateType,
                aggregate_id AS AggregateId,
                event_type AS EventType,
                payload_json AS PayloadJson,
                device_id AS DeviceId,
                operation_id AS OperationId,
                created_at AS CreatedAt,
                status AS Status,
                retry_count AS RetryCount,
                synced_at AS SyncedAt,
                error_message AS ErrorMessage
            FROM outbox_events
            WHERE status = 0
            ORDER BY created_at ASC
            LIMIT @batchSize;
        ";

        var results = await connection.QueryAsync<OutboxEvent>(
            new CommandDefinition(sql, new { batchSize }, cancellationToken: cancellationToken));

        return results.ToList();
    }

    public async Task MarkEventSyncedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE outbox_events
            SET status = 1,
                synced_at = @syncedAt,
                error_message = NULL
            WHERE id = @eventId;
        ";

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { eventId, syncedAt = DateTime.UtcNow.ToString("o") },
            cancellationToken: cancellationToken));
    }

    public async Task RecordEventFailureAsync(string eventId, string errorMessage, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE outbox_events
            SET retry_count = retry_count + 1,
                error_message = @errorMessage,
                status = CASE WHEN retry_count + 1 >= 10 THEN 2 ELSE status END
            WHERE id = @eventId;
        ";

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { eventId, errorMessage },
            cancellationToken: cancellationToken));
    }
}

public class SqliteDocumentSequenceService : IDocumentSequenceService
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteDocumentSequenceService(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string> GenerateInvoiceNumberAsync(
        string orgId,
        string branchId,
        string prefix = "INV",
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            var currentYear = DateTime.UtcNow.Year;
            var seqId = $"{orgId}_{branchId}_{prefix}_{currentYear}";

            // Insert initial sequence if not exists
            const string ensureSeqSql = @"
                INSERT OR IGNORE INTO document_sequences (
                    id, org_id, branch_id, doc_type, prefix, last_sequence_number, current_year
                ) VALUES (
                    @seqId, @orgId, @branchId, @prefix, @prefix, 0, @currentYear
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                ensureSeqSql,
                new { seqId, orgId, branchId, prefix, currentYear },
                transaction,
                cancellationToken: cancellationToken));

            // Atomically increment and get next number
            const string incrementSql = @"
                UPDATE document_sequences
                SET last_sequence_number = last_sequence_number + 1
                WHERE id = @seqId;

                SELECT last_sequence_number
                FROM document_sequences
                WHERE id = @seqId;
            ";

            var nextNum = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                incrementSql,
                new { seqId },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();

            return $"{prefix}-{currentYear}-{nextNum:D6}";
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
