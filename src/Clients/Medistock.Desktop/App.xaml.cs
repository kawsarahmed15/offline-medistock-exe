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
        services.AddTransient<ExpiryDashboardViewModel>();
        services.AddTransient<ScheduleRegisterViewModel>();
        services.AddTransient<PurchaseEntryViewModel>();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            using var scope = Services.CreateScope();
            var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
            await migrator.MigrateAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
            await seeder.SeedIfEmptyAsync();
        }
        catch { }

        _window = new MainWindow();
        _window.Activate();
    }
}
