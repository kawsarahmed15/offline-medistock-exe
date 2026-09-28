using System;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Updates;

namespace Medistock.Infrastructure.Sync.Updates;

/// <summary>
/// Checks for app updates and downloads them silently in the background.
/// </summary>
public interface IUpdateService
{
    /// <summary>Raised when a newer version is available. Carries the update details.</summary>
    event EventHandler<UpdateCheckResponse>? UpdateAvailable;

    /// <summary>
    /// Queries the server for a newer version. Returns null if offline or server error.
    /// Fires <see cref="UpdateAvailable"/> if a new version exists.
    /// </summary>
    Task<UpdateCheckResponse?> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the installer for the given update response.
    /// Returns the local file path of the downloaded installer, or null on failure.
    /// </summary>
    Task<string?> DownloadUpdateAsync(
        UpdateCheckResponse update,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true if an update installer has already been downloaded and is staged.
    /// </summary>
    bool IsUpdateDownloaded(out string? installerPath);
}
