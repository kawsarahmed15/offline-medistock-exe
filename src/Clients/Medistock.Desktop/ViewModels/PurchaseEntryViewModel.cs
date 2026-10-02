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
using Medistock.Domain.Common;
using Medistock.Domain.Products;
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
    private string _stockType = "Tablet (Tab)";

    [ObservableProperty]
    private int _packUnits = 10;

    [ObservableProperty]
    private decimal _stripCount = 1;

    [ObservableProperty]
    private decimal _piecesPerStrip = 10;

    [ObservableProperty]
    private decimal _stripPrice = 0;

    [ObservableProperty]
    private decimal _stripMrp = 0;

    private bool _isSyncingPackQty = false;
    private bool _isSyncingRates = false;

    [ObservableProperty]
    private string _batchNumber = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _expiryDate = default;

    private decimal _previousMrp = 0;

    [ObservableProperty]
    private decimal _quantity = 10;

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

    partial void OnStripCountChanged(decimal value)
    {
        if (!_isSyncingPackQty)
        {
            _isSyncingPackQty = true;
            try
            {
                Quantity = Math.Max(0, value * PiecesPerStrip);
                OnPropertyChanged(nameof(QuantityDouble));
            }
            finally
            {
                _isSyncingPackQty = false;
            }
        }
        OnPropertyChanged(nameof(StripCountDouble));
        OnPropertyChanged(nameof(PackCalculationDisplay));
        Recalculate();
    }

    partial void OnPiecesPerStripChanged(decimal value)
    {
        if (!_isSyncingPackQty)
        {
            _isSyncingPackQty = true;
            try
            {
                PackUnits = (int)Math.Max(1, value);
                Quantity = Math.Max(0, StripCount * value);
                OnPropertyChanged(nameof(QuantityDouble));
            }
            finally
            {
                _isSyncingPackQty = false;
            }
        }

        if (!_isSyncingRates && value > 0)
        {
            _isSyncingRates = true;
            try
            {
                // Inward pricing is based on strip price; calculate piece rate = strip price / pieces in strip
                if (StripPrice > 0)
                {
                    UnitPrice = Math.Round(StripPrice / value, 4);
                    OnPropertyChanged(nameof(UnitPriceDouble));
                }
                else if (UnitPrice > 0)
                {
                    StripPrice = Math.Round(UnitPrice * value, 2);
                    OnPropertyChanged(nameof(StripPriceDouble));
                }

                if (StripMrp > 0)
                {
                    Mrp = Math.Round(StripMrp / value, 4);
                    SaleRate = Mrp;
                    OnPropertyChanged(nameof(MrpDouble));
                    OnPropertyChanged(nameof(SaleRateDouble));
                }
                else if (Mrp > 0)
                {
                    StripMrp = Math.Round(Mrp * value, 2);
                    OnPropertyChanged(nameof(StripMrpDouble));
                }
            }
            finally
            {
                _isSyncingRates = false;
            }
        }

        OnPropertyChanged(nameof(PiecesPerStripDouble));
        OnPropertyChanged(nameof(PackCalculationDisplay));
        OnPropertyChanged(nameof(UnitPricePieceDisplay));
        OnPropertyChanged(nameof(MrpPieceDisplay));
        OnPropertyChanged(nameof(StripPriceBreakdownDisplay));
        OnPropertyChanged(nameof(StripMrpBreakdownDisplay));
        Recalculate();
    }

    partial void OnStripPriceChanged(decimal value)
    {
        if (!_isSyncingRates)
        {
            _isSyncingRates = true;
            try
            {
                var pcs = PiecesPerStrip > 0 ? PiecesPerStrip : 1;
                UnitPrice = Math.Round(value / pcs, 4);
                OnPropertyChanged(nameof(UnitPriceDouble));
            }
            finally
            {
                _isSyncingRates = false;
            }
        }
        OnPropertyChanged(nameof(StripPriceDouble));
        OnPropertyChanged(nameof(UnitPricePieceDisplay));
        OnPropertyChanged(nameof(StripPriceBreakdownDisplay));
        Recalculate();
    }

    partial void OnStripMrpChanged(decimal value)
    {
        if (!_isSyncingRates)
        {
            _isSyncingRates = true;
            try
            {
                var pcs = PiecesPerStrip > 0 ? PiecesPerStrip : 1;
                var perPiece = Math.Round(value / pcs, 4);
                Mrp = perPiece;
                SaleRate = perPiece; // Selling price considered as MRP
                _previousMrp = perPiece;
                OnPropertyChanged(nameof(MrpDouble));
                OnPropertyChanged(nameof(SaleRateDouble));
                OnPropertyChanged(nameof(SaleRate));
            }
            finally
            {
                _isSyncingRates = false;
            }
        }
        OnPropertyChanged(nameof(StripMrpDouble));
        OnPropertyChanged(nameof(MrpPieceDisplay));
        OnPropertyChanged(nameof(StripMrpBreakdownDisplay));
        Recalculate();
    }

    partial void OnQuantityChanged(decimal value)
    {
        if (!_isSyncingPackQty)
        {
            _isSyncingPackQty = true;
            try
            {
                if (PiecesPerStrip > 0)
                {
                    StripCount = Math.Round(value / PiecesPerStrip, 2);
                    OnPropertyChanged(nameof(StripCountDouble));
                }
            }
            finally
            {
                _isSyncingPackQty = false;
            }
        }
        OnPropertyChanged(nameof(QuantityDouble));
        OnPropertyChanged(nameof(PackCalculationDisplay));
        Recalculate();
    }

    partial void OnFreeQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(FreeQuantityDouble));
        Recalculate();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        if (!_isSyncingRates)
        {
            _isSyncingRates = true;
            try
            {
                var pcs = PiecesPerStrip > 0 ? PiecesPerStrip : 1;
                StripPrice = Math.Round(value * pcs, 2);
                OnPropertyChanged(nameof(StripPriceDouble));
            }
            finally
            {
                _isSyncingRates = false;
            }
        }
        OnPropertyChanged(nameof(UnitPriceDouble));
        OnPropertyChanged(nameof(UnitPricePieceDisplay));
        OnPropertyChanged(nameof(StripPriceBreakdownDisplay));
        Recalculate();
    }

    partial void OnMrpChanged(decimal value)
    {
        if (!_isSyncingRates)
        {
            _isSyncingRates = true;
            try
            {
                var pcs = PiecesPerStrip > 0 ? PiecesPerStrip : 1;
                StripMrp = Math.Round(value * pcs, 2);
                SaleRate = value; // MRP is the sale price
                _previousMrp = value;
                OnPropertyChanged(nameof(StripMrpDouble));
                OnPropertyChanged(nameof(SaleRateDouble));
                OnPropertyChanged(nameof(SaleRate));
            }
            finally
            {
                _isSyncingRates = false;
            }
        }
        else
        {
            SaleRate = value;
            _previousMrp = value;
            OnPropertyChanged(nameof(SaleRateDouble));
            OnPropertyChanged(nameof(SaleRate));
        }
        OnPropertyChanged(nameof(MrpDouble));
        OnPropertyChanged(nameof(MrpPieceDisplay));
        OnPropertyChanged(nameof(StripMrpBreakdownDisplay));
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

    public double StripCountDouble
    {
        get => (double)StripCount;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                StripCount = (decimal)value;
            }
        }
    }

    public double PiecesPerStripDouble
    {
        get => (double)PiecesPerStrip;
        set
        {
            if (!double.IsNaN(value) && value > 0)
            {
                PiecesPerStrip = (decimal)value;
            }
        }
    }

    public string UnitPackagingHoverText
    {
        get
        {
            if (PiecesPerStrip > 1 || Unit == "STRIP")
            {
                return $"{StripCount:0.##} Strip{(StripCount == 1 ? "" : "s")} ({PiecesPerStrip:0.##} Pcs/Strip) — Total {Quantity:0.##} Pcs";
            }
            if (!string.IsNullOrWhiteSpace(StockType))
            {
                if (StockType.Contains("Syrup") || StockType.Contains("Drop") || StockType.Contains("Inj"))
                {
                    return $"{Quantity:0.##} Unit{(Quantity == 1 ? "" : "s")} ({PackUnits}ml)";
                }
                if (StockType.Contains("Cream"))
                {
                    return $"{Quantity:0.##} Tube{(Quantity == 1 ? "" : "s")} ({PackUnits}gm)";
                }
            }
            return $"{Quantity:0.##} {Unit}";
        }
    }

    public string PackCalculationDisplay
    {
        get
        {
            if (PiecesPerStrip > 1 || Unit == "STRIP")
            {
                return $"{StripCount:0.##} Strip × {PiecesPerStrip:0.##} Pcs = {Quantity:0.##} Total Qty";
            }
            if (!string.IsNullOrWhiteSpace(StockType) && (StockType.Contains("Syrup") || StockType.Contains("Drop") || StockType.Contains("Inj") || StockType.Contains("Cream")))
            {
                return $"{Quantity:0.##} Units ({PackUnits}{(StockType.Contains("Cream") ? "gm" : "ml")})";
            }
            return $"{Quantity:0.##} {Unit}";
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

    public double StripPriceDouble
    {
        get => (double)StripPrice;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                StripPrice = (decimal)value;
            }
        }
    }

    public double StripMrpDouble
    {
        get => (double)StripMrp;
        set
        {
            if (!double.IsNaN(value) && value >= 0)
            {
                StripMrp = (decimal)value;
            }
        }
    }

    public string UnitPricePieceDisplay => PiecesPerStrip > 1
        ? $"₹{UnitPrice:0.00}/pc"
        : $"₹{UnitPrice:0.00}";

    public string MrpPieceDisplay => PiecesPerStrip > 1
        ? $"₹{Mrp:0.00}/pc"
        : $"₹{Mrp:0.00}";

    public string StripPriceBreakdownDisplay => PiecesPerStrip > 1
        ? $"Strip Buying Rate: ₹{StripPrice:0.##} ÷ {PiecesPerStrip:0.##} Pcs = ₹{UnitPrice:0.####}/Pc"
        : $"Buying Rate: ₹{UnitPrice:0.##}";

    public string StripMrpBreakdownDisplay => PiecesPerStrip > 1
        ? $"Strip MRP: ₹{StripMrp:0.##} ÷ {PiecesPerStrip:0.##} Pcs = ₹{Mrp:0.####}/Pc"
        : $"MRP: ₹{Mrp:0.##}";

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
        OnPropertyChanged(nameof(UnitPricePieceDisplay));
        OnPropertyChanged(nameof(MrpPieceDisplay));
        OnPropertyChanged(nameof(StripPriceBreakdownDisplay));
        OnPropertyChanged(nameof(StripMrpBreakdownDisplay));
        OnPropertyChanged(nameof(StripPriceDouble));
        OnPropertyChanged(nameof(StripMrpDouble));
        OnPropertyChanged(nameof(UnitPackagingHoverText));
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
    private readonly Medistock.Application.Products.Commands.IProductService? _productService;
    private readonly string _orgId = "org-1";
    private readonly string _branchId = "br-1";
    private readonly string _warehouseId = "wh-1";

