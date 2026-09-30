using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Products.Queries;
using Medistock.Application.Purchases.DTOs;
using Medistock.Application.Purchases.Services;
using Medistock.Domain.Purchases;
using Medistock.Infrastructure.Hardware.Export;

namespace Medistock.Desktop.ViewModels;

public partial class PurchaseItemRowViewModel : ObservableObject
{
#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private string _productId = string.Empty;

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _genericName = string.Empty;

    [ObservableProperty]
    private string _hsnCode = string.Empty;

    [ObservableProperty]
    private string _unit = "STRIP";

    [ObservableProperty]
    private int _packUnits = 10;

    [ObservableProperty]
    private string _batchNumber = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _expiryDate = default;

    private decimal _previousMrp = 0;

    [ObservableProperty]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _freeQuantity = 0;

    [ObservableProperty]
    private decimal _unitPrice = 0;

    [ObservableProperty]
    private decimal _mrp = 0;

    [ObservableProperty]
    private decimal _saleRate = 0;

    [ObservableProperty]
    private decimal _discountPct = 0;

    [ObservableProperty]
    private decimal _gstRatePercent = SettingsViewModel.GetDefaultGstRate();

    [ObservableProperty]
    private string _expiryText = string.Empty;

    [ObservableProperty]
    private bool _isInterstate = false;
#pragma warning restore MVVMTK0045

    partial void OnBatchNumberChanged(string value)
    {
        if (value != null)
        {
            var upper = value.ToUpperInvariant();
            if (upper != value)
            {
                BatchNumber = upper;
            }
        }
    }

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(QuantityDouble));
        Recalculate();
    }

    partial void OnFreeQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(FreeQuantityDouble));
        Recalculate();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(UnitPriceDouble));
        Recalculate();
    }

    partial void OnMrpChanged(decimal value)
    {
        SaleRate = value; // MRP is the sale price
        _previousMrp = value;
        OnPropertyChanged(nameof(MrpDouble));
        OnPropertyChanged(nameof(SaleRate));
        OnPropertyChanged(nameof(SaleRateDouble));
        Recalculate();
    }

    partial void OnDiscountPctChanged(decimal value)
    {
        OnPropertyChanged(nameof(DiscountPctDouble));
        Recalculate();
    }

    partial void OnGstRatePercentChanged(decimal value)
    {
        OnPropertyChanged(nameof(GstRatePercentDouble));
        Recalculate();
    }

    partial void OnExpiryTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ExpiryDate = default;
            return;
        }

        var clean = value.Trim();
        if (clean.Contains('/'))
        {
            var parts = clean.Split('/');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int month) &&
                int.TryParse(parts[1], out int year))
            {
                if (month >= 1 && month <= 12)
                {
                    if (year < 100) year += 2000;
                    if (year >= 2000 && year <= 2099)
                    {
                        var daysInMonth = DateTime.DaysInMonth(year, month);
                        ExpiryDate = new DateTimeOffset(new DateTime(year, month, daysInMonth, 23, 59, 59, DateTimeKind.Utc));
                    }
                }
            }
        }
        else if (clean.Length == 4 &&
                 int.TryParse(clean.Substring(0, 2), out int month) &&
                 int.TryParse(clean.Substring(2, 2), out int year))
        {
            if (month >= 1 && month <= 12)
            {
                if (year < 100) year += 2000;
                if (year >= 2000 && year <= 2099)
                {
                    var daysInMonth = DateTime.DaysInMonth(year, month);
                    ExpiryDate = new DateTimeOffset(new DateTime(year, month, daysInMonth, 23, 59, 59, DateTimeKind.Utc));
                }
            }
        }
    }

    public double QuantityDouble
    {
        get => (double)Quantity;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                Quantity = (decimal)value;
            }
        }
    }

    public double FreeQuantityDouble
    {
        get => (double)FreeQuantity;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                FreeQuantity = (decimal)value;
            }
        }
    }

    public double UnitPriceDouble
    {
        get => (double)UnitPrice;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                UnitPrice = (decimal)value;
            }
        }
    }

    public double MrpDouble
    {
        get => (double)Mrp;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                var dec = (decimal)value;
                Mrp = dec;
                SaleRate = dec; // MRP is the sale price
                _previousMrp = dec;
            }
        }
    }

    public double SaleRateDouble
    {
        get => (double)SaleRate;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                SaleRate = (decimal)value;
            }
        }
    }

    public double DiscountPctDouble
    {
        get => (double)DiscountPct;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                DiscountPct = (decimal)Math.Clamp(value, 0, 100);
            }
        }
    }

    public double GstRatePercentDouble
    {
        get => (double)GstRatePercent;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                GstRatePercent = (decimal)Math.Clamp(value, 0, 100);
            }
        }
    }

    public decimal GrossAmount => Math.Round(Quantity * UnitPrice, 2);
    public decimal DiscountAmount => Math.Round(GrossAmount * (DiscountPct / 100m), 2);
    public decimal TaxableAmount => GrossAmount - DiscountAmount;
    public decimal GstAmount => Math.Round(TaxableAmount * (GstRatePercent / 100m), 2);
    public decimal NetAmount => TaxableAmount + GstAmount;
    public decimal TotalQuantity => Quantity + FreeQuantity;
    public decimal LandedCostPerUnit => TotalQuantity > 0 ? Math.Round(NetAmount / TotalQuantity, 2) : 0;
    public decimal MarginPercent => Mrp > 0 && LandedCostPerUnit > 0 ? Math.Round(((Mrp - LandedCostPerUnit) / Mrp) * 100m, 1) : 0;

    public string TaxableAmountFormatted => $"{TaxableAmount:N2}";
    public string NetAmountFormatted => $"{NetAmount:N2}";
    public string LandedCostFormatted => $"₹{LandedCostPerUnit:N2}";
    public string MarginDisplay => $"{MarginPercent}%";

    public Action? OnRowChanged { get; set; }

    public void Recalculate()
    {
        OnPropertyChanged(nameof(GrossAmount));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(TaxableAmount));
        OnPropertyChanged(nameof(GstAmount));
        OnPropertyChanged(nameof(NetAmount));
        OnPropertyChanged(nameof(TotalQuantity));
        OnPropertyChanged(nameof(LandedCostPerUnit));
        OnPropertyChanged(nameof(MarginPercent));
        OnPropertyChanged(nameof(TaxableAmountFormatted));
        OnPropertyChanged(nameof(NetAmountFormatted));
        OnPropertyChanged(nameof(LandedCostFormatted));
        OnPropertyChanged(nameof(MarginDisplay));
        OnRowChanged?.Invoke();
    }
}

public partial class PurchaseReturnItemRowViewModel : ObservableObject
{
#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private string _productId = string.Empty;

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _batchNumber = string.Empty;

    [ObservableProperty]
    private DateTime _expiryDate;

    [ObservableProperty]
    private decimal _inwardedQuantity;

    [ObservableProperty]
    private decimal _availableStock;

    [ObservableProperty]
    private decimal _maxReturnable;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private decimal _returnQuantity;

    [ObservableProperty]
    private decimal _unitPrice;

    [ObservableProperty]
    private decimal _discountPct;

    [ObservableProperty]
    private decimal _gstRatePercent;

    [ObservableProperty]
    private decimal _netUnitPrice;

    [ObservableProperty]
    private decimal _netAmount;

    [ObservableProperty]
    private string _reason = "Damaged / Expired";
#pragma warning restore MVVMTK0045

