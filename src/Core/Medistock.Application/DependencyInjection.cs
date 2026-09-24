using Medistock.Application.Compliance.Services;
using Medistock.Application.Inventory.Services;
using Medistock.Application.Products.Queries;
using Medistock.Application.Purchases.Services;
using Medistock.Application.Sales.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Medistock.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPosTransactionService, PosTransactionService>();
        services.AddScoped<IProductSearchService, ProductSearchService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IScheduleDrugService, ScheduleDrugService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<Medistock.Application.Accounting.Services.IAccountingService, Medistock.Application.Accounting.Services.AccountingService>();
        return services;
    }
}
