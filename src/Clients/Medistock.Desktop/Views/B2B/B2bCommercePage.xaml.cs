using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Medistock.Desktop.ViewModels;
using Medistock.Contracts.B2B;

namespace Medistock.Desktop.Views.B2B;

public sealed partial class B2bCommercePage : Page
{
    public B2bCommerceViewModel ViewModel { get; }

    public B2bCommercePage(B2bCommerceViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
        this.Loaded += B2bCommercePage_Loaded;
    }

    private async void B2bCommercePage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void AddToCart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WholesalerCatalogItemDto item })
        {
            ViewModel.AddToCart(item);
        }
    }

    private void RemoveCartItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: B2bCartItemViewModel item })
        {
            ViewModel.RemoveFromCart(item);
        }
    }

    private async void ReceiveOrder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: B2bOrderSummaryDto order })
        {
            await ViewModel.ReceiveAndStockOrderAsync(order);
        }
    }
}
