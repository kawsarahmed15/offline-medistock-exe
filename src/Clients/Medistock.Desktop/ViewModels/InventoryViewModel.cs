using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Inventory.DTOs;
using Medistock.Application.Inventory.Services;
using Medistock.Domain.Common;
using Medistock.Infrastructure.Hardware.Export;

namespace Medistock.Desktop.ViewModels;

public partial class MetricDetailRowViewModel : ObservableObject
{
    public string Col1 { get; set; } = string.Empty;
    public string Col2 { get; set; } = string.Empty;
    public string Col3 { get; set; } = string.Empty;
    public string Col4 { get; set; } = string.Empty;
    public string Col5 { get; set; } = string.Empty;
    public string Col6 { get; set; } = string.Empty;
    public string Col7 { get; set; } = string.Empty;
    public string BadgeText { get; set; } = string.Empty;
    public string BadgeBackgroundHex { get; set; } = "#2516A34A";
    public string BadgeForegroundHex { get; set; } = "#16A34A";
    public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);
}

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

    [ObservableProperty]
    private decimal _estimatedProfit;

    [ObservableProperty]
    private decimal _estimatedProfitMarginPercent;

    [ObservableProperty]
    private decimal _revenueThisMonth;

    [ObservableProperty]
    private int _monthlyInvoicesCount;

    [ObservableProperty]
    private decimal _cashCollectionThisMonth;

    [ObservableProperty]
    private int _cashInvoicesCount;

    [ObservableProperty]
    private decimal _onlineCollectionThisMonth;

    [ObservableProperty]
    private int _onlineInvoicesCount;

    [ObservableProperty]
    private decimal _allTimeRevenue;

    [ObservableProperty]
    private decimal _allTimeCash;

    [ObservableProperty]
    private decimal _allTimeOnline;

    [ObservableProperty]
    private bool _isMetricDetailModalOpen;

    [ObservableProperty]
    private string _metricDetailTitle = string.Empty;

    [ObservableProperty]
    private string _metricDetailCategory = string.Empty;

    [ObservableProperty]
    private string _metricDetailSummary = string.Empty;

    [ObservableProperty]
    private string _metricDetailSearchText = string.Empty;

    [ObservableProperty]
    private string _metricDetailCol1Header = "COL 1";

    [ObservableProperty]
    private string _metricDetailCol2Header = "COL 2";

    [ObservableProperty]
    private string _metricDetailCol3Header = "COL 3";

    [ObservableProperty]
    private string _metricDetailCol4Header = "COL 4";

    [ObservableProperty]
    private string _metricDetailCol5Header = "COL 5";

    [ObservableProperty]
    private string _metricDetailCol6Header = "COL 6";

    [ObservableProperty]
    private string _metricDetailCol7Header = "COL 7";

    [ObservableProperty]
    private bool _isMetricDetailLoading;