#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private string _selectedTab = "History"; // "History", "Suppliers", "Entry"

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

    public string SaveButtonText => IsEditingInvoice ? "Save Changes [Ctrl+S]" : "Save [Ctrl+S]";
    public string FormHeaderTitle => IsEditingInvoice 
        ? $"✏️ EDITING PURCHASE BILL ({EditingInvoicePurchaseNo})" 
        : "New Purchase Inward Entry";

    [ObservableProperty]
    private bool _isPurchaseEntryScreenOpen = false;

    [ObservableProperty]
    private bool _isExitConfirmDialogOpen = false;

    [ObservableProperty]
    private bool _isSaveSummaryModalOpen = false;

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
    private string _supplierInvoiceDateText = DateTime.UtcNow.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    partial void OnSupplierInvoiceDateChanged(DateTimeOffset value)
    {
        _supplierInvoiceDateText = value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SupplierInvoiceDateText));
    }

    partial void OnSupplierInvoiceDateTextChanged(string value)
    {
        if (DateTime.TryParseExact(value?.Trim(), new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" },
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
        {
            _supplierInvoiceDate = new DateTimeOffset(dt, TimeSpan.Zero);
            OnPropertyChanged(nameof(SupplierInvoiceDate));
        }
    }

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

    public decimal TotalTax => Math.Round(CgstTotal + SgstTotal + IgstTotal, 2, MidpointRounding.AwayFromZero);
    public IEnumerable<PurchaseItemRowViewModel> ValidLineItems => LineItems.Where(i => !string.IsNullOrWhiteSpace(i.ProductName));
    public ObservableCollection<PurchaseItemRowViewModel> PreviewLineItems { get; } = new();

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

    // Add Product Modal State
    [ObservableProperty]
    private bool _isAddProductModalOpen = false;

    [ObservableProperty]
    private string _newProductName = string.Empty;

    [ObservableProperty]
    private string _newProductCategory = string.Empty;

    [ObservableProperty]
    private string _newProductManufacturer = string.Empty;

    [ObservableProperty]
    private decimal _newProductInitialStockQty = 10;

    [ObservableProperty]
    private decimal _newProductBuyingPrice = 0;

    [ObservableProperty]
    private decimal _newProductSellingPrice = 0;

    [ObservableProperty]
    private decimal _newProductMrp = 0;

    [ObservableProperty]
    private string _newProductStockType = "Tablet (Tab)";

    [ObservableProperty]
    private int _newProductPackOptions = 10;

    [ObservableProperty]
    private int _newProductStripCount = 1;

    [ObservableProperty]
    private int _newProductPcsPerStrip = 10;

    [ObservableProperty]
    private string _newProductPackSizeText = "100ml";

    [ObservableProperty]
    private string _newProductExpiryText = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _newProductExpiryDate = default;

    [ObservableProperty]
    private decimal _newProductTaxPercent = SettingsViewModel.GetDefaultGstRate();

    [ObservableProperty]
    private string _newProductBatch = string.Empty;

    [ObservableProperty]
    private string _newProductHsnCode = "3004";

    [ObservableProperty]
    private bool _newProductIsPrescriptionRequired = false;

    [ObservableProperty]
    private string _newProductValidationMessage = string.Empty;

    public PurchaseItemRowViewModel? PendingProductRow { get; set; }

    public ObservableCollection<string> StockTypeOptions { get; } = new()
    {
        "General / Other",
        "Tablet (Tab)",
        "Capsule (Cap)",
        "Syrup (Syp)",
        "Injection (Inj)",
        "Cream",
        "Drop"
    };

    public ObservableCollection<decimal> TaxRateOptions { get; } = new()
    {
        0m,
        5m,
        12m,
        18m,
        28m
    };

    public ObservableCollection<string> VolumeMlOptions { get; } = new()
    {
        "2ml",
        "5ml",
        "10ml",
        "15ml",
        "30ml",
        "60ml",
        "100ml",
        "200ml",
        "450ml"
    };

    public ObservableCollection<string> WeightGmOptions { get; } = new()
    {
        "5gm",
        "10gm",
        "15gm",
        "20gm",
        "25gm",
        "30gm",
        "50gm",
        "100gm"
    };

    public bool IsTabletOrCapsule => NewProductStockType is "Tablet (Tab)" or "Capsule (Cap)";
    public bool IsVolumeMlType => NewProductStockType is "Syrup (Syp)" or "Injection (Inj)" or "Drop";
    public bool IsWeightGmType => NewProductStockType == "Cream";
    public bool IsGeneralType => NewProductStockType == "General / Other";

    public decimal NewProductPieceBuyingPrice =>
        IsTabletOrCapsule && NewProductPcsPerStrip > 0
            ? Math.Round(NewProductBuyingPrice / NewProductPcsPerStrip, 2)
            : NewProductBuyingPrice;

    public decimal NewProductPieceMrp =>
        IsTabletOrCapsule && NewProductPcsPerStrip > 0
            ? Math.Round(NewProductMrp / NewProductPcsPerStrip, 2)
            : NewProductMrp;

    public string NewProductMrpHeader => IsTabletOrCapsule ? "Strip MRP (₹) *" : "MRP (₹) *";
    public string NewProductBuyingPriceHeader => IsTabletOrCapsule ? "Strip Buying Price (₹)" : "Buying Price (₹)";

    public string NewProductPriceBreakdownText
    {
        get
        {
            if (IsTabletOrCapsule && NewProductPcsPerStrip > 0)
            {
                return $"1 Pc MRP = ₹{NewProductPieceMrp:0.00} (₹{NewProductMrp:0.##} ÷ {NewProductPcsPerStrip}) | 1 Pc Buying = ₹{NewProductPieceBuyingPrice:0.00} (₹{NewProductBuyingPrice:0.##} ÷ {NewProductPcsPerStrip})";
            }
            return $"Per Unit: MRP = ₹{NewProductMrp:0.00} | Buying = ₹{NewProductBuyingPrice:0.00}";
        }
    }

    public string NewProductStockBreakdownText
    {
        get
        {
            if (IsTabletOrCapsule)
            {
                var strips = NewProductStripCount;
                var totalUnits = (int)NewProductInitialStockQty;
                return $"Available: {totalUnits} Pcs ({strips} {(strips == 1 ? "Strip" : "Strips")} × {NewProductPcsPerStrip} Pcs)";
            }
            else if (IsVolumeMlType)
            {
                var unitName = NewProductStockType == "Injection (Inj)" ? "Vials" : "Bottles";
                return $"Available: {(int)NewProductInitialStockQty} {unitName} ({NewProductPackSizeText})";
            }
            else if (IsWeightGmType)
            {
                return $"Available: {(int)NewProductInitialStockQty} Tubes ({NewProductPackSizeText})";
            }
            return $"Available: {(int)NewProductInitialStockQty} Units";
        }
    }

    public double NewProductStripCountDouble
    {
        get => NewProductStripCount;
        set { NewProductStripCount = double.IsNaN(value) ? 0 : (int)Math.Max(0, value); OnPropertyChanged(); }
    }

    public double NewProductPcsPerStripDouble
    {
        get => NewProductPcsPerStrip;
        set { NewProductPcsPerStrip = double.IsNaN(value) ? 10 : (int)Math.Max(1, value); OnPropertyChanged(); }
    }

    public double NewProductPackOptionsDouble
    {
        get => NewProductPackOptions;
        set { NewProductPackOptions = double.IsNaN(value) ? 10 : (int)Math.Max(1, value); OnPropertyChanged(); }
    }

    public double NewProductMrpDouble
    {
        get => (double)NewProductMrp;
        set { NewProductMrp = double.IsNaN(value) ? 0m : (decimal)Math.Max(0, value); OnPropertyChanged(); }
    }

    public double NewProductBuyingPriceDouble
    {
        get => (double)NewProductBuyingPrice;
        set { NewProductBuyingPrice = double.IsNaN(value) ? 0m : (decimal)Math.Max(0, value); OnPropertyChanged(); }
    }

    public double NewProductSellingPriceDouble
    {
        get => (double)NewProductSellingPrice;
        set { NewProductSellingPrice = double.IsNaN(value) ? 0m : (decimal)Math.Max(0, value); OnPropertyChanged(); }
    }

    public double NewProductInitialStockQtyDouble
    {
        get => (double)NewProductInitialStockQty;
        set { NewProductInitialStockQty = double.IsNaN(value) ? 0m : (decimal)Math.Max(0, value); OnPropertyChanged(); }
    }

    public bool HasNewProductValidationMessage => !string.IsNullOrWhiteSpace(NewProductValidationMessage);

    partial void OnNewProductBatchChanged(string value)
    {
        if (value != null)
        {
            var upper = value.ToUpperInvariant();
            if (upper != value)
            {
                NewProductBatch = upper;
            }
        }
    }

    partial void OnNewProductStripCountChanged(int value)
    {
        if (IsTabletOrCapsule)
        {
            NewProductInitialStockQty = Math.Max(0, value) * Math.Max(0, NewProductPcsPerStrip);
            OnPropertyChanged(nameof(NewProductInitialStockQtyDouble));
            OnPropertyChanged(nameof(NewProductStockBreakdownText));
        }
    }

    partial void OnNewProductPcsPerStripChanged(int value)
    {
        if (IsTabletOrCapsule)
        {
            NewProductPackOptions = Math.Max(1, value);
            NewProductInitialStockQty = Math.Max(0, NewProductStripCount) * Math.Max(0, value);
            OnPropertyChanged(nameof(NewProductPackOptionsDouble));
            OnPropertyChanged(nameof(NewProductInitialStockQtyDouble));
            OnPropertyChanged(nameof(NewProductStockBreakdownText));
        }
        OnPropertyChanged(nameof(NewProductPieceMrp));
        OnPropertyChanged(nameof(NewProductPieceBuyingPrice));
        OnPropertyChanged(nameof(NewProductPriceBreakdownText));
    }

    partial void OnNewProductPackSizeTextChanged(string value)
    {
        OnPropertyChanged(nameof(NewProductStockBreakdownText));
    }

    partial void OnNewProductInitialStockQtyChanged(decimal value)
    {
        OnPropertyChanged(nameof(NewProductStockBreakdownText));
    }

    partial void OnNewProductBuyingPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(NewProductBuyingPriceDouble));
        OnPropertyChanged(nameof(NewProductPieceBuyingPrice));
        OnPropertyChanged(nameof(NewProductPriceBreakdownText));
    }

    partial void OnNewProductMrpChanged(decimal value)
    {
        NewProductSellingPrice = value;
        OnPropertyChanged(nameof(NewProductSellingPriceDouble));
        OnPropertyChanged(nameof(NewProductPieceMrp));
        OnPropertyChanged(nameof(NewProductPriceBreakdownText));
    }

    partial void OnNewProductStockTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsTabletOrCapsule));
        OnPropertyChanged(nameof(IsVolumeMlType));
        OnPropertyChanged(nameof(IsWeightGmType));
        OnPropertyChanged(nameof(IsGeneralType));

        if (value is "Tablet (Tab)" or "Capsule (Cap)")
        {
            if (NewProductPcsPerStrip <= 1)
            {
                NewProductPcsPerStrip = 10;
            }
            if (NewProductStripCount <= 0)
            {
                NewProductStripCount = 1;
            }
            NewProductPackOptions = NewProductPcsPerStrip;
            NewProductInitialStockQty = NewProductStripCount * NewProductPcsPerStrip;
        }
        else if (value is "Syrup (Syp)")
        {
            NewProductPackOptions = 1;
            NewProductPackSizeText = "100ml";
            NewProductInitialStockQty = 0;
        }
        else if (value is "Injection (Inj)")
        {
            NewProductPackOptions = 1;
            NewProductPackSizeText = "2ml";
            NewProductInitialStockQty = 0;
        }
        else if (value is "Drop")
        {
            NewProductPackOptions = 1;
            NewProductPackSizeText = "10ml";
            NewProductInitialStockQty = 0;
        }
        else if (value is "Cream")
        {
            NewProductPackOptions = 1;
            NewProductPackSizeText = "20gm";
            NewProductInitialStockQty = 0;
        }
        else
        {
            NewProductPackOptions = 1;
            NewProductPackSizeText = "1 Unit";
            NewProductInitialStockQty = 0;
        }

        OnPropertyChanged(nameof(NewProductStripCountDouble));
        OnPropertyChanged(nameof(NewProductPcsPerStripDouble));
        OnPropertyChanged(nameof(NewProductPackOptionsDouble));
        OnPropertyChanged(nameof(NewProductInitialStockQtyDouble));
        OnPropertyChanged(nameof(NewProductStockBreakdownText));
        OnPropertyChanged(nameof(NewProductPieceMrp));
        OnPropertyChanged(nameof(NewProductPieceBuyingPrice));
        OnPropertyChanged(nameof(NewProductPriceBreakdownText));
        OnPropertyChanged(nameof(NewProductMrpHeader));
        OnPropertyChanged(nameof(NewProductBuyingPriceHeader));
    }

    partial void OnNewProductValidationMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasNewProductValidationMessage));
    }

    partial void OnNewProductExpiryTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            NewProductExpiryDate = default;
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
                        NewProductExpiryDate = new DateTimeOffset(new DateTime(year, month, daysInMonth, 23, 59, 59, DateTimeKind.Utc));
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
                    NewProductExpiryDate = new DateTimeOffset(new DateTime(year, month, daysInMonth, 23, 59, 59, DateTimeKind.Utc));
                }
            }
        }
    }
