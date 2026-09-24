using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Inventory.DTOs;
using Medistock.Application.Inventory.Services;
using Medistock.Domain.Common;

namespace Medistock.Desktop.ViewModels;

public partial class ExpiryDashboardViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly string _warehouseId = "WH-MAIN";

#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalActiveBatches;

    [ObservableProperty]
    private decimal _totalStockQuantity;

    [ObservableProperty]
    private decimal _totalInventoryValueCost;

    [ObservableProperty]
    private decimal _totalInventoryValueMrp;

    // Band counts
    [ObservableProperty]
    private int _expiredCount;
    [ObservableProperty]
    private decimal _expiredValue;

    [ObservableProperty]
    private int _criticalCount;
    [ObservableProperty]
    private decimal _criticalValue;

    [ObservableProperty]
    private int _warningCount;
    [ObservableProperty]
    private decimal _warningValue;

    [ObservableProperty]
    private int _goodCount;
    [ObservableProperty]
    private decimal _goodValue;
#pragma warning restore MVVMTK0045

    public ObservableCollection<StockItemViewModel> UrgentExpiryBatches { get; } = new();

    public ExpiryDashboardViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        IsLoading = true;
        try
        {
            var dashboard = await _inventoryService.GetExpiryDashboardAsync(_warehouseId);

            TotalActiveBatches = dashboard.TotalActiveBatches;
            TotalStockQuantity = dashboard.TotalStockQuantity;
            TotalInventoryValueCost = dashboard.TotalInventoryValueCost;
            TotalInventoryValueMrp = dashboard.TotalInventoryValueMrp;

            ExpiredCount = dashboard.ExpiredBand.BatchCount;
            ExpiredValue = dashboard.ExpiredBand.TotalValueAtRisk;

            CriticalCount = dashboard.CriticalBand.BatchCount;
            CriticalValue = dashboard.CriticalBand.TotalValueAtRisk;

            WarningCount = dashboard.WarningBand.BatchCount;
            WarningValue = dashboard.WarningBand.TotalValueAtRisk;

            GoodCount = dashboard.GoodBand.BatchCount;
            GoodValue = dashboard.GoodBand.TotalValueAtRisk;

            UrgentExpiryBatches.Clear();
            foreach (var b in dashboard.ExpiredBand.TopBatches)
            {
                UrgentExpiryBatches.Add(new StockItemViewModel(b));
            }
            foreach (var b in dashboard.CriticalBand.TopBatches)
            {
                UrgentExpiryBatches.Add(new StockItemViewModel(b));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
