using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Application.Products.Queries;
using Medistock.Application.Purchases.DTOs;
using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Medistock.Desktop.Views.Purchases;

public sealed partial class PurchaseEntryPage : Page
{
    public PurchaseEntryViewModel ViewModel { get; }
    private const int TotalColumns = 11; // 0=Product, 1=Batch, 2=Expiry, 3=Hsn, 4=Unit, 5=Qty, 6=Free, 7=Cost, 8=Mrp, 9=Disc, 10=Gst

    private void Page_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsProductSearchOpen)
        {
            ViewModel.IsProductSearchOpen = false;
        }
    }

    public PurchaseEntryPage(PurchaseEntryViewModel viewModel)
    {
        ViewModel = viewModel;
        this.DataContext = ViewModel;
        this.InitializeComponent();
        this.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Page_KeyDown), handledEventsToo: true);

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PurchaseEntryViewModel.IsAddSupplierModalOpen) && ViewModel.IsAddSupplierModalOpen)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    NewSupplierNameBox?.Focus(FocusState.Programmatic);
                    NewSupplierNameBox?.SelectAll();
                });
            }
        };
    }

    private void NewSupplierNameBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus(FocusState.Programmatic);
            tb.SelectAll();
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        await Task.Delay(150);
        FocusControl("SupplierAutoSuggestBox");
        if (SupplierAutoSuggestBox != null)
        {
            SupplierAutoSuggestBox.Focus(FocusState.Programmatic);
        }
    }

    // ─── Global Key Router ───────────────────────────────────────────────────
    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 1. Modal Intercepts
        if (ViewModel.IsCancelConfirmOpen || ViewModel.IsAddSupplierModalOpen || ViewModel.IsInvoiceDetailsModalOpen)
        {
            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.DismissCancelConfirmCommand.Execute(null);
                ViewModel.CloseAddSupplierModalCommand.Execute(null);
                ViewModel.CloseInvoiceDetailsCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
            case VirtualKey.F2 when ViewModel.SelectedTab == "Entry":
                ViewModel.AddBlankRowCommand.Execute(null);
                FocusRowColumn(ViewModel.LineItems.Count - 1, 0);
                e.Handled = true;
                break;

            case VirtualKey.F5:
                _ = ViewModel.RefreshAllPurchaseDataCommand.ExecuteAsync(null);
                e.Handled = true;
                break;

            case VirtualKey.F6:
            case VirtualKey.S when ctrl:
                if (ViewModel.SelectedTab == "Entry")
                {
                    _ = ViewModel.PostPurchaseInvoiceCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                break;

            case VirtualKey.Escape:
                FocusControl("SupplierInvoiceNoBox");
                e.Handled = true;
                break;

            case VirtualKey.Delete when ctrl:
                if (ViewModel.SelectedTab == "Entry" && ViewModel.ActiveRow != null)
                {
                    ViewModel.RemoveRowCommand.Execute(ViewModel.ActiveRow);
                    e.Handled = true;
                }
                break;
        }
    }

    // ─── Header Field Navigation & Supplier Autocomplete ─────────────────────
    private void SupplierAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var query = sender.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(query))
            {
                sender.ItemsSource = ViewModel.Suppliers.Select(s => s.Name).ToList();
            }
            else
            {
                var matches = ViewModel.Suppliers
                    .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                (s.Gstin != null && s.Gstin.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    .Select(s => s.Name)
                    .ToList();

                var exact = matches.FirstOrDefault(m => string.Equals(m.Trim(), query, StringComparison.OrdinalIgnoreCase));
                if (exact == null)
                {
                    matches.Add($"➕ Add \"{query}\" (Press Enter for New)");
                }
                sender.ItemsSource = matches;
            }
        }
    }

    private void SupplierAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string selectedName)
        {
            if (selectedName.StartsWith("➕ Add ", StringComparison.OrdinalIgnoreCase))
            {
                var rawQuery = sender.Text?.Trim() ?? string.Empty;
                ViewModel.OpenAddSupplierModal();
                if (!string.IsNullOrWhiteSpace(rawQuery))
                {
                    ViewModel.NewSupplierName = rawQuery;
                }
                return;
            }

            var match = ViewModel.Suppliers.FirstOrDefault(s => string.Equals(s.Name, selectedName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                ViewModel.OnSupplierSelected(match);
            }
        }
    }

    private void SupplierAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ViewModel.IsAddSupplierModalOpen) return;

        string query = !string.IsNullOrWhiteSpace(args.QueryText) ? args.QueryText.Trim() : (sender.Text?.Trim() ?? string.Empty);

        if (args.ChosenSuggestion is string chosenName)
        {
            if (chosenName.StartsWith("➕ Add ", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.OpenAddSupplierModal();
                if (!string.IsNullOrWhiteSpace(query))
                {
                    ViewModel.NewSupplierName = query;
                }
                return;
            }

            var match = ViewModel.Suppliers.FirstOrDefault(s => string.Equals(s.Name, chosenName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                ViewModel.OnSupplierSelected(match);
                SupplierInvoiceNoBox?.Focus(FocusState.Programmatic);
                return;
            }
        }

        var existing = ViewModel.Suppliers.FirstOrDefault(s => string.Equals(s.Name.Trim(), query, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ViewModel.OnSupplierSelected(existing);
            SupplierInvoiceNoBox?.Focus(FocusState.Programmatic);
        }
        else
        {
            // Not found -> open Add Supplier Modal with pre-filled name
            ViewModel.OpenAddSupplierModal();
            if (!string.IsNullOrWhiteSpace(query))
            {
                ViewModel.NewSupplierName = query;
            }
            ViewModel.StatusMessage = $"⚠️ Wholesaler '{query}' not found. Please register in the popup modal.";
        }
    }

    private void SupplierAutoSuggestBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (sender is AutoSuggestBox asb)
            {
                SupplierAutoSuggestBox_QuerySubmitted(asb, new AutoSuggestBoxQuerySubmittedEventArgs());
                e.Handled = true;
            }
        }
    }

    private void InvoiceNoBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Tab)
        {
            FocusControl("SupplierGstinBox");
            e.Handled = true;
        }
    }

    private void GstinBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ViewModel.LineItems.Count == 0)
            {
                ViewModel.AddBlankRowCommand.Execute(null);
            }
            ViewModel.SetActiveRow(0);
            FocusRowColumn(0, 0);
            e.Handled = true;
        }
    }

    // ─── Grid Row Navigation & Product Search ──────────────────────────────
    private async void ProductNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.FocusState != FocusState.Unfocused)
        {
            var (rowIndex, _) = GetRowAndColIndex(sender);
            if (rowIndex >= 0 && rowIndex < ViewModel.LineItems.Count)
            {
                ViewModel.SetActiveRow(rowIndex);
                await ViewModel.SearchMedicinesAsync(tb.Text);
            }
        }
    }

    private async void ProductNameBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Down || e.Key == VirtualKey.Up || e.Key == VirtualKey.Enter || e.Key == VirtualKey.Escape)
        {
            if (ViewModel.IsProductSearchOpen && ViewModel.ProductSearchResults.Count > 0)
            {
                if (e.Key == VirtualKey.Down)
                {
                    ViewModel.MoveSearchSelectionDown();
                    if (ViewModel.SelectedProductSearchItem != null && ProductSearchListView != null)
                    {
                        ProductSearchListView.ScrollIntoView(ViewModel.SelectedProductSearchItem);
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.Up)
                {
                    ViewModel.MoveSearchSelectionUp();
                    if (ViewModel.SelectedProductSearchItem != null && ProductSearchListView != null)
                    {
                        ProductSearchListView.ScrollIntoView(ViewModel.SelectedProductSearchItem);
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.Enter)
                {
                    var (rowIndex, _) = GetRowAndColIndex(sender);
                    var targetRow = (rowIndex >= 0 && rowIndex < ViewModel.LineItems.Count)
                        ? ViewModel.LineItems[rowIndex]
                        : (ViewModel.ActiveRow ?? (ViewModel.LineItems.Count > 0 ? ViewModel.LineItems[0] : null));

                    var tb = sender as TextBox;
                    if (ViewModel.SelectedProductSearchIndex >= 0 && ViewModel.SelectedProductSearchIndex < ViewModel.ProductSearchResults.Count)
                    {
                        ViewModel.SelectHighlightedProduct(targetRow);
                    }
                    else if (tb != null && !string.IsNullOrWhiteSpace(tb.Text) && targetRow != null)
                    {
                        ViewModel.CreateOrApplyCustomProduct(tb.Text, targetRow);
                    }

                    ViewModel.IsProductSearchOpen = false;
                    FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1); // Move to Batch No
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.Escape)
                {
                    ViewModel.IsProductSearchOpen = false;
                    e.Handled = true;
                    return;
                }
            }

            await HandleProductNameInputKeyAsync(sender, e);
        }
    }

    private async void ProductNameBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled) return;

        if (ViewModel.IsProductSearchOpen && ViewModel.ProductSearchResults.Count > 0)
        {
            if (e.Key == VirtualKey.Down)
            {
                ViewModel.MoveSearchSelectionDown();
                if (ViewModel.SelectedProductSearchItem != null && ProductSearchListView != null)
                {
                    ProductSearchListView.ScrollIntoView(ViewModel.SelectedProductSearchItem);
                }
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Up)
            {
                ViewModel.MoveSearchSelectionUp();
                if (ViewModel.SelectedProductSearchItem != null && ProductSearchListView != null)
                {
                    ProductSearchListView.ScrollIntoView(ViewModel.SelectedProductSearchItem);
                }
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Enter)
            {
                var (rowIndex, _) = GetRowAndColIndex(sender);
                var targetRow = (rowIndex >= 0 && rowIndex < ViewModel.LineItems.Count)
                    ? ViewModel.LineItems[rowIndex]
                    : (ViewModel.ActiveRow ?? (ViewModel.LineItems.Count > 0 ? ViewModel.LineItems[0] : null));

                var tb = sender as TextBox;
                if (ViewModel.SelectedProductSearchIndex >= 0 && ViewModel.SelectedProductSearchIndex < ViewModel.ProductSearchResults.Count)
                {
                    ViewModel.SelectHighlightedProduct(targetRow);
                }
                else if (tb != null && !string.IsNullOrWhiteSpace(tb.Text) && targetRow != null)
                {
                    ViewModel.CreateOrApplyCustomProduct(tb.Text, targetRow);
                }

                ViewModel.IsProductSearchOpen = false;
                FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1);
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.IsProductSearchOpen = false;
                e.Handled = true;
                return;
            }
        }

        await HandleProductNameInputKeyAsync(sender, e);
    }

    private async Task HandleProductNameInputKeyAsync(object sender, KeyRoutedEventArgs e)
    {
        var (rowIndex, _) = GetRowAndColIndex(sender);
        if (rowIndex < 0 || rowIndex >= ViewModel.LineItems.Count)
        {
            rowIndex = ViewModel.ActiveRowIndex >= 0 ? ViewModel.ActiveRowIndex : 0;
        }

        var row = rowIndex < ViewModel.LineItems.Count ? ViewModel.LineItems[rowIndex] : null;
        var tb = sender as TextBox;

        if (e.Key == VirtualKey.Enter && tb != null && row != null)
        {
            if (!string.IsNullOrWhiteSpace(tb.Text))
            {
                if (tb.Text != row.ProductName || string.IsNullOrWhiteSpace(row.ProductId))
                {
                    await ViewModel.SearchRowProductAsync(tb.Text, row);
                }
            }
            ViewModel.IsProductSearchOpen = false;
            FocusRowColumn(rowIndex, 1); // Move to Batch No
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down)
        {
            if (rowIndex < ViewModel.LineItems.Count - 1)
            {
                FocusRowColumn(rowIndex + 1, 0);
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Up)
        {
            if (rowIndex > 0)
            {
                FocusRowColumn(rowIndex - 1, 0);
            }
            else
            {
                FocusControl("SupplierInvoiceNoBox");
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right)
        {
            if (tb != null && (tb.SelectionLength == tb.Text.Length || tb.SelectionStart == tb.Text.Length || string.IsNullOrEmpty(tb.Text)))
            {
                FocusRowColumn(rowIndex, 1);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Left)
        {
            if (tb != null && (tb.SelectionLength == tb.Text.Length || tb.SelectionStart == 0 || string.IsNullOrEmpty(tb.Text)))
            {
                FocusControl("SupplierInvoiceNoBox");
                e.Handled = true;
            }
        }
    }

    private void ProductSearchListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            var targetRow = ViewModel.ActiveRow ?? (ViewModel.LineItems.Count > 0 ? ViewModel.LineItems[0] : null);
            if (targetRow != null)
            {
                var rowIndex = ViewModel.LineItems.IndexOf(targetRow);
                ViewModel.SelectHighlightedProduct(targetRow);
                ViewModel.IsProductSearchOpen = false;
                if (rowIndex >= 0)
                {
                    FocusRowColumn(rowIndex, 1);
                }
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.IsProductSearchOpen = false;
            e.Handled = true;
        }
    }

    private void ProductSearchListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        var targetRow = ViewModel.ActiveRow ?? (ViewModel.LineItems.Count > 0 ? ViewModel.LineItems[0] : null);
        if (targetRow != null)
        {
            var rowIndex = ViewModel.LineItems.IndexOf(targetRow);
            if (e.ClickedItem is ProductSearchItemViewModel item)
            {
                ViewModel.SelectProductSearch(item, targetRow);
            }
            else if (e.ClickedItem is ProductSearchDto dto)
            {
                ViewModel.SelectProductSearch(dto, targetRow);
            }
            ViewModel.IsProductSearchOpen = false;
            if (rowIndex >= 0)
            {
                FocusRowColumn(rowIndex, 1);
            }
        }
    }

    private bool _isExpiryFormatting = false;

    private void RowCell_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 1. Intercept keys on ExpiryBox (Tag = 2): Strictly numeric digits and slash only!
        if (sender is TextBox tb && tb.Tag is string tag && tag == "2")
        {
            // Backspace special case: if cursor is right after slash (e.g. "01/"), delete back to "0"
            if (e.Key == VirtualKey.Back && tb.SelectionStart == 3 && tb.Text.Length == 3 && tb.Text.EndsWith('/'))
            {
                _isExpiryFormatting = true;
                try
                {
                    tb.Text = tb.Text.Substring(0, 1);
                    tb.SelectionStart = 1;
                    e.Handled = true;
                    return;
                }
                finally
                {
                    _isExpiryFormatting = false;
                }
            }

            // Check navigation / control keys
            if (e.Key == VirtualKey.Tab || e.Key == VirtualKey.Enter ||
                e.Key == VirtualKey.Up || e.Key == VirtualKey.Down ||
                e.Key == VirtualKey.Left || e.Key == VirtualKey.Right ||
                e.Key == VirtualKey.Home || e.Key == VirtualKey.End ||
                e.Key == VirtualKey.Escape || e.Key == VirtualKey.Back || e.Key == VirtualKey.Delete)
            {
                // Allowed - let standard navigation/editing proceed
            }
            else
            {
                var ctrlDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                var shiftDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

                // Allow clipboard shortcuts: Ctrl+C, Ctrl+V, Ctrl+A, Ctrl+X, Ctrl+Z
                if (ctrlDown && !shiftDown && (e.Key == VirtualKey.C || e.Key == VirtualKey.V ||
                    e.Key == VirtualKey.A || e.Key == VirtualKey.X || e.Key == VirtualKey.Z))
                {
                    // Allowed
                }
                else
                {
                    // Strictly numeric digits and slash:
                    bool isTopRowDigit = !shiftDown && (e.Key >= VirtualKey.Number0 && e.Key <= VirtualKey.Number9);
                    bool isNumpadDigit = e.Key >= VirtualKey.NumberPad0 && e.Key <= VirtualKey.NumberPad9;
                    bool isSlash = (!shiftDown && (int)e.Key == 191) || e.Key == VirtualKey.Divide;

                    if (!isTopRowDigit && !isNumpadDigit && !isSlash)
                    {
                        // Reject all alphabetical characters, symbols, spaces
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        // 2. Unit ComboBox (Tag = 4): Up/Down arrow changes the option in dropdown, without jumping rows!
        if (sender is ComboBox cb)
        {
            var (rIdx, cIdx) = GetRowAndColIndex(sender);
            if (e.Key == VirtualKey.Down)
            {
                if (cb.SelectedIndex < cb.Items.Count - 1)
                {
                    cb.SelectedIndex++;
                }
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Up)
            {
                if (cb.SelectedIndex > 0)
                {
                    cb.SelectedIndex--;
                }
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Right)
            {
                if (rIdx >= 0 && rIdx < ViewModel.LineItems.Count)
                {
                    FocusRowColumn(rIdx, cIdx + 1); // Move to Qty (col 5)
                }
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Left)
            {
                if (rIdx >= 0 && rIdx < ViewModel.LineItems.Count)
                {
                    FocusRowColumn(rIdx, cIdx - 1); // Move to HSN (col 3)
                }
                e.Handled = true;
                return;
            }
        }

        // 3. For all other controls (TextBox, NumberBox)
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up ||
            e.Key == VirtualKey.Right || e.Key == VirtualKey.Left)
        {
            var (rowIndex, colIndex) = GetRowAndColIndex(sender);
            if (rowIndex < 0 || rowIndex >= ViewModel.LineItems.Count) return;

            ViewModel.SetActiveRow(rowIndex);

            if (e.Key == VirtualKey.Enter)
            {
                HandleNextCellNavigation(rowIndex, colIndex);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Down)
            {
                if (rowIndex < ViewModel.LineItems.Count - 1)
                {
                    FocusRowColumn(rowIndex + 1, colIndex);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Up)
            {
                if (rowIndex > 0)
                {
                    FocusRowColumn(rowIndex - 1, colIndex);
                }
                else
                {
                    FocusControl("SupplierInvoiceNoBox");
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Right)
            {
                if (sender is TextBox textBox)
                {
                    if (textBox.SelectionLength == textBox.Text.Length ||
                        textBox.SelectionStart == textBox.Text.Length ||
                        string.IsNullOrEmpty(textBox.Text))
                    {
                        HandleNextCellNavigation(rowIndex, colIndex);
                        e.Handled = true;
                        return;
                    }
                }
                else if (sender is NumberBox)
                {
                    HandleNextCellNavigation(rowIndex, colIndex);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == VirtualKey.Left)
            {
                if (sender is TextBox textBox)
                {
                    if (textBox.SelectionLength == textBox.Text.Length ||
                        textBox.SelectionStart == 0 ||
                        string.IsNullOrEmpty(textBox.Text))
                    {
                        HandlePreviousCellNavigation(rowIndex, colIndex);
                        e.Handled = true;
                        return;
                    }
                }
                else if (sender is NumberBox)
                {
                    HandlePreviousCellNavigation(rowIndex, colIndex);
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    private void RowCell_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!e.Handled)
        {
            var (rowIndex, colIndex) = GetRowAndColIndex(sender);
            if (rowIndex < 0 || rowIndex >= ViewModel.LineItems.Count) return;

            ViewModel.SetActiveRow(rowIndex);

            if (e.Key == VirtualKey.Enter)
            {
                HandleNextCellNavigation(rowIndex, colIndex);
                e.Handled = true;
            }
        }
    }

    private void HandleNextCellNavigation(int rowIndex, int colIndex)
    {
        if (colIndex >= TotalColumns - 1)
        {
            // Last column of row (GST %) -> advance to next row
            if (rowIndex == ViewModel.LineItems.Count - 1)
            {
                ViewModel.AddBlankRowCommand.Execute(null);
            }
            FocusRowColumn(rowIndex + 1, 0);
        }
        else
        {
            FocusRowColumn(rowIndex, colIndex + 1);
        }
    }

    private void HandlePreviousCellNavigation(int rowIndex, int colIndex)
    {
        if (colIndex > 0)
        {
            FocusRowColumn(rowIndex, colIndex - 1);
        }
        else
        {
            FocusControl("SupplierInvoiceNoBox");
        }
    }

    private void ExpiryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isExpiryFormatting) return;
        if (sender is not TextBox tb) return;

        var raw = tb.Text;
        if (string.IsNullOrEmpty(raw)) return;

        // Strictly enforce numeric and slash only
        var filtered = new string(raw.Where(c => char.IsDigit(c) || c == '/').ToArray());

        // Ensure at most 1 slash exists
        int firstSlash = filtered.IndexOf('/');
        if (firstSlash >= 0)
        {
            var before = filtered.Substring(0, firstSlash);
            var after = filtered.Substring(firstSlash + 1).Replace("/", "");
            filtered = before + "/" + after;
        }

        if (filtered.Length > 5)
        {
            filtered = filtered.Substring(0, 5);
        }

        if (filtered != raw)
        {
            _isExpiryFormatting = true;
            try
            {
                var sel = Math.Min(tb.SelectionStart, filtered.Length);
                tb.Text = filtered;
                tb.SelectionStart = sel;
            }
            finally
            {
                _isExpiryFormatting = false;
            }
        }

        var text = tb.Text;
        _isExpiryFormatting = true;
        try
        {
            // Case 1: User entered 2 digits for month (e.g. "01" -> "01/")
            if (text.Length == 2 && !text.Contains('/') && char.IsDigit(text[0]) && char.IsDigit(text[1]))
            {
                if (int.TryParse(text, out int month) && month >= 1 && month <= 12)
                {
                    tb.Text = text + "/";
                    tb.SelectionStart = 3;
                }
            }
            // Case 2: User entered or pasted 4 digits without slash (e.g. "0127" -> "01/27")
            else if (text.Length == 4 && !text.Contains('/') && text.All(char.IsDigit))
            {
                if (int.TryParse(text.Substring(0, 2), out int month) && month >= 1 && month <= 12)
                {
                    tb.Text = $"{text.Substring(0, 2)}/{text.Substring(2, 2)}";
                    tb.SelectionStart = tb.Text.Length;
                }
            }
        }
        finally
        {
            _isExpiryFormatting = false;
        }
    }

    private void ExpiryBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var (rowIndex, _) = GetRowAndColIndex(sender);
        if (rowIndex >= 0 && rowIndex < ViewModel.LineItems.Count)
        {
            var row = ViewModel.LineItems[rowIndex];
            if (sender is TextBox tb && !string.IsNullOrWhiteSpace(tb.Text))
            {
                var text = tb.Text.Trim();
                if (text.Contains('/'))
                {
                    var parts = text.Split('/');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int y))
                    {
                        if (m >= 1 && m <= 12)
                        {
                            var normalized = $"{m:D2}/{y % 100:D2}";
                            if (tb.Text != normalized)
                            {
                                tb.Text = normalized;
                            }
                            row.ExpiryText = normalized;
                        }
                    }
                }
            }

            if (row.ExpiryDate != default && row.ExpiryDate < DateTimeOffset.UtcNow)
            {
                ViewModel.StatusMessage = $"⚠️ Row {rowIndex + 1}: Expiry date ({row.ExpiryText}) is in the past — please verify.";
            }
        }
    }

    private (int RowIndex, int ColIndex) GetRowAndColIndex(object sender)
    {
        if (sender is FrameworkElement fe)
        {
            int colIndex = 0;
            if (fe.Tag is string tagStr && int.TryParse(tagStr, out int parsedCol))
            {
                colIndex = parsedCol;
            }

            if (fe.DataContext is PurchaseItemRowViewModel rowItem)
            {
                int rowIndex = ViewModel.LineItems.IndexOf(rowItem);
                return (rowIndex, colIndex);
            }
        }
        return (-1, -1);
    }

    // ─── Focus Helpers ───────────────────────────────────────────────────────
    private void FocusControl(string name)
    {
        var ctrl = FindNamedControl<Control>(this, name);
        ctrl?.Focus(FocusState.Programmatic);
    }

    private void FocusRowColumn(int rowIndex, int colIndex)
    {
        if (LineItemsListView == null) return;
        if (rowIndex < 0 || rowIndex >= ViewModel.LineItems.Count) return;

        var item = ViewModel.LineItems[rowIndex];
        LineItemsListView.ScrollIntoView(item);

        void ApplyFocus()
        {
            var container = LineItemsListView.ContainerFromIndex(rowIndex) as ListViewItem;
            if (container == null)
            {
                LineItemsListView.UpdateLayout();
                container = LineItemsListView.ContainerFromIndex(rowIndex) as ListViewItem;
            }
            if (container == null) return;

            var inputs = GetAllInputControls(container);
            if (colIndex >= 0 && colIndex < inputs.Count)
            {
                var target = inputs[colIndex];
                target.Focus(FocusState.Programmatic);
                if (target is TextBox tb)
                {
                    tb.SelectAll();
                }
            }
        }

        ApplyFocus();
        DispatcherQueue.TryEnqueue(ApplyFocus);
    }

    private static List<Control> GetAllInputControls(DependencyObject parent)
    {
        var result = new List<Control>();
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBox tb && tb.IsEnabled && tb.Visibility == Visibility.Visible)
            {
                result.Add(tb);
            }
            else if (child is NumberBox nb && nb.IsEnabled && nb.Visibility == Visibility.Visible)
            {
                result.Add(nb);
            }
            else if (child is ComboBox cb && cb.IsEnabled && cb.Visibility == Visibility.Visible)
            {
                result.Add(cb);
            }
            else
            {
                result.AddRange(GetAllInputControls(child));
            }
        }
        return result;
    }

    private static T? FindNamedControl<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name) return fe;
            var found = FindNamedControl<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void HistorySearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.ApplyHistoryFilter();
    }

    private async void RecentPurchasesListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (RecentPurchasesListView?.SelectedItem is PurchaseInvoiceSummaryDto selected)
            {
                await ViewModel.ViewInvoiceDetailsAsync(selected);
                e.Handled = true;
            }
        }
    }

    private async void RecentPurchasesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PurchaseInvoiceSummaryDto clicked)
        {
            await ViewModel.ViewInvoiceDetailsAsync(clicked);
        }
    }

    private async void RecentPurchasesListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (RecentPurchasesListView?.SelectedItem is PurchaseInvoiceSummaryDto selected)
        {
            await ViewModel.ViewInvoiceDetailsAsync(selected);
        }
    }
}
