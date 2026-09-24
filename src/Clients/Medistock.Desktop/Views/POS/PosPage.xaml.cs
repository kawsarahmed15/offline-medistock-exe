using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Medistock.Application.Products.Queries;
using Medistock.Desktop.ViewModels;
using Windows.System;

namespace Medistock.Desktop.Views.POS;

public sealed partial class PosPage : Page
{
    public PosViewModel ViewModel { get; }

    public PosPage(PosViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();

        Loaded += (_, _) => SearchBox.Focus(FocusState.Programmatic);
    }

    private void SearchResults_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ProductSearchItemViewModel product)
        {
            ViewModel.AddToCart(product);
            SearchBox.Focus(FocusState.Programmatic);
        }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CartItemViewModel item)
        {
            ViewModel.RemoveCartItem(item);
        }
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            var text = SearchBox.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                // Check if it's a barcode scan or select top search result
                if (text.Length >= 8 && long.TryParse(text, out _))
                {
                    await ViewModel.ProcessBarcodeScanAsync(text);
                    SearchBox.Text = string.Empty;
                }
                else if (ViewModel.SearchResults.Count > 0)
                {
                    ViewModel.AddToCart(ViewModel.SearchResults[0]);
                    SearchBox.Text = string.Empty;
                }
            }
            e.Handled = true;
        }
    }
}
