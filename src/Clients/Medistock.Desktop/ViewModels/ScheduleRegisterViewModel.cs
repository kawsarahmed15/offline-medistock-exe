using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Compliance.Services;
using Medistock.Application.Inventory.DTOs;
using Medistock.Domain.Common;

namespace Medistock.Desktop.ViewModels;

public partial class ScheduleRegisterItemViewModel : ObservableObject
{
    public string Id { get; }
    public string InvoiceNo { get; }
    public DateTime SaleDate { get; }
    public string ProductName { get; }
    public DrugSchedule Schedule { get; }
    public string BatchNumber { get; }
    public DateTime ExpiryDate { get; }
    public decimal Quantity { get; }
    public string PatientName { get; }
    public string PatientPhone { get; }
    public string PatientAddress { get; }
    public string DoctorName { get; }
    public string DoctorRegNo { get; }
    public string PrescriptionRef { get; }
    public DateTime? PrescriptionDate { get; }
    public string DispensedByUserId { get; }

    public string SaleDateFormatted => SaleDate.ToString("dd-MMM-yyyy hh:mm tt");
    public string ExpiryFormatted => ExpiryDate.ToString("MM/yyyy");
    public string ScheduleBadgeText => Schedule switch
    {
        DrugSchedule.ScheduleH => "Schedule H",
        DrugSchedule.ScheduleH1 => "Schedule H1",
        DrugSchedule.ScheduleX_Narcotic => "Schedule X / Narcotic",
        _ => "OTC"
    };

    public ScheduleRegisterItemViewModel(ScheduleDrugRegisterDto dto)
    {
        Id = dto.Id;
        InvoiceNo = dto.InvoiceNo;
        SaleDate = dto.SaleDate;
        ProductName = dto.ProductName;
        Schedule = dto.Schedule;
        BatchNumber = dto.BatchNumber;
        ExpiryDate = dto.ExpiryDate;
        Quantity = dto.Quantity;
        PatientName = dto.PatientName;
        PatientPhone = dto.PatientPhone ?? "-";
        PatientAddress = dto.PatientAddress ?? "-";
        DoctorName = dto.DoctorName;
        DoctorRegNo = dto.DoctorRegNo ?? "-";
        PrescriptionRef = dto.PrescriptionRef ?? "-";
        PrescriptionDate = dto.PrescriptionDate;
        DispensedByUserId = dto.DispensedByUserId;
    }
}

public partial class ScheduleRegisterViewModel : ObservableObject
{
    private readonly IScheduleDrugService _scheduleDrugService;

#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private DrugSchedule? _selectedSchedule;

    [ObservableProperty]
    private DateTimeOffset? _startDate;

    [ObservableProperty]
    private DateTimeOffset? _endDate;

    [ObservableProperty]
    private int _totalRecordsCount;
#pragma warning restore MVVMTK0045

    public ObservableCollection<ScheduleRegisterItemViewModel> RegisterEntries { get; } = new();

    public ScheduleRegisterViewModel(IScheduleDrugService scheduleDrugService)
    {
        _scheduleDrugService = scheduleDrugService;
        _startDate = DateTimeOffset.UtcNow.AddMonths(-1);
        _endDate = DateTimeOffset.UtcNow;
    }

    [RelayCommand]
    public async Task LoadRegisterAsync()
    {
        IsLoading = true;
        try
        {
            var filter = new ScheduleDrugFilter(
                StartDate: StartDate?.UtcDateTime,
                EndDate: EndDate?.UtcDateTime,
                Schedule: SelectedSchedule,
                SearchQuery: string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery,
                Limit: 200
            );

            var records = await _scheduleDrugService.GetRegisterAsync(filter);

            RegisterEntries.Clear();
            foreach (var r in records)
            {
                RegisterEntries.Add(new ScheduleRegisterItemViewModel(r));
            }

            TotalRecordsCount = RegisterEntries.Count;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
