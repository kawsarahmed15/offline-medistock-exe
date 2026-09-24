using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Purchases;

public sealed partial class PurchaseEntryPage : Page
{
    public PurchaseEntryViewModel ViewModel { get; }

    public PurchaseEntryPage(PurchaseEntryViewModel viewModel)
    {
        ViewModel = viewModel;
        this.DataContext = ViewModel;
        this.InitializeComponent();

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadSuppliersAsync();
        };
    }
}
