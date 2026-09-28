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
    private const int TotalColumns = 12; // 0=Product, 1=Batch, 2=Expiry, 3=Hsn, 4=Unit, 5=Qty, 6=Free, 7=Cost, 8=Mrp, 9=Sale, 10=Disc, 11=Gst

    public PurchaseEntryPage(PurchaseEntryViewModel viewModel)
    {
        ViewModel = viewModel;
        this.DataContext = ViewModel;
        this.InitializeComponent();
        this.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Page_KeyDown), handledEventsToo: true);
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        await Task.Delay(150);
        FocusControl("SupplierComboBox");
        if (SupplierComboBox != null)
        {
            SupplierComboBox.IsDropDownOpen = true;
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

    // ─── Header Field Navigation ─────────────────────────────────────────────
    private void SupplierComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is SupplierDto supplier)
        {
            ViewModel.OnSupplierSelected(supplier);
        }
    }

    private void SupplierComboBox_DropDownClosed(object sender, object e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            FocusControl("SupplierInvoiceNoBox");
            if (SupplierInvoiceNoBox != null)
            {
                SupplierInvoiceNoBox.SelectAll();
            }
        });
    }

    private void SupplierComboBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Tab)
        {
            if (sender is ComboBox cb)
            {
                cb.IsDropDownOpen = false;
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                FocusControl("SupplierInvoiceNoBox");
                if (SupplierInvoiceNoBox != null)
                {
                    SupplierInvoiceNoBox.SelectAll();
                }
            });
            e.Handled = true;
        }
    }

    private void SupplierComboBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Tab)
        {
            if (sender is ComboBox cb)
            {
                cb.IsDropDownOpen = false;
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                FocusControl("SupplierInvoiceNoBox");
                if (SupplierInvoiceNoBox != null)
                {
                    SupplierInvoiceNoBox.SelectAll();
                }
            });
            e.Handled = true;
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
                    if (ProductSearchListView != null && ViewModel.SelectedProductSearchIndex >= 0 && ViewModel.SelectedProductSearchIndex < ProductSearchListView.Items.Count)
                    {
                        ProductSearchListView.SelectedIndex = ViewModel.SelectedProductSearchIndex;
                        ProductSearchListView.ScrollIntoView(ProductSearchListView.Items[ViewModel.SelectedProductSearchIndex]);
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == VirtualKey.Up)
                {
                    ViewModel.MoveSearchSelectionUp();
                    if (ProductSearchListView != null && ViewModel.SelectedProductSearchIndex >= 0 && ViewModel.SelectedProductSearchIndex < ProductSearchListView.Items.Count)
                    {
                        ProductSearchListView.SelectedIndex = ViewModel.SelectedProductSearchIndex;
                        ProductSearchListView.ScrollIntoView(ProductSearchListView.Items[ViewModel.SelectedProductSearchIndex]);
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
        if (!e.Handled)
        {
            await HandleProductNameInputKeyAsync(sender, e);
        }
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
        else if (e.Key == VirtualKey.Down || e.Key == VirtualKey.Up || e.Key == VirtualKey.Left || e.Key == VirtualKey.Right)
        {
            HandleCellNavigation(sender, e, rowIndex, 0);
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
            if (rowIndex >= 0)
            {
                FocusRowColumn(rowIndex, 1);
            }
        }
    }

    private void RowCell_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Down || e.Key == VirtualKey.Up || e.Key == VirtualKey.Enter)
        {
            var (rowIndex, colIndex) = GetRowAndColIndex(sender);
            if (rowIndex < 0 || rowIndex >= ViewModel.LineItems.Count) return;

            ViewModel.SetActiveRow(rowIndex);
            HandleCellNavigation(sender, e, rowIndex, colIndex);
        }
    }

    private void RowCell_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!e.Handled)
        {
            var (rowIndex, colIndex) = GetRowAndColIndex(sender);
            if (rowIndex < 0 || rowIndex >= ViewModel.LineItems.Count) return;

            ViewModel.SetActiveRow(rowIndex);
            HandleCellNavigation(sender, e, rowIndex, colIndex);
        }
    }

    private void HandleCellNavigation(object sender, KeyRoutedEventArgs e, int rowIndex, int colIndex)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
            case VirtualKey.Right when sender is not TextBox:
                if (colIndex >= TotalColumns - 1)
                {
                    // Last column of row
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
                e.Handled = true;
                break;

            case VirtualKey.Left when sender is not TextBox:
                if (colIndex > 0)
                {
                    FocusRowColumn(rowIndex, colIndex - 1);
                }
                else
                {
                    FocusControl("SupplierInvoiceNoBox");
                }
                e.Handled = true;
                break;

            case VirtualKey.Down:
                if (rowIndex < ViewModel.LineItems.Count - 1)
                {
                    FocusRowColumn(rowIndex + 1, colIndex);
                }
                e.Handled = true;
                break;

            case VirtualKey.Up:
                if (rowIndex > 0)
                {
                    FocusRowColumn(rowIndex - 1, colIndex);
                }
                else
                {
                    FocusControl("SupplierInvoiceNoBox");
                }
                e.Handled = true;
                break;

            case VirtualKey.Escape:
                FocusControl("SupplierInvoiceNoBox");
                e.Handled = true;
                break;
        }
    }

    private void ExpiryBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var (rowIndex, _) = GetRowAndColIndex(sender);
        if (rowIndex >= 0 && rowIndex < ViewModel.LineItems.Count)
        {
            var row = ViewModel.LineItems[rowIndex];
            if (row.ExpiryDate < DateTimeOffset.UtcNow)
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
}
