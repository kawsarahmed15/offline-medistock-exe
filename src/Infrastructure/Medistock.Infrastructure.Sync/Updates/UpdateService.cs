using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Updates;
using Medistock.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Sync.Updates;

/// <summary>
/// Polls the server for updates and downloads them silently.
/// Downloads are SHA256-verified before being saved to the staging directory.
/// </summary>
public class UpdateService : IUpdateService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService>? _logger;

    public event EventHandler<UpdateCheckResponse>? UpdateAvailable;

    public UpdateService(HttpClient httpClient, ILogger<UpdateService>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResponse?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var request = new UpdateCheckRequest
            {
                CurrentVersion = GetCurrentVersion(),
                Channel = "stable",
                OsArch = Environment.Is64BitOperatingSystem ? "win-x64" : "win-x86"
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/updates/check", request, JsonOpts, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Update check returned {StatusCode}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<UpdateCheckResponse>(JsonOpts, ct);
            if (result?.UpdateAvailable == true)
            {
                _logger?.LogInformation("Update available: {Version} (mandatory: {Mandatory})",
                    result.Version, result.IsMandatory);
                UpdateAvailable?.Invoke(this, result);
            }

            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger?.LogDebug("Update check skipped — offline or timeout");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during update check");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> DownloadUpdateAsync(
        UpdateCheckResponse update,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            _logger?.LogWarning("Update has no download URL");
            return null;
        }

        MedistockPaths.EnsureAllDirectoriesExist();

        var safeVersion = update.Version?.Replace(".", "_") ?? "unknown";
        var fileName = $"Medistock-Setup-v{safeVersion}.exe";
        var destPath = Path.Combine(MedistockPaths.UpdateStagingDirectory, fileName);

        // Skip if already downloaded
        if (File.Exists(destPath))
        {
            _logger?.LogInformation("Update {Version} already downloaded at {Path}", update.Version, destPath);
            return destPath;
        }

        var tempPath = destPath + ".tmp";
        try
        {
            using var response = await _httpClient.GetAsync(
                update.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? update.SizeBytes ?? 0;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write,
                FileShare.None, bufferSize: 81920, useAsync: true);

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;

                if (totalBytes > 0)
                    progress?.Report((int)(downloaded * 100 / totalBytes));
            }

            // Verify SHA256 hash before accepting the file
            if (!string.IsNullOrWhiteSpace(update.Sha256Hash))
            {
                fileStream.Position = 0;
                var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(fileStream, ct));
                if (!string.Equals(actualHash, update.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogError(
                        "SHA256 mismatch for {Version}! Expected {Expected}, got {Actual}",
                        update.Version, update.Sha256Hash, actualHash);
                    File.Delete(tempPath);
                    return null;
                }
            }

            // Atomic rename — move temp to final only after hash verified
            File.Move(tempPath, destPath, overwrite: true);
            _logger?.LogInformation("Update {Version} downloaded and verified at {Path}",
                update.Version, destPath);

            return destPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to download update {Version}", update.Version);
            if (File.Exists(tempPath)) File.Delete(tempPath);
            return null;
        }
    }

    /// <inheritdoc/>
    public bool IsUpdateDownloaded(out string? installerPath)
    {
        var dir = MedistockPaths.UpdateStagingDirectory;
        if (!Directory.Exists(dir))
        {
            installerPath = null;
            return false;
        }

        var files = Directory.GetFiles(dir, "Medistock-Setup-*.exe")
            .Where(f => !f.EndsWith(".tmp"))
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToArray();

        installerPath = files.Length > 0 ? files[0] : null;
        return installerPath != null;
    }

    private static string GetCurrentVersion()
    {
        return System.Reflection.Assembly.GetEntryAssembly()
            ?.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
