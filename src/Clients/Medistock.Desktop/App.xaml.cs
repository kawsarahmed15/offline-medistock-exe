using System;
using Medistock.Application;
using Medistock.Desktop.ViewModels;
using Medistock.Desktop.Views.POS;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Data.Migrations;
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

        services.AddTransient<PosViewModel>();
        services.AddTransient<PosPage>();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            using var scope = Services.CreateScope();
            var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
            await migrator.MigrateAsync();
        }
        catch { }

        _window = new MainWindow();
        _window.Activate();
    }
}
