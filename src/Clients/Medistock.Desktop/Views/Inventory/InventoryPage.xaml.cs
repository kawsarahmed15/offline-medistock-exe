using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml.Controls;

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
            if (e.Key == Windows.System.VirtualKey.Escape && ViewModel.IsEditProductModalOpen)
            {
                ViewModel.CloseEditProduct();
                e.Handled = true;
            }
        };
    }

    private void EditProductInput_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is not Control currentControl) return;

        var shiftDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var ctrlDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            ViewModel.CloseEditProduct();
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.F2 || (e.Key == Windows.System.VirtualKey.Enter && ctrlDown))
        {
            ViewModel.SaveProductDetailsCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (ReferenceEquals(currentControl, SaveEditProductButton))
            {
                ViewModel.SaveProductDetailsCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (ReferenceEquals(currentControl, CancelEditProductButton))
            {
                ViewModel.CloseEditProduct();
                e.Handled = true;
                return;
            }

            if (shiftDown)
            {
                var prev = GetPrevEditProductField(currentControl);
                if (prev != null)
                {
                    FocusEditProductField(prev);
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                var next = GetNextEditProductField(currentControl);
                if (next != null)
                {
                    FocusEditProductField(next);
                    e.Handled = true;
                    return;
                }
            }
        }

        if (e.Key == Windows.System.VirtualKey.Down)
        {
            // For ComboBox, allow normal dropdown option selection unless user presses Alt
            if (currentControl is ComboBox cb && !cb.IsDropDownOpen)
            {
                // Can drop down or navigate
            }
            var down = GetDownEditProductField(currentControl);
            if (down != null)
            {
                FocusEditProductField(down);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Windows.System.VirtualKey.Up)
        {
            var up = GetUpEditProductField(currentControl);
            if (up != null)
            {
                FocusEditProductField(up);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Windows.System.VirtualKey.Right)
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
                var right = GetRightEditProductField(currentControl);
                if (right != null)
                {
                    FocusEditProductField(right);
                    e.Handled = true;
                    return;
                }
            }
        }

        if (e.Key == Windows.System.VirtualKey.Left)
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
                var left = GetLeftEditProductField(currentControl);
                if (left != null)
                {
                    FocusEditProductField(left);
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    private void FocusEditProductField(Control target)
    {
        if (target == null) return;
        target.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        if (target is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    private Control? GetRightEditProductField(Control current)
    {
        if (ReferenceEquals(current, CancelEditProductButton)) return SaveEditProductButton;
        if (ReferenceEquals(current, SaveEditProductButton)) return CancelEditProductButton;
        return GetNextEditProductField(current);
    }

    private Control? GetLeftEditProductField(Control current)
    {
        if (ReferenceEquals(current, SaveEditProductButton)) return CancelEditProductButton;
        if (ReferenceEquals(current, CancelEditProductButton)) return SaveEditProductButton;
        return GetPrevEditProductField(current);
    }

    private Control? GetNextEditProductField(Control current)
    {
        if (ReferenceEquals(current, EditProductNameBox)) return EditGenericNameBox;
        if (ReferenceEquals(current, EditGenericNameBox)) return EditSaltCompositionBox;
        if (ReferenceEquals(current, EditSaltCompositionBox)) return EditManufacturerBox;
        if (ReferenceEquals(current, EditManufacturerBox)) return EditCategoryNameBox;
        if (ReferenceEquals(current, EditCategoryNameBox)) return EditHsnCodeBox;
        if (ReferenceEquals(current, EditHsnCodeBox)) return EditGstRatePercentBox;
        if (ReferenceEquals(current, EditGstRatePercentBox)) return EditScheduleComboBox;
        if (ReferenceEquals(current, EditScheduleComboBox)) return EditMinStockAlertBox;
        if (ReferenceEquals(current, EditMinStockAlertBox)) return EditBatchNumberBox;
        if (ReferenceEquals(current, EditBatchNumberBox)) return EditExpiryDatePicker;
        if (ReferenceEquals(current, EditExpiryDatePicker)) return EditMrpBox;
        if (ReferenceEquals(current, EditMrpBox)) return EditPurchaseRateBox;
        if (ReferenceEquals(current, EditPurchaseRateBox)) return EditSaleRateBox;
        if (ReferenceEquals(current, EditSaleRateBox)) return SaveEditProductButton;
        if (ReferenceEquals(current, CancelEditProductButton)) return SaveEditProductButton;
        if (ReferenceEquals(current, SaveEditProductButton)) return EditProductNameBox;
        return null;
    }

    private Control? GetPrevEditProductField(Control current)
    {
        if (ReferenceEquals(current, EditProductNameBox)) return SaveEditProductButton;
        if (ReferenceEquals(current, EditGenericNameBox)) return EditProductNameBox;
        if (ReferenceEquals(current, EditSaltCompositionBox)) return EditGenericNameBox;
        if (ReferenceEquals(current, EditManufacturerBox)) return EditSaltCompositionBox;
        if (ReferenceEquals(current, EditCategoryNameBox)) return EditManufacturerBox;
        if (ReferenceEquals(current, EditHsnCodeBox)) return EditCategoryNameBox;
        if (ReferenceEquals(current, EditGstRatePercentBox)) return EditHsnCodeBox;
        if (ReferenceEquals(current, EditScheduleComboBox)) return EditGstRatePercentBox;
        if (ReferenceEquals(current, EditMinStockAlertBox)) return EditScheduleComboBox;
        if (ReferenceEquals(current, EditBatchNumberBox)) return EditMinStockAlertBox;
        if (ReferenceEquals(current, EditExpiryDatePicker)) return EditBatchNumberBox;
        if (ReferenceEquals(current, EditMrpBox)) return EditExpiryDatePicker;
        if (ReferenceEquals(current, EditPurchaseRateBox)) return EditMrpBox;
        if (ReferenceEquals(current, EditSaleRateBox)) return EditPurchaseRateBox;
        if (ReferenceEquals(current, SaveEditProductButton)) return EditSaleRateBox;
        if (ReferenceEquals(current, CancelEditProductButton)) return EditSaleRateBox;
        return null;
    }

    private Control? GetDownEditProductField(Control current)
    {
        // Row 0 -> Row 1
        if (ReferenceEquals(current, EditProductNameBox)) return EditSaltCompositionBox;
        if (ReferenceEquals(current, EditGenericNameBox)) return EditManufacturerBox;

        // Row 1 -> Row 2
        if (ReferenceEquals(current, EditSaltCompositionBox)) return EditCategoryNameBox;
        if (ReferenceEquals(current, EditManufacturerBox)) return EditGstRatePercentBox;

        // Row 2 -> Row 3
        if (ReferenceEquals(current, EditCategoryNameBox)) return EditScheduleComboBox;
        if (ReferenceEquals(current, EditHsnCodeBox)) return EditScheduleComboBox;
        if (ReferenceEquals(current, EditGstRatePercentBox)) return EditMinStockAlertBox;

        // Row 3 -> Row 4
        if (ReferenceEquals(current, EditScheduleComboBox)) return EditBatchNumberBox;
        if (ReferenceEquals(current, EditMinStockAlertBox)) return EditExpiryDatePicker;

        // Row 4 -> Row 5
        if (ReferenceEquals(current, EditBatchNumberBox)) return EditMrpBox;
        if (ReferenceEquals(current, EditExpiryDatePicker)) return EditSaleRateBox;

        // Row 5 -> Row 6 (Buttons)
        if (ReferenceEquals(current, EditMrpBox)) return CancelEditProductButton;
        if (ReferenceEquals(current, EditPurchaseRateBox)) return SaveEditProductButton;
        if (ReferenceEquals(current, EditSaleRateBox)) return SaveEditProductButton;

        // Row 6 -> wrap to top
        if (ReferenceEquals(current, CancelEditProductButton)) return EditProductNameBox;
        if (ReferenceEquals(current, SaveEditProductButton)) return EditGenericNameBox;

        return null;
    }

    private Control? GetUpEditProductField(Control current)
    {
        // Row 6 -> Row 5
        if (ReferenceEquals(current, CancelEditProductButton)) return EditMrpBox;
        if (ReferenceEquals(current, SaveEditProductButton)) return EditSaleRateBox;

        // Row 5 -> Row 4
        if (ReferenceEquals(current, EditMrpBox)) return EditBatchNumberBox;
        if (ReferenceEquals(current, EditPurchaseRateBox)) return EditBatchNumberBox;
        if (ReferenceEquals(current, EditSaleRateBox)) return EditExpiryDatePicker;

        // Row 4 -> Row 3
        if (ReferenceEquals(current, EditBatchNumberBox)) return EditScheduleComboBox;
        if (ReferenceEquals(current, EditExpiryDatePicker)) return EditMinStockAlertBox;

        // Row 3 -> Row 2
        if (ReferenceEquals(current, EditScheduleComboBox)) return EditCategoryNameBox;
        if (ReferenceEquals(current, EditMinStockAlertBox)) return EditGstRatePercentBox;

        // Row 2 -> Row 1
        if (ReferenceEquals(current, EditCategoryNameBox)) return EditSaltCompositionBox;
        if (ReferenceEquals(current, EditHsnCodeBox)) return EditSaltCompositionBox;
        if (ReferenceEquals(current, EditGstRatePercentBox)) return EditManufacturerBox;

        // Row 1 -> Row 0
        if (ReferenceEquals(current, EditSaltCompositionBox)) return EditProductNameBox;
        if (ReferenceEquals(current, EditManufacturerBox)) return EditGenericNameBox;

        // Row 0 -> wrap to bottom
        if (ReferenceEquals(current, EditProductNameBox)) return CancelEditProductButton;
        if (ReferenceEquals(current, EditGenericNameBox)) return SaveEditProductButton;

        return null;
    }
}
