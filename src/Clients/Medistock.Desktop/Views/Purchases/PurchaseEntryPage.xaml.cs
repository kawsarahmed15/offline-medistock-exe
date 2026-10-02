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
    private const int TotalColumns = 12; // 0=Product, 1=Batch, 2=Expiry, 3=Hsn, 4=Strip, 5=Piece, 6=Qty, 7=Free, 8=Cost, 9=Mrp, 10=Disc, 11=Gst

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
        this.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(Page_PreviewKeyDown), handledEventsToo: true);
        this.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Page_KeyDown), handledEventsToo: true);

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PurchaseEntryViewModel.IsAddSupplierModalOpen))
            {
                if (ViewModel.IsAddSupplierModalOpen)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        NewSupplierNameBox?.Focus(FocusState.Programmatic);
                        NewSupplierNameBox?.SelectAll();
                    });
                }
                else
                {
                    // Focus on Supplier Invoice No when supplier modal closes with selected supplier
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(50);
                        if (ViewModel.SelectedSupplier != null)
                        {
                            SupplierInvoiceNoBox?.Focus(FocusState.Programmatic);
                            SupplierInvoiceNoBox?.SelectAll();
                        }
                        else
                        {
                            SupplierAutoSuggestBox?.Focus(FocusState.Programmatic);
                        }
                    });
                }
            }
            else if (e.PropertyName == nameof(PurchaseEntryViewModel.IsPurchaseEntryScreenOpen))
            {
                if (ViewModel.IsPurchaseEntryScreenOpen && !ViewModel.IsAddProductModalOpen && !ViewModel.IsAddSupplierModalOpen)
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(80);
                        if (ViewModel.IsPurchaseEntryScreenOpen && !ViewModel.IsAddProductModalOpen && !ViewModel.IsAddSupplierModalOpen)
                        {
                            SupplierAutoSuggestBox?.Focus(FocusState.Programmatic);
                        }
                    });
                }
            }
            else if (e.PropertyName == nameof(PurchaseEntryViewModel.IsAddProductModalOpen))
            {
                if (ViewModel.IsAddProductModalOpen)
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(100);
                        NewProductNameBox?.Focus(FocusState.Programmatic);
                        if (!string.IsNullOrWhiteSpace(NewProductNameBox?.Text))
                        {
                            NewProductNameBox.SelectAll();
                        }
                    });
                }
                else
                {
                    // Focus on last product in the list row when modal dismissed
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(60);
                        if (ViewModel.LineItems.Count > 0)
                        {
                            var targetIndex = ViewModel.LineItems.Count - 1;
                            ViewModel.SetActiveRow(targetIndex);
                            FocusRowColumn(targetIndex, 0);
                        }
                    });
                }
            }
            else if (e.PropertyName == nameof(PurchaseEntryViewModel.IsSaveSummaryModalOpen))
            {
                if (ViewModel.IsSaveSummaryModalOpen)
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(100);
                        ConfirmSaveSummaryButton?.Focus(FocusState.Programmatic);
                    });
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        PurchaseProductSearchBox?.Focus(FocusState.Programmatic);
                    });
                }
            }
        };
    }

    private void Page_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            if (ViewModel.IsExitConfirmDialogOpen)
            {
                ViewModel.CancelExitDialog();
                e.Handled = true;
                return;
            }
            if (ViewModel.IsSaveSummaryModalOpen)
            {
                ViewModel.CancelSaveSummary();
                e.Handled = true;
                return;
            }
            if (ViewModel.IsAddProductModalOpen)
            {
                ViewModel.CloseAddProductModal();
                e.Handled = true;
                return;
            }
            if (ViewModel.IsAddSupplierModalOpen)
            {
                ViewModel.CloseAddSupplierModalCommand.Execute(null);
                DispatcherQueue.TryEnqueue(() =>
                {
                    SupplierAutoSuggestBox?.Focus(FocusState.Programmatic);
                });
                e.Handled = true;
                return;
            }
            if (ViewModel.IsCancelConfirmOpen)
            {
                ViewModel.DismissCancelConfirmCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (ViewModel.IsInvoiceDetailsModalOpen)
            {
                ViewModel.CloseInvoiceDetailsCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (ViewModel.IsPurchaseReturnModalOpen)
            {
                ViewModel.ClosePurchaseReturnModal();
                e.Handled = true;
                return;
            }
            if (ViewModel.HasProductSearchResults || ViewModel.IsProductSearchOpen)
            {
                ViewModel.HasProductSearchResults = false;
                ViewModel.IsProductSearchOpen = false;
                e.Handled = true;
                return;
            }
            if (ViewModel.IsPurchaseEntryScreenOpen || ViewModel.SelectedTab == "Entry")
            {
                ViewModel.RequestClosePurchaseEntry();
                e.Handled = true;
                return;
            }
            else
            {
                // When on Master / Ledger screen, Esc navigates back to POS (home screen)
                App.MainWindowInstance?.NavigateToPos();
                e.Handled = true;
                return;
            }
        }

        // If any modal is open, don't intercept F2/F3 at page level
        if (ViewModel.IsAddProductModalOpen || ViewModel.IsAddSupplierModalOpen || ViewModel.IsCancelConfirmOpen || ViewModel.IsInvoiceDetailsModalOpen || ViewModel.IsSaveSummaryModalOpen || ViewModel.IsExitConfirmDialogOpen || ViewModel.IsPurchaseReturnModalOpen)
        {
            return;
        }

        if (e.Key == VirtualKey.F2)
        {
            if (!ViewModel.IsPurchaseEntryScreenOpen)
            {
                ViewModel.OpenPurchaseEntry();
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(100);
                    SupplierAutoSuggestBox?.Focus(FocusState.Programmatic);
                });
                e.Handled = true;
                return;
            }
            else
            {
                ViewModel.AddBlankRowCommand.Execute(null);
                var newIdx = ViewModel.LineItems.Count - 1;
                ViewModel.SetActiveRow(newIdx);
                DispatcherQueue.TryEnqueue(() =>
                {
                    FocusRowColumn(newIdx, 0);
                });
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.F3 && (ViewModel.IsPurchaseEntryScreenOpen || ViewModel.SelectedTab == "Entry"))
        {
            var targetRow = ViewModel.ActiveRow ?? (ViewModel.LineItems.Count > 0 ? ViewModel.LineItems[^1] : null);
            ViewModel.OpenAddProductModal(null, targetRow);
            e.Handled = true;
            return;
        }
    }

    private void NewSupplierNameBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus(FocusState.Programmatic);
            tb.SelectAll();
        }
    }

    private string? _pendingInitialProductName;

    public void PrepareNewProductEntry(string productName)
    {
        var trimmed = productName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return;
        _pendingInitialProductName = trimmed;

        ApplyPendingProductEntry();
    }

    private void ApplyPendingProductEntry()
    {
        if (string.IsNullOrWhiteSpace(_pendingInitialProductName)) return;

        var nameToSet = _pendingInitialProductName;
        _pendingInitialProductName = null;

        ViewModel.OpenPurchaseEntry();
        var targetRow = ViewModel.ActiveRow ?? (ViewModel.LineItems.Count > 0 ? ViewModel.LineItems[0] : null);
        if (targetRow != null)
        {
            ViewModel.OpenAddProductModal(nameToSet, targetRow);
        }
        else
        {
            ViewModel.OpenAddProductModal(nameToSet, null);
        }

        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(120);
            NewProductNameBox?.Focus(FocusState.Programmatic);
            if (!string.IsNullOrWhiteSpace(NewProductNameBox?.Text))
            {
                NewProductNameBox.SelectAll();
            }
        });
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        ApplyPendingProductEntry();
        if (!ViewModel.IsAddProductModalOpen && !ViewModel.IsAddSupplierModalOpen && ViewModel.IsPurchaseEntryScreenOpen)
        {
            await Task.Delay(150);
            if (!ViewModel.IsAddProductModalOpen && !ViewModel.IsAddSupplierModalOpen && ViewModel.IsPurchaseEntryScreenOpen)
            {
                FocusControl("SupplierAutoSuggestBox");
                if (SupplierAutoSuggestBox != null)
                {
                    SupplierAutoSuggestBox.Focus(FocusState.Programmatic);
                }
            }
        }
    }

    // ─── Global Key Router ───────────────────────────────────────────────────
    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 1. Modal Intercepts
        if (ViewModel.IsSaveSummaryModalOpen)
        {
            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.CancelSaveSummary();
                e.Handled = true;
            }
            return;
        }

        if (ViewModel.IsExitConfirmDialogOpen)
        {
            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.CancelExitDialog();
                e.Handled = true;
            }
            return;
        }

        if (ViewModel.IsCancelConfirmOpen || ViewModel.IsAddSupplierModalOpen || ViewModel.IsInvoiceDetailsModalOpen)
        {
            if (e.Key == VirtualKey.Escape)
            {
                bool wasAddSupplier = ViewModel.IsAddSupplierModalOpen;
                ViewModel.DismissCancelConfirmCommand.Execute(null);
                ViewModel.CloseAddSupplierModalCommand.Execute(null);
                ViewModel.CloseInvoiceDetailsCommand.Execute(null);
                if (wasAddSupplier)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SupplierAutoSuggestBox?.Focus(FocusState.Programmatic);
                    });
                }
                e.Handled = true;
            }
            return;
        }

        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        // If the key was already handled by child controls (e.g. PreviewKeyDown), do not process Delete or Ctrl+Z again!
        if (e.Handled && (e.Key == VirtualKey.Delete || (e.Key == VirtualKey.Z && ctrl)))
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.F2 when ViewModel.IsPurchaseEntryScreenOpen || ViewModel.SelectedTab == "Entry":
                ViewModel.AddBlankRowCommand.Execute(null);
                var f2Idx = ViewModel.LineItems.Count - 1;
                ViewModel.SetActiveRow(f2Idx);
                DispatcherQueue.TryEnqueue(() =>
                {
                    FocusRowColumn(f2Idx, 0);
                });
                e.Handled = true;
                break;

            case VirtualKey.F3 when ViewModel.IsPurchaseEntryScreenOpen || ViewModel.SelectedTab == "Entry":
                var f3TargetRow = ViewModel.ActiveRow ?? (ViewModel.LineItems.Count > 0 ? ViewModel.LineItems[^1] : null);
                ViewModel.OpenAddProductModal(null, f3TargetRow);
                e.Handled = true;
                break;

            case VirtualKey.F5:
                _ = ViewModel.RefreshAllPurchaseDataCommand.ExecuteAsync(null);
                e.Handled = true;
                break;

            case VirtualKey.F6:
            case VirtualKey.S when ctrl:
                if (ViewModel.IsPurchaseEntryScreenOpen || ViewModel.SelectedTab == "Entry")
                {
                    ViewModel.RequestSaveSummary();
                    e.Handled = true;
                }
                break;

            case VirtualKey.Z when ctrl:
                if (ViewModel.IsPurchaseEntryScreenOpen || ViewModel.SelectedTab == "Entry")
                {
                    if (e.Handled) return;
                    ViewModel.UndoRemoveRow();
                    if (ViewModel.ActiveRowIndex >= 0 && ViewModel.ActiveRowIndex < ViewModel.LineItems.Count)
                    {
                        FocusRowColumn(ViewModel.ActiveRowIndex, 0);
                    }
                    e.Handled = true;
                }
                break;

            case VirtualKey.Escape:
                if (ViewModel.IsPurchaseEntryScreenOpen || ViewModel.SelectedTab == "Entry")
                {
                    ViewModel.RequestClosePurchaseEntry();
                    e.Handled = true;
                }
                break;

            case VirtualKey.Delete:
                if (ViewModel.IsPurchaseEntryScreenOpen || ViewModel.SelectedTab == "Entry")
                {
                    if (e.Handled) return;

                    var focusedElement = FocusManager.GetFocusedElement(this.XamlRoot) as DependencyObject;
                    var targetRow = FindRowFromElement(focusedElement)
                                 ?? (ViewModel.ActiveRowIndex >= 0 && ViewModel.ActiveRowIndex < ViewModel.LineItems.Count ? ViewModel.LineItems[ViewModel.ActiveRowIndex] : null);
                    if (targetRow != null)
                    {
                        var rIdx = ViewModel.LineItems.IndexOf(targetRow);
                        ViewModel.RemoveRow(targetRow);
                        if (ViewModel.LineItems.Count > 0)
                        {
                            var nextIdx = Math.Clamp(rIdx, 0, ViewModel.LineItems.Count - 1);
                            FocusRowColumn(nextIdx, 0);
                        }
                        e.Handled = true;
                    }
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
        if (e.Key == VirtualKey.Left && sender is TextBox tb && (tb.SelectionStart == 0 || string.IsNullOrEmpty(tb.Text)))
        {
            SupplierAutoSuggestBox?.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Tab || e.Key == VirtualKey.Right || e.Key == VirtualKey.Down)
        {
            SupplierInvoiceDateBox?.Focus(FocusState.Programmatic);
            SupplierInvoiceDateBox?.SelectAll();
            e.Handled = true;
        }
    }

    private bool _isDateFormatting = false;

    private void SupplierInvoiceDateBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isDateFormatting) return;
        if (sender is TextBox tb)
        {
            var raw = tb.Text;
            if (string.IsNullOrEmpty(raw)) return;

            // Automatically format as DD/MM/YYYY
            if (raw.Length == 2 && !raw.Contains('/') && char.IsDigit(raw[0]) && char.IsDigit(raw[1]))
            {
                _isDateFormatting = true;
                try
                {
                    tb.Text = raw + "/";
                    tb.SelectionStart = tb.Text.Length;
                }
                finally
                {
                    _isDateFormatting = false;
                }
            }
            else if (raw.Length == 5 && raw.Count(c => c == '/') == 1 && char.IsDigit(raw[3]) && char.IsDigit(raw[4]))
            {
                _isDateFormatting = true;
                try
                {
                    tb.Text = raw + "/";
                    tb.SelectionStart = tb.Text.Length;
                }
                finally
                {
                    _isDateFormatting = false;
                }
            }
        }
    }

    private void SupplierInvoiceDateBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            // Left arrow to Invoice No
            if (e.Key == VirtualKey.Left && (tb.SelectionStart == 0 || string.IsNullOrEmpty(tb.Text)))
            {
                SupplierInvoiceNoBox?.Focus(FocusState.Programmatic);
                SupplierInvoiceNoBox?.SelectAll();
                e.Handled = true;
                return;
            }

            // Backspace handling: delete across slash
            if (e.Key == VirtualKey.Back)
            {
                if (tb.SelectionStart == 3 && tb.Text.Length >= 3 && tb.Text[2] == '/')
                {
                    _isDateFormatting = true;
                    try
                    {
                        tb.Text = tb.Text.Substring(0, 1) + (tb.Text.Length > 3 ? tb.Text.Substring(3) : "");
                        tb.SelectionStart = 1;
                        e.Handled = true;
                        return;
                    }
                    finally
                    {
                        _isDateFormatting = false;
                    }
                }
                else if (tb.SelectionStart == 6 && tb.Text.Length >= 6 && tb.Text[5] == '/')
                {
                    _isDateFormatting = true;
                    try
                    {
                        tb.Text = tb.Text.Substring(0, 4) + (tb.Text.Length > 6 ? tb.Text.Substring(6) : "");
                        tb.SelectionStart = 4;
                        e.Handled = true;
                        return;
                    }
                    finally
                    {
                        _isDateFormatting = false;
                    }
                }
            }
        }

        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Down || e.Key == VirtualKey.Right)
        {
            CommitDateAndFocusProductSearch();
            e.Handled = true;
        }
    }

    private void SupplierInvoiceDateBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Down || e.Key == VirtualKey.Right)
        {
            CommitDateAndFocusProductSearch();
            e.Handled = true;
        }
    }

    private void CommitDateAndFocusProductSearch()
    {
        if (SupplierInvoiceDateBox != null && !string.IsNullOrWhiteSpace(SupplierInvoiceDateBox.Text))
        {
            var text = SupplierInvoiceDateBox.Text.Trim();
            if (DateTime.TryParseExact(text, new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            {
                ViewModel.SupplierInvoiceDate = new DateTimeOffset(dt, TimeSpan.Zero);
            }
        }

        if (ViewModel.LineItems.Count == 0)
        {
            ViewModel.AddBlankRowCommand.Execute(null);
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            PurchaseProductSearchBox?.Focus(FocusState.Programmatic);
            PurchaseProductSearchBox?.SelectAll();
        });
    }

    // ─── Top Product Search Box Handlers ─────────────────────────────────────
    private void PurchaseProductSearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.HasProductSearchResults = false;
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Down)
        {
            if (ViewModel.HasProductSearchResults && ViewModel.ProductSearchResults.Count > 0)
            {
                ViewModel.MoveSearchSelectionDown();
                if (ViewModel.SelectedProductSearchItem != null && PurchaseProductSearchResultsList != null)
                {
                    PurchaseProductSearchResultsList.ScrollIntoView(ViewModel.SelectedProductSearchItem);
                }
                e.Handled = true;
                return;
            }
            else
            {
                FocusRowColumn(0, 0);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.Up)
        {
            if (ViewModel.HasProductSearchResults && ViewModel.ProductSearchResults.Count > 0 && ViewModel.SelectedProductSearchIndex > 0)
            {
                ViewModel.MoveSearchSelectionUp();
                if (ViewModel.SelectedProductSearchItem != null && PurchaseProductSearchResultsList != null)
                {
                    PurchaseProductSearchResultsList.ScrollIntoView(ViewModel.SelectedProductSearchItem);
                }
                e.Handled = true;
                return;
            }
            else
            {
                SupplierInvoiceDateBox?.Focus(FocusState.Programmatic);
                SupplierInvoiceDateBox?.SelectAll();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.Left)
        {
            if (sender is TextBox tb && (tb.SelectionLength == tb.Text.Length || tb.SelectionStart == 0 || string.IsNullOrEmpty(tb.Text)))
            {
                SupplierInvoiceDateBox?.Focus(FocusState.Programmatic);
                SupplierInvoiceDateBox?.SelectAll();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.Right)
        {
            if (sender is TextBox tb && (tb.SelectionLength == tb.Text.Length || tb.SelectionStart == tb.Text.Length || string.IsNullOrEmpty(tb.Text)))
            {
                FocusRowColumn(0, 0);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.Enter)
        {
            HandleProductSearchSubmit();
            e.Handled = true;
            return;
        }
    }

    private void PurchaseProductSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == VirtualKey.Enter)
        {
            HandleProductSearchSubmit();
            e.Handled = true;
        }
    }

    private void HandleProductSearchSubmit()
    {
        if (ViewModel.HasProductSearchResults && ViewModel.ProductSearchResults.Count > 0 && ViewModel.SelectedProductSearchItem != null)
        {
            var row = ViewModel.SelectTopProductSearch(ViewModel.SelectedProductSearchItem);
            var rowIndex = ViewModel.LineItems.IndexOf(row);
            FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1); // Focus Batch No
        }
        else if (!string.IsNullOrWhiteSpace(ViewModel.ProductSearchQuery))
        {
            ViewModel.AddSearchedProductModal();
        }
        else
        {
            // Empty / blank search query -> open Purchase Invoice Summary & Confirmation Preview Modal
            ViewModel.RequestSaveSummary();
        }
    }

    private void PurchaseProductSearchResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ProductSearchItemViewModel item)
        {
            var row = ViewModel.SelectTopProductSearch(item);
            var rowIndex = ViewModel.LineItems.IndexOf(row);
            FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1);
        }
    }

    private void PurchaseProductSearchResultsList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SelectedProductSearchItem != null)
        {
            var row = ViewModel.SelectTopProductSearch(ViewModel.SelectedProductSearchItem);
            var rowIndex = ViewModel.LineItems.IndexOf(row);
            FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.HasProductSearchResults = false;
            PurchaseProductSearchBox?.Focus(FocusState.Programmatic);
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
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (e.Key == VirtualKey.Z && ctrl)
        {
            ViewModel.UndoRemoveRow();
            if (ViewModel.ActiveRowIndex >= 0 && ViewModel.ActiveRowIndex < ViewModel.LineItems.Count)
            {
                FocusRowColumn(ViewModel.ActiveRowIndex, 0);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Delete && !ViewModel.IsProductSearchOpen)
        {
            var targetRow = FindRowFromElement(sender as DependencyObject) 
                         ?? (sender as FrameworkElement)?.DataContext as PurchaseItemRowViewModel;
            if (targetRow != null)
            {
                var rowIndex = ViewModel.LineItems.IndexOf(targetRow);
                ViewModel.RemoveRow(targetRow);
                if (ViewModel.LineItems.Count > 0)
                {
                    var nextIdx = Math.Clamp(rowIndex, 0, ViewModel.LineItems.Count - 1);
                    FocusRowColumn(nextIdx, 0);
                }
                e.Handled = true;
                return;
            }
        }

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
                        ViewModel.IsProductSearchOpen = false;
                        FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1); // Move to Batch No
                    }
                    else if (tb != null && !string.IsNullOrWhiteSpace(tb.Text) && targetRow != null)
                    {
                        var matching = ViewModel.ProductSearchResults.FirstOrDefault(p => string.Equals(p.Name.Trim(), tb.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (matching != null)
                        {
                            ViewModel.SelectProductSearch(matching, targetRow);
                            ViewModel.IsProductSearchOpen = false;
                            FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1);
                        }
                        else
                        {
                            ViewModel.OpenAddProductModal(tb.Text, targetRow);
                        }
                    }
                    else
                    {
                        ViewModel.IsProductSearchOpen = false;
                        FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1);
                    }

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
                    ViewModel.IsProductSearchOpen = false;
                    FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1);
                }
                else if (tb != null && !string.IsNullOrWhiteSpace(tb.Text) && targetRow != null)
                {
                    var matching = ViewModel.ProductSearchResults.FirstOrDefault(p => string.Equals(p.Name.Trim(), tb.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (matching != null)
                    {
                        ViewModel.SelectProductSearch(matching, targetRow);
                        ViewModel.IsProductSearchOpen = false;
                        FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1);
                    }
                    else
                    {
                        ViewModel.OpenAddProductModal(tb.Text, targetRow);
                    }
                }
                else
                {
                    ViewModel.IsProductSearchOpen = false;
                    FocusRowColumn(rowIndex >= 0 ? rowIndex : 0, 1);
                }

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
            if (!ViewModel.IsAddProductModalOpen)
            {
                FocusRowColumn(rowIndex, 1); // Move to Batch No
            }
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
                PurchaseProductSearchBox?.Focus(FocusState.Programmatic);
                PurchaseProductSearchBox?.SelectAll();
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
                PurchaseProductSearchBox?.Focus(FocusState.Programmatic);
                PurchaseProductSearchBox?.SelectAll();
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
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        // 0. Row shortcuts: Ctrl+Z (Undo) and Delete (Remove row)
        if (e.Key == VirtualKey.Z && ctrl)
        {
            ViewModel.UndoRemoveRow();
            if (ViewModel.ActiveRowIndex >= 0 && ViewModel.ActiveRowIndex < ViewModel.LineItems.Count)
            {
                FocusRowColumn(ViewModel.ActiveRowIndex, 0);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Delete)
        {
            var targetRow = FindRowFromElement(sender as DependencyObject) 
                         ?? (sender as FrameworkElement)?.DataContext as PurchaseItemRowViewModel;
            if (targetRow != null)
            {
                var rIdx = ViewModel.LineItems.IndexOf(targetRow);
                ViewModel.RemoveRow(targetRow);
                if (ViewModel.LineItems.Count > 0)
                {
                    var nextIdx = Math.Clamp(rIdx, 0, ViewModel.LineItems.Count - 1);
                    FocusRowColumn(nextIdx, 0);
                }
                e.Handled = true;
                return;
            }
        }
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

        // 2. For all row controls (TextBox, NumberBox)
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
                    PurchaseProductSearchBox?.Focus(FocusState.Programmatic);
                    PurchaseProductSearchBox?.SelectAll();
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
            // Last column of row (GST %) -> advance to top medicine search bar to add next medicine
            DispatcherQueue.TryEnqueue(() =>
            {
                PurchaseProductSearchBox?.Focus(FocusState.Programmatic);
                PurchaseProductSearchBox?.SelectAll();
            });
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
            PurchaseProductSearchBox?.Focus(FocusState.Programmatic);
            PurchaseProductSearchBox?.SelectAll();
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

    private PurchaseItemRowViewModel? FindRowFromElement(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is FrameworkElement fe && fe.DataContext is PurchaseItemRowViewModel row)
            {
                return row;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void Row_GotFocus(object sender, RoutedEventArgs e)
    {
        var row = (sender as FrameworkElement)?.DataContext as PurchaseItemRowViewModel
               ?? FindRowFromElement(e.OriginalSource as DependencyObject);
        if (row != null)
        {
            int idx = ViewModel.LineItems.IndexOf(row);
            if (idx >= 0 && idx != ViewModel.ActiveRowIndex)
            {
                ViewModel.SetActiveRow(idx);
            }
        }
    }

    private void RowDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var targetRow = FindRowFromElement(sender as DependencyObject) 
                     ?? (sender as FrameworkElement)?.DataContext as PurchaseItemRowViewModel;
        if (targetRow != null)
        {
            var idx = ViewModel.LineItems.IndexOf(targetRow);
            ViewModel.RemoveRow(targetRow);
            if (ViewModel.LineItems.Count > 0)
            {
                var nextIdx = Math.Clamp(idx, 0, ViewModel.LineItems.Count - 1);
                FocusRowColumn(nextIdx, 0);
            }
        }
    }

    private (int RowIndex, int ColIndex) GetRowAndColIndex(object sender)
    {
        if (sender is DependencyObject dobj)
        {
            int colIndex = -1;
            DependencyObject? curr = dobj;
            while (curr != null)
            {
                if (curr is FrameworkElement fe)
                {
                    if (colIndex < 0 && fe.Tag is string tagStr && int.TryParse(tagStr, out int parsedCol))
                    {
                        colIndex = parsedCol;
                    }
                    if (fe.DataContext is PurchaseItemRowViewModel rowItem)
                    {
                        int rowIndex = ViewModel.LineItems.IndexOf(rowItem);
                        return (rowIndex, Math.Max(0, colIndex));
                    }
                }
                curr = VisualTreeHelper.GetParent(curr);
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

    private void TotalStockValueCard_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.NavigateToInventory();
    }

    private void NewSupplierField_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CloseAddSupplierModalCommand.Execute(null);
            DispatcherQueue.TryEnqueue(() =>
            {
                SupplierAutoSuggestBox?.Focus(FocusState.Programmatic);
            });
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            if (sender == NewSupplierNameBox)
            {
                NewSupplierGstinBox?.Focus(FocusState.Programmatic);
                NewSupplierGstinBox?.SelectAll();
            }
            else if (sender == NewSupplierGstinBox)
            {
                NewSupplierDlNumberBox?.Focus(FocusState.Programmatic);
                NewSupplierDlNumberBox?.SelectAll();
            }
            else if (sender == NewSupplierDlNumberBox)
            {
                NewSupplierPhoneBox?.Focus(FocusState.Programmatic);
                NewSupplierPhoneBox?.SelectAll();
            }
            else if (sender == NewSupplierPhoneBox)
            {
                NewSupplierEmailBox?.Focus(FocusState.Programmatic);
                NewSupplierEmailBox?.SelectAll();
            }
            else if (sender == NewSupplierEmailBox)
            {
                NewSupplierAddressBox?.Focus(FocusState.Programmatic);
                NewSupplierAddressBox?.SelectAll();
            }
            else if (sender == NewSupplierAddressBox)
            {
                NewSupplierCreditDaysBox?.Focus(FocusState.Programmatic);
            }
            else if (sender == NewSupplierCreditDaysBox)
            {
                NewSupplierOpeningBalanceBox?.Focus(FocusState.Programmatic);
            }
            else if (sender == NewSupplierOpeningBalanceBox)
            {
                SaveNewSupplierBtn?.Focus(FocusState.Programmatic);
            }
            else if (sender == SaveNewSupplierBtn)
            {
                ViewModel.SaveNewSupplierCommand.Execute(null);
            }
            else if (sender == CancelNewSupplierBtn)
            {
                ViewModel.CloseAddSupplierModalCommand.Execute(null);
                DispatcherQueue.TryEnqueue(() =>
                {
                    SupplierAutoSuggestBox?.Focus(FocusState.Programmatic);
                });
            }
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Down)
        {
            if (sender == NewSupplierNameBox)
            {
                NewSupplierGstinBox?.Focus(FocusState.Programmatic);
                NewSupplierGstinBox?.SelectAll();
            }
            else if (sender == NewSupplierGstinBox)
            {
                NewSupplierPhoneBox?.Focus(FocusState.Programmatic);
                NewSupplierPhoneBox?.SelectAll();
            }
            else if (sender == NewSupplierDlNumberBox)
            {
                NewSupplierEmailBox?.Focus(FocusState.Programmatic);
                NewSupplierEmailBox?.SelectAll();
            }
            else if (sender == NewSupplierPhoneBox || sender == NewSupplierEmailBox)
            {
                NewSupplierAddressBox?.Focus(FocusState.Programmatic);
                NewSupplierAddressBox?.SelectAll();
            }
            else if (sender == NewSupplierAddressBox)
            {
                NewSupplierCreditDaysBox?.Focus(FocusState.Programmatic);
            }
            else if (sender == NewSupplierCreditDaysBox)
            {
                CancelNewSupplierBtn?.Focus(FocusState.Programmatic);
            }
            else if (sender == NewSupplierOpeningBalanceBox)
            {
                SaveNewSupplierBtn?.Focus(FocusState.Programmatic);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Up)
        {
            if (sender == SaveNewSupplierBtn)
            {
                NewSupplierOpeningBalanceBox?.Focus(FocusState.Programmatic);
            }
            else if (sender == CancelNewSupplierBtn)
            {
                NewSupplierCreditDaysBox?.Focus(FocusState.Programmatic);
            }
            else if (sender == NewSupplierOpeningBalanceBox || sender == NewSupplierCreditDaysBox)
            {
                NewSupplierAddressBox?.Focus(FocusState.Programmatic);
                NewSupplierAddressBox?.SelectAll();
            }
            else if (sender == NewSupplierAddressBox)
            {
                NewSupplierPhoneBox?.Focus(FocusState.Programmatic);
                NewSupplierPhoneBox?.SelectAll();
            }
            else if (sender == NewSupplierEmailBox)
            {
                NewSupplierDlNumberBox?.Focus(FocusState.Programmatic);
                NewSupplierDlNumberBox?.SelectAll();
            }
            else if (sender == NewSupplierPhoneBox)
            {
                NewSupplierGstinBox?.Focus(FocusState.Programmatic);
                NewSupplierGstinBox?.SelectAll();
            }
            else if (sender == NewSupplierDlNumberBox || sender == NewSupplierGstinBox)
            {
                NewSupplierNameBox?.Focus(FocusState.Programmatic);
                NewSupplierNameBox?.SelectAll();
            }
            e.Handled = true;
            return;
        }
    }

    // Add Product Modal Event Handlers
    private void NewProductNameBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus(FocusState.Programmatic);
            if (!string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.SelectAll();
            }
        }
    }

    private void NewProductExpiryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            var raw = tb.Text;
            if (raw.Length == 2 && !raw.Contains('/') && char.IsDigit(raw[0]) && char.IsDigit(raw[1]))
            {
                tb.Text = raw + "/";
                tb.SelectionStart = 3;
            }
        }
    }

    private async void NewProductField_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CloseAddProductModal();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            if (ReferenceEquals(sender, CancelNewProductBtn))
            {
                ViewModel.CloseAddProductModal();
                e.Handled = true;
                return;
            }

            if (ReferenceEquals(sender, SaveNewProductBtn) || ReferenceEquals(sender, NewProductRxCheckBox))
            {
                if (!string.IsNullOrWhiteSpace(ViewModel.NewProductName) && ViewModel.NewProductMrp > 0)
                {
                    await ViewModel.SaveNewProductAsync();
                    e.Handled = true;
                }
                else
                {
                    SaveNewProductBtn?.Focus(FocusState.Programmatic);
                    e.Handled = true;
                }
                return;
            }

            HandleAddProductFieldNext(sender);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Down)
        {
            if (ReferenceEquals(sender, NewProductStockTypeComboBox))
            {
                ViewModel.SelectNextStockType();
                e.Handled = true;
                return;
            }
            if (ReferenceEquals(sender, NewProductTaxComboBox))
            {
                ViewModel.SelectNextTaxRate();
                e.Handled = true;
                return;
            }
            if (ReferenceEquals(sender, NewProductVolumeMlComboBox) || ReferenceEquals(sender, NewProductWeightGmComboBox) ||
                ReferenceEquals(sender, NewProductPackOptionsBox) || ReferenceEquals(sender, NewProductPcsPerStripBox))
            {
                ViewModel.SelectNextPackSize();
                e.Handled = true;
                return;
            }

            HandleAddProductFieldDown(sender);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Up)
        {
            if (ReferenceEquals(sender, NewProductStockTypeComboBox))
            {
                ViewModel.SelectPreviousStockType();
                e.Handled = true;
                return;
            }
            if (ReferenceEquals(sender, NewProductTaxComboBox))
            {
                ViewModel.SelectPreviousTaxRate();
                e.Handled = true;
                return;
            }
            if (ReferenceEquals(sender, NewProductVolumeMlComboBox) || ReferenceEquals(sender, NewProductWeightGmComboBox) ||
                ReferenceEquals(sender, NewProductPackOptionsBox) || ReferenceEquals(sender, NewProductPcsPerStripBox))
            {
                ViewModel.SelectPreviousPackSize();
                e.Handled = true;
                return;
            }

            HandleAddProductFieldUp(sender);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Right)
        {
            if (sender is TextBox tb)
            {
                if (tb.SelectionLength == tb.Text.Length || tb.SelectionStart == tb.Text.Length || string.IsNullOrEmpty(tb.Text))
                {
                    HandleAddProductFieldRight(sender);
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                HandleAddProductFieldRight(sender);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.Left)
        {
            if (sender is TextBox tb)
            {
                if (tb.SelectionLength == tb.Text.Length || tb.SelectionStart == 0 || string.IsNullOrEmpty(tb.Text))
                {
                    HandleAddProductFieldLeft(sender);
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                HandleAddProductFieldLeft(sender);
                e.Handled = true;
                return;
            }
        }
    }

    private void FocusActivePackagingControl()
    {
        if (ViewModel.IsTabletOrCapsule)
        {
            NewProductStripCountBox?.Focus(FocusState.Programmatic);
        }
        else if (ViewModel.IsVolumeMlType)
        {
            NewProductVolumeMlComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ViewModel.IsWeightGmType)
        {
            NewProductWeightGmComboBox?.Focus(FocusState.Programmatic);
        }
        else
        {
            NewProductPackOptionsBox?.Focus(FocusState.Programmatic);
        }
    }

    private void HandleAddProductFieldDown(object sender)
    {
        if (ReferenceEquals(sender, NewProductNameBox))
        {
            NewProductManufacturerBox?.Focus(FocusState.Programmatic);
            NewProductManufacturerBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductManufacturerBox))
        {
            NewProductStockTypeComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductStockTypeComboBox))
        {
            NewProductHsnBox?.Focus(FocusState.Programmatic);
            NewProductHsnBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductStripCountBox) || ReferenceEquals(sender, NewProductPcsPerStripBox) ||
                 ReferenceEquals(sender, NewProductVolumeMlComboBox) || ReferenceEquals(sender, NewProductWeightGmComboBox) ||
                 ReferenceEquals(sender, NewProductPackOptionsBox))
        {
            NewProductTaxComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductHsnBox))
        {
            NewProductMrpBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductTaxComboBox))
        {
            NewProductBuyingPriceBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductMrpBox))
        {
            NewProductInitialStockQtyBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductBuyingPriceBox))
        {
            NewProductBatchBox?.Focus(FocusState.Programmatic);
            NewProductBatchBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductInitialStockQtyBox) || ReferenceEquals(sender, NewProductBatchBox) || ReferenceEquals(sender, NewProductExpiryBox))
        {
            NewProductRxCheckBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductRxCheckBox))
        {
            SaveNewProductBtn?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, CancelNewProductBtn) || ReferenceEquals(sender, SaveNewProductBtn))
        {
            NewProductNameBox?.Focus(FocusState.Programmatic);
            NewProductNameBox?.SelectAll();
        }
    }

    private void HandleAddProductFieldUp(object sender)
    {
        if (ReferenceEquals(sender, SaveNewProductBtn) || ReferenceEquals(sender, CancelNewProductBtn))
        {
            NewProductRxCheckBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductRxCheckBox))
        {
            NewProductInitialStockQtyBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductExpiryBox) || ReferenceEquals(sender, NewProductBatchBox))
        {
            NewProductBuyingPriceBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductInitialStockQtyBox))
        {
            NewProductMrpBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductBuyingPriceBox))
        {
            NewProductTaxComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductMrpBox))
        {
            NewProductHsnBox?.Focus(FocusState.Programmatic);
            NewProductHsnBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductTaxComboBox))
        {
            FocusActivePackagingControl();
        }
        else if (ReferenceEquals(sender, NewProductHsnBox))
        {
            NewProductStockTypeComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductStripCountBox) || ReferenceEquals(sender, NewProductPcsPerStripBox) ||
                 ReferenceEquals(sender, NewProductVolumeMlComboBox) || ReferenceEquals(sender, NewProductWeightGmComboBox) ||
                 ReferenceEquals(sender, NewProductPackOptionsBox))
        {
            NewProductManufacturerBox?.Focus(FocusState.Programmatic);
            NewProductManufacturerBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductStockTypeComboBox))
        {
            NewProductManufacturerBox?.Focus(FocusState.Programmatic);
            NewProductManufacturerBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductManufacturerBox))
        {
            NewProductNameBox?.Focus(FocusState.Programmatic);
            NewProductNameBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductNameBox))
        {
            SaveNewProductBtn?.Focus(FocusState.Programmatic);
        }
    }

    private void HandleAddProductFieldRight(object sender)
    {
        if (ReferenceEquals(sender, NewProductNameBox))
        {
            NewProductManufacturerBox?.Focus(FocusState.Programmatic);
            NewProductManufacturerBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductManufacturerBox))
        {
            NewProductStockTypeComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductStockTypeComboBox))
        {
            FocusActivePackagingControl();
        }
        else if (ReferenceEquals(sender, NewProductStripCountBox))
        {
            NewProductPcsPerStripBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductPcsPerStripBox) || ReferenceEquals(sender, NewProductVolumeMlComboBox) ||
                 ReferenceEquals(sender, NewProductWeightGmComboBox) || ReferenceEquals(sender, NewProductPackOptionsBox))
        {
            NewProductHsnBox?.Focus(FocusState.Programmatic);
            NewProductHsnBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductHsnBox))
        {
            NewProductTaxComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductTaxComboBox))
        {
            NewProductMrpBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductMrpBox))
        {
            NewProductBuyingPriceBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductBuyingPriceBox))
        {
            NewProductInitialStockQtyBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductInitialStockQtyBox))
        {
            NewProductBatchBox?.Focus(FocusState.Programmatic);
            NewProductBatchBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductBatchBox))
        {
            NewProductExpiryBox?.Focus(FocusState.Programmatic);
            NewProductExpiryBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductExpiryBox))
        {
            NewProductRxCheckBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductRxCheckBox))
        {
            CancelNewProductBtn?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, CancelNewProductBtn))
        {
            SaveNewProductBtn?.Focus(FocusState.Programmatic);
        }
    }

    private void HandleAddProductFieldLeft(object sender)
    {
        if (ReferenceEquals(sender, SaveNewProductBtn))
        {
            CancelNewProductBtn?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, CancelNewProductBtn))
        {
            NewProductRxCheckBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductRxCheckBox))
        {
            NewProductExpiryBox?.Focus(FocusState.Programmatic);
            NewProductExpiryBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductExpiryBox))
        {
            NewProductBatchBox?.Focus(FocusState.Programmatic);
            NewProductBatchBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductBatchBox))
        {
            NewProductInitialStockQtyBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductInitialStockQtyBox))
        {
            NewProductBuyingPriceBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductBuyingPriceBox))
        {
            NewProductMrpBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductMrpBox))
        {
            NewProductTaxComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductTaxComboBox))
        {
            NewProductHsnBox?.Focus(FocusState.Programmatic);
            NewProductHsnBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductHsnBox))
        {
            FocusActivePackagingControl();
        }
        else if (ReferenceEquals(sender, NewProductPcsPerStripBox))
        {
            NewProductStripCountBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductStripCountBox) || ReferenceEquals(sender, NewProductVolumeMlComboBox) ||
                 ReferenceEquals(sender, NewProductWeightGmComboBox) || ReferenceEquals(sender, NewProductPackOptionsBox))
        {
            NewProductStockTypeComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductStockTypeComboBox))
        {
            NewProductManufacturerBox?.Focus(FocusState.Programmatic);
            NewProductManufacturerBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductManufacturerBox))
        {
            NewProductNameBox?.Focus(FocusState.Programmatic);
            NewProductNameBox?.SelectAll();
        }
    }

    private void HandleAddProductFieldNext(object sender)
    {
        if (ReferenceEquals(sender, NewProductNameBox))
        {
            NewProductManufacturerBox?.Focus(FocusState.Programmatic);
            NewProductManufacturerBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductManufacturerBox))
        {
            NewProductStockTypeComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductStockTypeComboBox))
        {
            FocusActivePackagingControl();
        }
        else if (ReferenceEquals(sender, NewProductStripCountBox))
        {
            NewProductPcsPerStripBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductPcsPerStripBox) || ReferenceEquals(sender, NewProductVolumeMlComboBox) ||
                 ReferenceEquals(sender, NewProductWeightGmComboBox) || ReferenceEquals(sender, NewProductPackOptionsBox))
        {
            NewProductHsnBox?.Focus(FocusState.Programmatic);
            NewProductHsnBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductHsnBox))
        {
            NewProductTaxComboBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductTaxComboBox))
        {
            NewProductMrpBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductMrpBox))
        {
            NewProductBuyingPriceBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductBuyingPriceBox))
        {
            NewProductInitialStockQtyBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductInitialStockQtyBox))
        {
            NewProductBatchBox?.Focus(FocusState.Programmatic);
            NewProductBatchBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductBatchBox))
        {
            NewProductExpiryBox?.Focus(FocusState.Programmatic);
            NewProductExpiryBox?.SelectAll();
        }
        else if (ReferenceEquals(sender, NewProductExpiryBox))
        {
            NewProductRxCheckBox?.Focus(FocusState.Programmatic);
        }
        else if (ReferenceEquals(sender, NewProductRxCheckBox))
        {
            SaveNewProductBtn?.Focus(FocusState.Programmatic);
        }
    }

    private void SaveSummaryModalBtn_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ReferenceEquals(sender, ConfirmSaveSummaryButton))
            {
                _ = ViewModel.ConfirmAndSavePurchaseAsync();
            }
            else
            {
                ViewModel.CancelSaveSummary();
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CancelSaveSummary();
            e.Handled = true;
        }
    }

    private void SaveSummaryModalBtn_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left)
        {
            if (ReferenceEquals(sender, ConfirmSaveSummaryButton))
            {
                CancelSaveSummaryButton?.Focus(FocusState.Programmatic);
            }
            else
            {
                ConfirmSaveSummaryButton?.Focus(FocusState.Programmatic);
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CancelSaveSummary();
            e.Handled = true;
        }
    }
}
