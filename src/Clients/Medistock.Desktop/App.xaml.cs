using System;
using System.Threading.Tasks;
using Medistock.Application;
using Medistock.Desktop.Commands;
using Medistock.Desktop.ViewModels;
using Medistock.Desktop.Views.Auth;
using Medistock.Desktop.Views.POS;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Hardware;
using Medistock.Infrastructure.Identity;
using Medistock.Infrastructure.Identity.Services;
using Medistock.Infrastructure.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Medistock.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    public static MainWindow? MainWindowInstance { get; private set; }
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        // Ensure all data directories exist before anything else runs
        MedistockPaths.EnsureAllDirectoriesExist();

        this.UnhandledException += (s, e) =>
        {
            try
            {
                System.IO.File.AppendAllText(MedistockPaths.StartupLog,
                    $"[UNHANDLED XAML EXCEPTION] {e.Message} \n {e.Exception}\n");
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                System.IO.File.AppendAllText(MedistockPaths.StartupLog,
                    $"[APPDOMAIN UNHANDLED EXCEPTION] {e.ExceptionObject}\n");
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                System.IO.File.AppendAllText(MedistockPaths.StartupLog,
                    $"[UNOBSERVED TASK EXCEPTION] {e.Exception}\n");
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
        var cloudServerUri = new Uri("https://offline-medistock.teklin.in");

        services.AddInfrastructureData();
        services.AddInfrastructureHardware();
        services.AddInfrastructureSync(cloudServerUri);
        services.AddInfrastructureIdentity(cloudServerUri);

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
        try
        {
            System.IO.File.AppendAllText(MedistockPaths.StartupLog,
                $"OnLaunched at {DateTime.UtcNow:O}\n");

            // Run DB migration & seed before activating UI
            try
            {
                using var scope = Services.CreateScope();
                var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
                migrator.MigrateAsync().GetAwaiter().GetResult();

                var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                seeder.SeedIfEmptyAsync().GetAwaiter().GetResult();

                System.IO.File.AppendAllText(MedistockPaths.StartupLog,
                    $"Database migration & seeding completed at {DateTime.UtcNow:O}\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(MedistockPaths.StartupLog,
                    $"Database init warning: {ex.Message}\n");
            }

            var mainWindow = new MainWindow();
            MainWindowInstance = mainWindow;
            _window = mainWindow;
            _window.Activate();

            System.IO.File.AppendAllText(MedistockPaths.StartupLog,
                $"MainWindow activated at {DateTime.UtcNow:O}\n");

            // Automatically start background services (UpdateChecker, OutboxSync, NetworkMonitor)
            StartBackgroundServices();
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(MedistockPaths.StartupLog, $"FATAL OnLaunched: {ex}\n");
            throw;
        }
    }

    private static readonly CancellationTokenSource _appCts = new();

    private static void StartBackgroundServices()
    {
        try
        {
            var hostedServices = Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
            foreach (var service in hostedServices)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await service.StartAsync(_appCts.Token);
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.AppendAllText(MedistockPaths.StartupLog,
                            $"[BackgroundService Start Warning ({service.GetType().Name})]: {ex.Message}\n");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(MedistockPaths.StartupLog,
                $"[StartBackgroundServices Warning]: {ex.Message}\n");
        }
    }
}
