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
        _shortcutService.RegisterAction("pos.new_sale", () => ViewModel.ClearBill());
        _shortcutService.RegisterAction("pos.search_product", () => SearchBox.Focus(FocusState.Programmatic));
        _shortcutService.RegisterAction("pos.payment", async () => await ViewModel.FinalizeSaleAsync());
        _shortcutService.RegisterAction("pos.clear_cart", () => ViewModel.ClearBill());
        _shortcutService.RegisterAction("app.help_shortcuts", ShowShortcutHelpDialog);

        // Attach global key down on the page root
        if (XamlRoot != null)
        {
            this.KeyDown += PosPage_KeyDown;
        }
    }

    private async void ShowShortcutHelpDialog()
    {
        var shortcuts = _shortcutService.GetActiveShortcuts("POS");
        var dialog = new ShortcutHelpDialog(_shortcutService.CurrentProfile.ProfileName, shortcuts)
        {
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void PosPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var isCtrl = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        var isAlt = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        var isShift = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

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
        if (e.Key == VirtualKey.Enter)
        {
            var text = SearchBox.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
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
