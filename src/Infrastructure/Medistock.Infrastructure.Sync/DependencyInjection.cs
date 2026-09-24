using Microsoft.Extensions.DependencyInjection;

namespace Medistock.Infrastructure.Sync;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureSync(this IServiceCollection services)
    {
        services.AddSingleton<IConnectivityService, ConnectivityService>();
        services.AddHostedService<OutboxSyncWorker>();
        return services;
    }
}
