using System;
using System.IO;
using System.Threading.Tasks;
using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Sales;

public sealed partial class PrintPreviewDialog : ContentDialog
{
    public PrintPreviewViewModel ViewModel { get; }

    public PrintPreviewDialog(PrintPreviewViewModel viewModel)
    {
        this.InitializeComponent();
        ViewModel = viewModel;
        ViewModel.OnRequestSavePdf += HandleSavePdfAsync;

        this.Loaded += PrintPreviewDialog_Loaded;
    }

    private async void PrintPreviewDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async();
            UpdateWebViewContent();
        }
        catch { }
    }

    private void UpdateWebViewContent()
    {
        if (PreviewWebView.CoreWebView2 != null && !string.IsNullOrWhiteSpace(ViewModel.CurrentHtmlContent))
        {
            PreviewWebView.NavigateToString(ViewModel.CurrentHtmlContent);
        }
    }

    private async void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateComboBox.SelectedItem is Contracts.Printing.BillTemplateConfig template)
        {
            await ViewModel.SwitchTemplateAsync(template);
            UpdateWebViewContent();
        }
    }

    private async void PrintButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true; // Keep dialog open while printing
        await ViewModel.PrintAsync();
    }

    private async void DownloadPdfButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true; // Keep dialog open while saving

        try
        {
            var invoiceNo = ViewModel.CurrentReceipt?.InvoiceNo ?? "Bill";
            var cleanInvoiceNo = string.Join("_", invoiceNo.Split(Path.GetInvalidFileNameChars()));
            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var targetPath = Path.Combine(docsPath, $"{cleanInvoiceNo}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            var success = await HandleSavePdfAsync(targetPath);
            if (success)
            {
                ViewModel.StatusMessage = $"✅ PDF exported successfully to: {targetPath}";
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(targetPath) { UseShellExecute = true });
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"PDF export error: {ex.Message}";
        }
    }

    private async Task<bool> HandleSavePdfAsync(string targetFilePath)
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async();
            if (PreviewWebView.CoreWebView2 != null)
            {
                var printSettings = PreviewWebView.CoreWebView2.Environment.CreatePrintSettings();
                printSettings.ShouldPrintBackgrounds = true;
                printSettings.Orientation = Microsoft.Web.WebView2.Core.CoreWebView2PrintOrientation.Portrait;

                var result = await PreviewWebView.CoreWebView2.PrintToPdfAsync(targetFilePath, printSettings);
                return result;
            }
        }
        catch { }
        return false;
    }
}
