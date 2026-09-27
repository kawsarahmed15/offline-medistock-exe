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
        }
        else
        {
            services.AddTransient<ICloudSyncClient, CloudSyncClient>();
            services.AddHttpClient<CloudSyncClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });
        }

        services.AddHostedService<OutboxSyncWorker>();
        return services;
    }
}
