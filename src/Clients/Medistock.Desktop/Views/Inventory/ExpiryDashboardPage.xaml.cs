using Medistock.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Inventory;

public sealed partial class ExpiryDashboardPage : Page
{
    public ExpiryDashboardViewModel ViewModel { get; }

    public ExpiryDashboardPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ExpiryDashboardViewModel>();
        this.DataContext = ViewModel;

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadDashboardAsync();
        };
    }
}
