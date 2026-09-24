using Medistock.Application.Products.Queries;
using Medistock.Application.Sales.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Medistock.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPosTransactionService, PosTransactionService>();
        services.AddScoped<IProductSearchService, ProductSearchService>();
        return services;
    }
}