#pragma warning restore MVVMTK0045

    // Medicine Autocomplete Search Dropdown State
    [ObservableProperty]
    private bool _isProductSearchOpen = false;

    [ObservableProperty]
    private string _productSearchQuery = string.Empty;

    [ObservableProperty]
    private bool _hasProductSearchResults = false;

    public string AddNewProductButtonText => string.IsNullOrWhiteSpace(ProductSearchQuery)
        ? "➕ Add New Product [F3]"
        : $"➕ Add \"{ProductSearchQuery.Trim()}\" [F3]";

    partial void OnProductSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(AddNewProductButtonText));
        _ = SearchProductsTopAsync(value);
    }

    public async Task SearchProductsTopAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ProductSearchResults.Clear();
            HasProductSearchResults = false;
            SelectedProductSearchIndex = -1;
            return;
        }

        try
        {
            var results = await _productSearchRepository.SearchProductsAsync(query.Trim(), _warehouseId, limit: 15);
            ProductSearchResults.Clear();
            foreach (var item in results)
            {
                ProductSearchResults.Add(ProductSearchItemViewModel.FromDto(item, query: query));
            }

            HasProductSearchResults = true;
            SelectedProductSearchIndex = ProductSearchResults.Count > 0 ? 0 : -1;
        }
        catch
        {
            HasProductSearchResults = !string.IsNullOrWhiteSpace(query);
        }
    }

    [RelayCommand]
    public void AddSearchedProductModal()
    {
        var q = ProductSearchQuery;
        HasProductSearchResults = false;
        OpenAddProductModal(q, null);
    }

    [RelayCommand]
    public void OpenPurchaseEntry()
    {
        IsPurchaseEntryScreenOpen = true;
        SelectedTab = "Entry";
        StatusMessage = "🚚 Purchase Entry workspace ready.";
    }

    public bool HasUnsavedData =>
        SelectedSupplier != null ||
        !string.IsNullOrWhiteSpace(SupplierInvoiceNo) ||
        LineItems.Any(r => !string.IsNullOrWhiteSpace(r.ProductName) || r.UnitPrice > 0 || r.StripPrice > 0 || r.Mrp > 0);

    [RelayCommand]
    public void RequestClosePurchaseEntry()
    {
        if (HasUnsavedData)
        {
            IsExitConfirmDialogOpen = true;
        }
        else
        {
            DiscardAndExit();
        }
    }

    [RelayCommand]
    public void DiscardAndExit()
    {
        IsExitConfirmDialogOpen = false;
        IsPurchaseEntryScreenOpen = false;
        SelectedTab = "History";
        if (IsEditingInvoice)
        {
            CancelEditInvoice();
        }
        else
        {
            ResetForm();
        }
        StatusMessage = "Invoices Recorded Ledger.";
    }

    [RelayCommand]
    public void SaveDraftAndExit()
    {
        IsExitConfirmDialogOpen = false;
        IsPurchaseEntryScreenOpen = false;
        SelectedTab = "History";
        StatusMessage = "💾 Purchase draft preserved. Open Purchase Entry anytime to resume.";
    }

    [RelayCommand]
    public void CancelExitDialog()
    {
        IsExitConfirmDialogOpen = false;
    }

    [RelayCommand]
    public void RequestSaveSummary()
    {
        var validItems = LineItems.Where(i => !string.IsNullOrWhiteSpace(i.ProductName)).ToList();
        if (validItems.Count == 0)
        {
            StatusMessage = "⚠️ Please add at least one medicine before saving.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
        {
            SupplierInvoiceNo = $"INV-{DateTime.Now:yyyyMMdd-HHmm}";
        }

        if (string.IsNullOrWhiteSpace(SelectedSupplierName))
        {
            SelectedSupplierName = SelectedSupplier?.Name ?? "Direct / Cash Supplier";
        }

        RecalculateTotals();

        PreviewLineItems.Clear();
        foreach (var item in validItems)
        {
            PreviewLineItems.Add(item);
        }

        OnPropertyChanged(nameof(PreviewLineItems));
        OnPropertyChanged(nameof(ValidLineItems));
        OnPropertyChanged(nameof(TotalTax));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(TaxableSubtotal));
        OnPropertyChanged(nameof(SupplierInvoiceNo));
        OnPropertyChanged(nameof(SelectedSupplierName));
        OnPropertyChanged(nameof(SupplierInvoiceDateText));

        IsSaveSummaryModalOpen = true;
    }

    [RelayCommand]
    public async Task ConfirmAndSavePurchaseAsync()
    {
        IsSaveSummaryModalOpen = false;
        await PostPurchaseInvoiceAsync();
    }

    [RelayCommand]
    public void CancelSaveSummary()
    {
        IsSaveSummaryModalOpen = false;
    }

    public void SelectNextStockType()
    {
        var idx = StockTypeOptions.IndexOf(NewProductStockType);
        if (idx >= 0 && idx < StockTypeOptions.Count - 1)
        {
            NewProductStockType = StockTypeOptions[idx + 1];
        }
        else
        {
            NewProductStockType = StockTypeOptions[0];
        }
    }

    public void SelectPreviousStockType()
    {
        var idx = StockTypeOptions.IndexOf(NewProductStockType);
        if (idx > 0)
        {
            NewProductStockType = StockTypeOptions[idx - 1];
        }
        else
        {
            NewProductStockType = StockTypeOptions[^1];
        }
    }

    public PurchaseItemRowViewModel SelectTopProductSearch(ProductSearchItemViewModel item)
    {
        PurchaseItemRowViewModel? targetRow = LineItems.FirstOrDefault(IsBlankRow);
        if (targetRow == null)
        {
            AddBlankRow();
            targetRow = LineItems[^1];
        }

        SelectProductSearch(item, targetRow);
        ProductSearchQuery = string.Empty;
        HasProductSearchResults = false;
        SelectedProductSearchIndex = -1;
        return targetRow;
    }

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
        IPurchaseExportService? exportService = null,
        Medistock.Application.Products.Commands.IProductService? productService = null)
    {
        _purchaseService = purchaseService;
        _productSearchRepository = productSearchRepository;
        _exportService = exportService;
        _productService = productService;

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
            StripCount = 1,
            PiecesPerStrip = 10,
            Quantity = 10,
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
            StripCount = src.StripCount,
            PiecesPerStrip = src.PiecesPerStrip,
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
            IsInterstate = src.IsInterstate,
            StockType = src.StockType
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
                ProductSearchResults.Add(ProductSearchItemViewModel.FromDto(item, query: query));
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
        // Packaging breakdown: extract pieces per strip from pack size description (e.g. 10x10, 1x15)
        var breakdown = PackagingHelper.Parse(item.PackSizeDescription);
        var tabs = breakdown.TabsPerStrip > 0 ? breakdown.TabsPerStrip : 10;
        row.PiecesPerStrip = tabs;
        row.PackUnits = tabs;
        row.StripCount = 1;
        row.Quantity = tabs;
        row.StockType = item.DosageForm.ToString();

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
                OpenAddProductModal(query, row);
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

            OpenAddProductModal(query, row);
        }
        catch
        {
            OpenAddProductModal(query, row);
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
        OnPropertyChanged(nameof(TotalTax));
        OnPropertyChanged(nameof(ValidLineItems));
    }

    public void ResetForm()
    {
        LineItems.Clear();
        SupplierInvoiceNo = string.Empty;
        SupplierInvoiceDate = DateTimeOffset.UtcNow;
        SupplierInvoiceDateText = DateTime.UtcNow.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
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

                    IsPurchaseEntryScreenOpen = false;
                    SelectedTab = "History";

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

                IsPurchaseEntryScreenOpen = false;
                SelectedTab = "History";

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
                    PiecesPerStrip = 10,
                    Quantity = item.Quantity,
                    StripCount = Math.Round(item.Quantity / 10m, 2),
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
            IsPurchaseEntryScreenOpen = true;
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

    // Add Product Modal Commands
    [RelayCommand]
    public void OpenAddProductModal(object? parameter = null)
    {
        string? initialName = parameter as string;
        OpenAddProductModal(initialName, null);
    }

    public void OpenAddProductModal(string? initialName, PurchaseItemRowViewModel? targetRow)
    {
        PendingProductRow = (targetRow != null && IsBlankRow(targetRow)) ? targetRow : null;
        NewProductName = initialName?.Trim() ?? string.Empty;
        NewProductCategory = string.Empty;
        NewProductManufacturer = string.Empty;
        NewProductStockType = "Tablet (Tab)";
        NewProductStripCount = 1;
        NewProductPcsPerStrip = 10;
        NewProductPackOptions = 10;
        NewProductInitialStockQty = 10;
        NewProductBuyingPrice = 0;
        NewProductSellingPrice = 0;
        NewProductMrp = 0;
        NewProductPackSizeText = "100ml";
        NewProductExpiryText = string.Empty;
        NewProductExpiryDate = default;
        NewProductTaxPercent = SettingsViewModel.GetDefaultGstRate();
        NewProductBatch = string.Empty;
        NewProductHsnCode = "3004";
        NewProductIsPrescriptionRequired = false;
        NewProductValidationMessage = string.Empty;

        OnPropertyChanged(nameof(IsTabletOrCapsule));
        OnPropertyChanged(nameof(IsVolumeMlType));
        OnPropertyChanged(nameof(IsWeightGmType));
        OnPropertyChanged(nameof(IsGeneralType));
        OnPropertyChanged(nameof(NewProductStripCountDouble));
        OnPropertyChanged(nameof(NewProductPcsPerStripDouble));
        OnPropertyChanged(nameof(NewProductPackOptionsDouble));
        OnPropertyChanged(nameof(NewProductMrpDouble));
        OnPropertyChanged(nameof(NewProductBuyingPriceDouble));
        OnPropertyChanged(nameof(NewProductSellingPriceDouble));
        OnPropertyChanged(nameof(NewProductInitialStockQtyDouble));
        OnPropertyChanged(nameof(NewProductStockBreakdownText));
        OnPropertyChanged(nameof(NewProductPieceMrp));
        OnPropertyChanged(nameof(NewProductPieceBuyingPrice));
        OnPropertyChanged(nameof(NewProductPriceBreakdownText));
        OnPropertyChanged(nameof(NewProductMrpHeader));
        OnPropertyChanged(nameof(NewProductBuyingPriceHeader));
        OnPropertyChanged(nameof(HasNewProductValidationMessage));

        IsProductSearchOpen = false;
        IsAddProductModalOpen = true;
    }

    public void SelectNextTaxRate()
    {
        if (TaxRateOptions.Count == 0) return;
        int idx = TaxRateOptions.IndexOf(NewProductTaxPercent);
        if (idx == -1) idx = 0;
        else idx = (idx + 1) % TaxRateOptions.Count;
        NewProductTaxPercent = TaxRateOptions[idx];
    }

    public void SelectPreviousTaxRate()
    {
        if (TaxRateOptions.Count == 0) return;
        int idx = TaxRateOptions.IndexOf(NewProductTaxPercent);
        if (idx == -1 || idx == 0) idx = TaxRateOptions.Count - 1;
        else idx--;
        NewProductTaxPercent = TaxRateOptions[idx];
    }

    public void SelectNextPackSize()
    {
        if (IsVolumeMlType)
        {
            if (VolumeMlOptions.Count == 0) return;
            int idx = VolumeMlOptions.IndexOf(NewProductPackSizeText);
            if (idx == -1) idx = 0;
            else idx = (idx + 1) % VolumeMlOptions.Count;
            NewProductPackSizeText = VolumeMlOptions[idx];
        }
        else if (IsWeightGmType)
        {
            if (WeightGmOptions.Count == 0) return;
            int idx = WeightGmOptions.IndexOf(NewProductPackSizeText);
            if (idx == -1) idx = 0;
            else idx = (idx + 1) % WeightGmOptions.Count;
            NewProductPackSizeText = WeightGmOptions[idx];
        }
        else if (IsGeneralType)
        {
            NewProductPackOptionsDouble = Math.Max(1, NewProductPackOptionsDouble + 1);
        }
        else if (IsTabletOrCapsule)
        {
            NewProductPcsPerStripDouble = Math.Max(1, NewProductPcsPerStripDouble + 1);
        }
    }

    public void SelectPreviousPackSize()
    {
        if (IsVolumeMlType)
        {
            if (VolumeMlOptions.Count == 0) return;
            int idx = VolumeMlOptions.IndexOf(NewProductPackSizeText);
            if (idx == -1 || idx == 0) idx = VolumeMlOptions.Count - 1;
            else idx--;
            NewProductPackSizeText = VolumeMlOptions[idx];
        }
        else if (IsWeightGmType)
        {
            if (WeightGmOptions.Count == 0) return;
            int idx = WeightGmOptions.IndexOf(NewProductPackSizeText);
            if (idx == -1 || idx == 0) idx = WeightGmOptions.Count - 1;
            else idx--;
            NewProductPackSizeText = WeightGmOptions[idx];
        }
        else if (IsGeneralType)
        {
            NewProductPackOptionsDouble = Math.Max(1, NewProductPackOptionsDouble - 1);
        }
        else if (IsTabletOrCapsule)
        {
            NewProductPcsPerStripDouble = Math.Max(1, NewProductPcsPerStripDouble - 1);
        }
    }

    [RelayCommand]
    public void CloseAddProductModal()
    {
        IsAddProductModalOpen = false;
        NewProductValidationMessage = string.Empty;
        PendingProductRow = null;
        ResetNewProductForm();
    }

    public void ResetNewProductForm()
    {
        NewProductName = string.Empty;
        NewProductCategory = string.Empty;
        NewProductManufacturer = string.Empty;
        NewProductStockType = "Tablet (Tab)";
        NewProductStripCount = 1;
        NewProductPcsPerStrip = 10;
        NewProductPackOptions = 10;
        NewProductInitialStockQty = 10;
        NewProductBuyingPrice = 0;
        NewProductSellingPrice = 0;
        NewProductMrp = 0;
        NewProductPackSizeText = "100ml";
        NewProductExpiryText = string.Empty;
        NewProductExpiryDate = default;
        NewProductTaxPercent = SettingsViewModel.GetDefaultGstRate();
        NewProductBatch = string.Empty;
        NewProductHsnCode = "3004";
        NewProductIsPrescriptionRequired = false;
        NewProductValidationMessage = string.Empty;

        OnPropertyChanged(nameof(IsTabletOrCapsule));
        OnPropertyChanged(nameof(IsVolumeMlType));
        OnPropertyChanged(nameof(IsWeightGmType));
        OnPropertyChanged(nameof(IsGeneralType));
        OnPropertyChanged(nameof(NewProductStripCountDouble));
        OnPropertyChanged(nameof(NewProductPcsPerStripDouble));
        OnPropertyChanged(nameof(NewProductPackOptionsDouble));
        OnPropertyChanged(nameof(NewProductMrpDouble));
        OnPropertyChanged(nameof(NewProductBuyingPriceDouble));
        OnPropertyChanged(nameof(NewProductSellingPriceDouble));
        OnPropertyChanged(nameof(NewProductInitialStockQtyDouble));
        OnPropertyChanged(nameof(NewProductStockBreakdownText));
        OnPropertyChanged(nameof(NewProductPieceMrp));
        OnPropertyChanged(nameof(NewProductPieceBuyingPrice));
        OnPropertyChanged(nameof(NewProductPriceBreakdownText));
        OnPropertyChanged(nameof(NewProductMrpHeader));
        OnPropertyChanged(nameof(NewProductBuyingPriceHeader));
        OnPropertyChanged(nameof(HasNewProductValidationMessage));
    }

    [RelayCommand]
    public async Task SaveNewProductAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProductName))
        {
            NewProductValidationMessage = "⚠️ Product Name is required.";
            return;
        }

        if (NewProductMrp <= 0)
        {
            NewProductValidationMessage = "⚠️ MRP must be greater than zero.";
            return;
        }

        if (NewProductBuyingPrice > NewProductMrp)
        {
            NewProductValidationMessage = "⚠️ Buying price cannot exceed MRP.";
            return;
        }

        DateTimeOffset expiryDate = NewProductExpiryDate;
        if (!string.IsNullOrWhiteSpace(NewProductExpiryText) && expiryDate == default)
        {
            NewProductValidationMessage = "⚠️ Expiry date format must be MM/YY (e.g. 12/28).";
            return;
        }

        var batchNo = string.IsNullOrWhiteSpace(NewProductBatch) ? "B1" : NewProductBatch.Trim().ToUpperInvariant();
        var expiryToUse = expiryDate != default ? expiryDate.UtcDateTime : DateTime.UtcNow.AddYears(2);

        DosageForm dosageForm;
        string baseUnit;
        switch (NewProductStockType)
        {
            case "Tablet (Tab)":
                dosageForm = DosageForm.Tablet;
                baseUnit = "TAB";
                break;
            case "Capsule (Cap)":
                dosageForm = DosageForm.Capsule;
                baseUnit = "CAP";
                break;
            case "Syrup (Syp)":
                dosageForm = DosageForm.Syrup;
                baseUnit = "BTL";
                break;
            case "Injection (Inj)":
                dosageForm = DosageForm.Injection;
                baseUnit = "VIAL";
                break;
            case "Cream":
                dosageForm = DosageForm.Cream;
                baseUnit = "TUBE";
                break;
            case "Drop":
                dosageForm = DosageForm.Drops;
                baseUnit = "BTL";
                break;
            default:
                dosageForm = DosageForm.Other;
                baseUnit = "UNIT";
                break;
        }

        int packUnits = IsTabletOrCapsule
            ? (NewProductPcsPerStrip > 0 ? NewProductPcsPerStrip : 10)
            : (NewProductPackOptions > 0 ? NewProductPackOptions : 1);

        string strengthSpec = IsTabletOrCapsule
            ? $"{NewProductPcsPerStrip} Tabs/Strip"
            : (string.IsNullOrWhiteSpace(NewProductPackSizeText) ? string.Empty : NewProductPackSizeText.Trim());

        var schedule = NewProductIsPrescriptionRequired ? DrugSchedule.ScheduleH : DrugSchedule.OTC;

        // Strip price to piece rate calculation: Piece Price = Strip Price / Pieces in strip
        var pieceMrp = IsTabletOrCapsule && NewProductPcsPerStrip > 0
            ? Math.Round(NewProductMrp / NewProductPcsPerStrip, 4)
            : NewProductMrp;
        var piecePurchaseRate = IsTabletOrCapsule && NewProductPcsPerStrip > 0
            ? Math.Round(NewProductBuyingPrice / NewProductPcsPerStrip, 4)
            : NewProductBuyingPrice;
        var pieceSaleRate = pieceMrp; // Selling price is considered as MRP

        string productId;
        try
        {
            var savedProductName = NewProductName.Trim();
            if (_productService != null)
            {
                var cmd = new Medistock.Application.Products.Commands.CreateProductWithBatchCommand(
                    OrgId: _orgId,
                    WarehouseId: _warehouseId,
                    Name: savedProductName,
                    BrandName: savedProductName,
                    GenericName: string.Empty,
                    Composition: string.Empty,
                    Strength: strengthSpec,
                    DosageForm: dosageForm,
                    PackUnits: packUnits,
                    BaseUnit: baseUnit,
                    HsnCode: string.IsNullOrWhiteSpace(NewProductHsnCode) ? "3004" : NewProductHsnCode.Trim(),
                    GstRatePercent: NewProductTaxPercent,
                    Schedule: schedule,
                    PrimaryBarcode: null,
                    ManufacturerName: string.IsNullOrWhiteSpace(NewProductManufacturer) ? null : NewProductManufacturer.Trim(),
                    IsColdChain: false,
                    BatchNumber: batchNo,
                    ExpiryDate: expiryToUse,
                    Mrp: pieceMrp,
                    PurchaseRate: piecePurchaseRate,
                    SaleRate: pieceSaleRate,
                    OpeningQuantity: NewProductInitialStockQty
                );

                var res = await _productService.CreateProductWithBatchAsync(cmd);
                if (!res.Success)
                {
                    NewProductValidationMessage = $"⚠️ {res.ErrorMessage}";
                    return;
                }
                productId = res.ProductId ?? $"prod_{Guid.NewGuid():N}";
            }
            else
            {
                productId = $"prod_{Guid.NewGuid():N}";
            }

            var row = (PendingProductRow != null && IsBlankRow(PendingProductRow))
                ? PendingProductRow
                : LineItems.FirstOrDefault(IsBlankRow);
            if (row == null)
            {
                AddBlankRow();
                row = LineItems[^1];
            }

            row.ProductId = productId;
            row.ProductName = savedProductName;
            row.GenericName = string.Empty;
            row.HsnCode = string.IsNullOrWhiteSpace(NewProductHsnCode) ? "3004" : NewProductHsnCode.Trim();
            row.GstRatePercent = NewProductTaxPercent;
            row.StockType = NewProductStockType;
            row.PiecesPerStrip = packUnits;
            row.PackUnits = packUnits;
            row.BatchNumber = batchNo;
            row.ExpiryDate = expiryDate;
            row.ExpiryText = NewProductExpiryText?.Trim() ?? string.Empty;

            if (IsTabletOrCapsule)
            {
                row.StripCount = NewProductStripCount;
                row.PiecesPerStrip = NewProductPcsPerStrip;
                row.Quantity = NewProductInitialStockQty;
                row.StripPrice = NewProductBuyingPrice;
                row.StripMrp = NewProductMrp;
                row.UnitPrice = piecePurchaseRate;
                row.Mrp = pieceMrp;
                row.SaleRate = pieceMrp;
            }
            else if (NewProductInitialStockQty > 0)
            {
                row.Quantity = NewProductInitialStockQty;
                row.StripCount = NewProductInitialStockQty;
                row.PiecesPerStrip = 1;
                row.StripPrice = NewProductBuyingPrice;
                row.StripMrp = NewProductMrp;
                row.UnitPrice = NewProductBuyingPrice;
                row.Mrp = NewProductMrp;
                row.SaleRate = NewProductMrp;
            }
            else
            {
                row.Quantity = 1;
                row.StripCount = 1;
                row.PiecesPerStrip = 1;
                row.StripPrice = NewProductBuyingPrice;
                row.StripMrp = NewProductMrp;
                row.UnitPrice = NewProductBuyingPrice;
                row.Mrp = NewProductMrp;
                row.SaleRate = NewProductMrp;
            }

            row.Recalculate();
            RecalculateTotals();

            // Clear the form data and top search after successful save
            ResetNewProductForm();
            ProductSearchQuery = string.Empty;
            HasProductSearchResults = false;
            IsAddProductModalOpen = false;
            PendingProductRow = null;
            StatusMessage = $"✅ Product '{savedProductName}' added to database and invoice.";
        }
        catch (Exception ex)
        {
            NewProductValidationMessage = $"⚠️ Failed to create product: {ex.Message}";
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
