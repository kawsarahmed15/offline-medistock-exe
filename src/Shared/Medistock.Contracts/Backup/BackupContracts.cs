using System;

namespace Medistock.Contracts.Backup;

public record CloudBackupInfo
{
    public required string BackupId { get; init; }
    public required string OrgId { get; init; }
    public required string DeviceId { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required long SizeBytes { get; init; }
    public required string AppVersion { get; init; }
}

public record CloudBackupUploadResponse
{
    public required bool Success { get; init; }
    public string? BackupId { get; init; }
    public string? ErrorMessage { get; init; }
}
