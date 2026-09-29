using System;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Updates;

namespace Medistock.Infrastructure.Sync.Updates;

/// <summary>
/// Checks for app updates, downloads them resiliently with resume support,
/// reports live progress, and coordinates seamless installation without data loss.
/// </summary>
public interface IUpdateService
{
    /// <summary>Raised when a newer version is available.</summary>
    event EventHandler<UpdateCheckResponse>? UpdateAvailable;

    /// <summary>Raised as download progress changes with percentage, bytes, and speed.</summary>
    event EventHandler<UpdateDownloadProgress>? DownloadProgressChanged;

    /// <summary>Raised when an update download and verification finishes successfully.</summary>
    event EventHandler<string>? UpdateDownloaded;

    /// <summary>The latest update check response received, if any.</summary>
    UpdateCheckResponse? LastCheckedUpdate { get; }

    /// <summary>Indicates if an update is currently being downloaded in the background.</summary>
    bool IsDownloading { get; }

    /// <summary>
    /// Queries the server for a newer version. Returns null if offline or server error.
    /// Fires <see cref="UpdateAvailable"/> if a new version exists.
    /// </summary>
    Task<UpdateCheckResponse?> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the installer for the given update with HTTP range resume support.
    /// If network drops and resumes, download continues from existing bytes.
    /// Returns the local file path of the verified installer, or null on failure.
    /// </summary>
    Task<string?> DownloadUpdateAsync(
        UpdateCheckResponse update,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true if an update installer has already been downloaded and verified in staging.
    /// </summary>
    bool IsUpdateDownloaded(out string? installerPath);

    /// <summary>
    /// Executes the update installer to update the software binaries while preserving user database and settings.
    /// </summary>
    Task<bool> ApplyUpdateAsync(
        string? installerPath = null,
        bool restartApp = true,
        CancellationToken ct = default);
}