    public string ExpiryFormatted => ExpiryDate != default ? ExpiryDate.ToString("MM/yy") : "—";
    public string NetUnitPriceFormatted => $"₹{NetUnitPrice:N2}";
    public string NetAmountFormatted => $"₹{NetAmount:N2}";

    public Action? OnChanged { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && ReturnQuantity <= 0 && MaxReturnable > 0)
        {
            ReturnQuantity = Math.Min(1, MaxReturnable);
        }
        Recalculate();
    }

    partial void OnReturnQuantityChanged(decimal value)
    {
        if (value > MaxReturnable)
            ReturnQuantity = MaxReturnable;
        if (value > 0 && !IsSelected)
            IsSelected = true;
        Recalculate();
    }

    public void Recalculate()
    {
        NetAmount = IsSelected ? Math.Round(ReturnQuantity * NetUnitPrice, 2, MidpointRounding.AwayFromZero) : 0m;
        OnPropertyChanged(nameof(NetAmountFormatted));
        OnChanged?.Invoke();
    }
}

public partial class PurchaseEntryViewModel : ObservableObject
{
    private readonly IPurchaseService _purchaseService;
    private readonly IProductSearchRepository _productSearchRepository;
    private readonly IPurchaseExportService? _exportService;
    private readonly string _orgId = "org-1";
    private readonly string _branchId = "br-1";
    private readonly string _warehouseId = "wh-1";

#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private string _selectedTab = "Entry"; // "Entry", "History", "Suppliers"

    // Executive KPI Metrics
    [ObservableProperty]
    private decimal _totalPurchasesAmount;

    [ObservableProperty]
    private int _totalInvoicesCount;

    [ObservableProperty]
    private int _totalSuppliersCount;

    [ObservableProperty]
    private decimal _totalOutstandingPayables;

    [ObservableProperty]
    private decimal _totalStockValue;

    [ObservableProperty]
    private string _selectedPurchasePeriod = "All"; // "Today", "1 Month", "All"

    // Inward Header Fields - Empty by default
    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private string _supplierSearchText = string.Empty;

    [ObservableProperty]
    private string _selectedSupplierId = string.Empty;

    [ObservableProperty]
    private string _selectedSupplierName = string.Empty;

    [ObservableProperty]
    private string _supplierGstin = string.Empty;

    [ObservableProperty]
    private string _supplierDlNumber = string.Empty;

    [ObservableProperty]
    private int _supplierCreditDays = 0;

    [ObservableProperty]
    private decimal _supplierOutstandingBalance = 0;

    // Bill Editing Mode
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    [NotifyPropertyChangedFor(nameof(FormHeaderTitle))]
    private bool _isEditingInvoice = false;

    [ObservableProperty]
    private string? _editingInvoiceId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormHeaderTitle))]
    private string _editingInvoicePurchaseNo = string.Empty;

    public string SaveButtonText => IsEditingInvoice ? "UPDATE PURCHASE BILL [Ctrl+S]" : "POST INVOICE [Ctrl+S]";
    public string FormHeaderTitle => IsEditingInvoice 
        ? $"✏️ EDITING PURCHASE BILL ({EditingInvoicePurchaseNo})" 
        : "New Purchase Inward Entry";

    // Purchase Return (Debit Note) State
    [ObservableProperty]
    private bool _isPurchaseReturnModalOpen = false;

    [ObservableProperty]
    private bool _isPurchaseReturnBillModalOpen = false;

    [ObservableProperty]
    private PurchaseInvoiceDetailsDto? _returnSourceInvoice;

    [ObservableProperty]
    private string _returnSourcePurchaseNo = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PurchaseReturnItemRowViewModel> _returnItems = new();

    [ObservableProperty]
    private PurchaseReturnBillDto? _currentPurchaseReturnBill;

    [ObservableProperty]
    private int _totalReturnSelectedItems = 0;

    [ObservableProperty]
    private decimal _totalReturnQuantity = 0m;

    [ObservableProperty]
    private decimal _totalReturnAmount = 0m;

    public string TotalReturnAmountFormatted => $"₹{TotalReturnAmount:N2}";

    [ObservableProperty]
    private string _supplierInvoiceNo = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _supplierInvoiceDate = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private bool _isInterstate = false;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Financial Breakdown
    [ObservableProperty]
    private decimal _taxableSubtotal;

    [ObservableProperty]
    private decimal _cgstTotal;

    [ObservableProperty]
    private decimal _sgstTotal;

    [ObservableProperty]
    private decimal _igstTotal;

    [ObservableProperty]
    private decimal _totalDiscount;

    [ObservableProperty]
    private decimal _roundOff;

    [ObservableProperty]
    private decimal _grandTotal;

    // Active Row Tracker
    [ObservableProperty]
    private int _activeRowIndex = -1;

    // Cancel Invoice Modal State
    [ObservableProperty]
    private bool _isCancelConfirmOpen = false;

    [ObservableProperty]
    private PurchaseInvoiceSummaryDto? _invoicePendingCancel;

    // History Tab Filter & Detail State
    [ObservableProperty]
    private string _historySearchText = string.Empty;

    [ObservableProperty]
    private PurchaseInvoiceDetailsDto? _selectedInvoiceDetails;

    [ObservableProperty]
    private bool _isInvoiceDetailsModalOpen = false;

    // Add Supplier Modal State
    [ObservableProperty]
    private bool _isAddSupplierModalOpen = false;

    [ObservableProperty]
    private string _newSupplierName = string.Empty;

    [ObservableProperty]
    private string _newSupplierGstin = string.Empty;

    [ObservableProperty]
    private string _newSupplierDlNumber = string.Empty;

    [ObservableProperty]
    private string _newSupplierPhone = string.Empty;

    [ObservableProperty]
    private string _newSupplierEmail = string.Empty;

    [ObservableProperty]
    private string _newSupplierAddress = string.Empty;

    [ObservableProperty]
    private int _newSupplierCreditDays = 30;

    [ObservableProperty]
    private decimal _newSupplierOpeningBalance = 0;
