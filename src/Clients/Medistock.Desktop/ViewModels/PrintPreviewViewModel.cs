using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.Printing;
using Medistock.Infrastructure.Hardware;
using Medistock.Infrastructure.Hardware.Printers;

namespace Medistock.Desktop.ViewModels;

public partial class PrintPreviewViewModel : ObservableObject
{
    private readonly IBillDocumentGenerator _generator;
    private readonly IBillTemplateRepository _templateRepo;
    private readonly IHardwarePrinterService _printerService;

    [ObservableProperty]
    private string _currentHtmlContent = "<html><body><h3>Loading Preview...</h3></body></html>";

    [ObservableProperty]
    private BillTemplateConfig? _selectedTemplate;

    [ObservableProperty]
    private PrinterDeviceInfo? _selectedPrinter;

    [ObservableProperty]
    private int _copies = 1;

    [ObservableProperty]
    private string _statusMessage = "Ready to print or download PDF.";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<BillTemplateConfig> AvailableTemplates { get; } = new();
    public ObservableCollection<PrinterDeviceInfo> InstalledPrinters { get; } = new();

    public SaleReceiptModel? CurrentReceipt { get; private set; }

    public event Func<string, Task<bool>>? OnRequestSavePdf;

    public PrintPreviewViewModel(
        IBillDocumentGenerator generator,
        IBillTemplateRepository templateRepo,
        IHardwarePrinterService printerService)
    {
        _generator = generator;
        _templateRepo = templateRepo;
        _printerService = printerService;
    }

    public async Task InitializeAsync(SaleReceiptModel receipt)
    {
        CurrentReceipt = receipt;
        IsBusy = true;
        StatusMessage = "Loading templates and printers...";

        try
        {
            var templates = await _templateRepo.ListAllTemplatesAsync();
            AvailableTemplates.Clear();
            foreach (var t in templates) AvailableTemplates.Add(t);

            var defaultTemplate = await _templateRepo.GetDefaultTemplateAsync();
            SelectedTemplate = AvailableTemplates.FirstOrDefault(t => t.Id == defaultTemplate.Id) ?? AvailableTemplates.FirstOrDefault();

            var printers = await _printerService.GetInstalledPrintersAsync();
            InstalledPrinters.Clear();
            foreach (var p in printers) InstalledPrinters.Add(p);

            SelectedPrinter = InstalledPrinters.FirstOrDefault(p => p.IsDefault) ?? InstalledPrinters.FirstOrDefault();

            await RefreshPreviewAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading preview: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshPreviewAsync()
    {
        if (CurrentReceipt == null || SelectedTemplate == null) return;

        try
        {
            CurrentHtmlContent = await _generator.GenerateHtmlBillAsync(CurrentReceipt, SelectedTemplate);
            StatusMessage = $"Preview updated for {SelectedTemplate.Name} ({SelectedTemplate.PaperSize}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating preview: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SwitchTemplateAsync(BillTemplateConfig template)
    {
        SelectedTemplate = template;
        await RefreshPreviewAsync();
    }

    [RelayCommand]
    public async Task PrintAsync()
    {
        if (CurrentReceipt == null || SelectedTemplate == null) return;

        IsBusy = true;
        StatusMessage = "Sending bill to printer...";

        try
        {
            var printerName = SelectedPrinter?.Name;
            var success = await _printerService.PrintReceiptAsync(CurrentReceipt, SelectedTemplate, printerName);

            if (success)
            {
                StatusMessage = $"Successfully printed to '{printerName ?? "Default"}'.";
            }
            else
            {
                StatusMessage = $"Print dispatched. Verify printer queue for '{printerName}'.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DownloadPdfAsync(string targetFilePath)
    {
        if (OnRequestSavePdf != null && !string.IsNullOrWhiteSpace(targetFilePath))
        {
            IsBusy = true;
            StatusMessage = "Exporting PDF...";
            try
            {
                var success = await OnRequestSavePdf.Invoke(targetFilePath);
                StatusMessage = success ? $"PDF saved to: {targetFilePath}" : "PDF export failed.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"PDF export error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
