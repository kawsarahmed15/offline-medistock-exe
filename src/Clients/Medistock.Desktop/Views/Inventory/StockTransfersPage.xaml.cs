using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Inventory;

public sealed partial class StockTransfersPage : Page
{
    public StockTransfersViewModel ViewModel { get; }

    public StockTransfersPage(StockTransfersViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
    }
}
