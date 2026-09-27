using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Sync;

namespace Medistock.Application.Common.Interfaces;

public interface ISyncStore
{
    Task<bool> HasIdempotencyKeyAsync(string orgId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<long> RecordProcessedEventAsync(SyncEventDto syncEvent, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<SyncEventDto> Events, long LatestSequence, bool HasMore)> GetEventsSinceSequenceAsync(
        string orgId,
        string branchId,
        long sinceSequence,
        int batchSize,
        CancellationToken cancellationToken = default);
}
