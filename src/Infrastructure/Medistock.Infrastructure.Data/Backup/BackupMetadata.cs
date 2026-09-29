using System;

namespace Medistock.Infrastructure.Data.Backup;

public record BackupMetadata
{
    public required string AppVersion { get; init; }
    public required string CreatedAt { get; init; }
    public required string MachineName { get; init; }
    public string? DatabaseVersion { get; init; }
}

public record BackupFileInfo(string FileName, string FullPath, DateTime CreatedAt, long SizeBytes);

public record RestoreResult(bool Success, string? ErrorMessage);
