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
        services.AddTransient<PosViewModel>();
        services.AddTransient<PosPage>();
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
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");

        try
        {
            _window = new MainWindow();
            _window.Activate();
            System.IO.File.AppendAllText(logFile, $"Window activated successfully at {DateTime.UtcNow:O}\n");

            // Run database migrations and seeding in background
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    using var scope = Services.CreateScope();
                    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
                    await migrator.MigrateAsync();

                    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                    await seeder.SeedIfEmptyAsync();
                    System.IO.File.AppendAllText(logFile, $"Database migration & seeding completed at {DateTime.UtcNow:O}\n");
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(logFile, $"Database init error: {ex}\n");
                }
            });
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(logFile, $"FATAL OnLaunched: {ex}\n");
            throw;
        }
    }
}

