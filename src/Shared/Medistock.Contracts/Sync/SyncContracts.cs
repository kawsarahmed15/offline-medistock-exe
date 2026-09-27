using System;
using System.Collections.Generic;

namespace Medistock.Contracts.Sync;

public enum SyncOperation
{
    Insert = 1,
    Update = 2,
    Delete = 3
}

public enum SyncItemStatus
{
    Success = 1,
    Duplicate = 2,
    Conflict = 3,
    Failed = 4
}

public record SyncEventDto
{
    public required string EventId { get; init; }
    public required string OrgId { get; init; }
    public required string BranchId { get; init; }
    public required string DeviceId { get; init; }
    public required string EventType { get; init; }
    public required string AggregateId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string PayloadJson { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public long ClientSequenceNumber { get; init; }
}

public record SyncPushRequest
{
    public required string OrgId { get; init; }
    public required string BranchId { get; init; }
    public required string DeviceId { get; init; }
    public required List<SyncEventDto> Events { get; init; } = new();
}

public record SyncItemResultDto
{
    public required string EventId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required SyncItemStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public long ServerSequenceNumber { get; init; }
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
}

public record SyncPushResponse
{
    public bool Success { get; init; }
    public int ProcessedCount { get; init; }
    public int SuccessCount { get; init; }
    public int DuplicateCount { get; init; }
    public int FailedCount { get; init; }
    public List<SyncItemResultDto> Results { get; init; } = new();
    public DateTime ServerTimeUtc { get; init; } = DateTime.UtcNow;
}

public record SyncPullRequest
{
    public required string OrgId { get; init; }
    public required string BranchId { get; init; }
    public required string DeviceId { get; init; }
    public long SinceSequenceNumber { get; init; }
    public int BatchSize { get; init; } = 100;
}

public record SyncPullResponse
{
    public required string OrgId { get; init; }
    public required string BranchId { get; init; }
    public long LatestSequenceNumber { get; init; }
    public bool HasMore { get; init; }
    public List<SyncEventDto> Events { get; init; } = new();
    public DateTime ServerTimeUtc { get; init; } = DateTime.UtcNow;
}
