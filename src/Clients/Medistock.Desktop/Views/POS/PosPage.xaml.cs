using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Products.Queries;
using Medistock.Contracts.Printing;
using Medistock.Desktop.Commands;
using Medistock.Desktop.Controls;
using Medistock.Desktop.Services;
using Medistock.Desktop.ViewModels;
using Medistock.Desktop.Views.Sales;
using Medistock.Infrastructure.Hardware.Printers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Medistock.Desktop.Views.POS;

public sealed partial class PosPage : Page
{
    public PosViewModel ViewModel { get; }
    private readonly IShortcutService _shortcutService;

    private bool _isInitialized = false;

    public PosPage(PosViewModel viewModel, IShortcutService shortcutService)
    {
        ViewModel = viewModel;
        _shortcutService = shortcutService;
        this.InitializeComponent();

        // Attach global key down on the page root with handledEventsToo
        this.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(PosPage_KeyDown), true);

        this.ActualThemeChanged += (s, ev) =>
        {
            if (ViewModel.IsSaleTypePromptOpen)
            {
                UpdateSaleTypeVisuals(ViewModel.SelectedSaleTypeIndex);
            }
        };

        ViewModel.PropertyChanged += (s, ev) =>
        {
            if (ev.PropertyName == nameof(PosViewModel.IsSaleTypePromptOpen) && ViewModel.IsSaleTypePromptOpen)
            {
                HighlightSaleType(ViewModel.SelectedSaleTypeIndex);
            }
            else if (ev.PropertyName == nameof(PosViewModel.IsPrintPreviewOpen) && ViewModel.IsPrintPreviewOpen)
            {
                _ = LoadPosPreviewHtmlAsync();
                DispatcherQueue.TryEnqueue(() =>
                {
                    ConfirmPrintPreviewButton.Focus(FocusState.Programmatic);
                });
            }
            else if (ev.PropertyName == nameof(PosViewModel.IsPrintPromptOpen) && ViewModel.IsPrintPromptOpen)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    PrintReceiptButton.Focus(FocusState.Programmatic);
                });
            }
            else if (ev.PropertyName == nameof(PosViewModel.IsSaveConfirmationOpen) && ViewModel.IsSaveConfirmationOpen)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ConfirmSaveButton.Focus(FocusState.Programmatic);
                });
            }
            else if (ev.PropertyName == nameof(PosViewModel.IsCloseTabConfirmationOpen) && ViewModel.IsCloseTabConfirmationOpen)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    CancelCloseTabButton.Focus(FocusState.Programmatic);
                });
            }
            else if (ev.PropertyName == nameof(PosViewModel.IsBatchPickerOpen))
            {
                if (ViewModel.IsBatchPickerOpen)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        BatchPickerList?.Focus(FocusState.Programmatic);
                        if (BatchPickerList != null && ViewModel.SelectedBatchIndex >= 0 && ViewModel.SelectedBatchIndex < ViewModel.SelectedProductBatches.Count)
                        {
                            BatchPickerList.SelectedIndex = ViewModel.SelectedBatchIndex;
                        }
                    });
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SearchBox.Focus(FocusState.Programmatic);
                        SearchBox.SelectAll();
                    });
                }
            }
        };

        Loaded += PosPage_Loaded;
        Unloaded += PosPage_Unloaded;
    }

    private void PosPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            // Register actions to named commands in shortcut service
            _shortcutService.RegisterAction("pos.new_tab", () => ViewModel.AddNewTab());
            _shortcutService.RegisterAction("pos.close_tab", () => ViewModel.CloseTab(ViewModel.ActiveTab));
            _shortcutService.RegisterAction("pos.next_tab", () => ViewModel.NextTab());
            _shortcutService.RegisterAction("pos.prev_tab", () => ViewModel.PreviousTab());
            _shortcutService.RegisterAction("pos.new_sale", () => { ViewModel.ClearBill(); HighlightSaleType(0); });
            _shortcutService.RegisterAction("pos.payment", () => { SyncActiveDiscountBox(); ViewModel.OpenSaveConfirmation(); DispatcherQueue.TryEnqueue(() => ConfirmSaveButton.Focus(FocusState.Programmatic)); });
            _shortcutService.RegisterAction("pos.print", () => { _ = HandlePrintShortcutAsync(); });
            _shortcutService.RegisterAction("pos.clear_cart", () => { ViewModel.ClearBill(); HighlightSaleType(0); });
            _shortcutService.RegisterAction("app.help_shortcuts", () => ViewModel.ToggleShortcutHelp());
        }

        if (ViewModel.ActiveTab != null && !ViewModel.ActiveTab.CartItems.Any())
        {
            ViewModel.IsSaleTypePromptOpen = true;
            HighlightSaleType(0);
        }
        else
        {
            FocusHeaderStart();
        }

        TypographyService.ScaleElementTree(this, TypographyService.CurrentScale);
    }

    private void PosPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.HasSearchResults = false;
        if (SearchResultsPopup != null)
        {
            SearchResultsPopup.IsOpen = false;
        }
    }

    public void FocusHeaderStart()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            InvoiceDateBox.Focus(FocusState.Programmatic);
            InvoiceDateBox.SelectAll();
        });
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

    private void InvoiceDateBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CustomerBox.Focus(FocusState.Programmatic);
            CustomerBox.SelectAll();
            e.Handled = true;
        }
    }

    private void CustomerBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CustomerMobileBox.Focus(FocusState.Programmatic);
            CustomerMobileBox.SelectAll();
            e.Handled = true;
        }
    }

    private void CustomerMobileBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            DoctorBox.Focus(FocusState.Programmatic);
            DoctorBox.SelectAll();
            e.Handled = true;
        }
    }

    private void DoctorBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void PaymentModeBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void PosPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // If an inner control (e.g. NumberBox, TextBox, DiscountBox) already handled this key event,
        // do not let PosPage_KeyDown re-process it as a modal confirmation or navigation action in the same stroke!
        if (e.Handled)
        {
            return;
        }

        var isCtrl = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        var isAlt = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        var isShift = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

        if (ViewModel.IsSaleTypePromptOpen)
        {
            var current = ViewModel.SelectedSaleTypeIndex;

            if (e.Key == VirtualKey.Right)
            {
                var next = current switch
                {
                    0 => 3, // Cash -> Credit
                    1 => 2, // UPI -> Card
                    3 => 0, // Credit -> Cash
                    2 => 1, // Card -> UPI
                    _ => 0
                };
                HighlightSaleType(next);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Left)
            {
                var prev = current switch
                {
                    3 => 0, // Credit -> Cash
                    2 => 1, // Card -> UPI
                    0 => 3, // Cash -> Credit
                    1 => 2, // UPI -> Card
                    _ => 0
                };
                HighlightSaleType(prev);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Down)
            {
                var down = current switch
                {
                    0 => 1, // Cash -> UPI
                    3 => 2, // Credit -> Card
                    1 => 0, // UPI -> Cash
                    2 => 3, // Card -> Credit
                    _ => 0
                };
                HighlightSaleType(down);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Up)
            {
                var up = current switch
                {
                    1 => 0, // UPI -> Cash
                    2 => 3, // Card -> Credit
                    0 => 1, // Cash -> UPI
                    3 => 2, // Credit -> Card
                    _ => 0
                };
                HighlightSaleType(up);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Number1 || e.Key == VirtualKey.NumberPad1 || e.Key == VirtualKey.C)
            {
                ViewModel.SelectSaleType(0);
                FocusHeaderStart();
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Number2 || e.Key == VirtualKey.NumberPad2 || e.Key == VirtualKey.R)
            {
                ViewModel.SelectSaleType(3);
                FocusHeaderStart();
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Number3 || e.Key == VirtualKey.NumberPad3 || e.Key == VirtualKey.U)
            {
                ViewModel.SelectSaleType(1);
                FocusHeaderStart();
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Number4 || e.Key == VirtualKey.NumberPad4 || e.Key == VirtualKey.D)
            {
                ViewModel.SelectSaleType(2);
                FocusHeaderStart();
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Enter)
            {
                ViewModel.SelectSaleType(ViewModel.SelectedSaleTypeIndex);
                FocusHeaderStart();
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.IsSaleTypePromptOpen = false;
                FocusHeaderStart();
                e.Handled = true;
                return;
            }
        }

        if (ViewModel.IsCloseTabConfirmationOpen)
        {
            var focused = FocusManager.GetFocusedElement(this.XamlRoot);
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
            {
                if (ReferenceEquals(focused, CancelCloseTabButton))
                {
                    ConfirmCloseTabButton.Focus(FocusState.Programmatic);
                }
                else
                {
                    CancelCloseTabButton.Focus(FocusState.Programmatic);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Enter)
            {
                if (ReferenceEquals(focused, ConfirmCloseTabButton))
                {
                    ViewModel.ConfirmCloseTab();
                }
                else
                {
                    ViewModel.CancelCloseTab();
                }
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.CancelCloseTab();
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }
        }

        if (ViewModel.IsSaveConfirmationOpen)
        {
            var focused = FocusManager.GetFocusedElement(this.XamlRoot);
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
            {
                if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Down)
                {
                    if (ReferenceEquals(focused, CancelSaveButton))
                    {
                        SaveAndPrintButton.Focus(FocusState.Programmatic);
                    }
                    else if (ReferenceEquals(focused, SaveAndPrintButton))
                    {
                        ConfirmSaveButton.Focus(FocusState.Programmatic);
                    }
                    else
                    {
                        CancelSaveButton.Focus(FocusState.Programmatic);
                    }
                }
                else
                {
                    if (ReferenceEquals(focused, ConfirmSaveButton))
                    {
                        SaveAndPrintButton.Focus(FocusState.Programmatic);
                    }
                    else if (ReferenceEquals(focused, SaveAndPrintButton))
                    {
                        CancelSaveButton.Focus(FocusState.Programmatic);
                    }
                    else
                    {
                        ConfirmSaveButton.Focus(FocusState.Programmatic);
                    }
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Enter)
            {
                if (ReferenceEquals(focused, CancelSaveButton))
                {
                    ViewModel.CloseSaveConfirmation();
                    FocusBottomBarDiscount();
                }
                else if (ReferenceEquals(focused, SaveAndPrintButton))
                {
                    ViewModel.SaveAndOpenPrintPreview();
                    DispatcherQueue.TryEnqueue(() => ConfirmPrintPreviewButton.Focus(FocusState.Programmatic));
                }
                else
                {
                    _ = SaveAndNextInvoiceAsync();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.CloseSaveConfirmation();
                FocusBottomBarDiscount();
                e.Handled = true;
                return;
            }
        }

        if (ViewModel.IsPrintPreviewOpen)
        {
            var focused = FocusManager.GetFocusedElement(this.XamlRoot);
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
            {
                if (ReferenceEquals(focused, ConfirmPrintPreviewButton))
                {
                    ClosePrintPreviewButton.Focus(FocusState.Programmatic);
                }
                else
                {
                    ConfirmPrintPreviewButton.Focus(FocusState.Programmatic);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Enter)
            {
                if (ReferenceEquals(focused, ClosePrintPreviewButton))
                {
                    ViewModel.ClosePrintPreview();
                    SearchBox.Focus(FocusState.Programmatic);
                }
                else
                {
                    _ = ConfirmPrintPreviewAsync();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.ClosePrintPreview();
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }
        }

        if (ViewModel.IsPrintPromptOpen)
        {
            var focused = FocusManager.GetFocusedElement(this.XamlRoot);
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
            {
                if (ReferenceEquals(focused, PrintReceiptButton))
                {
                    SkipPrintButton.Focus(FocusState.Programmatic);
                }
                else
                {
                    PrintReceiptButton.Focus(FocusState.Programmatic);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Enter)
            {
                if (ReferenceEquals(focused, SkipPrintButton))
                {
                    ViewModel.SkipPrint();
                    FocusHeaderStart();
                }
                else
                {
                    _ = ViewModel.PrintReceiptAsync();
                    FocusHeaderStart();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.SkipPrint();
                FocusHeaderStart();
                e.Handled = true;
                return;
            }
        }

        // --- Tab Navigation Hotkeys (Chrome & ERP style: F8/F9, Ctrl+Tab/Ctrl+Shift+Tab, Ctrl+T, Ctrl+W/Ctrl+F4, Alt+1..9) ---
        if (e.Key == VirtualKey.F8 || (isCtrl && isShift && e.Key == VirtualKey.Tab) || (isCtrl && e.Key == VirtualKey.PageUp))
        {
            ViewModel.PreviousTab();
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.F9 || (isCtrl && !isShift && e.Key == VirtualKey.Tab) || (isCtrl && e.Key == VirtualKey.PageDown))
        {
            ViewModel.NextTab();
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }

        if (isCtrl && e.Key == VirtualKey.T)
        {
            ViewModel.NewTab();
            FocusHeaderStart();
            e.Handled = true;
            return;
        }

        if ((isCtrl && e.Key == VirtualKey.W) || (isCtrl && e.Key == VirtualKey.F4))
        {
            ViewModel.RequestCloseTab(ViewModel.ActiveTab);
            e.Handled = true;
            return;
        }

        if (isAlt && !isCtrl && !isShift && e.Key >= VirtualKey.Number1 && e.Key <= VirtualKey.Number9)
        {
            int tabIndex = (int)e.Key - (int)VirtualKey.Number1;
            if (tabIndex >= 0 && tabIndex < ViewModel.InvoiceTabs.Count)
            {
                ViewModel.ActiveTabIndex = tabIndex;
                ViewModel.ActiveTab = ViewModel.InvoiceTabs[tabIndex];
                ViewModel.StatusMessage = $"Switched to {ViewModel.ActiveTab.TabTitle}";
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.F1)
        {
            ViewModel.ToggleShortcutHelp();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.F2)
        {
            if (ViewModel.IsCreateProductModalOpen)
            {
                _ = ViewModel.SaveCreateProductAsync();
            }
            else
            {
                ViewModel.OpenCreateProductModal();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.F3)
        {
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }

        if ((isCtrl && e.Key == VirtualKey.P) || e.Key == VirtualKey.F7)
        {
            if (ViewModel.ActiveTab != null && ViewModel.ActiveTab.CartItems.Any())
            {
                ViewModel.OpenPrintPreview();
                DispatcherQueue.TryEnqueue(() => ConfirmPrintPreviewButton.Focus(FocusState.Programmatic));
            }
            else
            {
                _ = HandlePrintShortcutAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.F6 || (isCtrl && e.Key == VirtualKey.S) || e.Key == VirtualKey.End)
        {
            ViewModel.OpenSaveConfirmation();
            DispatcherQueue.TryEnqueue(() =>
            {
                ConfirmSaveButton.Focus(FocusState.Programmatic);
            });
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            if (ViewModel.IsPrintPromptOpen)
            {
                ViewModel.SkipPrint();
                FocusHeaderStart();
                e.Handled = true;
                return;
            }

            if (ViewModel.IsCloseTabConfirmationOpen)
            {
                ViewModel.CancelCloseTab();
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (ViewModel.IsSaveConfirmationOpen)
            {
                ViewModel.CloseSaveConfirmation();
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (ViewModel.IsCreateProductModalOpen)
            {
                ViewModel.CloseCreateProductModal();
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (ViewModel.IsBatchPickerOpen)
            {
                ViewModel.CloseBatchPicker();
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (ViewModel.HasPendingItem)
            {
                ViewModel.CancelPendingLine();
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (ViewModel.IsShortcutHelpOpen)
            {
                ViewModel.CloseShortcutHelp();
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }
        }

        if (_shortcutService.TryExecuteShortcut(e.Key, isCtrl, isAlt, isShift, "POS"))
        {
            e.Handled = true;
            return;
        }

        if (!isCtrl && !isAlt && e.Key == VirtualKey.Back)
        {
            var focused = FocusManager.GetFocusedElement(this.XamlRoot);
            if (!ReferenceEquals(focused, SearchBox) && !(focused is TextBox) && !IsInsideNumberBox(focused) && !IsInsideComboBox(focused))
            {
                if (!ViewModel.IsCreateProductModalOpen && !ViewModel.IsSaveConfirmationOpen && !ViewModel.IsCloseTabConfirmationOpen && !ViewModel.IsShortcutHelpOpen && !ViewModel.IsBatchPickerOpen && !ViewModel.IsPrintPromptOpen && !ViewModel.IsSaleTypePromptOpen && !ViewModel.IsPrintPreviewOpen)
                {
                    SearchBox.Focus(FocusState.Programmatic);
                    if (!string.IsNullOrEmpty(SearchBox.Text))
                    {
                        SearchBox.Text = SearchBox.Text.Substring(0, SearchBox.Text.Length - 1);
                        SearchBox.SelectionStart = SearchBox.Text.Length;
                        SearchBox.SelectionLength = 0;
                    }
                    e.Handled = true;
                    return;
                }
            }
        }

        // Alphanumeric auto-focus routing to SearchBox
        if (!isCtrl && !isAlt && TryGetCharacterFromKey(e.Key, isShift, out char ch))
        {
            var focused = FocusManager.GetFocusedElement(this.XamlRoot);

            // If already inside SearchBox, allow default TextBox handling
            if (ReferenceEquals(focused, SearchBox))
            {
                return;
            }

            // If inside another active text entry box (Customer, Doctor, Date, or modal inputs), don't hijack
            if (focused is TextBox && !ReferenceEquals(focused, SearchBox))
            {
                return;
            }

            // If focused on or inside ComboBox (PaymentMode), don't hijack
            if (focused is ComboBox || IsInsideComboBox(focused))
            {
                return;
            }

            // If a modal is open, don't hijack
            if (ViewModel.IsCreateProductModalOpen || ViewModel.IsSaveConfirmationOpen || ViewModel.IsCloseTabConfirmationOpen || ViewModel.IsShortcutHelpOpen || ViewModel.IsBatchPickerOpen || ViewModel.IsPrintPromptOpen || ViewModel.IsSaleTypePromptOpen || ViewModel.IsPrintPreviewOpen)
            {
                return;
            }

            // If focused on or inside any NumberBox (e.g. QuantityInputBox, RowQtyBox) and typing a digit/decimal, let NumberBox handle it
            if (IsInsideNumberBox(focused) && (char.IsDigit(ch) || ch == '.'))
            {
                return;
            }

            if (ViewModel.HasPendingItem)
            {
                ViewModel.CancelPendingLine();
            }

            // Transfer focus and typed character to SearchBox
            SearchBox.Focus(FocusState.Programmatic);
            if (string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = ch.ToString();
            }
            else
            {
                SearchBox.Text += ch.ToString();
            }
            SearchBox.SelectionStart = SearchBox.Text.Length;
            SearchBox.SelectionLength = 0;
            e.Handled = true;
        }
    }

    private void PageBackground_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsSaleTypePromptOpen || ViewModel.IsSaveConfirmationOpen || ViewModel.IsCloseTabConfirmationOpen || ViewModel.IsPrintPreviewOpen || ViewModel.IsPrintPromptOpen || ViewModel.IsShortcutHelpOpen || ViewModel.IsBatchPickerOpen || ViewModel.IsCreateProductModalOpen)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject dep)
        {
            if (FindParent<TextBox>(dep) != null ||
                FindParent<Button>(dep) != null ||
                FindParent<NumberBox>(dep) != null ||
                FindParent<ComboBox>(dep) != null ||
                FindParent<ListViewItem>(dep) != null ||
                FindParent<CalendarDatePicker>(dep) != null)
            {
                return;
            }
        }

        // Clicking anywhere on whitespace / background automatically focuses SearchBox
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static bool IsInsideNumberBox(object? focused)
    {
        if (focused is NumberBox) return true;
        if (focused is DependencyObject dep)
        {
            var current = dep;
            while (current != null)
            {
                if (current is NumberBox) return true;
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return false;
    }

    private static bool IsInsideComboBox(object? focused)
    {
        if (focused is ComboBox) return true;
        if (focused is DependencyObject dep)
        {
            var current = dep;
            while (current != null)
            {
                if (current is ComboBox) return true;
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return false;
    }

    private static bool TryGetCharacterFromKey(VirtualKey key, bool isShift, out char ch)
    {
        ch = '\0';
        if (key >= VirtualKey.A && key <= VirtualKey.Z)
        {
            char baseChar = (char)('a' + (key - VirtualKey.A));
            ch = isShift ? char.ToUpperInvariant(baseChar) : baseChar;
            return true;
        }

        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
        {
            if (isShift)
            {
                char[] shiftSymbols = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
                ch = shiftSymbols[key - VirtualKey.Number0];
            }
            else
            {
                ch = (char)('0' + (key - VirtualKey.Number0));
            }
            return true;
        }

        if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
        {
            ch = (char)('0' + (key - VirtualKey.NumberPad0));
            return true;
        }

        if (key == VirtualKey.Space)
        {
            ch = ' ';
            return true;
        }

        return false;
    }

    private void SearchResults_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ProductSearchItemViewModel product)
        {
            var singleBatch = ViewModel.PrepareLineItem(product);
            SearchBox.Text = string.Empty;
            if (singleBatch)
            {
                ViewModel.CommitCurrentLine();
                DispatcherQueue.TryEnqueue(() =>
                {
                    SearchBox.Focus(FocusState.Programmatic);
                    SearchBox.SelectAll();
                });
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    BatchPickerList.Focus(FocusState.Programmatic);
                    if (ViewModel.SelectedBatchIndex >= 0 && ViewModel.SelectedBatchIndex < ViewModel.SelectedProductBatches.Count)
                    {
                        BatchPickerList.SelectedIndex = ViewModel.SelectedBatchIndex;
                    }
                });
            }
        }
    }

    private void BatchPickerList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ProductBatchDto batch)
        {
            ViewModel.SelectBatch(batch);
            ViewModel.CommitCurrentLine();
            SearchBox.Text = string.Empty;
            DispatcherQueue.TryEnqueue(() =>
            {
                SearchBox.Focus(FocusState.Programmatic);
                SearchBox.SelectAll();
            });
        }
    }

    private void BatchPickerList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ViewModel.SelectedBatchIndex >= 0 && ViewModel.SelectedBatchIndex < ViewModel.SelectedProductBatches.Count)
            {
                ViewModel.SelectBatch(ViewModel.SelectedProductBatches[ViewModel.SelectedBatchIndex]);
                ViewModel.CommitCurrentLine();
                SearchBox.Text = string.Empty;
                DispatcherQueue.TryEnqueue(() =>
                {
                    SearchBox.Focus(FocusState.Programmatic);
                    SearchBox.SelectAll();
                });
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CloseBatchPicker();
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void BatchPickerConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedBatchIndex >= 0 && ViewModel.SelectedBatchIndex < ViewModel.SelectedProductBatches.Count)
        {
            ViewModel.SelectBatch(ViewModel.SelectedProductBatches[ViewModel.SelectedBatchIndex]);
            ViewModel.CommitCurrentLine();
            SearchBox.Text = string.Empty;
            DispatcherQueue.TryEnqueue(() =>
            {
                SearchBox.Focus(FocusState.Programmatic);
                SearchBox.SelectAll();
            });
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
        var isCtrl = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

        if (e.Key == VirtualKey.F2)
        {
            ViewModel.OpenCreateProductModal();
            e.Handled = true;
            return;
        }

        if (isCtrl && (e.Key == VirtualKey.W || e.Key == VirtualKey.S))
        {
            ViewModel.OpenSaveConfirmation();
            DispatcherQueue.TryEnqueue(() =>
            {
                ConfirmSaveButton.Focus(FocusState.Programmatic);
            });
            e.Handled = true;
            return;
        }

        if (ViewModel.IsBatchPickerOpen)
        {
            if (e.Key == VirtualKey.Down)
            {
                ViewModel.MoveBatchSelectionDown();
                if (BatchPickerList.SelectedItem != null)
                {
                    BatchPickerList.ScrollIntoView(BatchPickerList.SelectedItem);
                }
                e.Handled = true;
                return;
            }
            else if (e.Key == VirtualKey.Up)
            {
                ViewModel.MoveBatchSelectionUp();
                if (BatchPickerList.SelectedItem != null)
                {
                    BatchPickerList.ScrollIntoView(BatchPickerList.SelectedItem);
                }
                e.Handled = true;
                return;
            }
            else if (e.Key == VirtualKey.Enter)
            {
                if (ViewModel.SelectedBatchIndex >= 0 && ViewModel.SelectedBatchIndex < ViewModel.SelectedProductBatches.Count)
                {
                    ViewModel.SelectBatch(ViewModel.SelectedProductBatches[ViewModel.SelectedBatchIndex]);
                    ViewModel.CommitCurrentLine();
                    SearchBox.Text = string.Empty;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SearchBox.Focus(FocusState.Programmatic);
                        SearchBox.SelectAll();
                    });
                }
                e.Handled = true;
                return;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                ViewModel.CloseBatchPicker();
                SearchBox.Focus(FocusState.Programmatic);
                SearchBox.SelectAll();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.Down)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text) && !ViewModel.SearchResults.Any())
            {
                if (ViewModel.ActiveTab != null && ViewModel.ActiveTab.CartItems.Count > 0)
                {
                    ViewModel.ActiveTab.SelectedCartIndex = 0;
                    FocusActiveCartRowStrip();
                    e.Handled = true;
                    return;
                }
            }

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
            if (string.IsNullOrWhiteSpace(SearchBox.Text) && ViewModel.ActiveTab != null && ViewModel.ActiveTab.CartItems.Count > 0)
            {
                if (ViewModel.ActiveTab.SelectedCartIndex < 0 || ViewModel.ActiveTab.SelectedCartIndex >= ViewModel.ActiveTab.CartItems.Count)
                {
                    ViewModel.ActiveTab.SelectedCartIndex = 0;
                }
                FocusActiveCartRowStrip();
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Enter)
        {
            var text = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                // MARG ERP Rule: Enter on a blank item search row focuses Bottom Bar Discount inputs
                if (ViewModel.ActiveTab != null && ViewModel.ActiveTab.CartItems.Any())
                {
                    FocusBottomBarDiscount();
                }
                else
                {
                    ViewModel.StatusMessage = "Cart is empty. Scan barcode or type medicine name to begin.";
                }
                e.Handled = true;
                return;
            }

            if (text.Length >= 8 && long.TryParse(text, out _))
            {
                await ViewModel.ProcessBarcodeScanAsync(text);
                SearchBox.Text = string.Empty;
                DispatcherQueue.TryEnqueue(() =>
                {
                    SearchBox.Focus(FocusState.Programmatic);
                    SearchBox.SelectAll();
                });
            }
            else if (ViewModel.SelectedSearchIndex >= 0 && ViewModel.SelectedSearchIndex < ViewModel.SearchResults.Count)
            {
                var selected = ViewModel.SearchResults[ViewModel.SelectedSearchIndex];
                var singleBatch = ViewModel.PrepareLineItem(selected);
                SearchBox.Text = string.Empty;
                if (singleBatch)
                {
                    ViewModel.CommitCurrentLine();
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SearchBox.Focus(FocusState.Programmatic);
                        SearchBox.SelectAll();
                    });
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        BatchPickerList.Focus(FocusState.Programmatic);
                        if (ViewModel.SelectedBatchIndex >= 0 && ViewModel.SelectedBatchIndex < ViewModel.SelectedProductBatches.Count)
                        {
                            BatchPickerList.SelectedIndex = ViewModel.SelectedBatchIndex;
                        }
                    });
                }
            }
            else if (ViewModel.SearchResults.Count > 0)
            {
                var selected = (ViewModel.SelectedSearchIndex >= 0 && ViewModel.SelectedSearchIndex < ViewModel.SearchResults.Count)
                    ? ViewModel.SearchResults[ViewModel.SelectedSearchIndex]
                    : ViewModel.SearchResults[0];

                var singleBatch = ViewModel.PrepareLineItem(selected);
                SearchBox.Text = string.Empty;
                if (singleBatch)
                {
                    ViewModel.CommitCurrentLine();
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SearchBox.Focus(FocusState.Programmatic);
                        SearchBox.SelectAll();
                    });
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        BatchPickerList.Focus(FocusState.Programmatic);
                        if (ViewModel.SelectedBatchIndex >= 0 && ViewModel.SelectedBatchIndex < ViewModel.SelectedProductBatches.Count)
                        {
                            BatchPickerList.SelectedIndex = ViewModel.SelectedBatchIndex;
                        }
                    });
                }
            }
            else
            {
                // If no search matches, open F2 create product with the query pre-filled
                ViewModel.OpenCreateProductModal();
            }
            e.Handled = true;
        }
    }

    private void FocusActiveCartRowStrip()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var idx = ViewModel.ActiveTab?.SelectedCartIndex ?? (ViewModel.ActiveTab?.CartItems.Count - 1 ?? -1);
            if (idx >= 0 && CartListView.ContainerFromIndex(idx) is ListViewItem container)
            {
                var stripBox = FindVisualChild<NumberBox>(container, nb => Grid.GetColumn(nb) == 5);
                stripBox?.Focus(FocusState.Programmatic);
            }
        });
    }

    private void HideDeleteButton(FrameworkElement element)
    {
        var btn = FindVisualChild<Button>(element, b => b.Name == "DeleteButton");
        if (btn != null)
        {
            btn.Visibility = Visibility.Collapsed;
            btn.Width = 0;
            btn.Height = 0;
            btn.MinWidth = 0;
            btn.MinHeight = 0;
            btn.MaxWidth = 0;
            btn.MaxHeight = 0;
            btn.Opacity = 0;
            btn.IsHitTestVisible = false;
        }
    }

    private void CartCell_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox directTb)
        {
            HideDeleteButton(directTb);
        }
        else if (sender is NumberBox nb)
        {
            var blueBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 13, 110, 253));
            var whiteBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            nb.Background = blueBrush;
            nb.Foreground = whiteBrush;
            nb.BorderBrush = blueBrush;

            HideDeleteButton(nb);

            var tb = FindVisualChild<TextBox>(nb);
            if (tb != null)
            {
                tb.Background = blueBrush;
                tb.Foreground = whiteBrush;
                tb.BorderBrush = blueBrush;
                tb.SelectionHighlightColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 10, 88, 202));
                tb.SelectAll();

                HideDeleteButton(tb);
            }
        }
    }

    private void CartCell_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is NumberBox nb)
        {
            nb.ClearValue(Control.BackgroundProperty);
            nb.ClearValue(Control.ForegroundProperty);
            nb.ClearValue(Control.BorderBrushProperty);

            var tb = FindVisualChild<TextBox>(nb);
            if (tb != null)
            {
                tb.ClearValue(Control.BackgroundProperty);
                tb.ClearValue(Control.ForegroundProperty);
                tb.ClearValue(Control.BorderBrushProperty);
                tb.ClearValue(TextBox.SelectionHighlightColorProperty);
            }
        }
    }

    private void NavigateCartRow(int direction, int targetColumn)
    {
        if (ViewModel.ActiveTab == null || ViewModel.ActiveTab.CartItems.Count == 0) return;

        var current = ViewModel.ActiveTab.SelectedCartIndex;
        var target = current + direction;
        if (target < 0)
        {
            // Up arrow from row 0 jumps focus to SearchBox prominently
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            return;
        }

        if (target >= ViewModel.ActiveTab.CartItems.Count)
        {
            // Down arrow past last row focuses bottom bar discount
            FocusBottomBarDiscount();
            return;
        }

        ViewModel.ActiveTab.SelectedCartIndex = target;
        CartListView.ScrollIntoView(ViewModel.ActiveTab.CartItems[target]);
        DispatcherQueue.TryEnqueue(() =>
        {
            CartListView.UpdateLayout();
            if (CartListView.ContainerFromIndex(target) is ListViewItem container)
            {
                var box = FindVisualChild<NumberBox>(container, nb => Grid.GetColumn(nb) == targetColumn);
                box?.Focus(FocusState.Programmatic);
            }
        });
    }

    private void CartRow_Cell_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Up)
        {
            if (sender is FrameworkElement fe)
            {
                NavigateCartRow(-1, Grid.GetColumn(fe));
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down)
        {
            if (sender is FrameworkElement fe)
            {
                NavigateCartRow(1, Grid.GetColumn(fe));
            }
            e.Handled = true;
        }
    }

    private void BottomBar_DiscountBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Up)
        {
            if (ViewModel.ActiveTab != null && ViewModel.ActiveTab.CartItems.Count > 0)
            {
                ViewModel.ActiveTab.SelectedCartIndex = ViewModel.ActiveTab.CartItems.Count - 1;
                FocusActiveCartRowStrip();
            }
            else
            {
                SearchBox.Focus(FocusState.Programmatic);
                SearchBox.SelectAll();
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down)
        {
            e.Handled = true;
        }
    }

    private void CartRow_Strip_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.E)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Up)
        {
            NavigateCartRow(-1, 5);
            e.Handled = true;
            return;
        }
        else if (e.Key == VirtualKey.Down)
        {
            NavigateCartRow(1, 5);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Right)
        {
            if (sender is FrameworkElement fe && FindParentGrid(fe) is Grid rowGrid)
            {
                var tabBox = rowGrid.Children.OfType<NumberBox>().FirstOrDefault(nb => Grid.GetColumn(nb) == 6);
                tabBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Left)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void CartRow_Tab_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.E)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Up)
        {
            NavigateCartRow(-1, 6);
            e.Handled = true;
            return;
        }
        else if (e.Key == VirtualKey.Down)
        {
            NavigateCartRow(1, 6);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Right)
        {
            if (sender is FrameworkElement fe && FindParentGrid(fe) is Grid rowGrid)
            {
                var freeBox = rowGrid.Children.OfType<NumberBox>().FirstOrDefault(nb => Grid.GetColumn(nb) == 7);
                freeBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Left)
        {
            if (sender is FrameworkElement fe && FindParentGrid(fe) is Grid rowGrid)
            {
                var stripBox = rowGrid.Children.OfType<NumberBox>().FirstOrDefault(nb => Grid.GetColumn(nb) == 5);
                stripBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape)
        {
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void CartRow_Free_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.E)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Up)
        {
            NavigateCartRow(-1, 7);
            e.Handled = true;
            return;
        }
        else if (e.Key == VirtualKey.Down)
        {
            NavigateCartRow(1, 7);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Right)
        {
            if (sender is FrameworkElement fe && FindParentGrid(fe) is Grid rowGrid)
            {
                var rateBox = rowGrid.Children.OfType<NumberBox>().FirstOrDefault(nb => Grid.GetColumn(nb) == 9);
                rateBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Left)
        {
            if (sender is FrameworkElement fe && FindParentGrid(fe) is Grid rowGrid)
            {
                var tabBox = rowGrid.Children.OfType<NumberBox>().FirstOrDefault(nb => Grid.GetColumn(nb) == 6);
                tabBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape)
        {
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void CartRow_Rate_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.E)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Up)
        {
            NavigateCartRow(-1, 9);
            e.Handled = true;
            return;
        }
        else if (e.Key == VirtualKey.Down)
        {
            NavigateCartRow(1, 9);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Right)
        {
            if (sender is FrameworkElement fe && FindParentGrid(fe) is Grid rowGrid)
            {
                var discBox = rowGrid.Children.OfType<NumberBox>().FirstOrDefault(nb => Grid.GetColumn(nb) == 11);
                discBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Left)
        {
            if (sender is FrameworkElement fe && FindParentGrid(fe) is Grid rowGrid)
            {
                var freeBox = rowGrid.Children.OfType<NumberBox>().FirstOrDefault(nb => Grid.GetColumn(nb) == 7);
                freeBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape)
        {
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void CartRow_Disc_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.E)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Up)
        {
            NavigateCartRow(-1, 11);
            e.Handled = true;
            return;
        }
        else if (e.Key == VirtualKey.Down)
        {
            NavigateCartRow(1, 11);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Right)
        {
            if (sender is NumberBox rowNb)
            {
                var rowTb = FindVisualChild<TextBox>(rowNb);
                if (rowTb != null && !string.IsNullOrWhiteSpace(rowTb.Text) && double.TryParse(rowTb.Text, out var parsedDisc))
                {
                    if (rowNb.DataContext is CartItemViewModel itemVm)
                    {
                        itemVm.DiscountPercent = (decimal)Math.Clamp(parsedDisc, 0, 100);
                    }
                }
                else if (!double.IsNaN(rowNb.Value) && rowNb.DataContext is CartItemViewModel itemVm)
                {
                    itemVm.DiscountPercent = (decimal)Math.Clamp(rowNb.Value, 0, 100);
                }
            }

            ViewModel.ActiveTab?.RecalculateTotals();
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Left)
        {
            if (sender is FrameworkElement fe && FindParentGrid(fe) is Grid rowGrid)
            {
                var rateBox = rowGrid.Children.OfType<NumberBox>().FirstOrDefault(nb => Grid.GetColumn(nb) == 9);
                rateBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private static Grid? FindParentGrid(FrameworkElement element)
    {
        DependencyObject current = element;
        while (current != null)
        {
            if (current is Grid g) return g;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && (predicate == null || predicate(element)))
            {
                return element;
            }
            var sub = FindVisualChild<T>(child, predicate);
            if (sub != null) return sub;
        }
        return null;
    }

    private void SyncActiveDiscountBox()
    {
        try
        {
            if (ViewModel.ActiveTab == null) return;

            if (BottomBar_DiscountPercentBox != null)
            {
                var percentTb = FindVisualChild<TextBox>(BottomBar_DiscountPercentBox);
                if (percentTb != null && !string.IsNullOrWhiteSpace(percentTb.Text) && double.TryParse(percentTb.Text, out var pVal))
                {
                    ViewModel.ActiveTab.BillDiscountPercent = (decimal)Math.Clamp(pVal, 0, 100);
                }
                else if (!double.IsNaN(BottomBar_DiscountPercentBox.Value) && BottomBar_DiscountPercentBox.Value >= 0)
                {
                    ViewModel.ActiveTab.BillDiscountPercent = (decimal)Math.Clamp(BottomBar_DiscountPercentBox.Value, 0, 100);
                }
            }

            if (BottomBar_DiscountFlatBox != null)
            {
                var flatTb = FindVisualChild<TextBox>(BottomBar_DiscountFlatBox);
                if (flatTb != null && !string.IsNullOrWhiteSpace(flatTb.Text) && double.TryParse(flatTb.Text, out var fVal))
                {
                    if (ViewModel.ActiveTab.BillDiscountPercent == 0 && fVal >= 0)
                    {
                        ViewModel.ActiveTab.BillDiscountAmount = (decimal)Math.Max(0, fVal);
                    }
                }
                else if (!double.IsNaN(BottomBar_DiscountFlatBox.Value) && BottomBar_DiscountFlatBox.Value >= 0 && ViewModel.ActiveTab.BillDiscountPercent == 0)
                {
                    ViewModel.ActiveTab.BillDiscountAmount = (decimal)Math.Max(0, BottomBar_DiscountFlatBox.Value);
                }
            }

            ViewModel.ActiveTab.RecalculateTotals();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POS] SyncActiveDiscountBox error: {ex.Message}");
        }
    }

    private void BottomBar_DiscountPercentBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (ViewModel.ActiveTab != null && !double.IsNaN(args.NewValue) && args.NewValue >= 0)
        {
            ViewModel.ActiveTab.BillDiscountPercent = (decimal)Math.Clamp(args.NewValue, 0, 100);
            ViewModel.ActiveTab.RecalculateTotals();
        }
    }

    private void BottomBar_DiscountFlatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (ViewModel.ActiveTab != null && !double.IsNaN(args.NewValue) && args.NewValue >= 0)
        {
            ViewModel.ActiveTab.BillDiscountAmount = (decimal)Math.Max(0, args.NewValue);
            ViewModel.ActiveTab.RecalculateTotals();
        }
    }

    private void FocusBottomBarDiscount()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            BottomBar_DiscountPercentBox.Focus(FocusState.Programmatic);
            var tb = FindVisualChild<TextBox>(BottomBar_DiscountPercentBox);
            tb?.SelectAll();
        });
    }

    private void BottomBar_DiscountBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            SyncActiveDiscountBox();
            DispatcherQueue.TryEnqueue(() =>
            {
                BottomBar_PayAndPrintButton.Focus(FocusState.Programmatic);
            });
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape || e.Key == VirtualKey.Up)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right && ReferenceEquals(sender, BottomBar_DiscountPercentBox))
        {
            BottomBar_DiscountFlatBox.Focus(FocusState.Programmatic);
            var tb = FindVisualChild<TextBox>(BottomBar_DiscountFlatBox);
            tb?.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Left && ReferenceEquals(sender, BottomBar_DiscountFlatBox))
        {
            BottomBar_DiscountPercentBox.Focus(FocusState.Programmatic);
            var tb = FindVisualChild<TextBox>(BottomBar_DiscountPercentBox);
            tb?.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Left && ReferenceEquals(sender, BottomBar_DiscountPercentBox))
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void CancelSaveButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseSaveConfirmation();
        FocusBottomBarDiscount();
    }

    private void ConfirmSaveButton_Click(object sender, RoutedEventArgs e)
    {
        _ = SaveAndNextInvoiceAsync();
    }

    private void SaveAndPrintButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveAndOpenPrintPreview();
        DispatcherQueue.TryEnqueue(() => ConfirmPrintPreviewButton.Focus(FocusState.Programmatic));
    }

    private async Task SaveAndNextInvoiceAsync()
    {
        try
        {
            await ViewModel.SaveAndNextInvoiceAsync();
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void SaveConfirmationBtn_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ReferenceEquals(sender, CancelSaveButton))
            {
                ViewModel.CloseSaveConfirmation();
                FocusBottomBarDiscount();
            }
            else if (ReferenceEquals(sender, SaveAndPrintButton))
            {
                ViewModel.SaveAndOpenPrintPreview();
                DispatcherQueue.TryEnqueue(() => ConfirmPrintPreviewButton.Focus(FocusState.Programmatic));
            }
            else
            {
                _ = SaveAndNextInvoiceAsync();
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CloseSaveConfirmation();
            FocusBottomBarDiscount();
            e.Handled = true;
        }
    }

    private void SaveConfirmationBtn_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
        {
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Down)
            {
                if (ReferenceEquals(sender, CancelSaveButton))
                {
                    SaveAndPrintButton.Focus(FocusState.Programmatic);
                }
                else if (ReferenceEquals(sender, SaveAndPrintButton))
                {
                    ConfirmSaveButton.Focus(FocusState.Programmatic);
                }
                else
                {
                    CancelSaveButton.Focus(FocusState.Programmatic);
                }
            }
            else if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Up)
            {
                if (ReferenceEquals(sender, ConfirmSaveButton))
                {
                    SaveAndPrintButton.Focus(FocusState.Programmatic);
                }
                else if (ReferenceEquals(sender, SaveAndPrintButton))
                {
                    CancelSaveButton.Focus(FocusState.Programmatic);
                }
                else
                {
                    ConfirmSaveButton.Focus(FocusState.Programmatic);
                }
            }
            e.Handled = true;
        }
    }

    private Brush GetThemeBrush(string key)
    {
        var isLight = this.ActualTheme == ElementTheme.Light;
        var themeDictName = isLight ? "Light" : "Dark";

        if (Microsoft.UI.Xaml.Application.Current.Resources.ThemeDictionaries.TryGetValue(themeDictName, out var dictObj) &&
            dictObj is ResourceDictionary themeDict &&
            themeDict.TryGetValue(key, out var val) &&
            val is Brush themeBrush)
        {
            return themeBrush;
        }

        if (this.Resources.TryGetValue(key, out var pageVal) && pageVal is Brush pageBrush)
        {
            return pageBrush;
        }

        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var rootVal) && rootVal is Brush rootBrush)
        {
            return rootBrush;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void UpdateSaleTypeVisuals(int index)
    {
        var accentBrush = GetThemeBrush("AccentPrimary");
        var defaultBorder = GetThemeBrush("BorderDefault");
        var subtleAccentBg = GetThemeBrush("AccentSubtle");
        var defaultBg = GetThemeBrush("SurfaceSubtle");
        var elevatedBg = GetThemeBrush("SurfaceElevated");

        if (SaleTypeCashBtn != null)
        {
            SaleTypeCashBtn.BorderBrush = index == 0 ? accentBrush : defaultBorder;
            SaleTypeCashBtn.BorderThickness = new Thickness(index == 0 ? 2.5 : 1.5);
            SaleTypeCashBtn.Background = index == 0 ? subtleAccentBg : defaultBg;
            if (SaleTypeCashBadge != null)
            {
                SaleTypeCashBadge.Background = index == 0 ? subtleAccentBg : elevatedBg;
            }
        }

        if (SaleTypeCreditBtn != null)
        {
            SaleTypeCreditBtn.BorderBrush = index == 3 ? accentBrush : defaultBorder;
            SaleTypeCreditBtn.BorderThickness = new Thickness(index == 3 ? 2.5 : 1.5);
            SaleTypeCreditBtn.Background = index == 3 ? subtleAccentBg : defaultBg;
            if (SaleTypeCreditBadge != null)
            {
                SaleTypeCreditBadge.Background = index == 3 ? subtleAccentBg : elevatedBg;
            }
        }

        if (SaleTypeUpiBtn != null)
        {
            SaleTypeUpiBtn.BorderBrush = index == 1 ? accentBrush : defaultBorder;
            SaleTypeUpiBtn.BorderThickness = new Thickness(index == 1 ? 2.5 : 1.5);
            SaleTypeUpiBtn.Background = index == 1 ? subtleAccentBg : defaultBg;
            if (SaleTypeUpiBadge != null)
            {
                SaleTypeUpiBadge.Background = index == 1 ? subtleAccentBg : elevatedBg;
            }
        }

        if (SaleTypeCardBtn != null)
        {
            SaleTypeCardBtn.BorderBrush = index == 2 ? accentBrush : defaultBorder;
            SaleTypeCardBtn.BorderThickness = new Thickness(index == 2 ? 2.5 : 1.5);
            SaleTypeCardBtn.Background = index == 2 ? subtleAccentBg : defaultBg;
            if (SaleTypeCardBadge != null)
            {
                SaleTypeCardBadge.Background = index == 2 ? subtleAccentBg : elevatedBg;
            }
        }
    }

    private void HighlightSaleType(int index)
    {
        ViewModel.SelectedSaleTypeIndex = index;
        if (ViewModel.ActiveTab != null)
        {
            ViewModel.ActiveTab.PaymentModeIndex = index;
        }

        UpdateSaleTypeVisuals(index);

        DispatcherQueue.TryEnqueue(() =>
        {
            switch (index)
            {
                case 0:
                    SaleTypeCashBtn?.Focus(FocusState.Programmatic);
                    break;
                case 3:
                    SaleTypeCreditBtn?.Focus(FocusState.Programmatic);
                    break;
                case 1:
                    SaleTypeUpiBtn?.Focus(FocusState.Programmatic);
                    break;
                case 2:
                    SaleTypeCardBtn?.Focus(FocusState.Programmatic);
                    break;
                default:
                    SaleTypeCashBtn?.Focus(FocusState.Programmatic);
                    break;
            }
        });
    }

    private void SaleTypeBtn_GotFocus(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, SaleTypeCashBtn))
        {
            ViewModel.SelectedSaleTypeIndex = 0;
            if (ViewModel.ActiveTab != null) ViewModel.ActiveTab.PaymentModeIndex = 0;
            UpdateSaleTypeVisuals(0);
        }
        else if (ReferenceEquals(sender, SaleTypeCreditBtn))
        {
            ViewModel.SelectedSaleTypeIndex = 3;
            if (ViewModel.ActiveTab != null) ViewModel.ActiveTab.PaymentModeIndex = 3;
            UpdateSaleTypeVisuals(3);
        }
        else if (ReferenceEquals(sender, SaleTypeUpiBtn))
        {
            ViewModel.SelectedSaleTypeIndex = 1;
            if (ViewModel.ActiveTab != null) ViewModel.ActiveTab.PaymentModeIndex = 1;
            UpdateSaleTypeVisuals(1);
        }
        else if (ReferenceEquals(sender, SaleTypeCardBtn))
        {
            ViewModel.SelectedSaleTypeIndex = 2;
            if (ViewModel.ActiveTab != null) ViewModel.ActiveTab.PaymentModeIndex = 2;
            UpdateSaleTypeVisuals(2);
        }
    }

    private void SaleTypeBtn_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel.SelectSaleType(ViewModel.SelectedSaleTypeIndex);
            FocusHeaderStart();
            e.Handled = true;
        }
    }

    private void SaleTypeBtn_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
        {
            var current = ViewModel.SelectedSaleTypeIndex;
            int next = current;

            if (e.Key == VirtualKey.Right)
            {
                next = current switch
                {
                    0 => 3, // Cash -> Credit
                    1 => 2, // UPI -> Card
                    3 => 0, // Credit -> Cash
                    2 => 1, // Card -> UPI
                    _ => 0
                };
            }
            else if (e.Key == VirtualKey.Left)
            {
                next = current switch
                {
                    3 => 0, // Credit -> Cash
                    2 => 1, // Card -> UPI
                    0 => 3, // Cash -> Credit
                    1 => 2, // UPI -> Card
                    _ => 0
                };
            }
            else if (e.Key == VirtualKey.Down)
            {
                next = current switch
                {
                    0 => 1, // Cash -> UPI
                    3 => 2, // Credit -> Card
                    1 => 0, // UPI -> Cash
                    2 => 3, // Card -> Credit
                    _ => 0
                };
            }
            else if (e.Key == VirtualKey.Up)
            {
                next = current switch
                {
                    1 => 0, // UPI -> Cash
                    2 => 3, // Card -> Credit
                    0 => 1, // Cash -> UPI
                    3 => 2, // Credit -> Card
                    _ => 0
                };
            }

            HighlightSaleType(next);
            e.Handled = true;
        }
    }

    private void SaleTypeCash_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectSaleType(0);
        FocusHeaderStart();
    }

    private void SaleTypeCredit_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectSaleType(3);
        FocusHeaderStart();
    }

    private void SaleTypeUpi_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectSaleType(1);
        FocusHeaderStart();
    }

    private void SaleTypeCard_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectSaleType(2);
        FocusHeaderStart();
    }

    private async Task ConfirmPrintPreviewAsync()
    {
        await ViewModel.ConfirmPrintPreviewAndSaveAsync();
        FocusHeaderStart();
    }

    private void ClosePrintPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClosePrintPreview();
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    private void PrintPreviewBtn_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ReferenceEquals(sender, ClosePrintPreviewButton))
            {
                ViewModel.ClosePrintPreview();
                SearchBox.Focus(FocusState.Programmatic);
                SearchBox.SelectAll();
            }
            else if (ReferenceEquals(sender, DownloadPdfPreviewButton))
            {
                _ = HandlePosPreviewPdfDownloadAsync();
            }
            else if (ReferenceEquals(sender, ConfirmPrintPreviewButton))
            {
                _ = ConfirmPrintPreviewAsync();
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.ClosePrintPreview();
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void PrintPreviewBtn_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
        {
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Down)
            {
                if (ReferenceEquals(sender, ClosePrintPreviewButton))
                {
                    DownloadPdfPreviewButton.Focus(FocusState.Programmatic);
                }
                else if (ReferenceEquals(sender, DownloadPdfPreviewButton))
                {
                    ConfirmPrintPreviewButton.Focus(FocusState.Programmatic);
                }
                else
                {
                    ClosePrintPreviewButton.Focus(FocusState.Programmatic);
                }
            }
            else if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Up)
            {
                if (ReferenceEquals(sender, ConfirmPrintPreviewButton))
                {
                    DownloadPdfPreviewButton.Focus(FocusState.Programmatic);
                }
                else if (ReferenceEquals(sender, DownloadPdfPreviewButton))
                {
                    ClosePrintPreviewButton.Focus(FocusState.Programmatic);
                }
                else
                {
                    ConfirmPrintPreviewButton.Focus(FocusState.Programmatic);
                }
            }
            e.Handled = true;
        }
    }

    private void ConfirmCloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ConfirmCloseTab();
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void CancelCloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelCloseTab();
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void CloseTabModalBtn_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ReferenceEquals(sender, ConfirmCloseTabButton))
            {
                ViewModel.ConfirmCloseTab();
            }
            else
            {
                ViewModel.CancelCloseTab();
            }
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CancelCloseTab();
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void CloseTabModalBtn_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
        {
            if (ReferenceEquals(sender, CancelCloseTabButton))
            {
                ConfirmCloseTabButton.Focus(FocusState.Programmatic);
            }
            else
            {
                CancelCloseTabButton.Focus(FocusState.Programmatic);
            }
            e.Handled = true;
        }
    }

    private async Task LoadPosPreviewHtmlAsync()
    {
        try
        {
            var receipt = BuildReceiptModelFromActiveTab();
            var templateRepo = App.Services.GetRequiredService<IBillTemplateRepository>();
            var generator = App.Services.GetRequiredService<IBillDocumentGenerator>();

            var template = await templateRepo.GetDefaultTemplateAsync();
            var html = await generator.GenerateHtmlBillAsync(receipt, template);

            await PosPreviewWebView.EnsureCoreWebView2Async();
            if (PosPreviewWebView.CoreWebView2 != null)
            {
                PosPreviewWebView.NavigateToString(html);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POS] Error loading preview HTML: {ex.Message}");
        }
    }

    private async Task HandlePosPreviewPdfDownloadAsync()
    {
        try
        {
            var tab = ViewModel.ActiveTab;
            var invoiceNo = tab?.DisplayInvoiceNo ?? $"INV-{DateTime.Now:yyyyMMddHHmmss}";
            var cleanInvoiceNo = string.Join("_", invoiceNo.Split(Path.GetInvalidFileNameChars()));
            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var targetPath = Path.Combine(docsPath, $"{cleanInvoiceNo}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            await PosPreviewWebView.EnsureCoreWebView2Async();
            if (PosPreviewWebView.CoreWebView2 != null)
            {
                var printSettings = PosPreviewWebView.CoreWebView2.Environment.CreatePrintSettings();
                printSettings.ShouldPrintBackgrounds = true;
                printSettings.Orientation = Microsoft.Web.WebView2.Core.CoreWebView2PrintOrientation.Portrait;

                var success = await PosPreviewWebView.CoreWebView2.PrintToPdfAsync(targetPath, printSettings);
                if (success)
                {
                    ViewModel.StatusMessage = $"✅ PDF exported successfully: {targetPath}";
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(targetPath) { UseShellExecute = true });
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"PDF export error: {ex.Message}";
        }
    }

    private void PrintPromptBtn_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ReferenceEquals(sender, SkipPrintButton))
            {
                ViewModel.SkipPrint();
                FocusHeaderStart();
            }
            else
            {
                _ = ViewModel.PrintReceiptAsync();
                FocusHeaderStart();
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.SkipPrint();
            FocusHeaderStart();
            e.Handled = true;
        }
    }

    private void ConfirmPrintPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmPrintPreviewAsync();
    }

    private void PayAndPrintBottomBar_Click(object sender, RoutedEventArgs e)
    {
        SyncActiveDiscountBox();
        if (ViewModel.ActiveTab != null && ViewModel.ActiveTab.CartItems.Any())
        {
            ViewModel.OpenSaveConfirmation();
            DispatcherQueue.TryEnqueue(() => ConfirmSaveButton.Focus(FocusState.Programmatic));
        }
        else
        {
            ViewModel.StatusMessage = "Cart is empty. Scan barcode or type medicine name to begin.";
        }
    }

    private async void PrintReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PrintReceiptAsync();
        FocusHeaderStart();
    }

    private async void DownloadPdfPromptButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SkipPrint();
        var receipt = BuildReceiptModelForLastSale();
        await OpenPrintPreviewDialogAsync(receipt);
    }

    private async void DownloadPdfPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        await HandlePosPreviewPdfDownloadAsync();
    }

    private SaleReceiptModel BuildReceiptModelFromActiveTab()
    {
        var tab = ViewModel.ActiveTab;
        var invoiceNo = tab?.DisplayInvoiceNo ?? $"INV-{DateTime.Now:yyyyMMddHHmmss}";
        var billDiscountPercent = tab?.BillDiscountPercent ?? 0m;
        var billDiscountAmount = tab?.BillDiscountAmount ?? 0m;
        var eligibleSubtotal = tab?.CartItems.Where(i => i.DiscountPercent == 0).Sum(i => i.GrossAmount) ?? 0m;

        var items = tab?.CartItems.Select(c =>
        {
            decimal itemDiscPct = c.DiscountPercent;
            if (itemDiscPct == 0)
            {
                if (billDiscountPercent > 0)
                {
                    itemDiscPct = billDiscountPercent;
                }
                else if (billDiscountAmount > 0 && eligibleSubtotal > 0)
                {
                    itemDiscPct = Math.Round((billDiscountAmount / eligibleSubtotal) * 100m, 2, MidpointRounding.AwayFromZero);
                }
            }

            var lineGross = c.GrossAmount;
            var lineDisc = Math.Round(lineGross * (itemDiscPct / 100m), 2, MidpointRounding.AwayFromZero);
            decimal net = lineGross - lineDisc;

            return new ReceiptItemModel(
                c.ProductName,
                c.BatchNumber,
                c.ExpiryDate,
                c.Quantity,
                c.UnitPrice,
                net,
                c.GstRatePercent,
                c.FreeQuantity,
                itemDiscPct,
                lineDisc,
                c.Mrp,
                "3004",
                c.PackSizeDescription,
                string.Empty
            );
        }).ToList() ?? new List<ReceiptItemModel>();

        var subtotal = tab?.Subtotal ?? 0m;
        var cgst = tab?.Cgst ?? 0m;
        var sgst = tab?.Sgst ?? 0m;
        var igst = tab?.Igst ?? 0m;
        var grandTotal = tab?.GrandTotal ?? 0m;
        var taxable = grandTotal - (cgst + sgst + igst);

        return new SaleReceiptModel(
            PharmacyName: "MEDISTOCK PHARMACY & HEALTHCARE",
            PharmacyAddress: "Main Road, Healthcare Complex, Suite 101",
            PharmacyPhone: "+91 98765 43210",
            Gstin: "19ABCDE1234F1Z5",
            DlNumbers: "20B/21B-WB-102938",
            InvoiceNo: invoiceNo,
            InvoiceDate: tab?.InvoiceDate ?? DateTime.Now,
            CounterName: ViewModel.CounterName,
            CashierName: ViewModel.CashierName,
            CustomerName: string.IsNullOrWhiteSpace(tab?.CustomerName) ? "WALK-IN CUSTOMER" : tab.CustomerName.Trim().ToUpperInvariant(),
            DoctorName: string.IsNullOrWhiteSpace(tab?.DoctorName) ? string.Empty : tab.DoctorName.Trim().ToUpperInvariant(),
            Items: items,
            Subtotal: taxable > 0 ? taxable : subtotal,
            CgstAmount: cgst,
            SgstAmount: sgst,
            IgstAmount: igst,
            RoundOff: 0m,
            GrandTotal: grandTotal,
            Payments: new List<ReceiptPaymentModel>
            {
                new(tab?.PaymentMode.ToString() ?? "Cash", grandTotal, null)
            }
        );
    }

    private SaleReceiptModel BuildReceiptModelForLastSale()
    {
        var invoiceNo = !string.IsNullOrEmpty(ViewModel.LastCompletedInvoiceNo) ? ViewModel.LastCompletedInvoiceNo : $"INV-{DateTime.Now:yyyyMMddHHmmss}";
        var customer = !string.IsNullOrEmpty(ViewModel.LastCompletedCustomer) ? ViewModel.LastCompletedCustomer.Trim().ToUpperInvariant() : "WALK-IN CUSTOMER";
        var amount = ViewModel.LastCompletedAmount > 0 ? ViewModel.LastCompletedAmount : 0m;

        return new SaleReceiptModel(
            PharmacyName: "MEDISTOCK PHARMACY & HEALTHCARE",
            PharmacyAddress: "Main Road, Healthcare Complex, Suite 101",
            PharmacyPhone: "+91 98765 43210",
            Gstin: "19ABCDE1234F1Z5",
            DlNumbers: "20B/21B-WB-102938",
            InvoiceNo: invoiceNo,
            InvoiceDate: DateTime.Now,
            CounterName: ViewModel.CounterName,
            CashierName: ViewModel.CashierName,
            CustomerName: customer,
            DoctorName: string.Empty,
            Items: new List<ReceiptItemModel>
            {
                new("Pharmacy Sale Item(s)", "BAT01", DateTime.Now.AddYears(1), 1, amount, amount, 12m)
            },
            Subtotal: amount * 0.88m,
            CgstAmount: amount * 0.06m,
            SgstAmount: amount * 0.06m,
            IgstAmount: 0m,
            RoundOff: 0m,
            GrandTotal: amount,
            Payments: new List<ReceiptPaymentModel>
            {
                new("Cash", amount, null)
            }
        );
    }

    private async Task OpenPrintPreviewDialogAsync(SaleReceiptModel receipt)
    {
        try
        {
            var previewVm = App.Services.GetRequiredService<PrintPreviewViewModel>();
            await previewVm.InitializeAsync(receipt);
            var dialog = new PrintPreviewDialog(previewVm)
            {
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Print Preview Error: {ex.Message}";
        }
    }

    private void SkipPrintButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SkipPrint();
        FocusHeaderStart();
    }

    private void PrintPromptBtn_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Left || e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
        {
            if (ReferenceEquals(sender, PrintReceiptButton))
            {
                SkipPrintButton.Focus(FocusState.Programmatic);
            }
            else
            {
                PrintReceiptButton.Focus(FocusState.Programmatic);
            }
            e.Handled = true;
        }
    }

    private async Task HandlePrintShortcutAsync()
    {
        if (ViewModel.IsPrintPromptOpen)
        {
            await ViewModel.PrintReceiptAsync();
            FocusHeaderStart();
        }
        else if (!string.IsNullOrEmpty(ViewModel.LastCompletedInvoiceNo))
        {
            await ViewModel.PrintReceiptAsync();
        }
        else
        {
            ViewModel.StatusMessage = "No recent invoice to print. [F7 / Ctrl+P]";
        }
    }

    private void CartListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Up && ViewModel.ActiveTab?.SelectedCartIndex == 0)
        {
            // Up arrow on row 0 jumps directly to SearchBox
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Insert)
        {
            // MARG ERP Rule: Pressing Insert on a row arms insert-to-splice before that row
            ViewModel.SetInsertSpliceMode();
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Enter)
        {
            // Move focus into the selected row's Strip box (Col 5)
            var idx = ViewModel.ActiveTab?.SelectedCartIndex ?? -1;
            if (idx >= 0 && CartListView.ContainerFromIndex(idx) is ListViewItem container)
            {
                var stripBox = FindVisualChild<NumberBox>(container, nb => Grid.GetColumn(nb) == 5);
                stripBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Left)
        {
            // Move focus into the selected row's Discount box (Col 11)
            var idx = ViewModel.ActiveTab?.SelectedCartIndex ?? -1;
            if (idx >= 0 && CartListView.ContainerFromIndex(idx) is ListViewItem container)
            {
                var discBox = FindVisualChild<NumberBox>(container, nb => Grid.GetColumn(nb) == 11);
                discBox?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Add)
        {
            ViewModel.IncreaseSelectedCartQuantity();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Subtract)
        {
            ViewModel.DecreaseSelectedCartQuantity();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.F3 || e.Key == VirtualKey.Escape)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
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

