using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.Printing;
using Medistock.Infrastructure.Hardware;
using Medistock.Infrastructure.Hardware.Printers;

namespace Medistock.Desktop.ViewModels;

public partial class BillCustomizerViewModel : ObservableObject
{
    private readonly IBillTemplateRepository _templateRepo;
    private readonly IBillDocumentGenerator _generator;

    [ObservableProperty]
    private BillTemplateConfig? _currentTemplate;

    [ObservableProperty]
    private string _livePreviewHtml = "<html><body><h3>Generating Live Preview...</h3></body></html>";

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<BillTemplateConfig> SavedTemplates { get; } = new();
    public ObservableCollection<BillColumnConfig> EditableColumns { get; } = new();

    // Sample receipt used for live preview rendering
    private static readonly SaleReceiptModel SampleReceipt = new(
        PharmacyName: "APEX PHARMACY & WELLNESS",
        PharmacyAddress: "Shop 101, Ground Floor, Royal Complex, MG Road, Mumbai",
        PharmacyPhone: "+91 98200 54321 / 022-28004455",
        Gstin: "27ABCDE1234F1Z5",
        DlNumbers: "MH-MZ2-445566, MH-MZ2-445567 (20B/21B)",
        InvoiceNo: "INV-2026-0142",
        InvoiceDate: DateTime.Now,
        CounterName: "Counter 1",
        CashierName: "Dr. Sandeep Mehta (Pharmacist)",
        CustomerName: "Vikram Malhotra",
        DoctorName: "Dr. Arvind Rao (MD, DNB)",
        Items: new List<ReceiptItemModel>
        {
            new("Dolo 650mg Tablet", "BT9921", DateTime.Now.AddMonths(18), 2, 33.60m, 67.20m, 12.0m),
            new("Pan D Capsule 10s", "PD1029", DateTime.Now.AddMonths(24), 1, 198.00m, 198.00m, 12.0m),
            new("Augmentin 625 Duo", "AUG881", DateTime.Now.AddMonths(12), 1, 223.50m, 223.50m, 12.0m),
            new("Azithral 500mg Tab", "AZ502", DateTime.Now.AddMonths(20), 1, 132.00m, 132.00m, 12.0m)
        },
        Subtotal: 554.46m,
        CgstAmount: 33.12m,
        SgstAmount: 33.12m,
        IgstAmount: 0m,
        RoundOff: 0.30m,
        GrandTotal: 621.00m,
        Payments: new List<ReceiptPaymentModel>
        {
            new("UPI / GPay", 621.00m, "UPI/20260927/9944")
        }
    );

