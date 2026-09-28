using System;
using Microsoft.Extensions.DependencyInjection;

namespace Medistock.Infrastructure.Sync;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureSync(this IServiceCollection services, Uri? syncServerBaseUri = null)
    {
        services.AddSingleton<IConnectivityService, ConnectivityService>();

        if (syncServerBaseUri != null)
        {
            services.AddHttpClient<ICloudSyncClient, CloudSyncClient>(client =>
            {
                client.BaseAddress = syncServerBaseUri;
                client.Timeout = TimeSpan.FromSeconds(10);
            });
            services.AddHttpClient<Updates.IUpdateService, Updates.UpdateService>(client =>
            {
                client.BaseAddress = syncServerBaseUri;
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient<Backup.ICloudBackupService, Backup.CloudBackupService>(client =>
            {
                client.BaseAddress = syncServerBaseUri;
                client.Timeout = TimeSpan.FromSeconds(60);
            });
        }
        else
        {
            services.AddTransient<ICloudSyncClient, CloudSyncClient>();
            services.AddHttpClient<CloudSyncClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });
            services.AddTransient<Updates.IUpdateService, Updates.UpdateService>();
            services.AddHttpClient<Updates.UpdateService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddTransient<Backup.ICloudBackupService, Backup.CloudBackupService>();
            services.AddHttpClient<Backup.CloudBackupService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            });
        }

        services.AddHostedService<OutboxSyncWorker>();
        services.AddHostedService<Updates.UpdateCheckerWorker>();
        services.AddHostedService<NetworkMonitorService>();
        services.AddSingleton<Updates.IUpdateInstallCoordinator, Updates.UpdateInstallCoordinator>();
        return services;
    }
}