#pragma warning restore MVVMTK0045

    public ObservableCollection<MetricDetailRowViewModel> MetricDetailRows { get; } = new();
    private readonly List<MetricDetailRowViewModel> _allMetricDetailRows = new();

    public string TotalBatchesCountDisplay => $"({TotalItemsCount} batches)";
    public string TotalStockValueFormatted => $"{TotalStockValue:N2}";
    public string TotalStockValueMrpFormatted => $"MRP Val: ₹{TotalStockValueMrp:N2}";

    public string EstimatedProfitFormatted => $"{EstimatedProfit:N2}";
    public string EstimatedProfitMarginDisplay => EstimatedProfitMarginPercent > 0
        ? $"Margin: ~{EstimatedProfitMarginPercent:F1}% on active stock"
        : "Stock Margin Valuation";

    public string RevenueThisMonthFormatted => $"{RevenueThisMonth:N2}";
    public string MonthlyInvoicesCountDisplay => $"{MonthlyInvoicesCount} invoices finalized this month";

    public string CashCollectionThisMonthFormatted => $"{CashCollectionThisMonth:N2}";
    public string CashCollectionCountDisplay => $"{CashInvoicesCount} cash transactions this month";

    public string OnlineCollectionThisMonthFormatted => $"{OnlineCollectionThisMonth:N2}";
    public string OnlineCollectionCountDisplay => $"{OnlineInvoicesCount} UPI / Card payments this month";

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

            // Load live monthly financial KPIs (Revenue, Cash Collection, Online Collection)
            try
            {
                var financials = await _inventoryService.GetFinancialMetricsAsync();
                RevenueThisMonth = financials.RevenueThisMonth;
                MonthlyInvoicesCount = financials.MonthlyInvoicesCount;
                CashCollectionThisMonth = financials.CashCollectionThisMonth;
                CashInvoicesCount = financials.CashInvoicesCount;
                OnlineCollectionThisMonth = financials.OnlineCollectionThisMonth;
                OnlineInvoicesCount = financials.OnlineInvoicesCount;
                AllTimeRevenue = financials.AllTimeRevenue;
                AllTimeCash = financials.AllTimeCash;
                AllTimeOnline = financials.AllTimeOnline;

                OnPropertyChanged(nameof(RevenueThisMonthFormatted));
                OnPropertyChanged(nameof(MonthlyInvoicesCountDisplay));
                OnPropertyChanged(nameof(CashCollectionThisMonthFormatted));
                OnPropertyChanged(nameof(CashCollectionCountDisplay));
                OnPropertyChanged(nameof(OnlineCollectionThisMonthFormatted));
                OnPropertyChanged(nameof(OnlineCollectionCountDisplay));
            }
            catch { }
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

        // Calculate Estimated Profit across active stock batches: (MRP minus Product buying cost) * AvailableQuantity
        EstimatedProfit = allViewModels.Sum(i => Math.Max(0, (i.Mrp - (i.NetPurchaseRate > 0 ? i.NetPurchaseRate : i.PurchaseRate)) * i.AvailableQuantity));
        EstimatedProfitMarginPercent = TotalStockValueMrp > 0 ? (EstimatedProfit / TotalStockValueMrp) * 100 : 0m;

        OnPropertyChanged(nameof(TotalBatchesCountDisplay));
        OnPropertyChanged(nameof(TotalStockValueFormatted));
        OnPropertyChanged(nameof(TotalStockValueMrpFormatted));
        OnPropertyChanged(nameof(EstimatedProfitFormatted));
        OnPropertyChanged(nameof(EstimatedProfitMarginDisplay));

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

    partial void OnMetricDetailSearchTextChanged(string value)
    {
        FilterMetricDetailRows();
    }

    private void FilterMetricDetailRows()
    {
        var q = MetricDetailSearchText?.Trim() ?? string.Empty;
        MetricDetailRows.Clear();
        if (string.IsNullOrWhiteSpace(q))
        {
            foreach (var r in _allMetricDetailRows)
            {
                MetricDetailRows.Add(r);
            }
        }
        else
        {
            foreach (var r in _allMetricDetailRows.Where(r =>
                (r.Col1?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Col2?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Col3?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Col4?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Col5?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Col6?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Col7?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.BadgeText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)))
            {
                MetricDetailRows.Add(r);
            }
        }
    }

    [RelayCommand]
    public void CloseMetricDetail()
    {
        IsMetricDetailModalOpen = false;
        MetricDetailSearchText = string.Empty;
        MetricDetailRows.Clear();
        _allMetricDetailRows.Clear();
    }

    [RelayCommand]
    public async Task ShowMetricDetailAsync(string metricType)
    {
        MetricDetailCategory = metricType;
        MetricDetailSearchText = string.Empty;
        _allMetricDetailRows.Clear();
        MetricDetailRows.Clear();
        IsMetricDetailLoading = true;
        IsMetricDetailModalOpen = true;

        try
        {
            var nearExpiryDays = SettingsViewModel.GetNearExpiryDays();

            switch (metricType)
            {
                case "TotalProducts":
                    MetricDetailTitle = "📦 Total Products & Active Stock Batches Ledger";
                    MetricDetailCol1Header = "PRODUCT NAME";
                    MetricDetailCol2Header = "BATCH NO.";
                    MetricDetailCol3Header = "EXPIRY";
                    MetricDetailCol4Header = "AVAIL QTY";
                    MetricDetailCol5Header = "COST (₹)";
                    MetricDetailCol6Header = "MRP (₹)";
                    MetricDetailCol7Header = "VALUATION (₹)";

                    var sortedBatches = _allLoadedDtoItems.OrderBy(i => i.ProductName).ThenBy(i => i.ExpiryDate).ToList();
                    foreach (var i in sortedBatches)
                    {
                        var isExp = i.ExpiryDate.Date <= DateTime.UtcNow.Date;
                        var isNear = !isExp && i.ExpiryDate.Date <= DateTime.UtcNow.AddDays(nearExpiryDays).Date;
                        var isOos = i.AvailableQuantity <= 0;
                        var isLow = !isOos && i.AvailableQuantity <= (i.MinStockAlert > 0 ? i.MinStockAlert : 10m);

                        var badge = isExp ? "EXPIRED" : (isNear ? "EXP NEAR" : (isOos ? "OUT OF STOCK" : (isLow ? "LOW STOCK" : "IN STOCK")));
                        var bBg = isExp || isNear || isOos ? "#25DC2626" : (isLow ? "#25D97706" : "#2516A34A");
                        var bFg = isExp || isNear || isOos ? "#DC2626" : (isLow ? "#D97706" : "#16A34A");

                        _allMetricDetailRows.Add(new MetricDetailRowViewModel
                        {
                            Col1 = i.ProductName,
                            Col2 = i.BatchNumber,
                            Col3 = i.ExpiryDate.ToString("MM/yyyy"),
                            Col4 = $"{i.AvailableQuantity:0.##}",
                            Col5 = $"₹{i.PurchaseRate:N2}",
                            Col6 = $"₹{i.Mrp:N2}",
                            Col7 = $"₹{i.StockValueAtCost:N2}",
                            BadgeText = badge,
                            BadgeBackgroundHex = bBg,
                            BadgeForegroundHex = bFg
                        });
                    }

                    MetricDetailSummary = $"{TotalProductsAvailable} Unique Products | {_allLoadedDtoItems.Count} Batches | Total Cost Valuation: ₹{TotalStockValueFormatted} | Total MRP: ₹{TotalStockValueMrpFormatted}";
                    break;

                case "ExpiryNear":
                    MetricDetailTitle = "⚠️ Expiry Near & Expired Batches (Action Required)";
                    MetricDetailCol1Header = "PRODUCT NAME";
                    MetricDetailCol2Header = "BATCH NO.";
                    MetricDetailCol3Header = "EXPIRY DATE";
                    MetricDetailCol4Header = "DAYS LEFT";
                    MetricDetailCol5Header = "AVAIL QTY";
                    MetricDetailCol6Header = "COST RATE (₹)";
                    MetricDetailCol7Header = "VALUE AT RISK (₹)";

                    var expiryBatches = _allLoadedDtoItems
                        .Where(i => i.ExpiryDate.Date <= DateTime.UtcNow.AddDays(nearExpiryDays).Date)
                        .OrderBy(i => i.ExpiryDate)
                        .ToList();

                    foreach (var i in expiryBatches)
                    {
                        var isExp = i.ExpiryDate.Date <= DateTime.UtcNow.Date;
                        var daysLeft = (i.ExpiryDate.Date - DateTime.UtcNow.Date).Days;
                        var badge = isExp ? "EXPIRED" : $"EXP IN {daysLeft}D";

                        _allMetricDetailRows.Add(new MetricDetailRowViewModel
                        {
                            Col1 = i.ProductName,
                            Col2 = i.BatchNumber,
                            Col3 = i.ExpiryDate.ToString("dd-MM-yyyy"),
                            Col4 = isExp ? "EXPIRED" : $"{daysLeft} days",
                            Col5 = $"{i.AvailableQuantity:0.##}",
                            Col6 = $"₹{i.PurchaseRate:N2}",
                            Col7 = $"₹{i.StockValueAtCost:N2}",
                            BadgeText = badge,
                            BadgeBackgroundHex = isExp ? "#35DC2626" : "#22D97706",
                            BadgeForegroundHex = isExp ? "#DC2626" : "#D97706"
                        });
                    }

                    var totalAtRisk = expiryBatches.Sum(b => b.StockValueAtCost);
                    MetricDetailSummary = $"{expiryBatches.Count} Batches Requiring Attention | Total Inventory Value at Risk: ₹{totalAtRisk:N2}";
                    break;

                case "LowStock":
                    MetricDetailTitle = "📉 Low Stock & Out of Stock Reorder Register";
                    MetricDetailCol1Header = "PRODUCT NAME";
                    MetricDetailCol2Header = "BATCH NO.";
                    MetricDetailCol3Header = "AVAIL QTY";
                    MetricDetailCol4Header = "MIN ALERT";
                    MetricDetailCol5Header = "SHORTAGE";
                    MetricDetailCol6Header = "BUYING COST (₹)";
                    MetricDetailCol7Header = "EST. REORDER COST (₹)";

                    var lowStockBatches = _allLoadedDtoItems
                        .Where(i => i.AvailableQuantity <= (i.MinStockAlert > 0 ? i.MinStockAlert : 10m))
                        .OrderBy(i => i.AvailableQuantity)
                        .ToList();

                    decimal totalReorderEst = 0m;
                    foreach (var i in lowStockBatches)
                    {
                        var alertLevel = i.MinStockAlert > 0 ? i.MinStockAlert : 10m;
                        var shortage = Math.Max(0, alertLevel - i.AvailableQuantity);
                        var costRate = i.NetPurchaseRate > 0 ? i.NetPurchaseRate : i.PurchaseRate;
                        var reorderCost = Math.Round(shortage * costRate, 2);
                        totalReorderEst += reorderCost;
                        var isOos = i.AvailableQuantity <= 0;

                        _allMetricDetailRows.Add(new MetricDetailRowViewModel
                        {
                            Col1 = i.ProductName,
                            Col2 = i.BatchNumber,
                            Col3 = $"{i.AvailableQuantity:0.##}",
                            Col4 = $"{alertLevel:0.##}",
                            Col5 = $"{shortage:0.##}",
                            Col6 = $"₹{costRate:N2}",
                            Col7 = $"₹{reorderCost:N2}",
                            BadgeText = isOos ? "OUT OF STOCK" : "LOW STOCK",
                            BadgeBackgroundHex = isOos ? "#25DC2626" : "#25D97706",
                            BadgeForegroundHex = isOos ? "#DC2626" : "#D97706"
                        });
                    }

                    MetricDetailSummary = $"{lowStockBatches.Count} Items At or Below Alert Threshold | Estimated Cost to Restock: ₹{totalReorderEst:N2}";
                    break;

                case "StockValuation":
                    MetricDetailTitle = "💎 Total Stock Valuation (Net Buying Cost vs MRP)";
                    MetricDetailCol1Header = "PRODUCT NAME";
                    MetricDetailCol2Header = "BATCH NO.";
                    MetricDetailCol3Header = "AVAIL QTY";
                    MetricDetailCol4Header = "NET COST RATE";
                    MetricDetailCol5Header = "COST VALUATION";
                    MetricDetailCol6Header = "MRP RATE";
                    MetricDetailCol7Header = "MRP VALUATION";

                    var valBatches = _allLoadedDtoItems.OrderByDescending(i => i.StockValueAtCost).ToList();
                    foreach (var i in valBatches)
                    {
                        var netRate = i.NetPurchaseRate > 0 ? i.NetPurchaseRate : i.PurchaseRate;
                        _allMetricDetailRows.Add(new MetricDetailRowViewModel
                        {
                            Col1 = i.ProductName,
                            Col2 = i.BatchNumber,
                            Col3 = $"{i.AvailableQuantity:0.##}",
                            Col4 = $"₹{netRate:N2}",
                            Col5 = $"₹{i.StockValueAtCost:N2}",
                            Col6 = $"₹{i.Mrp:N2}",
                            Col7 = $"₹{i.StockValueAtMrp:N2}",
                            BadgeText = i.GstRatePercent > 0 ? $"GST {i.GstRatePercent:0.#}%" : "0% GST",
                            BadgeBackgroundHex = "#250284C7",
                            BadgeForegroundHex = "#0284C7"
                        });
                    }

                    MetricDetailSummary = $"Total Active Batches: {valBatches.Count} | Total Cost Valuation: ₹{TotalStockValueFormatted} | Total MRP Valuation: ₹{TotalStockValueMrpFormatted}";
                    break;

                case "EstimatedProfit":
                    MetricDetailTitle = "📈 Estimated Inventory Profit Breakdown (MRP - Product Buying Cost)";
                    MetricDetailCol1Header = "PRODUCT NAME";
                    MetricDetailCol2Header = "BATCH NO.";
                    MetricDetailCol3Header = "AVAIL QTY";
                    MetricDetailCol4Header = "BUYING COST";
                    MetricDetailCol5Header = "MRP";
                    MetricDetailCol6Header = "PROFIT / UNIT";
                    MetricDetailCol7Header = "TOTAL EST. PROFIT";

                    var profitItems = _allLoadedDtoItems
                        .Select(i =>
                        {
                            var buyingCost = i.NetPurchaseRate > 0 ? i.NetPurchaseRate : i.PurchaseRate;
                            var unitProfit = Math.Max(0, i.Mrp - buyingCost);
                            var totalProfit = Math.Round(unitProfit * i.AvailableQuantity, 2);
                            var margin = i.Mrp > 0 ? Math.Round((unitProfit / i.Mrp) * 100m, 1) : 0m;
                            return new { Item = i, BuyingCost = buyingCost, UnitProfit = unitProfit, TotalProfit = totalProfit, Margin = margin };
                        })
                        .OrderByDescending(x => x.TotalProfit)
                        .ToList();

                    foreach (var x in profitItems)
                    {
                        _allMetricDetailRows.Add(new MetricDetailRowViewModel
                        {
                            Col1 = x.Item.ProductName,
                            Col2 = x.Item.BatchNumber,
                            Col3 = $"{x.Item.AvailableQuantity:0.##}",
                            Col4 = $"₹{x.BuyingCost:N2}",
                            Col5 = $"₹{x.Item.Mrp:N2}",
                            Col6 = $"₹{x.UnitProfit:N2}",
                            Col7 = $"₹{x.TotalProfit:N2}",
                            BadgeText = $"{x.Margin:F1}% Margin",
                            BadgeBackgroundHex = x.Margin >= 25 ? "#2516A34A" : (x.Margin >= 15 ? "#250284C7" : "#25D97706"),
                            BadgeForegroundHex = x.Margin >= 25 ? "#16A34A" : (x.Margin >= 15 ? "#0284C7" : "#D97706")
                        });
                    }

                    MetricDetailSummary = $"Total Batches: {profitItems.Count} | Total Estimated Profit: ₹{EstimatedProfitFormatted} | Overall Margin: {EstimatedProfitMarginDisplay}";
                    break;

                case "RevenueThisMonth":
                    MetricDetailTitle = "🧾 Revenue This Month - Finalized Sales Invoices";
                    MetricDetailCol1Header = "INVOICE NO.";
                    MetricDetailCol2Header = "DATE & TIME";
                    MetricDetailCol3Header = "CUSTOMER";
                    MetricDetailCol4Header = "ITEMS";
                    MetricDetailCol5Header = "PAYMENT MODES";
                    MetricDetailCol6Header = "SUBTOTAL (₹)";
                    MetricDetailCol7Header = "NET TOTAL (₹)";

                    var revRows = await _inventoryService.GetFinancialMetricDetailsAsync("RevenueThisMonth");
                    foreach (var r in revRows)
                    {
                        _allMetricDetailRows.Add(new MetricDetailRowViewModel
                        {
                            Col1 = r.Col1,
                            Col2 = r.Col2,
                            Col3 = r.Col3,
                            Col4 = r.Col4,
                            Col5 = r.Col5,
                            Col6 = r.Col6,
                            Col7 = r.Col7,
                            BadgeText = r.BadgeText,
                            BadgeBackgroundHex = "#2516A34A",
                            BadgeForegroundHex = r.BadgeColor
                        });
                    }

                    MetricDetailSummary = $"{revRows.Count} Invoices Finalized This Month | Total Sales Revenue: ₹{RevenueThisMonthFormatted}";
                    break;

                case "CashCollection":
                    MetricDetailTitle = "💵 Cash Collection This Month - Cash Receipt Ledger";
                    MetricDetailCol1Header = "INVOICE NO.";
                    MetricDetailCol2Header = "DATE & TIME";
                    MetricDetailCol3Header = "CUSTOMER";
                    MetricDetailCol4Header = "REGISTER";
                    MetricDetailCol5Header = "PAYMENT MODE";
                    MetricDetailCol6Header = "INVOICE TOTAL (₹)";
                    MetricDetailCol7Header = "CASH RECEIVED (₹)";

                    var cashRows = await _inventoryService.GetFinancialMetricDetailsAsync("CashCollection");
                    foreach (var r in cashRows)
                    {
                        _allMetricDetailRows.Add(new MetricDetailRowViewModel
                        {
                            Col1 = r.Col1,
                            Col2 = r.Col2,
                            Col3 = r.Col3,
                            Col4 = r.Col4,
                            Col5 = r.Col5,
                            Col6 = r.Col6,
                            Col7 = r.Col7,
                            BadgeText = r.BadgeText,
                            BadgeBackgroundHex = "#250D9488",
                            BadgeForegroundHex = r.BadgeColor
                        });
                    }

                    MetricDetailSummary = $"{cashRows.Count} Cash Transactions This Month | Total Cash Collected: ₹{CashCollectionThisMonthFormatted}";
                    break;

                case "OnlineCollection":
                    MetricDetailTitle = "📱 Online & Digital Collections This Month - UPI, QR & Card Ledger";
                    MetricDetailCol1Header = "INVOICE NO.";
                    MetricDetailCol2Header = "DATE & TIME";
                    MetricDetailCol3Header = "CUSTOMER";
                    MetricDetailCol4Header = "REFERENCE / TXN";
                    MetricDetailCol5Header = "DIGITAL METHOD";
                    MetricDetailCol6Header = "INVOICE TOTAL (₹)";
                    MetricDetailCol7Header = "AMOUNT COLLECTED (₹)";

                    var onlineRows = await _inventoryService.GetFinancialMetricDetailsAsync("OnlineCollection");
                    foreach (var r in onlineRows)
                    {
                        _allMetricDetailRows.Add(new MetricDetailRowViewModel
                        {
                            Col1 = r.Col1,
                            Col2 = r.Col2,
                            Col3 = r.Col3,
                            Col4 = r.Col4,
                            Col5 = r.Col5,
                            Col6 = r.Col6,
                            Col7 = r.Col7,
                            BadgeText = r.BadgeText,
                            BadgeBackgroundHex = "#257C3AED",
                            BadgeForegroundHex = r.BadgeColor
                        });
                    }

                    MetricDetailSummary = $"{onlineRows.Count} Digital Transactions This Month | Total Online Collected: ₹{OnlineCollectionThisMonthFormatted}";
                    break;
            }

            FilterMetricDetailRows();
        }
        catch (Exception ex)
        {
            MetricDetailSummary = $"Error loading details: {ex.Message}";
        }
        finally
        {
            IsMetricDetailLoading = false;
        }
    }

    [RelayCommand]
    public async Task ExportMetricDetailCsvAsync()
    {
        try
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var safeTitle = string.Join("_", (MetricDetailTitle ?? "MetricDetails").Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(folder, fileName);

            var sb = new StringBuilder();
            sb.AppendLine($"\"{MetricDetailCol1Header}\",\"{MetricDetailCol2Header}\",\"{MetricDetailCol3Header}\",\"{MetricDetailCol4Header}\",\"{MetricDetailCol5Header}\",\"{MetricDetailCol6Header}\",\"{MetricDetailCol7Header}\",\"Status\"");

            foreach (var row in _allMetricDetailRows)
            {
                sb.AppendLine($"\"{EscapeCsv(row.Col1)}\",\"{EscapeCsv(row.Col2)}\",\"{EscapeCsv(row.Col3)}\",\"{EscapeCsv(row.Col4)}\",\"{EscapeCsv(row.Col5)}\",\"{EscapeCsv(row.Col6)}\",\"{EscapeCsv(row.Col7)}\",\"{EscapeCsv(row.BadgeText)}\"");
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            TryOpenFile(filePath);
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"CSV Export failed: {ex.Message}";
        }
    }

    private static string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("\"", "\"\"");
    }
}
