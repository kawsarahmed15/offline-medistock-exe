using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Inventory.DTOs;
using Medistock.Application.Inventory.Services;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Hardware.Export;

namespace Medistock.Desktop.ViewModels;

public partial class StockItemViewModel : ObservableObject
{
    public string ProductId { get; }
    public string ProductName { get; }
    public string GenericName { get; }
    public string SaltComposition { get; }
    public string Manufacturer { get; }
    public string CategoryName { get; }
    public string HsnCode { get; }
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
    public decimal GstRatePercent { get; }
    public decimal NetPurchaseRate { get; }
    public decimal SaleRate { get; }
    public decimal StockValueAtMrp { get; }
    public decimal StockValueAtCost { get; }
    public decimal MinStockAlert { get; }
    public int NearExpiryDays { get; set; }

    public bool IsExpired => ExpiryDate.Date <= DateTime.UtcNow.Date;
    public bool IsNearExpiry => !IsExpired && ExpiryDate.Date <= DateTime.UtcNow.AddDays(NearExpiryDays).Date;
    public bool IsOutOfStock => AvailableQuantity <= 0;
    public bool IsLowStock => !IsOutOfStock && AvailableQuantity <= MinStockAlert;

    public string ExpiryText => ExpiryDate.ToString("MM/yyyy");
    public string ExpiryBadgeText => IsExpired ? "EXPIRED" : (IsNearExpiry ? $"EXP NEAR ({DaysUntilExpiry}d)" : "GOOD");
    public string ExpiryBadgeColor => (IsExpired || IsNearExpiry) ? "#DC2626" : "#16A34A";
    public bool HasExpiryBadge => IsExpired || IsNearExpiry;

    public string StatusText => IsExpired ? "EXPIRED" : (IsNearExpiry ? "EXP NEAR" : (IsOutOfStock ? "OUT OF STOCK" : (IsLowStock ? "LOW STOCK" : "GOOD")));
    public string StatusBadgeBackgroundHex => (IsExpired || IsNearExpiry || IsOutOfStock) ? "#25DC2626" : (IsLowStock ? "#25D97706" : "#2516A34A");
    public string StatusBadgeForegroundHex => (IsExpired || IsNearExpiry || IsOutOfStock) ? "#DC2626" : (IsLowStock ? "#D97706" : "#16A34A");

    public string MrpFormatted => $"{Mrp:F2}";
    public string PurchaseRateFormatted => $"{PurchaseRate:F2}";
    public string NetPurchaseRateFormatted => $"{NetPurchaseRate:F2}";
    public string StockValueAtCostFormatted => $"{StockValueAtCost:F2}";

    public string StockDisplay => IsOutOfStock ? "0 (OOS)" : (IsLowStock ? $"{AvailableQuantity:0.#} (LOW)" : $"{AvailableQuantity:0.#}");
    public string StockForegroundHex => IsOutOfStock ? "#DC2626" : (IsLowStock ? "#D97706" : "#16A34A");
    public string ExpiryForegroundHex => (IsExpired || IsNearExpiry) ? "#DC2626" : "#64748B";

    public string RowBackgroundHex => IsExpired 
        ? "#25DC2626" 
        : (IsNearExpiry ? "#15DC2626" : "#00000000");

    public string RowBorderHex => IsExpired 
        ? "#DC2626" 
        : (IsNearExpiry ? "#80DC2626" : "#00000000");

    public string ScheduleBadgeText => Schedule switch
    {
        DrugSchedule.ScheduleH => "Sch-H",
        DrugSchedule.ScheduleH1 => "Sch-H1",
        DrugSchedule.ScheduleX_Narcotic => "Narcotic",
        _ => "OTC"
    };

    public bool IsScheduleDrug => Schedule != DrugSchedule.OTC;

