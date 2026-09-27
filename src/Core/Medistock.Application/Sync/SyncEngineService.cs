using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.Sync;
using Microsoft.Extensions.Logging;

namespace Medistock.Application.Sync;

public interface ISyncEngineService
{
    Task<SyncPushResponse> ProcessPushBatchAsync(
        SyncPushRequest request,
        string authenticatedOrgId,
        CancellationToken cancellationToken = default);

    Task<SyncPullResponse> ProcessPullBatchAsync(
        SyncPullRequest request,
        string authenticatedOrgId,
        CancellationToken cancellationToken = default);
}

public class SyncEngineService : ISyncEngineService
{
    private readonly ISyncStore _syncStore;
    private readonly ILogger<SyncEngineService>? _logger;

    public SyncEngineService(ISyncStore syncStore, ILogger<SyncEngineService>? logger = null)
    {
        _syncStore = syncStore ?? throw new ArgumentNullException(nameof(syncStore));
        _logger = logger;
    }

    public async Task<SyncPushResponse> ProcessPushBatchAsync(
        SyncPushRequest request,
        string authenticatedOrgId,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        // Enforce strict tenant isolation: Caller cannot push data for another org
        if (!string.Equals(request.OrgId, authenticatedOrgId, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning("Tenant mismatch on sync push: Request Org {RequestOrg} != Auth Org {AuthOrg}", request.OrgId, authenticatedOrgId);
            return new SyncPushResponse
            {
                Success = false,
                ProcessedCount = 0,
                FailedCount = request.Events.Count,
                Results = request.Events.ConvertAll(e => new SyncItemResultDto
                {
                    EventId = e.EventId,
                    IdempotencyKey = e.IdempotencyKey,
                    Status = SyncItemStatus.Failed,
                    ErrorMessage = "Tenant isolation violation: OrgId does not match token claims"
                })
            };
        }

        var results = new List<SyncItemResultDto>();
        int successCount = 0;
        int duplicateCount = 0;
        int failedCount = 0;

        foreach (var evt in request.Events)
        {
            try
            {
                // Validate event
                if (string.IsNullOrWhiteSpace(evt.IdempotencyKey) || string.IsNullOrWhiteSpace(evt.EventId))
                {
                    results.Add(new SyncItemResultDto
                    {
                        EventId = evt.EventId,
                        IdempotencyKey = evt.IdempotencyKey ?? string.Empty,
                        Status = SyncItemStatus.Failed,
                        ErrorMessage = "Missing EventId or IdempotencyKey"
                    });
                    failedCount++;
                    continue;
                }

                // Check Idempotency
                bool isDuplicate = await _syncStore.HasIdempotencyKeyAsync(request.OrgId, evt.IdempotencyKey, cancellationToken);
                if (isDuplicate)
                {
                    _logger?.LogInformation("Duplicate event skipped for IdempotencyKey: {Key}", evt.IdempotencyKey);
                    results.Add(new SyncItemResultDto
                    {
                        EventId = evt.EventId,
                        IdempotencyKey = evt.IdempotencyKey,
                        Status = SyncItemStatus.Duplicate,
                        ErrorMessage = null
                    });
                    duplicateCount++;
                    continue;
                }

                // Record event & get monotonic server sequence number
                long serverSeq = await _syncStore.RecordProcessedEventAsync(evt, cancellationToken);

                results.Add(new SyncItemResultDto
                {
                    EventId = evt.EventId,
                    IdempotencyKey = evt.IdempotencyKey,
                    Status = SyncItemStatus.Success,
                    ServerSequenceNumber = serverSeq
                });
                successCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing sync event {EventId}", evt.EventId);
                results.Add(new SyncItemResultDto
                {
                    EventId = evt.EventId,
                    IdempotencyKey = evt.IdempotencyKey,
                    Status = SyncItemStatus.Failed,
                    ErrorMessage = ex.Message
                });
                failedCount++;
            }
        }

        return new SyncPushResponse
        {
            Success = failedCount == 0,
            ProcessedCount = results.Count,
            SuccessCount = successCount,
            DuplicateCount = duplicateCount,
            FailedCount = failedCount,
            Results = results,
            ServerTimeUtc = DateTime.UtcNow
        };
    }

    public async Task<SyncPullResponse> ProcessPullBatchAsync(
        SyncPullRequest request,
        string authenticatedOrgId,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (!string.Equals(request.OrgId, authenticatedOrgId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Tenant isolation violation: OrgId does not match token claims");
        }

        var (events, latestSeq, hasMore) = await _syncStore.GetEventsSinceSequenceAsync(
            request.OrgId,
            request.BranchId,
            request.SinceSequenceNumber,
            request.BatchSize,
            cancellationToken);

        return new SyncPullResponse
        {
            OrgId = request.OrgId,
            BranchId = request.BranchId,
            LatestSequenceNumber = latestSeq,
            HasMore = hasMore,
            Events = new List<SyncEventDto>(events),
            ServerTimeUtc = DateTime.UtcNow
        };
    }
}
