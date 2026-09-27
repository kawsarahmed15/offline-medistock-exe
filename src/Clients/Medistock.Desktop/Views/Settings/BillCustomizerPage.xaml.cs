using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Printing;
using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Settings;

public sealed partial class BillCustomizerPage : Page
{
    public BillCustomizerViewModel ViewModel { get; }
    private CancellationTokenSource? _previewDebounceCts;

    public BillCustomizerPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetService(typeof(BillCustomizerViewModel)) as BillCustomizerViewModel
                    ?? throw new InvalidOperationException("BillCustomizerViewModel not registered.");

        this.Loaded += BillCustomizerPage_Loaded;
    }

    private async void BillCustomizerPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        try
        {
            await LivePreviewWebView.EnsureCoreWebView2Async();
            UpdateWebViewContent();
        }
        catch { }
    }

    private void UpdateWebViewContent()
    {
        if (LivePreviewWebView.CoreWebView2 != null && !string.IsNullOrWhiteSpace(ViewModel.LivePreviewHtml))
        {
            LivePreviewWebView.NavigateToString(ViewModel.LivePreviewHtml);
        }
    }

    private async void TemplatePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplatePicker.SelectedItem is BillTemplateConfig selected)
        {
            await ViewModel.SelectTemplateAsync(selected);
            this.Bindings.Update();
            UpdateWebViewContent();
        }
    }

    private void SyncTextBoxToTemplate(object sender)
    {
        if (ViewModel.CurrentTemplate == null || sender is not TextBox tb) return;

        if (ReferenceEquals(tb, TemplateNameBox)) ViewModel.CurrentTemplate.Name = TemplateNameBox.Text;
        else if (ReferenceEquals(tb, PrimaryColorBox)) ViewModel.CurrentTemplate.Styling.PrimaryColorHex = PrimaryColorBox.Text;
        else if (ReferenceEquals(tb, StoreNameBox)) ViewModel.CurrentTemplate.Header.StoreName = StoreNameBox.Text;
        else if (ReferenceEquals(tb, TaglineBox)) ViewModel.CurrentTemplate.Header.Tagline = TaglineBox.Text;
        else if (ReferenceEquals(tb, AddressLine1Box)) ViewModel.CurrentTemplate.Header.AddressLine1 = AddressLine1Box.Text;
        else if (ReferenceEquals(tb, AddressLine2Box)) ViewModel.CurrentTemplate.Header.AddressLine2 = AddressLine2Box.Text;
        else if (ReferenceEquals(tb, PhoneBox)) ViewModel.CurrentTemplate.Header.Phone = PhoneBox.Text;
        else if (ReferenceEquals(tb, EmailBox)) ViewModel.CurrentTemplate.Header.Email = EmailBox.Text;
        else if (ReferenceEquals(tb, GstinBox)) ViewModel.CurrentTemplate.Header.Gstin = GstinBox.Text;
        else if (ReferenceEquals(tb, DlNumbersBox)) ViewModel.CurrentTemplate.Header.DlNumbers = DlNumbersBox.Text;
        else if (ReferenceEquals(tb, FssaiBox)) ViewModel.CurrentTemplate.Header.Fssai = FssaiBox.Text;
        else if (ReferenceEquals(tb, SaleInvoiceTitleBox)) ViewModel.CurrentTemplate.Header.SaleInvoiceTitle = SaleInvoiceTitleBox.Text;
        else if (ReferenceEquals(tb, TermsAndConditionsBox)) ViewModel.CurrentTemplate.Footer.TermsAndConditionsText = TermsAndConditionsBox.Text;
        else if (ReferenceEquals(tb, GreetingBox)) ViewModel.CurrentTemplate.Footer.GreetingText = GreetingBox.Text;
        else if (ReferenceEquals(tb, PharmacistSignTitleBox)) ViewModel.CurrentTemplate.Signatures.PharmacistSignTitle = PharmacistSignTitleBox.Text;
        else if (ReferenceEquals(tb, AuthorizedSignTitleBox)) ViewModel.CurrentTemplate.Signatures.AuthorizedSignTitle = AuthorizedSignTitleBox.Text;
    }

    private async Task RequestLivePreviewUpdateAsync(int delayMs = 60)
    {
        _previewDebounceCts?.Cancel();
        _previewDebounceCts = new CancellationTokenSource();
        var token = _previewDebounceCts.Token;

        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, token);
            }
            if (token.IsCancellationRequested) return;

            await ViewModel.RefreshLivePreviewAsync();
            UpdateWebViewContent();
        }
        catch (TaskCanceledException) { }
    }

    private void OnControlChanged(object sender, RoutedEventArgs e)
    {
        SyncTextBoxToTemplate(sender);
        _ = RequestLivePreviewUpdateAsync(60);
    }

    private void OnColumnHeaderTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is BillColumnConfig col)
        {
            col.HeaderTitle = tb.Text;
        }
        _ = RequestLivePreviewUpdateAsync(60);
    }

    private void OnColumnToggled(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is BillColumnConfig col)
        {
            col.IsVisible = cb.IsChecked == true;
        }
        _ = RequestLivePreviewUpdateAsync(0);
    }

    private void MoveColUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is BillColumnConfig col)
        {
            ViewModel.MoveColumnUp(col);
            _ = RequestLivePreviewUpdateAsync(0);
        }
    }

    private void MoveColDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is BillColumnConfig col)
        {
            ViewModel.MoveColumnDown(col);
            _ = RequestLivePreviewUpdateAsync(0);
        }
    }

    private async void PresetA4_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyPresetByIdAsync("preset_a4_standard");
        this.Bindings.Update();
        UpdateWebViewContent();
    }

    private async void PresetA4Modern_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyPresetByIdAsync("preset_a4_modern");
        this.Bindings.Update();
        UpdateWebViewContent();
    }

    private async void PresetA4Wholesale_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyPresetByIdAsync("preset_a4_wholesale");
        this.Bindings.Update();
        UpdateWebViewContent();
    }

    private async void PresetA5_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyPresetByIdAsync("preset_a5_compact");
        this.Bindings.Update();
        UpdateWebViewContent();
    }

    private async void PresetA5Rx_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyPresetByIdAsync("preset_a5_portrait");
        this.Bindings.Update();
        UpdateWebViewContent();
    }

    private async void Preset80mm_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyPresetByIdAsync("preset_thermal_80mm");
        this.Bindings.Update();
        UpdateWebViewContent();
    }

    private async void Preset80mmModern_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyPresetByIdAsync("preset_thermal_80mm_modern");
        this.Bindings.Update();
        UpdateWebViewContent();
    }

    private async void Preset58mm_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyPresetByIdAsync("preset_thermal_58mm");
        this.Bindings.Update();
        UpdateWebViewContent();
    }

    private async void RefreshPreview_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshLivePreviewAsync();
        UpdateWebViewContent();
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var json = ViewModel.ExportTemplateAsJson();
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(json);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            ViewModel.StatusMessage = "✅ Template JSON copied to clipboard!";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Export error: {ex.Message}";
        }
    }

    private async void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        var inputTextBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 280,
            FontSize = 12,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Courier New, monospace"),
            PlaceholderText = "Paste bill template JSON here, or choose a .json file..."
        };

        var dialogStack = new StackPanel { Spacing = 10, Width = 520 };
        dialogStack.Children.Add(new TextBlock
        {
            Text = "Paste your custom bill template JSON below to import into your library:",
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextSecondary"]
        });
        dialogStack.Children.Add(inputTextBox);

        var dialog = new ContentDialog
        {
            Title = "📥 Import Bill Template JSON",
            Content = dialogStack,
            PrimaryButtonText = "Import Template",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputTextBox.Text))
        {
            var success = await ViewModel.ImportTemplateFromJsonAsync(inputTextBox.Text.Trim());
            if (success)
            {
                this.Bindings.Update();
                UpdateWebViewContent();
            }
        }
    }
}
