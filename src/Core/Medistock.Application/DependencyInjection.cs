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
        services.AddScoped<Medistock.Application.Products.Commands.IProductService, Medistock.Application.Products.Commands.ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IScheduleDrugService, ScheduleDrugService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<Medistock.Application.Accounting.Services.IAccountingService, Medistock.Application.Accounting.Services.AccountingService>();
        services.AddScoped<Medistock.Application.Sales.Services.ISaleReturnService, Medistock.Application.Sales.Services.SaleReturnService>();
        services.AddScoped<IGstReportService, GstReportService>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<Medistock.Application.Sync.ISyncEngineService, Medistock.Application.Sync.SyncEngineService>();
        services.AddScoped<Medistock.Application.B2B.Services.IB2bCommerceService, Medistock.Application.B2B.Services.B2bCommerceService>();
        services.AddScoped<Medistock.Application.Customers.Services.ICustomerService, Medistock.Application.Customers.Services.CustomerService>();
        return services;
    }
}
