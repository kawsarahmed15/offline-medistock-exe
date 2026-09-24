using Medistock.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Inventory;

public sealed partial class InventoryPage : Page
{
    public InventoryViewModel ViewModel { get; }

    public InventoryPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<InventoryViewModel>();
        this.DataContext = ViewModel;

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadStocksAsync();
        };
    }
}
