using System;
using Medistock.Application.Products.Queries;
using Medistock.Desktop.Commands;
using Medistock.Desktop.Controls;
using Medistock.Desktop.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Medistock.Desktop.Views.POS;

public sealed partial class PosPage : Page
{
    public PosViewModel ViewModel { get; }
    private readonly IShortcutService _shortcutService;

    public PosPage(PosViewModel viewModel, IShortcutService shortcutService)
    {
        ViewModel = viewModel;
        _shortcutService = shortcutService;
        this.InitializeComponent();

        Loaded += PosPage_Loaded;
    }

    private void PosPage_Loaded(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus(FocusState.Programmatic);

        // Register actions to named commands in shortcut service
        _shortcutService.RegisterAction("pos.new_tab", () => ViewModel.AddNewTab());
        _shortcutService.RegisterAction("pos.close_tab", () => ViewModel.CloseTab(ViewModel.ActiveTab));
        _shortcutService.RegisterAction("pos.next_tab", () => ViewModel.NextTab());
        _shortcutService.RegisterAction("pos.prev_tab", () => ViewModel.PreviousTab());
        _shortcutService.RegisterAction("pos.new_sale", () => ViewModel.ClearBill());
        _shortcutService.RegisterAction("pos.search_product", () => SearchBox.Focus(FocusState.Programmatic));
        _shortcutService.RegisterAction("pos.payment", async () => await ViewModel.FinalizeSaleAsync());
        _shortcutService.RegisterAction("pos.clear_cart", () => ViewModel.ClearBill());
        _shortcutService.RegisterAction("app.help_shortcuts", () => ViewModel.ToggleShortcutHelp());

        // Attach global key down on the page root
        if (XamlRoot != null)
        {
            this.KeyDown += PosPage_KeyDown;
        }
    }

    private void TabHeader_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is InvoiceTabViewModel tab)
        {
            ViewModel.ActiveTab = tab;
            ViewModel.ActiveTabIndex = ViewModel.InvoiceTabs.IndexOf(tab);
            SearchBox.Focus(FocusState.Programmatic);
        }
    }

    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is InvoiceTabViewModel tab)
        {
            ViewModel.CloseTab(tab);
        }
    }

    private void PosPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var isCtrl = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        var isAlt = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        var isShift = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

        if (e.Key == VirtualKey.F1)
        {
            ViewModel.ToggleShortcutHelp();
            e.Handled = true;
            return;
        }

        if (_shortcutService.TryExecuteShortcut(e.Key, isCtrl, isAlt, isShift, "POS"))
        {
            e.Handled = true;
        }
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
        if (e.Key == VirtualKey.Down)
        {
            ViewModel.MoveSearchSelectionDown();
            if (SearchResultsList.SelectedItem != null)
            {
                SearchResultsList.ScrollIntoView(SearchResultsList.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Up)
        {
            ViewModel.MoveSearchSelectionUp();
            if (SearchResultsList.SelectedItem != null)
            {
                SearchResultsList.ScrollIntoView(SearchResultsList.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right)
        {
            if (ViewModel.ActiveTab != null && ViewModel.ActiveTab.CartItems.Count > 0)
            {
                ViewModel.IncreaseSelectedCartQuantity();
                CartListView.Focus(FocusState.Programmatic);
                if (CartListView.SelectedItem != null)
                {
                    CartListView.ScrollIntoView(CartListView.SelectedItem);
                }
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Left)
        {
            if (ViewModel.ActiveTab != null && ViewModel.ActiveTab.CartItems.Count > 0)
            {
                ViewModel.DecreaseSelectedCartQuantity();
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Enter)
        {
            var text = SearchBox.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                if (text.Length >= 8 && long.TryParse(text, out _))
                {
                    await ViewModel.ProcessBarcodeScanAsync(text);
                    SearchBox.Text = string.Empty;
                }
                else if (ViewModel.SelectedSearchIndex >= 0 && ViewModel.SelectedSearchIndex < ViewModel.SearchResults.Count)
                {
                    ViewModel.AddToCart(ViewModel.SearchResults[ViewModel.SelectedSearchIndex]);
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

    private void CartListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Right)
        {
            ViewModel.IncreaseSelectedCartQuantity();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Left)
        {
            ViewModel.DecreaseSelectedCartQuantity();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.F3 || e.Key == VirtualKey.Escape)
        {
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Delete)
        {
            if (ViewModel.ActiveTab != null && ViewModel.ActiveTab.SelectedCartIndex >= 0 && ViewModel.ActiveTab.SelectedCartIndex < ViewModel.ActiveTab.CartItems.Count)
            {
                ViewModel.RemoveCartItem(ViewModel.ActiveTab.CartItems[ViewModel.ActiveTab.SelectedCartIndex]);
                e.Handled = true;
            }
        }
    }
}

