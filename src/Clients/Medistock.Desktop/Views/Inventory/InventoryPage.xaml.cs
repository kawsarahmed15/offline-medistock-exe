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

        this.KeyDown += (s, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape && ViewModel.IsEditProductModalOpen)
            {
                ViewModel.CloseEditProduct();
                e.Handled = true;
            }
        };
    }
}
