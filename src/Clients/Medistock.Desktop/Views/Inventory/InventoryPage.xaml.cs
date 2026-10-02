using System;
using System.Threading.Tasks;
using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Medistock.Desktop.Views.Inventory;

public sealed partial class InventoryPage : Page
{
    public InventoryViewModel ViewModel { get; }

    public InventoryPage(InventoryViewModel viewModel)
    {
        ViewModel = viewModel;
        this.DataContext = ViewModel;
        this.InitializeComponent();

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadStocksAsync();
        };

        this.KeyDown += (s, e) =>
        {
            if (e.Key == VirtualKey.Escape)
            {
                if (ViewModel.IsMetricDetailModalOpen)
                {
                    ViewModel.CloseMetricDetail();
                    e.Handled = true;
                }
                else if (ViewModel.IsAddProductModalOpen)
                {
                    ViewModel.CloseAddProductModal();
                    e.Handled = true;
                }
            }
            else if (e.Key == VirtualKey.F2 && !ViewModel.IsAddProductModalOpen && !ViewModel.IsMetricDetailModalOpen)
            {
                ViewModel.OpenAddProductModal();
                e.Handled = true;
            }
        };
    }

    private void NewProductNameBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsAddProductModalOpen)
        {
            FocusAddProductModal();
        }
    }

    private async void FocusAddProductModal()
    {
        NewProductNameBox?.Focus(FocusState.Programmatic);
        NewProductNameBox?.SelectAll();

        for (int i = 0; i < 4; i++)
        {
            await Task.Delay(25 * (i + 1));
            if (!ViewModel.IsAddProductModalOpen) return;
            NewProductNameBox?.Focus(FocusState.Programmatic);
            NewProductNameBox?.SelectAll();
        }
    }

    private void NewProductExpiryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            var raw = tb.Text?.Trim() ?? string.Empty;
            if (raw.Length == 2 && !raw.Contains('/') && int.TryParse(raw, out int m) && m >= 1 && m <= 12)
            {
                tb.Text = raw + "/";
                tb.SelectionStart = tb.Text.Length;
            }
            else if (raw.Length == 4 && !raw.Contains('/') && int.TryParse(raw.Substring(0, 2), out int mm) && mm >= 1 && mm <= 12)
            {
                tb.Text = raw.Substring(0, 2) + "/" + raw.Substring(2, 2);
                tb.SelectionStart = tb.Text.Length;
            }
        }
    }

    private async void NewProductField_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is not Control currentControl) return;

        var shiftDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var ctrlDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        // Escape: close modal
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CloseAddProductModal();
            e.Handled = true;
            return;
        }

        // F2 or Ctrl+Enter: instant Save from any field
        if (e.Key == VirtualKey.F2 || (e.Key == VirtualKey.Enter && ctrlDown))
        {
            await ViewModel.SaveNewProductAsync();
            e.Handled = true;
            return;
        }

        // Enter: move to next field, or trigger action on buttons
        if (e.Key == VirtualKey.Enter)
        {
            if (ReferenceEquals(currentControl, SaveNewProductBtn))
            {
                await ViewModel.SaveNewProductAsync();
                e.Handled = true;
                return;
            }
            if (ReferenceEquals(currentControl, CancelNewProductBtn))
            {
                ViewModel.CloseAddProductModal();
                e.Handled = true;
                return;
            }

            if (shiftDown)
            {
                var prev = GetPrevProductField(currentControl);
                if (prev != null)
                {
                    FocusProductField(prev);
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                var next = GetNextProductField(currentControl);
                if (next != null)
                {
                    FocusProductField(next);
                    e.Handled = true;
                    return;
                }
            }
        }

        // Arrow Down: move focus to field below or drop down ComboBox
        if (e.Key == VirtualKey.Down)
        {
            if (currentControl is ComboBox cb && !cb.IsDropDownOpen)
            {
                if (ReferenceEquals(currentControl, NewProductStockTypeComboBox))
                {
                    ViewModel.SelectNextStockType();
                    e.Handled = true;
                    return;
                }
                if (ReferenceEquals(currentControl, NewProductTaxComboBox))
                {
                    ViewModel.SelectNextTaxRate();
                    e.Handled = true;
                    return;
                }
                if (ReferenceEquals(currentControl, NewProductVolumeMlComboBox) || ReferenceEquals(currentControl, NewProductWeightGmComboBox))
                {
                    ViewModel.SelectNextPackSize();
                    e.Handled = true;
                    return;
                }
            }

            var down = GetDownProductField(currentControl);
            if (down != null)
            {
                FocusProductField(down);
                e.Handled = true;
                return;
            }
        }

        // Arrow Up: move focus to field above
        if (e.Key == VirtualKey.Up)
        {
            if (currentControl is ComboBox cb && !cb.IsDropDownOpen)
            {
                if (ReferenceEquals(currentControl, NewProductStockTypeComboBox))
                {
                    ViewModel.SelectPreviousStockType();
                    e.Handled = true;
                    return;
                }
                if (ReferenceEquals(currentControl, NewProductTaxComboBox))
                {
                    ViewModel.SelectPreviousTaxRate();
                    e.Handled = true;
                    return;
                }
                if (ReferenceEquals(currentControl, NewProductVolumeMlComboBox) || ReferenceEquals(currentControl, NewProductWeightGmComboBox))
                {
                    ViewModel.SelectPreviousPackSize();
                    e.Handled = true;
                    return;
                }
            }

            var up = GetUpProductField(currentControl);
            if (up != null)
            {
                FocusProductField(up);
                e.Handled = true;
                return;
            }
        }

        // Arrow Right
        if (e.Key == VirtualKey.Right)
        {
            bool canNavigate = true;
            if (currentControl is TextBox tb)
            {
                canNavigate = string.IsNullOrEmpty(tb.Text) ||
                              tb.SelectionLength == tb.Text.Length ||
                              tb.SelectionStart >= tb.Text.Length;
            }

            if (canNavigate)
            {
                var right = GetRightProductField(currentControl);
                if (right != null)
                {
                    FocusProductField(right);
                    e.Handled = true;
                    return;
                }
            }
        }

        // Arrow Left
        if (e.Key == VirtualKey.Left)
        {
            bool canNavigate = true;
            if (currentControl is TextBox tb)
            {
                canNavigate = string.IsNullOrEmpty(tb.Text) ||
                              tb.SelectionLength == tb.Text.Length ||
                              (tb.SelectionStart == 0 && tb.SelectionLength == 0);
            }

            if (canNavigate)
            {
                var left = GetLeftProductField(currentControl);
                if (left != null)
                {
                    FocusProductField(left);
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    private void FocusProductField(Control target)
    {
        if (target == null) return;
        target.Focus(FocusState.Programmatic);
        if (target is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    private Control? GetRightProductField(Control current)
    {
        if (ReferenceEquals(current, CancelNewProductBtn)) return SaveNewProductBtn;
        if (ReferenceEquals(current, SaveNewProductBtn)) return CancelNewProductBtn;
        return GetNextProductField(current);
    }

    private Control? GetLeftProductField(Control current)
    {
        if (ReferenceEquals(current, SaveNewProductBtn)) return CancelNewProductBtn;
        if (ReferenceEquals(current, CancelNewProductBtn)) return SaveNewProductBtn;
        return GetPrevProductField(current);
    }

    private Control? GetNextProductField(Control current)
    {
        if (ReferenceEquals(current, NewProductNameBox)) return NewProductManufacturerBox;
        if (ReferenceEquals(current, NewProductManufacturerBox)) return NewProductStockTypeComboBox;
        if (ReferenceEquals(current, NewProductStockTypeComboBox))
        {
            if (ViewModel.IsTabletOrCapsule) return NewProductStripCountBox;
            if (ViewModel.IsVolumeMlType) return NewProductVolumeMlComboBox;
            if (ViewModel.IsWeightGmType) return NewProductWeightGmComboBox;
            return NewProductPackOptionsBox;
        }
        if (ReferenceEquals(current, NewProductStripCountBox)) return NewProductPcsPerStripBox;
        if (ReferenceEquals(current, NewProductPcsPerStripBox) ||
            ReferenceEquals(current, NewProductVolumeMlComboBox) ||
            ReferenceEquals(current, NewProductWeightGmComboBox) ||
            ReferenceEquals(current, NewProductPackOptionsBox)) return NewProductHsnBox;

        if (ReferenceEquals(current, NewProductHsnBox)) return NewProductTaxComboBox;
        if (ReferenceEquals(current, NewProductTaxComboBox)) return NewProductMrpBox;
        if (ReferenceEquals(current, NewProductMrpBox)) return NewProductBuyingPriceBox;
        if (ReferenceEquals(current, NewProductBuyingPriceBox)) return NewProductInitialStockQtyBox;
        if (ReferenceEquals(current, NewProductInitialStockQtyBox)) return NewProductBatchBox;
        if (ReferenceEquals(current, NewProductBatchBox)) return NewProductExpiryBox;
        if (ReferenceEquals(current, NewProductExpiryBox)) return NewProductRxCheckBox;
        if (ReferenceEquals(current, NewProductRxCheckBox)) return SaveNewProductBtn;
        if (ReferenceEquals(current, CancelNewProductBtn)) return SaveNewProductBtn;
        if (ReferenceEquals(current, SaveNewProductBtn)) return NewProductNameBox;
        return null;
    }

    private Control? GetPrevProductField(Control current)
    {
        if (ReferenceEquals(current, NewProductNameBox)) return SaveNewProductBtn;
        if (ReferenceEquals(current, NewProductManufacturerBox)) return NewProductNameBox;
        if (ReferenceEquals(current, NewProductStockTypeComboBox)) return NewProductManufacturerBox;
        if (ReferenceEquals(current, NewProductStripCountBox)) return NewProductStockTypeComboBox;
        if (ReferenceEquals(current, NewProductPcsPerStripBox)) return NewProductStripCountBox;
        if (ReferenceEquals(current, NewProductVolumeMlComboBox) ||
            ReferenceEquals(current, NewProductWeightGmComboBox) ||
            ReferenceEquals(current, NewProductPackOptionsBox)) return NewProductStockTypeComboBox;

        if (ReferenceEquals(current, NewProductHsnBox))
        {
            if (ViewModel.IsTabletOrCapsule) return NewProductPcsPerStripBox;
            if (ViewModel.IsVolumeMlType) return NewProductVolumeMlComboBox;
            if (ViewModel.IsWeightGmType) return NewProductWeightGmComboBox;
            return NewProductPackOptionsBox;
        }

        if (ReferenceEquals(current, NewProductTaxComboBox)) return NewProductHsnBox;
        if (ReferenceEquals(current, NewProductMrpBox)) return NewProductTaxComboBox;
        if (ReferenceEquals(current, NewProductBuyingPriceBox)) return NewProductMrpBox;
        if (ReferenceEquals(current, NewProductInitialStockQtyBox)) return NewProductBuyingPriceBox;
        if (ReferenceEquals(current, NewProductBatchBox)) return NewProductInitialStockQtyBox;
        if (ReferenceEquals(current, NewProductExpiryBox)) return NewProductBatchBox;
        if (ReferenceEquals(current, NewProductRxCheckBox)) return NewProductExpiryBox;
        if (ReferenceEquals(current, SaveNewProductBtn)) return NewProductRxCheckBox;
        if (ReferenceEquals(current, CancelNewProductBtn)) return NewProductRxCheckBox;
        return null;
    }

    private Control? GetDownProductField(Control current)
    {
        if (ReferenceEquals(current, NewProductNameBox)) return NewProductStockTypeComboBox;
        if (ReferenceEquals(current, NewProductManufacturerBox))
        {
            if (ViewModel.IsTabletOrCapsule) return NewProductStripCountBox;
            if (ViewModel.IsVolumeMlType) return NewProductVolumeMlComboBox;
            if (ViewModel.IsWeightGmType) return NewProductWeightGmComboBox;
            return NewProductPackOptionsBox;
        }
        if (ReferenceEquals(current, NewProductStockTypeComboBox)) return NewProductHsnBox;
        if (ReferenceEquals(current, NewProductStripCountBox) ||
            ReferenceEquals(current, NewProductPcsPerStripBox) ||
            ReferenceEquals(current, NewProductVolumeMlComboBox) ||
            ReferenceEquals(current, NewProductWeightGmComboBox) ||
            ReferenceEquals(current, NewProductPackOptionsBox)) return NewProductTaxComboBox;

        if (ReferenceEquals(current, NewProductHsnBox)) return NewProductMrpBox;
        if (ReferenceEquals(current, NewProductTaxComboBox)) return NewProductBuyingPriceBox;
        if (ReferenceEquals(current, NewProductMrpBox)) return NewProductInitialStockQtyBox;
        if (ReferenceEquals(current, NewProductBuyingPriceBox)) return NewProductBatchBox;
        if (ReferenceEquals(current, NewProductInitialStockQtyBox) ||
            ReferenceEquals(current, NewProductBatchBox) ||
            ReferenceEquals(current, NewProductExpiryBox)) return NewProductRxCheckBox;
        if (ReferenceEquals(current, NewProductRxCheckBox)) return SaveNewProductBtn;
        if (ReferenceEquals(current, CancelNewProductBtn)) return NewProductNameBox;
        if (ReferenceEquals(current, SaveNewProductBtn)) return NewProductManufacturerBox;
        return null;
    }

    private Control? GetUpProductField(Control current)
    {
        if (ReferenceEquals(current, SaveNewProductBtn) || ReferenceEquals(current, CancelNewProductBtn)) return NewProductRxCheckBox;
        if (ReferenceEquals(current, NewProductRxCheckBox)) return NewProductInitialStockQtyBox;
        if (ReferenceEquals(current, NewProductInitialStockQtyBox)) return NewProductMrpBox;
        if (ReferenceEquals(current, NewProductBatchBox) || ReferenceEquals(current, NewProductExpiryBox)) return NewProductBuyingPriceBox;
        if (ReferenceEquals(current, NewProductMrpBox)) return NewProductHsnBox;
        if (ReferenceEquals(current, NewProductBuyingPriceBox)) return NewProductTaxComboBox;
        if (ReferenceEquals(current, NewProductHsnBox)) return NewProductStockTypeComboBox;
        if (ReferenceEquals(current, NewProductTaxComboBox))
        {
            if (ViewModel.IsTabletOrCapsule) return NewProductStripCountBox;
            if (ViewModel.IsVolumeMlType) return NewProductVolumeMlComboBox;
            if (ViewModel.IsWeightGmType) return NewProductWeightGmComboBox;
            return NewProductPackOptionsBox;
        }
        if (ReferenceEquals(current, NewProductStockTypeComboBox)) return NewProductNameBox;
        if (ReferenceEquals(current, NewProductStripCountBox) ||
            ReferenceEquals(current, NewProductPcsPerStripBox) ||
            ReferenceEquals(current, NewProductVolumeMlComboBox) ||
            ReferenceEquals(current, NewProductWeightGmComboBox) ||
            ReferenceEquals(current, NewProductPackOptionsBox)) return NewProductManufacturerBox;

        return null;
    }
}
