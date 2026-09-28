using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Sync.Updates;

/// <summary>
/// Background service that checks for updates every 4 hours.
/// - First check fires 2 minutes after app startup (lets the app fully initialize first)
/// - Subsequent checks every 4 hours with ±30 minute jitter to prevent thundering herd
///   from thousands of simultaneous clients all hitting the server at the same time
/// - Never crashes the host — all exceptions are swallowed
/// </summary>
public class UpdateCheckerWorker : BackgroundService
{
    private readonly IUpdateService _updateService;
    private readonly ILogger<UpdateCheckerWorker>? _logger;

    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(4);

    // Random jitter: ±30 minutes so 1000 clients don't all check at the same time
    private readonly TimeSpan _jitter = TimeSpan.FromMinutes(Random.Shared.Next(-30, 30));

    public UpdateCheckerWorker(IUpdateService updateService, ILogger<UpdateCheckerWorker>? logger = null)
    {
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("Update checker started. First check in {Minutes} minutes.", InitialDelay.TotalMinutes);

        // Wait for app to fully initialize before first check
        try { await Task.Delay(InitialDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger?.LogDebug("Checking for updates...");
                var update = await _updateService.CheckForUpdateAsync(stoppingToken);

                if (update?.UpdateAvailable == true && update.DownloadUrl != null)
                {
                    // Start background download (don't block the checker loop)
                    _ = Task.Run(async () =>
                    {
                        _logger?.LogInformation("Downloading update {Version}...", update.Version);
                        var path = await _updateService.DownloadUpdateAsync(update, ct: stoppingToken);
                        if (path != null)
                            _logger?.LogInformation("Update {Version} ready at {Path}", update.Version, path);
                    }, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                // Never crash the host — log and continue
                _logger?.LogWarning(ex, "Update check cycle failed. Will retry in {Hours}h.", CheckInterval.TotalHours);
            }

            var nextCheck = CheckInterval + _jitter;
            _logger?.LogDebug("Next update check in {Minutes:F0} minutes.", nextCheck.TotalMinutes);

            try { await Task.Delay(nextCheck, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger?.LogInformation("Update checker stopped.");
    }
}
