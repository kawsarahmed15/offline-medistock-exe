using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Inventory;

public sealed partial class InventoryPage : Page
{
    public InventoryViewModel ViewModel { get; }

    public InventoryPage(InventoryViewModel viewModel)
    {
        ViewModel = viewModel;
        this.DataContext = ViewModel;
        this.InitializeComponent();

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadStocksAsync();
        };
    }
}