    public StockItemViewModel(StockSummaryItemDto dto, int nearExpiryDays = 90)
    {
        ProductId = dto.ProductId;
        ProductName = dto.ProductName;
        GenericName = dto.GenericName ?? "";
        SaltComposition = dto.SaltComposition ?? "";
        Manufacturer = dto.Manufacturer ?? "";
        CategoryName = dto.CategoryName ?? "General";
        HsnCode = dto.HsnCode ?? "3004";
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
        GstRatePercent = dto.GstRatePercent;
        NetPurchaseRate = dto.NetPurchaseRate > 0 ? dto.NetPurchaseRate : Math.Round(dto.PurchaseRate * (1m + (dto.GstRatePercent / 100m)), 2, MidpointRounding.AwayFromZero);
        SaleRate = dto.SaleRate;
        StockValueAtMrp = dto.StockValueAtMrp;
        StockValueAtCost = dto.StockValueAtCost;
        MinStockAlert = dto.MinStockAlert > 0 ? dto.MinStockAlert : 10.0m;
        NearExpiryDays = nearExpiryDays;
    }
}

public partial class InventoryViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IInventoryExportService? _exportService;
    private readonly string _warehouseId = "wh-1";
    private readonly List<StockSummaryItemDto> _allLoadedDtoItems = new();

#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilterTab = "All";

    [ObservableProperty]
    private ExpiryBand? _selectedExpiryBand;

    [ObservableProperty]
    private DrugSchedule? _selectedSchedule;

    [ObservableProperty]
    private bool _lowStockOnly;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private StockItemViewModel? _selectedItem;

    [ObservableProperty]
    private int _totalItemsCount;

    [ObservableProperty]
    private int _totalProductsAvailable;

    [ObservableProperty]
    private int _expiryNearProductsCount;

    [ObservableProperty]
    private int _lowStockProductsCount;

    [ObservableProperty]
    private decimal _totalStockValue;

    [ObservableProperty]
    private decimal _totalStockValueMrp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportMessage))]
    private string _exportStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isEditProductModalOpen;

    [ObservableProperty]
    private string _editProductId = string.Empty;

    [ObservableProperty]
    private string _editProductName = string.Empty;

    [ObservableProperty]
    private string _editGenericName = string.Empty;

    [ObservableProperty]
    private string _editSaltComposition = string.Empty;

    [ObservableProperty]
    private string _editManufacturer = string.Empty;

    [ObservableProperty]
    private string _editCategoryName = string.Empty;

    [ObservableProperty]
    private string _editHsnCode = string.Empty;

    [ObservableProperty]
    private decimal _editGstRatePercent = 12.0m;

    [ObservableProperty]
    private int _editScheduleIndex = 0;

    [ObservableProperty]
    private decimal _editMinStockAlert = 10.0m;

    [ObservableProperty]
    private string _editBatchId = string.Empty;

    [ObservableProperty]
    private string _editBatchNumber = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _editExpiryDate = DateTimeOffset.UtcNow.AddYears(1);

    [ObservableProperty]
    private decimal _editMrp;

    [ObservableProperty]
    private decimal _editPurchaseRate;

    [ObservableProperty]
    private decimal _editSaleRate;

    [ObservableProperty]
    private string _editErrorMessage = string.Empty;

    [ObservableProperty]
    private bool _isSavingProduct;
