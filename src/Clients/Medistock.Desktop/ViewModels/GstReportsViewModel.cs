#pragma warning disable MVVMTK0045

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Compliance.DTOs;
using Medistock.Application.Compliance.Services;

namespace Medistock.Desktop.ViewModels;

public partial class GstReportsViewModel : ObservableObject
{
    private readonly IGstReportService _gstService;

    [ObservableProperty]
    private string _statusMessage = "Ready. Select filing period.";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _selectedYear = DateTime.Now.Year;

    [ObservableProperty]
    private int _selectedMonth = DateTime.Now.Month;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private Gstr1ReportDto? _gstr1;

    [ObservableProperty]
    private Gstr2ReportDto? _gstr2;

    [ObservableProperty]
    private Gstr3bReportDto? _gstr3b;

    [ObservableProperty]
    private string _lastExportedJsonPath = string.Empty;

    public ObservableCollection<int> AvailableYears { get; } = new() { 2025, 2026, 2027 };
    public ObservableCollection<string> MonthNames { get; } = new()
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    public GstReportsViewModel(IGstReportService gstService)
    {
        _gstService = gstService;
        _ = LoadReportsAsync();
    }

    [RelayCommand]
    public async Task LoadReportsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = $"Generating GSTR reports for {SelectedMonth:D2}/{SelectedYear}...";

            var fromDate = new DateTime(SelectedYear, SelectedMonth, 1);
            var toDate = fromDate.AddMonths(1).AddTicks(-1);

            var filter = new GstPeriodFilter(
                OrgId: "org-1",
                BranchId: "branch-1",
                Year: SelectedYear,
                Month: SelectedMonth,
                FromDate: fromDate,
                ToDate: toDate
            );

            Gstr1 = await _gstService.GenerateGstr1Async(filter);
            Gstr2 = await _gstService.GenerateGstr2Async(filter);
            Gstr3b = await _gstService.GenerateGstr3bAsync(filter);

            StatusMessage = $"GSTR reports generated for {MonthNames[SelectedMonth - 1]} {SelectedYear}. Net Cash Tax Payable: ₹{Gstr3b.NetTotalPayable:N2}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating reports: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ExportGstr1JsonAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Exporting GSTN-compliant JSON...";

            var fromDate = new DateTime(SelectedYear, SelectedMonth, 1);
            var toDate = fromDate.AddMonths(1).AddTicks(-1);

            var filter = new GstPeriodFilter(
                OrgId: "org-1",
                BranchId: "branch-1",
                Year: SelectedYear,
                Month: SelectedMonth,
                FromDate: fromDate,
                ToDate: toDate
            );

            var json = await _gstService.ExportGstr1JsonAsync(filter);
            var fileName = $"GSTR1_{Gstr1?.Gstin ?? "GSTIN"}_{SelectedMonth:D2}{SelectedYear}.json";
            var exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            await File.WriteAllTextAsync(exportPath, json);

            LastExportedJsonPath = exportPath;
            StatusMessage = $"Exported GSTN JSON to: {fileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting JSON: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
