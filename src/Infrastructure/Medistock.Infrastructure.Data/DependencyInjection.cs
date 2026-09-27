using Medistock.Application.Common.Interfaces;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Medistock.Infrastructure.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureData(this IServiceCollection services, string? sqliteDbPath = null)
    {
        services.AddSingleton<ISqliteConnectionFactory>(_ => new SqliteConnectionFactory(sqliteDbPath));
        services.AddScoped<IDatabaseMigrator, DatabaseMigrator>();
        services.AddScoped<IDataSeeder, DataSeeder>();
        services.AddScoped<IProductSearchRepository, SqliteProductSearchRepository>();
        services.AddScoped<IProductRepository, SqliteProductRepository>();
        services.AddScoped<IStockRepository, SqliteStockRepository>();
        services.AddScoped<ISaleRepository, SqliteSaleRepository>();
        services.AddScoped<IInventoryRepository, SqliteInventoryRepository>();
        services.AddScoped<IScheduleDrugRepository, SqliteScheduleDrugRepository>();
        services.AddScoped<ISupplierRepository, SqliteSupplierRepository>();
        services.AddScoped<IPurchaseRepository, SqlitePurchaseRepository>();
        services.AddScoped<IAccountingRepository, SqliteAccountingRepository>();
        services.AddScoped<ISaleReturnRepository, SqliteSaleReturnRepository>();
        services.AddScoped<IGstReportRepository, SqliteGstReportRepository>();
        services.AddScoped<IStockTransferRepository, SqliteStockTransferRepository>();
        services.AddScoped<ISyncStore, SqliteSyncStore>();
        services.AddScoped<IB2bCommerceRepository, SqliteB2bCommerceRepository>();
        services.AddScoped<IOutboxRepository, SqliteOutboxRepository>();
        services.AddScoped<IDocumentSequenceService, SqliteDocumentSequenceService>();
        services.AddScoped<IBillTemplateRepository, SqliteBillTemplateRepository>();
        services.AddScoped<IPrinterConfigRepository, SqliteBillTemplateRepository>();

        return services;
    }
}