#pragma warning restore MVVMTK0045

    public string TotalBatchesCountDisplay => $"({TotalItemsCount} batches)";
    public string TotalStockValueFormatted => $"{TotalStockValue:N2}";
    public string TotalStockValueMrpFormatted => $"MRP Val: ₹{TotalStockValueMrp:N2}";

    public bool HasExportMessage => !string.IsNullOrWhiteSpace(ExportStatusMessage);

    public ObservableCollection<StockItemViewModel> StockItems { get; } = new();

    public InventoryViewModel(IInventoryService inventoryService, IInventoryExportService? exportService = null)
    {
        _inventoryService = inventoryService;
        _exportService = exportService;
    }

    [RelayCommand]
    public async Task LoadStocksAsync()
    {
        IsLoading = true;
        ExportStatusMessage = string.Empty;
        try
        {
            var results = await _inventoryService.GetStockSummaryAsync(
                warehouseId: _warehouseId,
                searchQuery: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                expiryBand: SelectedExpiryBand,
                schedule: SelectedSchedule,
                lowStockOnly: LowStockOnly,
                limit: 1000
            );

            _allLoadedDtoItems.Clear();
            _allLoadedDtoItems.AddRange(results);

            ApplyFilterAndDisplay();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void SelectFilterTab(string filterTab)
    {
        SelectedFilterTab = filterTab;
        ApplyFilterAndDisplay();
    }

    private void ApplyFilterAndDisplay()
    {
        var nearExpiryDays = SettingsViewModel.GetNearExpiryDays();
        var allViewModels = _allLoadedDtoItems.Select(item => new StockItemViewModel(item, nearExpiryDays)).ToList();

        // Calculate KPI totals across the entire dataset
        TotalItemsCount = allViewModels.Count;
        TotalProductsAvailable = allViewModels.Select(i => i.ProductId).Distinct().Count();
        ExpiryNearProductsCount = allViewModels.Count(i => i.IsExpired || i.IsNearExpiry);
        LowStockProductsCount = allViewModels.Count(i => i.IsLowStock || i.IsOutOfStock);
        TotalStockValue = allViewModels.Sum(i => i.StockValueAtCost);
        TotalStockValueMrp = allViewModels.Sum(i => i.StockValueAtMrp);

        OnPropertyChanged(nameof(TotalBatchesCountDisplay));
        OnPropertyChanged(nameof(TotalStockValueFormatted));
        OnPropertyChanged(nameof(TotalStockValueMrpFormatted));

        // Filter for display
        IEnumerable<StockItemViewModel> filtered = allViewModels;
        if (SelectedFilterTab == "NearExpiry")
        {
            filtered = filtered.Where(i => i.IsNearExpiry);
        }
        else if (SelectedFilterTab == "Expired")
        {
            filtered = filtered.Where(i => i.IsExpired);
        }
        else if (SelectedFilterTab == "LowStock")
        {
            filtered = filtered.Where(i => i.IsLowStock || i.IsOutOfStock);
        }
        else if (SelectedFilterTab == "ScheduleDrugs")
        {
            filtered = filtered.Where(i => i.IsScheduleDrug);
        }

        StockItems.Clear();
        foreach (var item in filtered)
        {
            StockItems.Add(item);
        }
    }

    private (IReadOnlyList<InventoryExportRow> Rows, InventoryExportMetadata Meta) PrepareExportData()
    {
        var store = SettingsViewModel.GetStoreInfo();
        var rows = StockItems.Select((item, idx) => new InventoryExportRow(
            Index: idx + 1,
            ProductName: item.ProductName,
            GenericName: item.GenericName,
            SaltComposition: item.SaltComposition,
            BatchNumber: item.BatchNumber,
            ExpiryDate: item.ExpiryDate,
            DaysUntilExpiry: item.DaysUntilExpiry,
            Schedule: item.ScheduleBadgeText,
            AvailableQuantity: item.AvailableQuantity,
            MinStockAlert: item.MinStockAlert,
            Mrp: item.Mrp,
            PurchaseRate: item.PurchaseRate,
            StockValueAtCost: item.StockValueAtCost,
            StockValueAtMrp: item.StockValueAtMrp,
            StatusText: item.StatusText,
            IsExpired: item.IsExpired,
            IsNearExpiry: item.IsNearExpiry,
            IsLowStock: item.IsLowStock,
            IsOutOfStock: item.IsOutOfStock
        )).ToList();

        var meta = new InventoryExportMetadata(
            PharmacyName: store.PharmacyName,
            StoreAddress: store.StoreAddress,
            ContactPhone: store.ContactPhone,
            Gstin: store.Gstin,
            ExportDate: DateTime.Now,
            TotalProducts: TotalProductsAvailable,
            TotalBatches: TotalItemsCount,
            NearExpiryCount: ExpiryNearProductsCount,
            LowStockCount: LowStockProductsCount,
            TotalStockValueCost: TotalStockValue,
            TotalStockValueMrp: TotalStockValueMrp
        );

        return (rows, meta);
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        if (StockItems.Count == 0)
        {
            ExportStatusMessage = "No stock records to export.";
            return;
        }

        IsExporting = true;
        try
        {
            var (rows, meta) = PrepareExportData();
            var service = _exportService ?? new InventoryExportService();
            var path = await service.ExportToExcelAsync(rows, meta);

            ExportStatusMessage = $"✅ Excel exported successfully: {path}";
            TryOpenFile(path);
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"❌ Excel export failed: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    public async Task ExportWordAsync()
    {
        if (StockItems.Count == 0)
        {
            ExportStatusMessage = "No stock records to export.";
            return;
        }

        IsExporting = true;
        try
        {
            var (rows, meta) = PrepareExportData();
            var service = _exportService ?? new InventoryExportService();
            var path = await service.ExportToWordAsync(rows, meta);

            ExportStatusMessage = $"✅ Word document exported successfully: {path}";
            TryOpenFile(path);
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"❌ Word export failed: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        if (StockItems.Count == 0)
        {
            ExportStatusMessage = "No stock records to export.";
            return;
        }

        IsExporting = true;
        try
        {
            var (rows, meta) = PrepareExportData();
            var service = _exportService ?? new InventoryExportService();
            var path = await service.ExportToPdfHtmlAsync(rows, meta);

            ExportStatusMessage = $"✅ Report exported successfully: {path}";
            TryOpenFile(path);
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"❌ Export failed: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private static void TryOpenFile(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch { }
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

    [RelayCommand]
    public void OpenEditProduct(StockItemViewModel? item)
    {
        if (item == null) return;

        EditProductId = item.ProductId;
        EditProductName = item.ProductName;
        EditGenericName = item.GenericName;
        EditSaltComposition = item.SaltComposition;
        EditManufacturer = item.Manufacturer;
        EditCategoryName = item.CategoryName;
        EditHsnCode = item.HsnCode;
        EditGstRatePercent = item.GstRatePercent;
        EditScheduleIndex = (int)item.Schedule;
        EditMinStockAlert = item.MinStockAlert;

        EditBatchId = item.BatchId;
        EditBatchNumber = item.BatchNumber;
        EditExpiryDate = item.ExpiryDate > DateTime.MinValue ? new DateTimeOffset(item.ExpiryDate) : DateTimeOffset.UtcNow.AddYears(1);
        EditMrp = item.Mrp;
        EditPurchaseRate = item.PurchaseRate;
        EditSaleRate = item.SaleRate;

        EditErrorMessage = string.Empty;
        IsEditProductModalOpen = true;
    }

    [RelayCommand]
    public void CloseEditProduct()
    {
        IsEditProductModalOpen = false;
        EditErrorMessage = string.Empty;
    }

    [RelayCommand]
    public async Task SaveProductDetailsAsync()
    {
        if (string.IsNullOrWhiteSpace(EditProductName))
        {
            EditErrorMessage = "Product name is required.";
            return;
        }

        IsSavingProduct = true;
        EditErrorMessage = string.Empty;

        try
        {
            var command = new UpdateProductDetailsCommand(
                ProductId: EditProductId,
                ProductName: EditProductName.Trim(),
                GenericName: string.IsNullOrWhiteSpace(EditGenericName) ? null : EditGenericName.Trim(),
                SaltComposition: string.IsNullOrWhiteSpace(EditSaltComposition) ? null : EditSaltComposition.Trim(),
                Manufacturer: string.IsNullOrWhiteSpace(EditManufacturer) ? null : EditManufacturer.Trim(),
                CategoryName: string.IsNullOrWhiteSpace(EditCategoryName) ? null : EditCategoryName.Trim(),
                HsnCode: string.IsNullOrWhiteSpace(EditHsnCode) ? "3004" : EditHsnCode.Trim(),
                GstRatePercent: EditGstRatePercent,
                Schedule: (DrugSchedule)EditScheduleIndex,
                MinStockAlert: EditMinStockAlert,
                BatchId: string.IsNullOrWhiteSpace(EditBatchId) ? null : EditBatchId,
                BatchNumber: string.IsNullOrWhiteSpace(EditBatchNumber) ? null : EditBatchNumber.Trim(),
                ExpiryDate: EditExpiryDate.UtcDateTime,
                Mrp: EditMrp,
                PurchaseRate: EditPurchaseRate,
                SaleRate: EditSaleRate
            );

            var result = await _inventoryService.UpdateProductDetailsAsync(command);
            if (result.Success)
            {
                IsEditProductModalOpen = false;
                await LoadStocksAsync();
            }
            else
            {
                EditErrorMessage = result.ErrorMessage ?? "Failed to update product details.";
            }
        }
        catch (Exception ex)
        {
            EditErrorMessage = ex.Message;
        }
        finally
        {
            IsSavingProduct = false;
        }
    }
}