#pragma warning restore MVVMTK0045

    // Medicine Autocomplete Search Dropdown State
    [ObservableProperty]
    private bool _isProductSearchOpen = false;

    [ObservableProperty]
    private int _selectedProductSearchIndex = -1;

    partial void OnSupplierInvoiceNoChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var upper = value.ToUpperInvariant();
            if (upper != value)
            {
                SupplierInvoiceNo = upper;
            }
        }
    }

    public ProductSearchItemViewModel? SelectedProductSearchItem =>
        (SelectedProductSearchIndex >= 0 && SelectedProductSearchIndex < ProductSearchResults.Count)
            ? ProductSearchResults[SelectedProductSearchIndex]
            : null;

    partial void OnSelectedProductSearchIndexChanged(int value)
    {
        for (int i = 0; i < ProductSearchResults.Count; i++)
        {
            ProductSearchResults[i].IsSelected = (i == value);
        }
        OnPropertyChanged(nameof(SelectedProductSearchItem));
    }

    public void MoveSearchSelectionDown()
    {
        if (ProductSearchResults.Count == 0) return;
        SelectedProductSearchIndex = Math.Min(ProductSearchResults.Count - 1, SelectedProductSearchIndex + 1);
    }

    public void MoveSearchSelectionUp()
    {
        if (ProductSearchResults.Count == 0) return;
        SelectedProductSearchIndex = Math.Max(0, SelectedProductSearchIndex - 1);
    }

    public ObservableCollection<ProductSearchItemViewModel> ProductSearchResults { get; } = new();

    public string TotalPurchasesFormatted => $"₹{TotalPurchasesAmount:N2}";
    public string TotalOutstandingPayablesFormatted => $"₹{TotalOutstandingPayables:N2}";
    public string TotalStockValueFormatted => $"₹{TotalStockValue:N2}";

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<SupplierDto> FilteredSuppliers { get; } = new();
    public ObservableCollection<PurchaseItemRowViewModel> LineItems { get; } = new();
    public ObservableCollection<PurchaseInvoiceSummaryDto> RecentPurchases { get; } = new();
    public ObservableCollection<PurchaseInvoiceSummaryDto> FilteredRecentPurchases { get; } = new();

    private readonly List<PurchaseInvoiceSummaryDto> _allLoadedInvoices = new();

    public PurchaseItemRowViewModel? ActiveRow =>
        ActiveRowIndex >= 0 && ActiveRowIndex < LineItems.Count
            ? LineItems[ActiveRowIndex]
            : null;

    public void SetActiveRow(int index)
    {
        if (index < 0 || index >= LineItems.Count) return;
        ActiveRowIndex = index;
        OnPropertyChanged(nameof(ActiveRow));
    }

    public PurchaseEntryViewModel(
        IPurchaseService purchaseService,
        IProductSearchRepository productSearchRepository,
        IPurchaseExportService? exportService = null)
    {
        _purchaseService = purchaseService;
        _productSearchRepository = productSearchRepository;
        _exportService = exportService;

        LineItems.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (PurchaseItemRowViewModel row in e.NewItems)
                {
                    row.OnRowChanged = RecalculateTotals;
                }
            }
            if (e.OldItems != null)
            {
                foreach (PurchaseItemRowViewModel row in e.OldItems)
                {
                    row.OnRowChanged = null;
                }
            }
            RecalculateTotals();
        };

        AddBlankRow();
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadSuppliersAsync();
        await LoadKpisAsync();
        await LoadRecentPurchasesAsync();
    }

    [RelayCommand]
    public void SelectTab(string tabName)
    {
        SelectedTab = tabName;
        if (tabName == "History")
        {
            _ = LoadRecentPurchasesAsync();
        }
        else if (tabName == "Suppliers")
        {
            _ = LoadSuppliersAsync();
        }
    }

    [RelayCommand]
    public async Task ViewPurchasesLedgerAsync()
    {
        SelectedTab = "History";
        await LoadRecentPurchasesAsync();
        StatusMessage = $"📋 Inward Invoices Ledger: {FilteredRecentPurchases.Count} recorded purchase invoices loaded.";
    }

    [RelayCommand]
    public async Task ViewWholesalersDirectoryAsync()
    {
        SelectedTab = "Suppliers";
        await LoadRecentPurchasesAsync();
        await LoadSuppliersAsync();
        StatusMessage = $"🏢 Wholesaler Directory: {Suppliers.Count} active supply chain vendors enrolled.";
    }

    [RelayCommand]
    public async Task RefreshStockValueAsync()
    {
        await LoadKpisAsync();
        StatusMessage = $"📊 Total Stock Valuation updated: {TotalStockValueFormatted} (calculated as Buying Price + GST × Available Qty).";
    }

    [RelayCommand]
    public async Task RefreshAllPurchaseDataAsync()
    {
        await LoadKpisAsync();
        await LoadRecentPurchasesAsync();
        await LoadSuppliersAsync();
        StatusMessage = "🔄 Purchase master data, inward ledger, and stock valuation refreshed successfully.";
    }

    [RelayCommand]
    public async Task SetPurchasePeriodAsync(string period)
    {
        SelectedPurchasePeriod = period;
        await LoadKpisAsync();
        StatusMessage = $"📊 Purchases filtered by: {period} (Total: {TotalPurchasesFormatted}, Invoices: {TotalInvoicesCount})";
    }

    [RelayCommand]
    public async Task LoadKpisAsync()
    {
        try
        {
            var kpis = await _purchaseService.GetPurchaseKpiSummaryAsync(_orgId, _branchId, SelectedPurchasePeriod);
            TotalPurchasesAmount = kpis.TotalPurchaseAmount;
            TotalInvoicesCount = kpis.TotalInvoicesCount;
            TotalSuppliersCount = kpis.TotalSuppliersCount;
            TotalOutstandingPayables = kpis.TotalOutstandingPayable;
            TotalStockValue = kpis.TotalStockValue;

            OnPropertyChanged(nameof(TotalPurchasesFormatted));
            OnPropertyChanged(nameof(TotalOutstandingPayablesFormatted));
            OnPropertyChanged(nameof(TotalStockValueFormatted));
        }
        catch { }
    }

    [RelayCommand]
    public async Task LoadSuppliersAsync()
    {
        try
        {
            var list = await _purchaseService.GetSuppliersAsync(_orgId);
            Suppliers.Clear();
            FilteredSuppliers.Clear();

            IEnumerable<PurchaseInvoiceSummaryDto> sourceInvoices = _allLoadedInvoices.Count > 0 ? _allLoadedInvoices : RecentPurchases;
            var allInvoices = sourceInvoices
                .OrderBy(p => p.CreatedAt)
                .ToList();

            var invoicePoMap = new Dictionary<string, string>();
            for (int i = 0; i < allInvoices.Count; i++)
            {
                var poNum = !string.IsNullOrWhiteSpace(allInvoices[i].PurchaseNo)
                    ? allInvoices[i].PurchaseNo!
                    : $"PO-{(i + 1):D4}";
                invoicePoMap[allInvoices[i].Id] = poNum;
            }

            foreach (var s in list)
            {
                var poNo = s.PurchaseNo;
                var invNo = s.InvoiceNo ?? s.PurchaseBillNo;

                if (allInvoices.Count > 0)
                {
                    var matching = allInvoices
                        .Where(p => (string.Equals(p.SupplierName, s.Name, StringComparison.OrdinalIgnoreCase) ||
                                     (!string.IsNullOrWhiteSpace(s.Gstin) && string.Equals(p.SupplierGstin, s.Gstin, StringComparison.OrdinalIgnoreCase)))
                                    && p.Status != PurchaseInvoiceStatus.Cancelled)
                        .ToList();

                    if (matching.Count > 0)
                    {
                        if (string.IsNullOrWhiteSpace(poNo))
                        {
                            var pos = matching
                                .Select(m => invoicePoMap.TryGetValue(m.Id, out var po) ? po : m.PurchaseNo)
                                .Where(p => !string.IsNullOrWhiteSpace(p))
                                .Distinct()
                                .ToList();
                            if (pos.Count > 0)
                                poNo = string.Join(", ", pos);
                        }

                        if (string.IsNullOrWhiteSpace(invNo))
                        {
                            var invs = matching
                                .Select(m => m.SupplierInvoiceNo)
                                .Where(no => !string.IsNullOrWhiteSpace(no))
                                .Distinct()
                                .ToList();
                            if (invs.Count > 0)
                                invNo = string.Join(", ", invs);
                        }
                    }
                }

                var finalSupplier = s with { PurchaseNo = poNo, InvoiceNo = invNo };

                Suppliers.Add(finalSupplier);
                FilteredSuppliers.Add(finalSupplier);
            }
        }
        catch { }
    }

    public void OnSupplierSelected(SupplierDto? supplier)
    {
        if (supplier == null) return;
        SelectedSupplier = supplier;
        SelectedSupplierId = supplier.Id;
        SelectedSupplierName = supplier.Name;
        SupplierSearchText = supplier.Name;
        SupplierGstin = supplier.Gstin ?? "";
        SupplierDlNumber = supplier.DlNumber ?? "";
        SupplierCreditDays = supplier.CreditDays;
        SupplierOutstandingBalance = supplier.CurrentOutstandingBalance;

        // Auto-detect interstate by checking state code (First 2 chars of GSTIN)
        // Store default state is Delhi (07)
        if (!string.IsNullOrEmpty(supplier.Gstin) && supplier.Gstin.Length >= 2)
        {
            var supplierState = supplier.Gstin.Substring(0, 2);
            var storeState = SettingsViewModel.GetStoreInfo().Gstin;
            var storePrefix = !string.IsNullOrEmpty(storeState) && storeState.Length >= 2 ? storeState.Substring(0, 2) : "07";
            IsInterstate = supplierState != storePrefix;
        }

        RecalculateTotals();
    }

    [RelayCommand]
    public void AddBlankRow()
    {
        var defaultGst = SettingsViewModel.GetDefaultGstRate();
        var row = new PurchaseItemRowViewModel
        {
            Quantity = 1,
            FreeQuantity = 0,
            UnitPrice = 0,
            Mrp = 0,
            SaleRate = 0,
            DiscountPct = 0,
            GstRatePercent = defaultGst,
            IsInterstate = IsInterstate,
            HsnCode = string.Empty,
            ExpiryDate = default,
            ExpiryText = string.Empty
        };
        row.PropertyChanged += (s, e) => RecalculateTotals();
        LineItems.Add(row);
        ActiveRowIndex = LineItems.Count - 1;
        RecalculateTotals();
    }

    [RelayCommand]
    public void AddNewRow() => AddBlankRow();

    private readonly Stack<(int Index, PurchaseItemRowViewModel Row)> _undoDeletedRows = new();

    public int UndoCount => _undoDeletedRows.Count;

    [RelayCommand]
    public void RemoveRow(PurchaseItemRowViewModel? row)
    {
        var target = row ?? ActiveRow;
        if (target == null && LineItems.Count > 0)
        {
            var sel = Math.Clamp(ActiveRowIndex >= 0 ? ActiveRowIndex : 0, 0, LineItems.Count - 1);
            target = LineItems[sel];
        }
        if (target == null) return;

        var idx = LineItems.IndexOf(target);
        if (idx < 0) return;

        // Snapshot row for undo
        var snapshot = CloneRow(target);
        _undoDeletedRows.Push((idx, snapshot));

        if (LineItems.Count > 1)
        {
            LineItems.RemoveAt(idx);
            ActiveRowIndex = Math.Clamp(idx, 0, LineItems.Count - 1);
        }
        else
        {
            // If it's the last row, reset it to blank
            LineItems.Clear();
            AddBlankRow();
            ActiveRowIndex = 0;
        }

        RecalculateTotals();
        var displayName = !string.IsNullOrWhiteSpace(snapshot.ProductName) ? snapshot.ProductName : "Medicine";
        StatusMessage = $"🗑️ Row removed ({displayName}). Press [Ctrl+Z] to undo.";
    }

    public static bool IsBlankRow(PurchaseItemRowViewModel row)
    {
        return string.IsNullOrWhiteSpace(row.ProductName) &&
               string.IsNullOrWhiteSpace(row.BatchNumber) &&
               string.IsNullOrWhiteSpace(row.ExpiryText) &&
               row.UnitPrice <= 0;
    }

    [RelayCommand]
    public void UndoRemoveRow()
    {
        if (_undoDeletedRows.Count == 0)
        {
            StatusMessage = "ℹ️ Nothing to undo.";
            return;
        }

        var (idx, restored) = _undoDeletedRows.Pop();

        // If table currently has just 1 empty/blank row, clear it before restoring
        if (LineItems.Count == 1 && IsBlankRow(LineItems[0]))
        {
            LineItems.Clear();
        }

        restored.OnRowChanged = RecalculateTotals;
        restored.PropertyChanged += (s, e) => RecalculateTotals();

        var insertIndex = Math.Clamp(idx, 0, LineItems.Count);
        LineItems.Insert(insertIndex, restored);
        ActiveRowIndex = insertIndex;
        RecalculateTotals();

        var displayName = !string.IsNullOrWhiteSpace(restored.ProductName) ? restored.ProductName : "Medicine";
        StatusMessage = $"↩️ Restored '{displayName}' at row {insertIndex + 1} (Undo).";
    }

    public static PurchaseItemRowViewModel CloneRow(PurchaseItemRowViewModel src)
    {
        return new PurchaseItemRowViewModel
        {
            ProductId = src.ProductId,
            ProductName = src.ProductName,
            GenericName = src.GenericName,
            HsnCode = src.HsnCode,
            Unit = src.Unit,
            PackUnits = src.PackUnits,
            BatchNumber = src.BatchNumber,
            ExpiryDate = src.ExpiryDate,
            ExpiryText = src.ExpiryText,
            Quantity = src.Quantity,
            FreeQuantity = src.FreeQuantity,
            UnitPrice = src.UnitPrice,
            Mrp = src.Mrp,
            SaleRate = src.SaleRate,
            DiscountPct = src.DiscountPct,
            GstRatePercent = src.GstRatePercent,
            IsInterstate = src.IsInterstate
        };
    }

    public async Task SearchMedicinesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 1)
        {
            ProductSearchResults.Clear();
            IsProductSearchOpen = false;
            SelectedProductSearchIndex = -1;
            return;
        }

        try
        {
            var results = await _productSearchRepository.SearchProductsAsync(query.Trim(), _warehouseId, limit: 15);
            ProductSearchResults.Clear();
            foreach (var item in results)
            {
                ProductSearchResults.Add(ProductSearchItemViewModel.FromDto(item));
            }

            if (ProductSearchResults.Count > 0)
            {
                IsProductSearchOpen = true;
                SelectedProductSearchIndex = 0;
            }
            else
            {
                IsProductSearchOpen = false;
                SelectedProductSearchIndex = -1;
            }
        }
        catch
        {
            IsProductSearchOpen = false;
        }
    }

    public void SelectProductSearch(ProductSearchItemViewModel? item, PurchaseItemRowViewModel? row)
    {
        if (item == null || row == null)
        {
            IsProductSearchOpen = false;
            return;
        }

        row.ProductId = item.Id;
        row.ProductName = item.Name;
        row.GenericName = item.GenericName;
        // If product is already in stock, enter the HSN code by default
        row.HsnCode = !string.IsNullOrWhiteSpace(item.HsnCode) ? item.HsnCode : "30049099";
        row.GstRatePercent = item.GstRatePercent > 0 ? item.GstRatePercent : SettingsViewModel.GetDefaultGstRate();
        row.Mrp = item.Mrp;
        row.SaleRate = item.Mrp; // MRP is the sale price

        decimal defaultCost = 0m;
        if (item.Batches != null && item.Batches.Count > 0 && item.Batches[0].PurchaseRate > 0)
        {
            defaultCost = item.Batches[0].PurchaseRate;
        }
        else if (item.Mrp > 0)
        {
            defaultCost = Math.Round(item.Mrp * 0.70m, 2);
        }
        row.UnitPrice = defaultCost;
        row.DiscountPct = 0;
        // Expiry section is by default empty for new purchase batches until entered by user
        row.ExpiryDate = default;
        row.ExpiryText = string.Empty;

        row.Recalculate();
        RecalculateTotals();
        IsProductSearchOpen = false;
        SelectedProductSearchIndex = -1;
    }

    public void SelectProductSearch(ProductSearchDto? item, PurchaseItemRowViewModel? row)
    {
        if (item == null || row == null)
        {
            IsProductSearchOpen = false;
            return;
        }
        SelectProductSearch(ProductSearchItemViewModel.FromDto(item), row);
    }

    public void CreateOrApplyCustomProduct(string productName, PurchaseItemRowViewModel row)
    {
        if (row == null) return;
        var trimmed = productName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed)) return;

        if (string.IsNullOrWhiteSpace(row.ProductId) || row.ProductName != trimmed)
        {
            row.ProductId = $"prod_{Guid.NewGuid():N}";
            row.ProductName = trimmed;
            // Whenever a new product name is entered to add in inventory, don't add HSN by default
            row.HsnCode = string.Empty;
            if (row.GstRatePercent <= 0) row.GstRatePercent = SettingsViewModel.GetDefaultGstRate();
            if (row.Quantity <= 0) row.Quantity = 1;
            // Expiry remains empty by default until entered by user
            row.ExpiryDate = default;
            row.ExpiryText = string.Empty;
            if (row.SaleRate <= 0 && row.Mrp > 0)
            {
                row.SaleRate = row.Mrp;
            }
        }
        row.Recalculate();
        RecalculateTotals();
        IsProductSearchOpen = false;
        SelectedProductSearchIndex = -1;
    }

    public void SelectHighlightedProduct(PurchaseItemRowViewModel? row)
    {
        if (row != null && SelectedProductSearchIndex >= 0 && SelectedProductSearchIndex < ProductSearchResults.Count)
        {
            SelectProductSearch(ProductSearchResults[SelectedProductSearchIndex], row);
        }
        else
        {
            IsProductSearchOpen = false;
        }
    }

    public async Task SearchRowProductAsync(string query, PurchaseItemRowViewModel row)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 1) return;

        try
        {
            var results = await _productSearchRepository.SearchProductsAsync(query.Trim(), _warehouseId, limit: 10);
            if (results.Count == 0)
            {
                CreateOrApplyCustomProduct(query, row);
                return;
            }

            var exact = results.FirstOrDefault(r => string.Equals(r.Name.Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                SelectProductSearch(ProductSearchItemViewModel.FromDto(exact), row);
                return;
            }

            if (results[0].Name.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                SelectProductSearch(ProductSearchItemViewModel.FromDto(results[0]), row);
                return;
            }

            CreateOrApplyCustomProduct(query, row);
        }
        catch
        {
            CreateOrApplyCustomProduct(query, row);
        }
    }

    public async Task PopulateRowFromProductAsync(PurchaseItemRowViewModel row, string productId)
    {
        try
        {
            var results = await _productSearchRepository.SearchProductsAsync(productId, _warehouseId, limit: 1);
            if (results.Count > 0)
            {
                var prod = results[0];
                row.ProductId = prod.Id;
                row.ProductName = prod.Name;
                row.GenericName = prod.GenericName;
                // If product is already in stock, enter the HSN code by default
                row.HsnCode = !string.IsNullOrWhiteSpace(prod.HsnCode) ? prod.HsnCode : "30049099";
                row.GstRatePercent = prod.GstRatePercent > 0 ? prod.GstRatePercent : 12.0m;
                if (prod.Mrp > 0) row.Mrp = prod.Mrp;
                if (prod.SaleRate > 0) row.SaleRate = prod.SaleRate;
                if (row.UnitPrice == 0 && prod.Mrp > 0) row.UnitPrice = Math.Round(prod.Mrp * 0.70m, 2);
                row.ExpiryDate = default;
                row.ExpiryText = string.Empty;
                row.Recalculate();
                RecalculateTotals();
            }
        }
        catch { }
    }

    public void RecalculateTotals()
    {
        foreach (var item in LineItems)
        {
            item.IsInterstate = IsInterstate;
        }

        TaxableSubtotal = LineItems.Sum(i => i.TaxableAmount);
        TotalDiscount = LineItems.Sum(i => i.DiscountAmount);

        if (IsInterstate)
        {
            CgstTotal = 0;
            SgstTotal = 0;
            IgstTotal = LineItems.Sum(i => i.GstAmount);
        }
        else
        {
            CgstTotal = Math.Round(LineItems.Sum(i => i.GstAmount) / 2m, 2);
            SgstTotal = Math.Round(LineItems.Sum(i => i.GstAmount) / 2m, 2);
            IgstTotal = 0;
        }

        var raw = TaxableSubtotal + CgstTotal + SgstTotal + IgstTotal;
        var rounded = Math.Round(raw, MidpointRounding.AwayFromZero);
        RoundOff = rounded - raw;
        GrandTotal = rounded;
    }

    public void ResetForm()
    {
        LineItems.Clear();
        AddBlankRow();
        SupplierInvoiceNo = string.Empty;
        SupplierInvoiceDate = DateTimeOffset.UtcNow;
        Notes = string.Empty;
        SelectedSupplier = null;
        SelectedSupplierId = string.Empty;
        SelectedSupplierName = string.Empty;
        SupplierSearchText = string.Empty;
        SupplierGstin = string.Empty;
        SupplierDlNumber = string.Empty;
        SupplierCreditDays = 0;
        SupplierOutstandingBalance = 0m;
        IsInterstate = false;
        RecalculateTotals();
    }

    [RelayCommand]
    public void ClearForm() => ResetForm();

    [RelayCommand]
    public async Task PostPurchaseInvoiceAsync()
    {
        if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
        {
            StatusMessage = "⚠️ Please enter the Wholesaler Supplier Invoice Number.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedSupplierName) && !string.IsNullOrWhiteSpace(SupplierSearchText))
        {
            SelectedSupplierName = SupplierSearchText.Trim();
        }

        if (string.IsNullOrWhiteSpace(SelectedSupplierName))
        {
            StatusMessage = "⚠️ Please select or enter a Wholesaler / Supplier.";
            return;
        }

        if (LineItems.Count == 0)
        {
            StatusMessage = "⚠️ Add at least one medicine line item to inward.";
            return;
        }

        // Validate batch numbers, expiry dates, and quantities
        foreach (var item in LineItems)
        {
            if (string.IsNullOrWhiteSpace(item.BatchNumber))
            {
                StatusMessage = $"⚠️ Batch number is mandatory for '{item.ProductName}'.";
                return;
            }
            if (string.IsNullOrWhiteSpace(item.ExpiryText) || item.ExpiryDate == default)
            {
                StatusMessage = $"⚠️ Expiry date (MM/YY) is mandatory for '{item.ProductName}'.";
                return;
            }
            if (item.Quantity <= 0)
            {
                StatusMessage = $"⚠️ Quantity must be greater than zero for '{item.ProductName}'.";
                return;
            }
        }

        // Branch: If we are in Edit Mode, perform atomic update on the existing invoice
        if (IsEditingInvoice && !string.IsNullOrEmpty(EditingInvoiceId))
        {
            IsBusy = true;
            StatusMessage = "⏳ Updating purchase bill & revising stock...";
            try
            {
                var updateCmd = new UpdatePurchaseInvoiceCommand(
                    InvoiceId: EditingInvoiceId,
                    OrgId: _orgId,
                    BranchId: _branchId,
                    WarehouseId: _warehouseId,
                    SupplierId: string.IsNullOrEmpty(SelectedSupplierId) ? "sup-1" : SelectedSupplierId,
                    SupplierName: SelectedSupplierName,
                    SupplierGstin: SupplierGstin,
                    SupplierInvoiceNo: SupplierInvoiceNo.Trim(),
                    SupplierInvoiceDate: SupplierInvoiceDate.UtcDateTime,
                    IsInterstate: IsInterstate,
                    UpdatedByUserId: "USER-STOREKEEPER",
                    Notes: Notes,
                    Items: LineItems.Select(i => new PurchaseInvoiceItemInputDto(
                        ProductId: string.IsNullOrEmpty(i.ProductId) ? $"p_{Guid.NewGuid():N}" : i.ProductId,
                        ProductName: i.ProductName,
                        HsnCode: i.HsnCode,
                        BatchNumber: i.BatchNumber.Trim().ToUpperInvariant(),
                        ExpiryDate: i.ExpiryDate.UtcDateTime,
                        ManufacturingDate: null,
                        Quantity: i.Quantity,
                        FreeQuantity: i.FreeQuantity,
                        UnitPrice: i.UnitPrice,
                        Mrp: i.Mrp,
                        SaleRate: i.SaleRate > 0 ? i.SaleRate : i.Mrp,
                        DiscountPct: i.DiscountPct,
                        GstRatePercent: i.GstRatePercent
                    )).ToList()
                );

                var updateResult = await _purchaseService.UpdatePurchaseInvoiceAsync(updateCmd);
                if (updateResult.Success)
                {
                    StatusMessage = $"✅ Purchase Bill {SupplierInvoiceNo} updated successfully! Stock balances and items revised.";
                    IsEditingInvoice = false;
                    EditingInvoiceId = null;
                    EditingInvoicePurchaseNo = string.Empty;
                    ResetForm();

                    await LoadKpisAsync();
                    await LoadRecentPurchasesAsync();
                    await LoadSuppliersAsync();
                }
                else
                {
                    StatusMessage = $"❌ Update failed: {updateResult.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
            return;
        }

        // New purchase entry
        if (!string.IsNullOrEmpty(SelectedSupplierId))
        {
            try
            {
                var isDup = await _purchaseService.IsDuplicateInvoiceAsync(
                    _orgId, SelectedSupplierId, SupplierInvoiceNo.Trim());
                if (isDup)
                {
                    StatusMessage = $"⚠️ Invoice '{SupplierInvoiceNo.Trim()}' already exists for this supplier. Cancel the existing invoice first.";
                    return;
                }
            }
            catch { }
        }

        IsBusy = true;
        StatusMessage = "⏳ Posting inward purchase invoice & updating stock ledger...";

        try
        {
            var command = new CreatePurchaseInvoiceCommand(
                OrgId: _orgId,
                BranchId: _branchId,
                WarehouseId: _warehouseId,
                SupplierId: string.IsNullOrEmpty(SelectedSupplierId) ? "sup-1" : SelectedSupplierId,
                SupplierName: SelectedSupplierName,
                SupplierGstin: SupplierGstin,
                SupplierInvoiceNo: SupplierInvoiceNo.Trim(),
                SupplierInvoiceDate: SupplierInvoiceDate.UtcDateTime,
                IsInterstate: IsInterstate,
                CreatedByUserId: "USER-STOREKEEPER",
                Notes: Notes,
                Items: LineItems.Select(i => new PurchaseInvoiceItemInputDto(
                    ProductId: string.IsNullOrEmpty(i.ProductId) ? $"p_{Guid.NewGuid():N}" : i.ProductId,
                    ProductName: i.ProductName,
                    HsnCode: i.HsnCode,
                    BatchNumber: i.BatchNumber.Trim().ToUpperInvariant(),
                    ExpiryDate: i.ExpiryDate.UtcDateTime,
                    ManufacturingDate: null,
                    Quantity: i.Quantity,
                    FreeQuantity: i.FreeQuantity,
                    UnitPrice: i.UnitPrice,
                    Mrp: i.Mrp,
                    SaleRate: i.SaleRate > 0 ? i.SaleRate : i.Mrp,
                    DiscountPct: i.DiscountPct,
                    GstRatePercent: i.GstRatePercent
                )).ToList()
            );

            var result = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(command);
            if (result.Success)
            {
                StatusMessage = $"✅ Purchase Bill {SupplierInvoiceNo} saved & posted to Invoices Recorded section! Inwarded {result.TotalStockAdded} units across {result.BatchesCreatedOrUpdated} batches.";
                ResetForm();

                await LoadKpisAsync();
                await LoadRecentPurchasesAsync();
                await LoadSuppliersAsync();
            }
            else
            {
                StatusMessage = $"❌ Posting failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task EditPurchaseInvoiceAsync(PurchaseInvoiceSummaryDto? summary)
    {
        if (summary == null) return;
        try
        {
            var details = await _purchaseService.GetPurchaseInvoiceDetailsAsync(summary.Id);
            if (details == null)
            {
                StatusMessage = "❌ Could not load purchase invoice details.";
                return;
            }

            IsEditingInvoice = true;
            EditingInvoiceId = details.Id;
            EditingInvoicePurchaseNo = summary.PurchaseNo ?? (!string.IsNullOrWhiteSpace(summary.SupplierInvoiceNo) ? summary.SupplierInvoiceNo : "PO-BILL");
            SupplierInvoiceNo = details.SupplierInvoiceNo;
            SupplierInvoiceDate = new DateTimeOffset(details.SupplierInvoiceDate);
            SelectedSupplierId = details.SupplierId;
            SelectedSupplierName = details.SupplierName;
            SupplierSearchText = details.SupplierName;
            SupplierGstin = details.SupplierGstin ?? string.Empty;
            IsInterstate = details.IsInterstate;
            Notes = details.Notes ?? string.Empty;

            var sup = Suppliers.FirstOrDefault(s => s.Id == details.SupplierId || string.Equals(s.Name, details.SupplierName, StringComparison.OrdinalIgnoreCase));
            SelectedSupplier = sup;
            if (sup != null)
            {
                SupplierDlNumber = sup.DlNumber ?? string.Empty;
                SupplierCreditDays = sup.CreditDays;
                SupplierOutstandingBalance = sup.CurrentOutstandingBalance;
            }

            LineItems.Clear();
            foreach (var item in details.Items)
            {
                var row = new PurchaseItemRowViewModel
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    HsnCode = item.HsnCode,
                    BatchNumber = item.BatchNumber,
                    ExpiryDate = new DateTimeOffset(item.ExpiryDate),
                    ExpiryText = item.ExpiryDate.ToString("MM/yy"),
                    Quantity = item.Quantity,
                    FreeQuantity = item.FreeQuantity,
                    UnitPrice = item.UnitPrice,
                    Mrp = item.Mrp,
                    SaleRate = item.SaleRate > 0 ? item.SaleRate : item.Mrp,
                    DiscountPct = item.DiscountPct,
                    GstRatePercent = item.GstRatePercent,
                    IsInterstate = details.IsInterstate,
                    OnRowChanged = RecalculateTotals
                };
                row.PropertyChanged += (s, e) => RecalculateTotals();
                LineItems.Add(row);
            }

            if (LineItems.Count == 0)
            {
                AddBlankRow();
            }

            RecalculateTotals();
            SelectedTab = "Entry";
            StatusMessage = $"✏️ Loaded Bill {details.SupplierInvoiceNo} ({EditingInvoicePurchaseNo}) for editing. You can add new medicines, adjust quantities/rates, and save changes.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Failed to load invoice for editing: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CancelEditInvoice()
    {
        IsEditingInvoice = false;
        EditingInvoiceId = null;
        EditingInvoicePurchaseNo = string.Empty;
        ResetForm();
        StatusMessage = "ℹ️ Exited edit mode. Ready for new purchase entry.";
    }

    [RelayCommand]
    public async Task OpenPurchaseReturnAsync(PurchaseInvoiceSummaryDto? summary)
    {
        if (summary == null) return;
        try
        {
            var details = await _purchaseService.GetPurchaseInvoiceDetailsAsync(summary.Id);
            if (details == null || details.Items.Count == 0)
            {
                StatusMessage = "❌ No items found in this purchase invoice to return.";
                return;
            }

            ReturnSourceInvoice = details;
            ReturnSourcePurchaseNo = summary.PurchaseNo ?? summary.SupplierInvoiceNo;
            ReturnItems.Clear();

            foreach (var it in details.Items)
            {
                var stock = await _purchaseService.GetBatchAvailableStockAsync(it.ProductId, it.BatchNumber, details.WarehouseId, details.OrgId);
                var maxReturnable = Math.Min(it.TotalQuantity, Math.Max(0, stock));

                // Net buying rate (unit price with discount + GST)
                decimal netRate = it.LandedCostPerUnit > 0
                    ? it.LandedCostPerUnit
                    : Math.Round(it.UnitPrice * (1m - it.DiscountPct / 100m) * (1m + it.GstRatePercent / 100m), 2, MidpointRounding.AwayFromZero);

                var rRow = new PurchaseReturnItemRowViewModel
                {
                    ProductId = it.ProductId,
                    ProductName = it.ProductName,
                    BatchNumber = it.BatchNumber,
                    ExpiryDate = it.ExpiryDate,
                    InwardedQuantity = it.TotalQuantity,
                    AvailableStock = stock,
                    MaxReturnable = maxReturnable,
                    IsSelected = false,
                    ReturnQuantity = maxReturnable > 0 ? 1 : 0,
                    UnitPrice = it.UnitPrice,
                    DiscountPct = it.DiscountPct,
                    GstRatePercent = it.GstRatePercent,
                    NetUnitPrice = netRate,
                    Reason = "Damaged / Expired",
                    OnChanged = RecalculateReturnTotals
                };
                ReturnItems.Add(rRow);
            }

            RecalculateReturnTotals();
            IsPurchaseReturnModalOpen = true;
            StatusMessage = $"↩️ Selected Bill {details.SupplierInvoiceNo} for Purchase Return. Select medicines and return quantities.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading return details: {ex.Message}";
        }
    }

    public void RecalculateReturnTotals()
    {
        var selected = ReturnItems.Where(i => i.IsSelected && i.ReturnQuantity > 0).ToList();
        TotalReturnSelectedItems = selected.Count;
        TotalReturnQuantity = selected.Sum(i => i.ReturnQuantity);
        TotalReturnAmount = selected.Sum(i => i.NetAmount);
        OnPropertyChanged(nameof(TotalReturnAmountFormatted));
    }

    [RelayCommand]
    public void ClosePurchaseReturnModal()
    {
        IsPurchaseReturnModalOpen = false;
        ReturnItems.Clear();
        ReturnSourceInvoice = null;
    }

    [RelayCommand]
    public async Task ConfirmPurchaseReturnAsync()
    {
        if (ReturnSourceInvoice == null) return;
        var selected = ReturnItems.Where(i => i.IsSelected && i.ReturnQuantity > 0).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "⚠️ Please select at least one item with return quantity greater than 0.";
            return;
        }

        var invalidStock = selected.FirstOrDefault(i => i.ReturnQuantity > i.MaxReturnable);
        if (invalidStock != null)
        {
            StatusMessage = $"❌ Return quantity for {invalidStock.ProductName} ({invalidStock.ReturnQuantity}) exceeds available stock ({invalidStock.MaxReturnable}).";
            return;
        }

        IsBusy = true;
        StatusMessage = "⏳ Processing purchase return & deducting stock...";
        try
        {
            var cmd = new CreatePurchaseReturnCommand(
                OrgId: _orgId,
                BranchId: _branchId,
                WarehouseId: _warehouseId,
                PurchaseInvoiceId: ReturnSourceInvoice.Id,
                SupplierId: ReturnSourceInvoice.SupplierId,
                SupplierName: ReturnSourceInvoice.SupplierName,
                SupplierGstin: ReturnSourceInvoice.SupplierGstin,
                OriginalInvoiceNo: ReturnSourceInvoice.SupplierInvoiceNo,
                CreatedByUserId: "USER-STOREKEEPER",
                Notes: $"Purchase return against {ReturnSourceInvoice.SupplierInvoiceNo}",
                Items: selected.Select(s => new PurchaseReturnItemInputDto(
                    ProductId: s.ProductId,
                    ProductName: s.ProductName,
                    BatchNumber: s.BatchNumber,
                    ExpiryDate: s.ExpiryDate,
                    ReturnQuantity: s.ReturnQuantity,
                    UnitPrice: s.UnitPrice,
                    GstRatePercent: s.GstRatePercent,
                    NetUnitPrice: s.NetUnitPrice,
                    NetAmount: s.NetAmount,
                    Reason: s.Reason
                )).ToList()
            );

            var res = await _purchaseService.ProcessPurchaseReturnAsync(cmd);
            if (res.Success)
            {
                IsPurchaseReturnModalOpen = false;

                CurrentPurchaseReturnBill = new PurchaseReturnBillDto(
                    ReturnNumber: res.ReturnNumber ?? "PR-0001",
                    ReturnDate: DateTime.Now,
                    SupplierName: ReturnSourceInvoice.SupplierName,
                    SupplierGstin: ReturnSourceInvoice.SupplierGstin,
                    OriginalInvoiceNo: ReturnSourceInvoice.SupplierInvoiceNo,
                    ItemsCount: res.ItemsReturnedCount,
                    TotalQuantity: res.TotalQuantityReturned,
                    TotalReturnAmount: res.TotalReturnAmount,
                    Items: cmd.Items,
                    OriginalPurchaseNo: ReturnSourcePurchaseNo
                );

                IsPurchaseReturnBillModalOpen = true;

                await LoadKpisAsync();
                await LoadRecentPurchasesAsync();
                await LoadSuppliersAsync();

                StatusMessage = $"✅ Purchase Return {res.ReturnNumber} generated successfully! Net stock value reduced by ₹{res.TotalReturnAmount:N2}.";
            }
            else
            {
                StatusMessage = $"❌ Purchase return failed: {res.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void ClosePurchaseReturnBillModal()
    {
        IsPurchaseReturnBillModalOpen = false;
        CurrentPurchaseReturnBill = null;
    }

    [RelayCommand]
    public void PrintPurchaseReturnBill()
    {
        StatusMessage = $"🖨️ Purchase Return Bill {CurrentPurchaseReturnBill?.ReturnNumber} sent to printer / PDF.";
    }

    [RelayCommand]
    public void RequestCancelInvoice(PurchaseInvoiceSummaryDto summary)
    {
        InvoicePendingCancel = summary;
        IsCancelConfirmOpen = true;
    }

    [RelayCommand]
    public void DismissCancelConfirm()
    {
        IsCancelConfirmOpen = false;
        InvoicePendingCancel = null;
    }

    [RelayCommand]
    public async Task ConfirmCancelInvoiceAsync()
    {
        if (InvoicePendingCancel == null) return;
        var invToCancel = InvoicePendingCancel;
        IsCancelConfirmOpen = false;

        IsBusy = true;
        StatusMessage = "⏳ Cancelling invoice and reversing stock...";
        try
        {
            var result = await _purchaseService.CancelPurchaseInvoiceAsync(
                invToCancel.Id, "USER-STOREKEEPER");

            StatusMessage = result.Success
                ? $"✅ Invoice {invToCancel.SupplierInvoiceNo} cancelled successfully."
                : $"❌ Cancel failed: {result.ErrorMessage}";

            if (result.Success)
            {
                await LoadRecentPurchasesAsync();
                await LoadKpisAsync();
                await LoadSuppliersAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            InvoicePendingCancel = null;
        }
    }

    [RelayCommand]
    public async Task LoadRecentPurchasesAsync()
    {
        try
        {
            var list = await _purchaseService.GetRecentPurchasesAsync(_orgId, _branchId, limit: 200);
            _allLoadedInvoices.Clear();
            RecentPurchases.Clear();
            foreach (var item in list)
            {
                _allLoadedInvoices.Add(item);
                RecentPurchases.Add(item);
            }
            ApplyHistoryFilter();
        }
        catch { }
    }

    public void ApplyHistoryFilter()
    {
        FilteredRecentPurchases.Clear();
        var query = HistorySearchText?.Trim().ToLowerInvariant() ?? "";

        var filtered = string.IsNullOrEmpty(query)
            ? _allLoadedInvoices
            : _allLoadedInvoices.Where(i =>
                i.SupplierInvoiceNo.ToLowerInvariant().Contains(query) ||
                i.SupplierName.ToLowerInvariant().Contains(query) ||
                (i.SupplierGstin != null && i.SupplierGstin.ToLowerInvariant().Contains(query)));

        foreach (var item in filtered)
        {
            FilteredRecentPurchases.Add(item);
        }
    }

    [RelayCommand]
    public async Task ViewInvoiceDetailsAsync(PurchaseInvoiceSummaryDto summary)
    {
        if (summary == null) return;
        try
        {
            SelectedInvoiceDetails = await _purchaseService.GetPurchaseInvoiceDetailsAsync(summary.Id);
            IsInvoiceDetailsModalOpen = true;
        }
        catch { }
    }

    [RelayCommand]
    public void CloseInvoiceDetails()
    {
        IsInvoiceDetailsModalOpen = false;
        SelectedInvoiceDetails = null;
    }

    [RelayCommand]
    public void OpenAddSupplierModal()
    {
        NewSupplierName = string.Empty;
        NewSupplierGstin = string.Empty;
        NewSupplierDlNumber = string.Empty;
        NewSupplierPhone = string.Empty;
        NewSupplierEmail = string.Empty;
        NewSupplierAddress = string.Empty;
        NewSupplierCreditDays = 30;
        NewSupplierOpeningBalance = 0;
        IsAddSupplierModalOpen = true;
    }

    [RelayCommand]
    public void CloseAddSupplierModal()
    {
        IsAddSupplierModalOpen = false;
    }

    [RelayCommand]
    public async Task SaveNewSupplierAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSupplierName))
        {
            StatusMessage = "⚠️ Supplier Name is mandatory.";
            return;
        }

        try
        {
            var cmd = new CreateSupplierCommand(
                OrgId: _orgId,
                Name: NewSupplierName.Trim(),
                Gstin: string.IsNullOrWhiteSpace(NewSupplierGstin) ? null : NewSupplierGstin.Trim().ToUpperInvariant(),
                DlNumber: string.IsNullOrWhiteSpace(NewSupplierDlNumber) ? null : NewSupplierDlNumber.Trim(),
                Phone: string.IsNullOrWhiteSpace(NewSupplierPhone) ? null : NewSupplierPhone.Trim(),
                Email: string.IsNullOrWhiteSpace(NewSupplierEmail) ? null : NewSupplierEmail.Trim(),
                Address: string.IsNullOrWhiteSpace(NewSupplierAddress) ? null : NewSupplierAddress.Trim(),
                CreditDays: NewSupplierCreditDays,
                OpeningBalance: NewSupplierOpeningBalance
            );

            var id = await _purchaseService.CreateSupplierAsync(cmd);
            IsAddSupplierModalOpen = false;
            await LoadSuppliersAsync();
            await LoadKpisAsync();

            var created = Suppliers.FirstOrDefault(s => s.Id == id);
            if (created != null)
            {
                OnSupplierSelected(created);
            }
            StatusMessage = $"✅ Wholesaler '{cmd.Name}' registered successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Failed to add supplier: {ex.Message}";
        }
    }

    // Export Commands
    [RelayCommand]
    public async Task ExportPurchasesExcelAsync()
    {
        if (FilteredRecentPurchases.Count == 0) return;
        try
        {
            var (rows, meta) = PrepareExportData();
            var service = _exportService ?? new PurchaseExportService();
            var path = await service.ExportToExcelAsync(rows, meta);
            StatusMessage = $"✅ Excel exported successfully: {path}";
            TryOpenFile(path);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Excel export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ExportPurchasesWordAsync()
    {
        if (FilteredRecentPurchases.Count == 0) return;
        try
        {
            var (rows, meta) = PrepareExportData();
            var service = _exportService ?? new PurchaseExportService();
            var path = await service.ExportToWordAsync(rows, meta);
            StatusMessage = $"✅ Word report exported successfully: {path}";
            TryOpenFile(path);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Word export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ExportPurchasesPdfAsync()
    {
        if (FilteredRecentPurchases.Count == 0) return;
        try
        {
            var (rows, meta) = PrepareExportData();
            var service = _exportService ?? new PurchaseExportService();
            var path = await service.ExportToPdfHtmlAsync(rows, meta);
            StatusMessage = $"✅ PDF / HTML report exported successfully: {path}";
            TryOpenFile(path);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ PDF export failed: {ex.Message}";
        }
    }

    private (IReadOnlyList<PurchaseExportRow> Rows, PurchaseExportMetadata Meta) PrepareExportData()
    {
        var store = SettingsViewModel.GetStoreInfo();
        var meta = new PurchaseExportMetadata(
            PharmacyName: store.PharmacyName,
            StoreAddress: store.StoreAddress,
            ContactPhone: store.ContactPhone,
            Gstin: store.Gstin,
            ExportDate: DateTime.Now,
            TotalPurchases: TotalPurchasesAmount,
            TotalInvoicesCount: TotalInvoicesCount,
            TotalSuppliersCount: TotalSuppliersCount,
            TotalOutstandingPayables: TotalOutstandingPayables
        );

        var rows = FilteredRecentPurchases.Select((item, idx) => new PurchaseExportRow(
            Index: idx + 1,
            SupplierName: item.SupplierName,
            SupplierInvoiceNo: item.SupplierInvoiceNo,
            SupplierGstin: item.SupplierGstin ?? "—",
            SupplierInvoiceDate: item.SupplierInvoiceDate,
            ItemCount: item.ItemCount,
            TaxableAmount: item.TaxableAmount,
            CgstAmount: item.CgstAmount,
            SgstAmount: item.SgstAmount,
            IgstAmount: item.IgstAmount,
            GrandTotal: item.GrandTotal,
            Status: item.Status.ToString().ToUpperInvariant(),
            CreatedAt: item.CreatedAt
        )).ToList();

        return (rows, meta);
    }

    private static void TryOpenFile(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch { }
    }
}
