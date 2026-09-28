using System;

namespace Medistock.Contracts.Updates;

/// <summary>Desktop → Server: check if a newer app version exists.</summary>
public record UpdateCheckRequest
{
    public required string CurrentVersion { get; init; }
    public required string Channel { get; init; }    // "stable" | "beta"
    public required string OsArch { get; init; }     // "win-x64" | "win-arm64"
}

/// <summary>Server → Desktop: update availability and download info.</summary>
public record UpdateCheckResponse
{
    public required bool UpdateAvailable { get; init; }
    public string? Version { get; init; }
    public bool IsMandatory { get; init; }
    public string? DownloadUrl { get; init; }
    public string? Sha256Hash { get; init; }
    public long? SizeBytes { get; init; }
    public string? ReleaseNotes { get; init; }
    /// <summary>If set, any installed version older than this must update before using the app.</summary>
    public string? MinVersionRequired { get; init; }
}

/// <summary>Developer → CloudApi: publish a new update.</summary>
public record PublishUpdateRequest
{
    public required string Version { get; init; }
    public required string Channel { get; init; }
    public required bool IsMandatory { get; init; }
    public required string DownloadUrl { get; init; }
    public required string Sha256Hash { get; init; }
    public required long SizeBytes { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? MinVersionRequired { get; init; }
}