    public BillCustomizerViewModel(
        IBillTemplateRepository templateRepo,
        IBillDocumentGenerator generator)
    {
        _templateRepo = templateRepo;
        _generator = generator;
        _currentTemplate = BillTemplatePresets.CreateA4StandardPreset();
        SyncColumnsCollection();
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading template configuration...";

        try
        {
            var list = await _templateRepo.ListAllTemplatesAsync();
            SavedTemplates.Clear();
            foreach (var t in list) SavedTemplates.Add(t);

            var defaultT = await _templateRepo.GetDefaultTemplateAsync();
            CurrentTemplate = SavedTemplates.FirstOrDefault(t => t.Id == defaultT.Id) ?? SavedTemplates.FirstOrDefault() ?? BillTemplatePresets.CreateA4StandardPreset();

            SyncColumnsCollection();
            await RefreshLivePreviewAsync();
            StatusMessage = $"Loaded '{CurrentTemplate.Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading templates: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SyncColumnsCollection()
    {
        if (CurrentTemplate == null) return;
        EditableColumns.Clear();
        foreach (var c in CurrentTemplate.Columns.OrderBy(c => c.DisplayOrder))
        {
            EditableColumns.Add(c);
        }
    }

    public async Task RefreshLivePreviewAsync()
    {
        if (CurrentTemplate == null) return;

        try
        {
            LivePreviewHtml = await _generator.GenerateHtmlBillAsync(SampleReceipt, CurrentTemplate);
        }
        catch (Exception ex)
        {
            LivePreviewHtml = $"<html><body><p style='color:red;'>Preview generation error: {ex.Message}</p></body></html>";
        }
    }

    [RelayCommand]
    public async Task SelectTemplateAsync(BillTemplateConfig template)
    {
        CurrentTemplate = template;
        SyncColumnsCollection();
        await RefreshLivePreviewAsync();
        StatusMessage = $"Switched to template: '{template.Name}'.";
    }

    [RelayCommand]
    public async Task ApplyPresetByIdAsync(string presetId)
    {
        var preset = BillTemplatePresets.GetAllPresets().FirstOrDefault(p => p.Id == presetId)
                     ?? BillTemplatePresets.CreateA4StandardPreset();

        if (CurrentTemplate != null)
        {
            preset.Id = CurrentTemplate.Id;
            preset.Name = CurrentTemplate.Name;
            preset.IsDefault = CurrentTemplate.IsDefault;
        }

        CurrentTemplate = preset;
        SyncColumnsCollection();
        await RefreshLivePreviewAsync();
        StatusMessage = $"Applied preset layout: '{preset.Name}'.";
    }

    [RelayCommand]
    public async Task ApplyPresetAsync(PaperSize paperSize)
    {
        BillTemplateConfig preset = paperSize switch
        {
            PaperSize.A5_Landscape => BillTemplatePresets.CreateA5CompactPreset(),
            PaperSize.A5_Portrait => BillTemplatePresets.CreateA5RxPortraitPreset(),
            PaperSize.Thermal_80mm => BillTemplatePresets.CreateThermal80mmPreset(),
            PaperSize.Thermal_58mm => BillTemplatePresets.CreateThermal58mmPreset(),
            _ => BillTemplatePresets.CreateA4StandardPreset()
        };

        if (CurrentTemplate != null)
        {
            preset.Id = CurrentTemplate.Id;
            preset.Name = CurrentTemplate.Name;
            preset.IsDefault = CurrentTemplate.IsDefault;
        }

        CurrentTemplate = preset;
        SyncColumnsCollection();
        await RefreshLivePreviewAsync();
        StatusMessage = $"Applied preset layout for {paperSize}.";
    }

    [RelayCommand]
    public async Task SaveTemplateAsync()
    {
        if (CurrentTemplate == null) return;

        IsBusy = true;
        StatusMessage = "Saving template...";

        try
        {
            // Sync column order
            for (int i = 0; i < EditableColumns.Count; i++)
            {
                EditableColumns[i].DisplayOrder = i + 1;
            }
            CurrentTemplate.Columns = EditableColumns.ToList();

            await _templateRepo.SaveTemplateAsync(CurrentTemplate);

            var list = await _templateRepo.ListAllTemplatesAsync();
            SavedTemplates.Clear();
            foreach (var t in list) SavedTemplates.Add(t);

            await RefreshLivePreviewAsync();
            StatusMessage = $"✅ Template '{CurrentTemplate.Name}' saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error saving template: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SetAsDefaultAsync()
    {
        if (CurrentTemplate == null) return;

        try
        {
            await _templateRepo.SetDefaultTemplateAsync(CurrentTemplate.Id);
            foreach (var t in SavedTemplates) t.IsDefault = (t.Id == CurrentTemplate.Id);
            CurrentTemplate.IsDefault = true;
            StatusMessage = $"✅ '{CurrentTemplate.Name}' set as default bill template.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error setting default: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DuplicateCurrentTemplateAsync()
    {
        if (CurrentTemplate == null) return;

        var json = ExportTemplateAsJson();
        var copy = BillTemplateJsonSerializer.Deserialize(json);
        if (copy != null)
        {
            copy.Id = Guid.NewGuid().ToString();
            copy.Name = $"{CurrentTemplate.Name} (Copy)";
            copy.IsDefault = false;

            await _templateRepo.SaveTemplateAsync(copy);

            var list = await _templateRepo.ListAllTemplatesAsync();
            SavedTemplates.Clear();
            foreach (var t in list) SavedTemplates.Add(t);

            CurrentTemplate = SavedTemplates.FirstOrDefault(t => t.Id == copy.Id) ?? copy;
            SyncColumnsCollection();
            await RefreshLivePreviewAsync();
            StatusMessage = $"✅ Created copy: '{copy.Name}'.";
        }
    }

    [RelayCommand]
    public async Task DeleteCurrentTemplateAsync()
    {
        if (CurrentTemplate == null) return;

        if (SavedTemplates.Count <= 1)
        {
            StatusMessage = "Cannot delete the only remaining template.";
            return;
        }

        try
        {
            var idToDelete = CurrentTemplate.Id;
            await _templateRepo.DeleteTemplateAsync(idToDelete);

            var list = await _templateRepo.ListAllTemplatesAsync();
            SavedTemplates.Clear();
            foreach (var t in list) SavedTemplates.Add(t);

            var next = SavedTemplates.FirstOrDefault(t => t.IsDefault) ?? SavedTemplates.FirstOrDefault() ?? BillTemplatePresets.CreateA4StandardPreset();
            CurrentTemplate = next;
            SyncColumnsCollection();
            await RefreshLivePreviewAsync();
            StatusMessage = "Template deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CreateNewTemplateAsync(string templateName)
    {
        var newTemplate = BillTemplatePresets.CreateA4StandardPreset();
        newTemplate.Id = Guid.NewGuid().ToString();
        newTemplate.Name = string.IsNullOrWhiteSpace(templateName) ? "Custom Pharmacy Invoice" : templateName;
        newTemplate.IsDefault = false;

        CurrentTemplate = newTemplate;
        SyncColumnsCollection();
        await SaveTemplateAsync();
        StatusMessage = $"Created new template: '{newTemplate.Name}'.";
    }

    public void MoveColumnUp(BillColumnConfig column)
    {
        int index = EditableColumns.IndexOf(column);
        if (index > 0)
        {
            EditableColumns.Move(index, index - 1);
            for (int i = 0; i < EditableColumns.Count; i++)
            {
                EditableColumns[i].DisplayOrder = i + 1;
            }
            if (CurrentTemplate != null)
            {
                CurrentTemplate.Columns = EditableColumns.ToList();
            }
            _ = RefreshLivePreviewAsync();
        }
    }

    public void MoveColumnDown(BillColumnConfig column)
    {
        int index = EditableColumns.IndexOf(column);
        if (index >= 0 && index < EditableColumns.Count - 1)
        {
            EditableColumns.Move(index, index + 1);
            for (int i = 0; i < EditableColumns.Count; i++)
            {
                EditableColumns[i].DisplayOrder = i + 1;
            }
            if (CurrentTemplate != null)
            {
                CurrentTemplate.Columns = EditableColumns.ToList();
            }
            _ = RefreshLivePreviewAsync();
        }
    }

    public string ExportTemplateAsJson()
    {
        if (CurrentTemplate == null) return "{}";
        return BillTemplateJsonSerializer.Serialize(CurrentTemplate);
    }

    public async Task<bool> ImportTemplateFromJsonAsync(string json)
    {
        try
        {
            var template = BillTemplateJsonSerializer.Deserialize(json);
            if (template != null)
            {
                template.Id = Guid.NewGuid().ToString();
                if (string.IsNullOrWhiteSpace(template.Name)) template.Name = "Imported Template";
                template.IsDefault = false;

                // Ensure non-null collections and configs
                template.Styling ??= new BillStyleConfig();
                template.Header ??= new BillHeaderConfig();
                template.Metadata ??= new BillMetadataConfig();
                template.Columns ??= BillTemplatePresets.GetAllAvailableColumns();
                template.TaxSummary ??= new BillTaxSummaryConfig();
                template.Footer ??= new BillFooterConfig();
                template.Signatures ??= new BillSignatureConfig();

                await _templateRepo.SaveTemplateAsync(template);

                var list = await _templateRepo.ListAllTemplatesAsync();
                SavedTemplates.Clear();
                foreach (var t in list) SavedTemplates.Add(t);

                CurrentTemplate = SavedTemplates.FirstOrDefault(t => t.Id == template.Id) ?? template;
                SyncColumnsCollection();
                await RefreshLivePreviewAsync();
                StatusMessage = $"✅ Successfully imported '{template.Name}'!";
                return true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
        }
        return false;
    }
}
