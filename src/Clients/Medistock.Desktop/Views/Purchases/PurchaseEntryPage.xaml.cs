using Medistock.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Purchases;

public sealed partial class PurchaseEntryPage : Page
{
    public PurchaseEntryViewModel ViewModel { get; }

    public PurchaseEntryPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<PurchaseEntryViewModel>();
        this.DataContext = ViewModel;

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadSuppliersAsync();
        };
    }
}
