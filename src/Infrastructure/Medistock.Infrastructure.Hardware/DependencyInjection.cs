using Microsoft.Extensions.DependencyInjection;

namespace Medistock.Infrastructure.Hardware;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureHardware(this IServiceCollection services)
    {
        services.AddSingleton<IReceiptPrinter, EscPosReceiptPrinter>();
        services.AddSingleton<ICashDrawer, EscPosReceiptPrinter>();
        return services;
    }
}
