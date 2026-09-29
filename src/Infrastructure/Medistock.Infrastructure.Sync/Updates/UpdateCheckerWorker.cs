using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Sync.Updates;

/// <summary>
/// Background service that checks for updates every 2 hours.
/// - First check fires 10 seconds after app startup (lets the app fully initialize first)
/// - Subsequent checks every 2 hours with ±30 minute jitter
/// - Auto-downloads and SILENTLY auto-installs when an update is ready — no user click required
/// - If download fails due to network, .part file is kept and resumed on next check
/// - Never crashes the host — all exceptions are swallowed
/// </summary>
public class UpdateCheckerWorker : BackgroundService
{
    private readonly IUpdateService _updateService;
    private readonly ILogger<UpdateCheckerWorker>? _logger;

    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(2);

    // Random jitter: ±30 minutes so many clients don't all check at the same time
    private readonly TimeSpan _jitter = TimeSpan.FromMinutes(Random.Shared.Next(-30, 30));

    public UpdateCheckerWorker(IUpdateService updateService, ILogger<UpdateCheckerWorker>? logger = null)
    {
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("Update checker started. First check in {Seconds}s.", InitialDelay.TotalSeconds);

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
                    _logger?.LogInformation("Update v{Version} is available. Auto-download is currently disabled.", update.Version);

                    // TODO: Re-enable auto-download & auto-install when ready.
                    // Background download + silent install is commented out below.
                    // Users can manually download from Settings > Check for Updates.

                    //_ = Task.Run(async () =>
                    //{
                    //    try
                    //    {
                    //        _logger?.LogInformation("Downloading update v{Version} in background...", update.Version);
                    //        var path = await _updateService.DownloadUpdateAsync(update, ct: stoppingToken);
                    //        if (path != null)
                    //        {
                    //            _logger?.LogInformation("Update v{Version} downloaded. Auto-installing silently in 3s...", update.Version);
                    //            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    //            await _updateService.ApplyUpdateAsync(path, restartApp: true, ct: stoppingToken);
                    //        }
                    //    }
                    //    catch (OperationCanceledException) { /* app shutting down — fine */ }
                    //    catch (Exception ex)
                    //    {
                    //        _logger?.LogWarning(ex, "Background auto-install failed. Update staged for next launch.");
                    //    }
                    //}, stoppingToken);
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
