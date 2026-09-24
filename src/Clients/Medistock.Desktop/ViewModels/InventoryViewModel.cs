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

public partial class StockItemViewModel : ObservableObject
{
    public string ProductId { get; }
    public string ProductName { get; }
    public string GenericName { get; }
    public string SaltComposition { get; }
    public string Manufacturer { get; }
    public string CategoryName { get; }
    public DrugSchedule Schedule { get; }
    public string BatchId { get; }
    public string BatchNumber { get; }
    public DateTime ExpiryDate { get; }
    public int DaysUntilExpiry { get; }
    public ExpiryBand ExpiryStatus { get; }
    public decimal AvailableQuantity { get; set; }
    public decimal ReservedQuantity { get; }
    public decimal TotalQuantity { get; }
    public decimal Mrp { get; }
    public decimal PurchaseRate { get; }
    public decimal SaleRate { get; }
    public decimal StockValueAtMrp { get; }
    public decimal StockValueAtCost { get; }

    public string ExpiryText => ExpiryDate.ToString("MM/yyyy");
    public string ExpiryBadgeText => ExpiryStatus switch
    {
        ExpiryBand.Expired => "EXPIRED",
        ExpiryBand.Critical => $"CRITICAL ({DaysUntilExpiry}d)",
        ExpiryBand.Warning => $"WARNING ({DaysUntilExpiry}d)",
        _ => "GOOD"
    };

    public string ExpiryBadgeColor => ExpiryStatus switch
    {
        ExpiryBand.Expired => "#DC2626",   // Danger / Red
        ExpiryBand.Critical => "#EA580C",  // Deep Orange
        ExpiryBand.Warning => "#D97706",   // Amber / Warning
        _ => "#16A34A"                     // Pharmacy Green / Success
    };

    public string ScheduleBadgeText => Schedule switch
    {
        DrugSchedule.ScheduleH => "Sch-H",
        DrugSchedule.ScheduleH1 => "Sch-H1",
        DrugSchedule.ScheduleX_Narcotic => "Narcotic",
        _ => "OTC"
    };

    public bool IsScheduleDrug => Schedule != DrugSchedule.OTC;

    public StockItemViewModel(StockSummaryItemDto dto)
    {
        ProductId = dto.ProductId;
        ProductName = dto.ProductName;
        GenericName = dto.GenericName ?? "";
        SaltComposition = dto.SaltComposition ?? "";
        Manufacturer = dto.Manufacturer ?? "";
        CategoryName = dto.CategoryName ?? "General";
        Schedule = dto.Schedule;
        BatchId = dto.BatchId;
        BatchNumber = dto.BatchNumber;
        ExpiryDate = dto.ExpiryDate;
        DaysUntilExpiry = dto.DaysUntilExpiry;
        ExpiryStatus = dto.ExpiryStatus;
        AvailableQuantity = dto.AvailableQuantity;
        ReservedQuantity = dto.ReservedQuantity;
        TotalQuantity = dto.TotalQuantity;
        Mrp = dto.Mrp;
        PurchaseRate = dto.PurchaseRate;
        SaleRate = dto.SaleRate;
        StockValueAtMrp = dto.StockValueAtMrp;
        StockValueAtCost = dto.StockValueAtCost;
    }
}

public partial class InventoryViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly string _warehouseId = "WH-MAIN";

#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ExpiryBand? _selectedExpiryBand;

    [ObservableProperty]
    private DrugSchedule? _selectedSchedule;

    [ObservableProperty]
    private bool _lowStockOnly;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private StockItemViewModel? _selectedItem;

    [ObservableProperty]
    private int _totalItemsCount;

    [ObservableProperty]
    private decimal _totalStockValue;
#pragma warning restore MVVMTK0045

    public ObservableCollection<StockItemViewModel> StockItems { get; } = new();

    public InventoryViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [RelayCommand]
    public async Task LoadStocksAsync()
    {
        IsLoading = true;
        try
        {
            var results = await _inventoryService.GetStockSummaryAsync(
                warehouseId: _warehouseId,
                searchQuery: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                expiryBand: SelectedExpiryBand,
                schedule: SelectedSchedule,
                lowStockOnly: LowStockOnly,
                limit: 200
            );

            StockItems.Clear();
            foreach (var item in results)
            {
                StockItems.Add(new StockItemViewModel(item));
            }

            TotalItemsCount = StockItems.Count;
            TotalStockValue = StockItems.Sum(i => i.StockValueAtCost);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AdjustStockAsync((string BatchId, string ProductId, decimal Delta, string Reason) args)
    {
        var adjustmentType = args.Delta >= 0 ? StockAdjustmentType.Add : StockAdjustmentType.Reduce;
        var request = new StockAdjustmentRequest(
            OrgId: "ORG-01",
            BranchId: "BR-01",
            WarehouseId: _warehouseId,
            ProductId: args.ProductId,
            BatchId: args.BatchId,
            AdjustmentType: adjustmentType,
            Quantity: Math.Abs(args.Delta),
            Reason: args.Reason,
            UserId: "USER-01",
            DeviceId: "POS-01"
        );

        var result = await _inventoryService.AdjustStockAsync(request);
        if (result.Success)
        {
            await LoadStocksAsync();
        }
    }

    [RelayCommand]
    public async Task QuarantineExpiredBatchAsync(StockItemViewModel item)
    {
        var result = await _inventoryService.QuarantineExpiredBatchAsync(
            orgId: "ORG-01",
            branchId: "BR-01",
            warehouseId: _warehouseId,
            productId: item.ProductId,
            batchId: item.BatchId,
            currentQuantity: item.AvailableQuantity,
            reason: "Manual Quarantine / Expiry Write-Off",
            userId: "USER-01",
            deviceId: "POS-01"
        );

        if (result.Success)
        {
            await LoadStocksAsync();
        }
    }
}
