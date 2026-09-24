using System;

namespace Medistock.Domain.Common;

public class OutboxEvent : Entity<string>
{
    public string AggregateType { get; private set; } = string.Empty;
    public string AggregateId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public string DeviceId { get; private set; } = string.Empty;
    public string OperationId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public OutboxEventStatus Status { get; private set; } = OutboxEventStatus.Pending;
    public int RetryCount { get; private set; }
    public DateTime? SyncedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    private OutboxEvent() { }

    public static OutboxEvent Create(
        string id,
        string aggregateType,
        string aggregateId,
        string eventType,
        string payloadJson,
        string deviceId,
        string operationId)
    {
        return new OutboxEvent
        {
            Id = id,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            EventType = eventType,
            PayloadJson = payloadJson,
            DeviceId = deviceId,
            OperationId = operationId,
            CreatedAt = DateTime.UtcNow,
            Status = OutboxEventStatus.Pending,
            RetryCount = 0
        };
    }

    public void MarkSynced()
    {
        Status = OutboxEventStatus.Synced;
        SyncedAt = DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void RecordFailure(string error)
    {
        RetryCount++;
        ErrorMessage = error;
        if (RetryCount >= 10)
        {
            Status = OutboxEventStatus.Failed;
        }
    }
}
