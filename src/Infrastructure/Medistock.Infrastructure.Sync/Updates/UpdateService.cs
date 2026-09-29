using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Updates;
using Medistock.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Sync.Updates;

/// <summary>
/// Resilient background update service with HTTP Range resume capability,
/// SHA-256 verification, and zero-data-loss application updating.
/// </summary>
public class UpdateService : IUpdateService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IUpdateInstallCoordinator? _installCoordinator;
    private readonly ILogger<UpdateService>? _logger;
    private readonly SemaphoreSlim _downloadLock = new(1, 1);

    public event EventHandler<UpdateCheckResponse>? UpdateAvailable;
    public event EventHandler<UpdateDownloadProgress>? DownloadProgressChanged;
    public event EventHandler<string>? UpdateDownloaded;

    public UpdateCheckResponse? LastCheckedUpdate { get; private set; }
    public bool IsDownloading { get; private set; }

    public UpdateService(
        HttpClient httpClient,
        IUpdateInstallCoordinator? installCoordinator = null,
        ILogger<UpdateService>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _installCoordinator = installCoordinator;
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
            LastCheckedUpdate = result;

            if (result?.UpdateAvailable == true)
            {
                _logger?.LogInformation("Update available: v{Version} (Mandatory: {Mandatory})",
                    result.Version, result.IsMandatory);
                UpdateAvailable?.Invoke(this, result);
            }

            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger?.LogDebug("Update check skipped — offline or server unreachable.");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during update check.");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> DownloadUpdateAsync(
        UpdateCheckResponse update,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            _logger?.LogWarning("Update has no download URL.");
            return null;
        }

        await _downloadLock.WaitAsync(ct);
        try
        {
            IsDownloading = true;
            MedistockPaths.EnsureAllDirectoriesExist();

            var safeVersion = update.Version?.Replace(".", "_") ?? "unknown";
            var fileName = $"Medistock-Setup-v{safeVersion}.exe";
            var destPath = Path.Combine(MedistockPaths.UpdateStagingDirectory, fileName);
            var partPath = destPath + ".part";

            // If destination already exists, verify SHA256 and return
            if (File.Exists(destPath))
            {
                if (await VerifySha256Async(destPath, update.Sha256Hash, ct))
                {
                    _logger?.LogInformation("Update v{Version} is already downloaded and verified at {Path}", update.Version, destPath);
                    var completedProgress = new UpdateDownloadProgress(
                        update.Version ?? "Latest",
                        100.0,
                        new FileInfo(destPath).Length,
                        new FileInfo(destPath).Length,
                        0,
                        "Update downloaded and verified. Ready to install.",
                        IsCompleted: true
                    );
                    progress?.Report(completedProgress);
                    DownloadProgressChanged?.Invoke(this, completedProgress);
                    UpdateDownloaded?.Invoke(this, destPath);
                    return destPath;
                }
                else
                {
                    File.Delete(destPath);
                }
            }

            const int maxRetries = 10;
            int attempt = 0;

            while (attempt < maxRetries)
            {
                ct.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    long existingLength = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
                    using var request = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl);

                    if (existingLength > 0)
                    {
                        request.Headers.Range = new RangeHeaderValue(existingLength, null);
                        _logger?.LogInformation("Resuming download from byte offset {Offset} (Attempt {Attempt})", existingLength, attempt);
                    }

                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        // File on disk might be corrupted or server file changed; start clean
                        File.Delete(partPath);
                        existingLength = 0;
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    bool isPartial = response.StatusCode == HttpStatusCode.PartialContent;
                    long totalBytes;

                    if (isPartial && response.Content.Headers.ContentRange?.Length.HasValue == true)
                    {
                        totalBytes = response.Content.Headers.ContentRange.Length.Value;
                    }
                    else if (response.Content.Headers.ContentLength.HasValue)
                    {
                        totalBytes = isPartial ? (existingLength + response.Content.Headers.ContentLength.Value) : response.Content.Headers.ContentLength.Value;
                    }
                    else
                    {
                        totalBytes = update.SizeBytes ?? 0;
                    }

                    var fileMode = (isPartial && existingLength > 0) ? FileMode.Append : FileMode.Create;
                    await using var fileStream = new FileStream(partPath, fileMode, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    await using var contentStream = await response.Content.ReadAsStreamAsync(ct);

                    var buffer = new byte[81920];
                    long totalDownloaded = isPartial ? existingLength : 0;
                    var stopwatch = Stopwatch.StartNew();
                    long bytesSinceLastCalc = 0;
                    var lastSpeedCalc = stopwatch.Elapsed;
                    double currentSpeed = 0;

                    int read;
                    while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                        totalDownloaded += read;
                        bytesSinceLastCalc += read;

                        var now = stopwatch.Elapsed;
                        var elapsedSec = (now - lastSpeedCalc).TotalSeconds;
                        if (elapsedSec >= 0.5)
                        {
                            currentSpeed = bytesSinceLastCalc / elapsedSec;
                            bytesSinceLastCalc = 0;
                            lastSpeedCalc = now;

                            var percentage = totalBytes > 0 ? (totalDownloaded * 100.0 / totalBytes) : 0.0;
                            percentage = Math.Min(99.9, Math.Max(0.0, percentage));

                            var mbDown = totalDownloaded / (1024.0 * 1024.0);
                            var mbTotal = totalBytes / (1024.0 * 1024.0);
                            var speedMb = currentSpeed / (1024.0 * 1024.0);

                            var p = new UpdateDownloadProgress(
                                update.Version ?? "Latest",
                                percentage,
                                totalDownloaded,
                                totalBytes,
                                currentSpeed,
                                $"Downloading v{update.Version}: {percentage:0.0}% ({mbDown:0.1} MB / {mbTotal:0.1} MB @ {speedMb:0.2} MB/s)"
                            );
                            progress?.Report(p);
                            DownloadProgressChanged?.Invoke(this, p);
                        }
                    }

                    await fileStream.FlushAsync(ct);
                    fileStream.Close();

                    // Download completed — verify checksum
                    var isVerified = await VerifySha256Async(partPath, update.Sha256Hash, ct);
                    if (!isVerified)
                    {
                        _logger?.LogError("Checksum verification failed for {File}. Deleting and retrying.", partPath);
                        File.Delete(partPath);
                        continue;
                    }

                    // Move verified part file to final destination
                    File.Move(partPath, destPath, overwrite: true);
                    _logger?.LogInformation("Update v{Version} successfully downloaded to {Path}", update.Version, destPath);

                    var finalProgress = new UpdateDownloadProgress(
                        update.Version ?? "Latest",
                        100.0,
                        totalDownloaded,
                        totalBytes,
                        0,
                        $"✓ Update v{update.Version} downloaded successfully and ready to install!",
                        IsCompleted: true
                    );
                    progress?.Report(finalProgress);
                    DownloadProgressChanged?.Invoke(this, finalProgress);
                    UpdateDownloaded?.Invoke(this, destPath);

                    return destPath;
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or SocketException)
                {
                    _logger?.LogWarning("Network interruption during update download (Attempt {Attempt}/{Max}): {Msg}",
                        attempt, maxRetries, ex.Message);

                    var resumeProgress = new UpdateDownloadProgress(
                        update.Version ?? "Latest",
                        File.Exists(partPath) ? (new FileInfo(partPath).Length * 100.0 / Math.Max(1, update.SizeBytes ?? 1)) : 0,
                        File.Exists(partPath) ? new FileInfo(partPath).Length : 0,
                        update.SizeBytes ?? 0,
                        0,
                        $"Network disconnected. Waiting to resume download (Attempt {attempt}/{maxRetries})..."
                    );
                    progress?.Report(resumeProgress);
                    DownloadProgressChanged?.Invoke(this, resumeProgress);

                    // Wait with backoff before resuming
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(attempt, 5)))), ct);
                }
            }

            _logger?.LogError("Failed to download update v{Version} after maximum retry attempts.", update.Version);
            var failedProgress = new UpdateDownloadProgress(
                update.Version ?? "Latest",
                0,
                0,
                0,
                0,
                "Download failed due to repeated network connection issues.",
                IsFailed: true
            );
            progress?.Report(failedProgress);
            DownloadProgressChanged?.Invoke(this, failedProgress);
            return null;
        }
        finally
        {
            IsDownloading = false;
            _downloadLock.Release();
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
            .Where(f => !f.EndsWith(".part") && !f.EndsWith(".tmp"))
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToArray();

        installerPath = files.Length > 0 ? files[0] : null;
        return installerPath != null;
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyUpdateAsync(
        string? installerPath = null,
        bool restartApp = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(installerPath))
        {
            if (!IsUpdateDownloaded(out installerPath) || string.IsNullOrWhiteSpace(installerPath))
            {
                _logger?.LogWarning("No staged update installer found to apply.");
                return false;
            }
        }

        if (!File.Exists(installerPath))
        {
            _logger?.LogError("Installer file does not exist at {Path}", installerPath);
            return false;
        }

        _logger?.LogInformation("Applying update from {InstallerPath}...", installerPath);

        // Pre-cleanup: Remove WebView2 EBWebView cache directories that Inno Setup cannot
        // overwrite when they are locked by the OS. Deleting them pre-install is safe — they
        // are regenerated automatically by WebView2 on next launch.
        PreCleanWebView2CacheDirs();

        // 1. If UpdateInstallCoordinator is available, try coordinator
        if (_installCoordinator != null)
        {
            var success = await _installCoordinator.InstallUpdateAsync(installerPath, ct);
            if (success && restartApp)
            {
                RestartApplication();
                return true;
            }
        }

        // 2. Direct standalone installer execution
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                // /VERYSILENT   = no UI dialogs
                // /SUPPRESSMSGBOXES = suppress all message boxes including errors
                // /NORESTART    = don't auto-reboot; we restart the app ourselves
                // /CLOSEAPPLICATIONS = gracefully close any running Medistock processes first
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                UseShellExecute = true
            };

            Process.Start(startInfo);

            if (restartApp)
            {
                // Small delay to allow installer process to spawn before we exit
                await Task.Delay(800, ct);
                Environment.Exit(0);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to launch update installer directly.");
            return false;
        }
    }

    /// <summary>
    /// Pre-cleans WebView2 EBWebView subdirectories that Inno Setup's overwrite logic
    /// cannot handle when the OS locks them. They are fully regenerated by WebView2 on next app
    /// launch, so deleting them before the installer runs is 100% safe.
    /// This prevents the "Could not find a part of the path ...WebView2\EBWebView\..." error.
    /// </summary>
    private void PreCleanWebView2CacheDirs()
    {
        try
        {
            // Typical WebView2 EBWebView cache dirs pattern inside install dir
            var installDir = Path.GetDirectoryName(
                Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(installDir)) return;

            var webview2Roots = Directory.GetDirectories(installDir, "*.WebView2", SearchOption.TopDirectoryOnly);
            foreach (var root in webview2Roots)
            {
                var ebWebView = Path.Combine(root, "EBWebView");
                if (Directory.Exists(ebWebView))
                {
                    DeleteDirectoryForgiving(ebWebView);
                    _logger?.LogInformation("Pre-cleaned WebView2 EBWebView cache at {Path}", ebWebView);
                }
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — installer may still succeed; log and continue
            _logger?.LogWarning("WebView2 pre-clean warning (non-fatal): {Msg}", ex.Message);
        }
    }

    /// <summary>Recursively deletes a directory, skipping locked files rather than throwing.</summary>
    private static void DeleteDirectoryForgiving(string path)
    {
        if (!Directory.Exists(path)) return;

        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            try { File.Delete(file); } catch { /* locked — skip */ }
        }

        foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                                      .OrderByDescending(d => d.Length)) // deepest first
        {
            try { Directory.Delete(dir); } catch { }
        }

        try { Directory.Delete(path); } catch { }
    }


    private static async Task<bool> VerifySha256Async(string filePath, string? expectedHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            // If no hash provided by server, accept file if size > 1MB
            return new FileInfo(filePath).Length > 1024 * 1024;
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var hashBytes = await SHA256.HashDataAsync(stream, ct);
            var actualHash = Convert.ToHexString(hashBytes);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void RestartApplication()
    {
        try
        {
            var mainModule = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(mainModule))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = mainModule,
                    UseShellExecute = true
                });
            }
        }
        catch { }
        finally
        {
            Environment.Exit(0);
        }
    }

    private static string GetCurrentVersion()
    {
        return System.Reflection.Assembly.GetEntryAssembly()
            ?.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
