using System;
using Medistock.Application;
using Medistock.Desktop.Commands;
using Medistock.Desktop.ViewModels;
using Medistock.Desktop.Views.POS;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Hardware;
using Medistock.Infrastructure.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Medistock.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        var logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");

        this.UnhandledException += (s, e) =>
        {
            try
            {
                System.IO.File.AppendAllText(logFile, $"[UNHANDLED XAML EXCEPTION] {e.Message} \n {e.Exception}\n");
            }
            catch { }
        };

        InitializeComponent();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddApplication();
        services.AddInfrastructureData();
        services.AddInfrastructureHardware();
        services.AddInfrastructureSync();

        services.AddSingleton<IShortcutService, ShortcutService>();
        services.AddSingleton<PosViewModel>();
        services.AddSingleton<PosPage>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<Views.Inventory.InventoryPage>();
        services.AddTransient<ExpiryDashboardViewModel>();
        services.AddTransient<Views.Inventory.ExpiryDashboardPage>();
        services.AddTransient<ScheduleRegisterViewModel>();
        services.AddTransient<Views.Compliance.ScheduleRegisterPage>();
        services.AddTransient<PurchaseEntryViewModel>();
        services.AddTransient<Views.Purchases.PurchaseEntryPage>();
        services.AddTransient<AccountingViewModel>();
        services.AddTransient<Views.Accounting.AccountingPage>();
        services.AddTransient<SalesHistoryViewModel>();
        services.AddTransient<Views.Sales.SalesHistoryPage>();
        services.AddTransient<GstReportsViewModel>();
        services.AddTransient<Views.Compliance.GstReportsPage>();
        services.AddTransient<StockTransfersViewModel>();
        services.AddTransient<Views.Inventory.StockTransfersPage>();
        services.AddTransient<B2bCommerceViewModel>();
        services.AddTransient<Views.B2B.B2bCommercePage>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<Views.Settings.SettingsPage>();
        services.AddTransient<PrintPreviewViewModel>();
        services.AddTransient<Views.Sales.PrintPreviewDialog>();
        services.AddTransient<BillCustomizerViewModel>();
        services.AddTransient<Views.Settings.BillCustomizerPage>();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");

        try
        {
            _window = new MainWindow();
            _window.Activate();
            System.IO.File.AppendAllText(logFile, $"Window activated successfully at {DateTime.UtcNow:O}\n");

            // Ensure database migrations and seed medicines are populated immediately
            try
            {
                using var scope = Services.CreateScope();
                var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
                migrator.MigrateAsync().GetAwaiter().GetResult();

                var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                seeder.SeedIfEmptyAsync().GetAwaiter().GetResult();
                System.IO.File.AppendAllText(logFile, $"Database migration & seeding completed at {DateTime.UtcNow:O}\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logFile, $"Database init error: {ex}\n");
            }
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(logFile, $"FATAL OnLaunched: {ex}\n");
            throw;
        }
    }
}

