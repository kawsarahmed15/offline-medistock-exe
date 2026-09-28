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
    private string _hsnCode = "30049099";

    [ObservableProperty]
    private string _unit = "STRIP";

    [ObservableProperty]
    private int _packUnits = 10;

    [ObservableProperty]
    private string _batchNumber = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _expiryDate = DateTimeOffset.UtcNow.AddMonths(24);

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

    partial void OnMrpChanged(decimal value)
    {
        if (SaleRate == 0 || SaleRate == _previousMrp)
        {
            SaleRate = value;
            OnPropertyChanged(nameof(SaleRateDouble));
        }
        _previousMrp = value;
        Recalculate();
    }

    partial void OnExpiryTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var clean = value.Trim();
        if (clean.Length >= 4 && clean.Contains('/'))
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
    }

    public decimal QuantityDouble
    {
        get => Quantity;
        set { Quantity = value; OnPropertyChanged(); Recalculate(); }
    }

    public decimal FreeQuantityDouble
    {
        get => FreeQuantity;
        set { FreeQuantity = value; OnPropertyChanged(); Recalculate(); }
    }

    public decimal UnitPriceDouble
    {
        get => UnitPrice;
        set { UnitPrice = value; OnPropertyChanged(); Recalculate(); }
    }

    public decimal MrpDouble
    {
        get => Mrp;
        set
        {
            Mrp = value;
            OnPropertyChanged();
            if (SaleRate == 0 || SaleRate == _previousMrp)
            {
                SaleRate = value;
                OnPropertyChanged(nameof(SaleRate));
                OnPropertyChanged(nameof(SaleRateDouble));
            }
            _previousMrp = value;
            Recalculate();
        }
    }

    public decimal SaleRateDouble
    {
        get => SaleRate;
        set { SaleRate = value; OnPropertyChanged(); Recalculate(); }
    }

    public decimal DiscountPctDouble
    {
        get => DiscountPct;
        set { DiscountPct = value; OnPropertyChanged(); Recalculate(); }
    }

    public decimal GstRatePercentDouble
    {
        get => GstRatePercent;
        set { GstRatePercent = value; OnPropertyChanged(); Recalculate(); }
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

    // Inward Header Fields
    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private string _selectedSupplierId = string.Empty;

    [ObservableProperty]
    private string _selectedSupplierName = "Apex Pharma Wholesalers & Distributors";

    [ObservableProperty]
    private string _supplierGstin = "07AABCU9603R1ZM";

    [ObservableProperty]
    private string _supplierDlNumber = "DL-20B-112233";

    [ObservableProperty]
    private int _supplierCreditDays = 30;

    [ObservableProperty]
    private decimal _supplierOutstandingBalance = 0;

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

    partial void OnSelectedProductSearchIndexChanged(int value)
    {
        for (int i = 0; i < ProductSearchResults.Count; i++)
        {
            ProductSearchResults[i].IsSelected = (i == value);
        }
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
    public async Task LoadKpisAsync()
    {
        try
        {
            var kpis = await _purchaseService.GetPurchaseKpiSummaryAsync(_orgId, _branchId);
            TotalPurchasesAmount = kpis.TotalPurchaseAmount;
            TotalInvoicesCount = kpis.TotalInvoicesCount;
            TotalSuppliersCount = kpis.TotalSuppliersCount;
            TotalOutstandingPayables = kpis.TotalOutstandingPayable;

            OnPropertyChanged(nameof(TotalPurchasesFormatted));
            OnPropertyChanged(nameof(TotalOutstandingPayablesFormatted));
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
            foreach (var s in list)
            {
                Suppliers.Add(s);
                FilteredSuppliers.Add(s);
            }

            if (Suppliers.Count > 0 && SelectedSupplier == null)
            {
                OnSupplierSelected(Suppliers[0]);
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
            ExpiryDate = DateTimeOffset.UtcNow.AddMonths(24),
            ExpiryText = DateTimeOffset.UtcNow.AddMonths(24).ToString("MM/yy")
        };
        row.PropertyChanged += (s, e) => RecalculateTotals();
        LineItems.Add(row);
        ActiveRowIndex = LineItems.Count - 1;
        RecalculateTotals();
    }

    [RelayCommand]
    public void AddNewRow() => AddBlankRow();

    [RelayCommand]
    public void RemoveRow(PurchaseItemRowViewModel row)
    {
        if (LineItems.Count > 1)
        {
            var idx = LineItems.IndexOf(row);
            LineItems.Remove(row);
            if (ActiveRowIndex >= LineItems.Count)
            {
                ActiveRowIndex = LineItems.Count - 1;
            }
            RecalculateTotals();
        }
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
        row.HsnCode = string.IsNullOrWhiteSpace(item.HsnCode) ? "3004" : item.HsnCode;
        row.GstRatePercent = item.GstRatePercent > 0 ? item.GstRatePercent : SettingsViewModel.GetDefaultGstRate();
        row.Mrp = item.Mrp;
        row.SaleRate = item.SaleRate > 0 ? item.SaleRate : item.Mrp;

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
        if (!string.IsNullOrWhiteSpace(item.BatchNumber)) row.BatchNumber = item.BatchNumber;
        if (item.NearestExpiryDate.HasValue)
        {
            row.ExpiryDate = item.NearestExpiryDate.Value;
            row.ExpiryText = item.NearestExpiryDate.Value.ToString("MM/yy");
        }

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
            if (string.IsNullOrWhiteSpace(row.HsnCode)) row.HsnCode = "30049099";
            if (row.GstRatePercent <= 0) row.GstRatePercent = SettingsViewModel.GetDefaultGstRate();
            if (row.Quantity <= 0) row.Quantity = 1;
            if (row.ExpiryDate == default || row.ExpiryDate <= DateTimeOffset.UtcNow)
            {
                row.ExpiryDate = DateTimeOffset.UtcNow.AddMonths(24);
                row.ExpiryText = row.ExpiryDate.ToString("MM/yy");
            }
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

            var prod = ProductSearchItemViewModel.FromDto(results[0]);
            SelectProductSearch(prod, row);
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
                row.HsnCode = prod.HsnCode;
                row.GstRatePercent = prod.GstRatePercent > 0 ? prod.GstRatePercent : 12.0m;
                if (prod.Mrp > 0) row.Mrp = prod.Mrp;
                if (prod.SaleRate > 0) row.SaleRate = prod.SaleRate;
                if (row.UnitPrice == 0 && prod.Mrp > 0) row.UnitPrice = Math.Round(prod.Mrp * 0.70m, 2);
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

    [RelayCommand]
    public async Task PostPurchaseInvoiceAsync()
    {
        if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
        {
            StatusMessage = "⚠️ Please enter the Wholesaler Supplier Invoice Number.";
            return;
        }

        if (LineItems.Count == 0)
        {
            StatusMessage = "⚠️ Add at least one medicine line item to inward.";
            return;
        }

        // Validate batch numbers and quantities
        foreach (var item in LineItems)
        {
            if (string.IsNullOrWhiteSpace(item.BatchNumber))
            {
                StatusMessage = $"⚠️ Batch number is mandatory for '{item.ProductName}'.";
                return;
            }
            if (item.Quantity <= 0)
            {
                StatusMessage = $"⚠️ Quantity must be greater than zero for '{item.ProductName}'.";
                return;
            }
        }

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
                StatusMessage = $"✅ Invoice {SupplierInvoiceNo} posted successfully! Inwarded {result.TotalStockAdded} units across {result.BatchesCreatedOrUpdated} batches.";
                LineItems.Clear();
                AddBlankRow();
                SupplierInvoiceNo = string.Empty;
                Notes = string.Empty;

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
