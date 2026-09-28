using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Infrastructure.Sync.Backup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Sync;

/// <summary>
/// Monitors system network status changes.
/// When internet connectivity is restored:
/// 1. Triggers outbox synchronization
/// 2. Checks and runs scheduled cloud backup
/// </summary>
public class NetworkMonitorService : IHostedService
{
    private readonly ICloudBackupService _cloudBackupService;
    private readonly IConnectivityService _connectivityService;
    private readonly ILogger<NetworkMonitorService>? _logger;

    public NetworkMonitorService(
        ICloudBackupService cloudBackupService,
        IConnectivityService connectivityService,
        ILogger<NetworkMonitorService>? logger = null)
    {
        _cloudBackupService = cloudBackupService ?? throw new ArgumentNullException(nameof(cloudBackupService));
        _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        _logger?.LogInformation("NetworkMonitorService registered for network changes.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        return Task.CompletedTask;
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            _logger?.LogInformation("Network became available. Triggering background tasks...");
            TriggerOnlineTasks();
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        if (NetworkInterface.GetIsNetworkAvailable())
        {
            _logger?.LogDebug("Network address changed. Verifying connectivity...");
            TriggerOnlineTasks();
        }
    }

    private void TriggerOnlineTasks()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var state = await _connectivityService.CheckConnectivityAsync();
                if (state != Domain.Common.ConnectivityState.LocalServerDownC)
                {
                    await _cloudBackupService.UploadBackupIfDueAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to complete tasks on network reconnect.");
            }
        });
    }
}
