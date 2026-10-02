#pragma warning disable MVVMTK0045

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Customers.DTOs;
using Medistock.Application.Customers.Services;
using Medistock.Application.Products.Commands;
using Medistock.Application.Products.Queries;
using Medistock.Application.Sales.Commands;
using Medistock.Domain.Common;
using Medistock.Domain.Products;

namespace Medistock.Desktop.ViewModels;

public class PosDraftStateDto
{
    public int TabCounter { get; set; } = 1;
    public int ActiveTabIndex { get; set; } = 0;
    public List<InvoiceTabDraftDto> Tabs { get; set; } = new();
}

public class InvoiceTabDraftDto
{
    public int TabNumber { get; set; }
    public string TabId { get; set; } = string.Empty;
    public string CustomInvoiceNo { get; set; } = string.Empty;
    public string InvoiceDate { get; set; } = string.Empty;
    public string CustomerName { get; set; } = "Walk-in Customer";
    public string CustomerMobile { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
    public bool IsInterstate { get; set; } = false;
    public decimal BillDiscountPercent { get; set; } = 0;
    public decimal BillDiscountAmount { get; set; } = 0;
    public int SelectedCartIndex { get; set; } = -1;
    public List<CartItemDraftDto> CartItems { get; set; } = new();
}

public class CartItemDraftDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string PackSizeDescription { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal GstRatePercent { get; set; }
    public bool IsColdChain { get; set; }
    public DrugSchedule Schedule { get; set; }
    public int StripsPerBox { get; set; } = 1;
    public int TabsPerStrip { get; set; } = 10;
    public int TotalUnitsPerBox { get; set; } = 10;
    public decimal AvailableQuantity { get; set; } = 0;
    public decimal StripQuantity { get; set; } = 1;
    public decimal TabQuantity { get; set; } = 0;
    public decimal Quantity { get; set; } = 10;
    public decimal FreeQuantity { get; set; } = 0;
    public decimal UnitPrice { get; set; } = 0;
    public decimal DiscountPercent { get; set; } = 0;
}

public class HighlightedSegment
{
    public string Text { get; set; } = string.Empty;
    public bool IsMatch { get; set; }
    public bool IsSecondary { get; set; }
    public string BackgroundHex => IsMatch ? "#350D6EFD" : "#00000000";
    public string BorderHex => IsMatch ? "#600D6EFD" : "#00000000";
    public string ForegroundHex => IsMatch ? "#0D6EFD" : (IsSecondary ? "TEXT_SECONDARY" : "TEXT_PRIMARY");
    public string FontWeight => IsMatch ? "Bold" : "SemiBold";
}

public partial class ProductSearchItemViewModel : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string GenericName { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public DosageForm DosageForm { get; set; }
    public string PackSizeDescription { get; set; } = string.Empty;
    public string HsnCode { get; set; } = string.Empty;
    public decimal GstRatePercent { get; set; }
    public DrugSchedule Schedule { get; set; }
    public bool IsPrescriptionRequired { get; set; }
    public bool IsColdChain { get; set; }
    public bool IsNarcotic { get; set; }
    public string? ManufacturerName { get; set; }
    public string? Barcode { get; set; }
    public string? BatchId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? NearestExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal SaleRate { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal MinStockAlert { get; set; } = 10;
    public int NearExpiryDays { get; set; } = 90;
    public List<ProductBatchDto> Batches { get; set; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NameSegments));
                OnPropertyChanged(nameof(GenericNameSegments));
            }
        }
    }

    public List<HighlightedSegment> NameSegments => BuildSegments(Name, SearchQuery, isSecondary: false);
    public List<HighlightedSegment> GenericNameSegments => BuildSegments(GenericName, SearchQuery, isSecondary: true);

    public static List<HighlightedSegment> BuildSegments(string? source, string? query, bool isSecondary = false)
    {
        if (string.IsNullOrEmpty(source))
            return new List<HighlightedSegment>();

        if (string.IsNullOrWhiteSpace(query))
            return new List<HighlightedSegment> { new() { Text = source, IsMatch = false, IsSecondary = isSecondary } };

        var terms = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
            return new List<HighlightedSegment> { new() { Text = source, IsMatch = false, IsSecondary = isSecondary } };

        var matchRanges = new List<(int Start, int End)>();
        foreach (var term in terms)
        {
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int found = source.IndexOf(term, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (found < 0) break;
                matchRanges.Add((found, found + term.Length));
                searchIndex = found + Math.Max(1, term.Length);
            }
        }

        if (matchRanges.Count == 0)
            return new List<HighlightedSegment> { new() { Text = source, IsMatch = false, IsSecondary = isSecondary } };

        matchRanges.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<(int Start, int End)>();
        var current = matchRanges[0];
        for (int i = 1; i < matchRanges.Count; i++)
        {
            var next = matchRanges[i];
            if (next.Start <= current.End)
            {
                current = (current.Start, Math.Max(current.End, next.End));
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);

        var segments = new List<HighlightedSegment>();
        int cursor = 0;
        foreach (var (start, end) in merged)
        {
            if (start > cursor)
            {
                segments.Add(new HighlightedSegment
                {
                    Text = source.Substring(cursor, start - cursor),
                    IsMatch = false,
                    IsSecondary = isSecondary
                });
            }
            segments.Add(new HighlightedSegment
            {
                Text = source.Substring(start, end - start),
                IsMatch = true,
                IsSecondary = isSecondary
            });
            cursor = end;
        }

        if (cursor < source.Length)
        {
            segments.Add(new HighlightedSegment
            {
                Text = source.Substring(cursor),
                IsMatch = false,
                IsSecondary = isSecondary
            });
        }

        return segments;
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HighlightBackgroundHex));
                OnPropertyChanged(nameof(HighlightBorderHex));
            }
        }
    }

    public bool IsExpired => NearestExpiryDate.HasValue && NearestExpiryDate.Value.Date <= DateTime.UtcNow.Date;
    public bool IsNearExpiry => !IsExpired && NearestExpiryDate.HasValue && NearestExpiryDate.Value.Date <= DateTime.UtcNow.AddDays(NearExpiryDays).Date;
    public bool IsOutOfStock => AvailableQuantity <= 0;
    public bool IsLowStock => !IsOutOfStock && AvailableQuantity <= MinStockAlert;

    public string StockDisplay
    {
        get
        {
            if (IsOutOfStock)
                return "0 (OOS)";

            string suffix = IsLowStock ? " (LOW)" : string.Empty;

            var breakdown = PackagingHelper.Parse(PackSizeDescription, DosageForm.ToString());
            var tabsPerStrip = breakdown.TabsPerStrip > 0 ? breakdown.TabsPerStrip : 10;

            bool isTabletOrCapsule = DosageForm is DosageForm.Tablet or DosageForm.Capsule
                || (!string.IsNullOrEmpty(PackSizeDescription) && (PackSizeDescription.Contains("TAB", StringComparison.OrdinalIgnoreCase) || PackSizeDescription.Contains("CAP", StringComparison.OrdinalIgnoreCase)));

            if (isTabletOrCapsule)
            {
                int totalUnits = (int)Math.Max(0, AvailableQuantity);
                int strips = totalUnits / tabsPerStrip;
                int loosePcs = totalUnits % tabsPerStrip;

                if (strips > 0 && loosePcs > 0)
                    return $"{strips} Strip {loosePcs} Pc{suffix}";
                else if (strips > 0)
                    return $"{strips} Strip{(strips > 1 ? "s" : "")}{suffix}";
                else if (loosePcs > 0)
                    return $"{loosePcs} Pc{(loosePcs > 1 ? "s" : "")}{suffix}";
                else
                    return $"0 (OOS)";
            }

            if (DosageForm is DosageForm.Syrup or DosageForm.Drops or DosageForm.Suspension)
                return $"{AvailableQuantity:0.##} Btl{suffix}";
            if (DosageForm is DosageForm.Injection)
                return $"{AvailableQuantity:0.##} Vial{suffix}";
            if (DosageForm is DosageForm.Cream or DosageForm.Ointment or DosageForm.Gel)
                return $"{AvailableQuantity:0.##} Tube{suffix}";
            if (DosageForm is DosageForm.Inhaler)
                return $"{AvailableQuantity:0.##} Inh{suffix}";

            return $"{AvailableQuantity:0.##} Pc{suffix}";
        }
    }

    public string ExpiryDisplay => NearestExpiryDate.HasValue ? NearestExpiryDate.Value.ToString("MM/yy") : "--/--";
    public string ExpiryBadge => IsExpired ? "EXPIRED" : (IsNearExpiry ? "EXP NEAR" : string.Empty);
    public bool HasExpiryBadge => IsExpired || IsNearExpiry;

    public string StockForegroundHex => IsOutOfStock 
        ? "#DC2626" 
        : (IsLowStock ? "#D97706" : "#16A34A");

    public string ExpiryForegroundHex => (IsExpired || IsNearExpiry) 
        ? "#DC2626" 
        : "#64748B";

    public string StockBadge => IsOutOfStock ? "OOS" : (IsLowStock ? "LOW" : string.Empty);
    public bool HasStockBadge => IsOutOfStock || IsLowStock;

    public string RowBackgroundHex => IsExpired 
        ? "#35DC2626" 
        : (IsNearExpiry 
            ? "#22DC2626" 
            : (IsOutOfStock 
                ? "#25DC2626" 
                : (IsLowStock ? "#20D97706" : "#00000000")));

    public string RowBorderHex => IsExpired 
        ? "#DC2626" 
        : (IsNearExpiry 
            ? "#80DC2626" 
            : (IsOutOfStock 
                ? "#DC2626" 
                : (IsLowStock ? "#D97706" : "#00000000")));

    public string HighlightBackgroundHex => IsSelected ? "#350D6EFD" : RowBackgroundHex;
    public string HighlightBorderHex => IsSelected ? "#0D6EFD" : RowBorderHex;

    public static ProductSearchItemViewModel FromDto(ProductSearchDto dto, int nearExpiryDays = 90, string query = "")
    {
        return new ProductSearchItemViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            BrandName = dto.BrandName,
            GenericName = dto.GenericName,
            Composition = dto.Composition,
            Strength = dto.Strength,
            DosageForm = dto.DosageForm,
            PackSizeDescription = dto.PackSizeDescription,
            HsnCode = dto.HsnCode,
            GstRatePercent = dto.GstRatePercent,
            Schedule = dto.Schedule,
            IsPrescriptionRequired = dto.IsPrescriptionRequired,
            IsColdChain = dto.IsColdChain,
            IsNarcotic = dto.IsNarcotic,
            ManufacturerName = dto.ManufacturerName,
            Barcode = dto.Barcode,
            BatchId = dto.BatchId,
            BatchNumber = dto.BatchNumber,
            NearestExpiryDate = dto.NearestExpiryDate,
            Mrp = dto.Mrp,
            SaleRate = dto.SaleRate,
            AvailableQuantity = dto.AvailableQuantity,
            MinStockAlert = dto.MinStockAlert > 0 ? dto.MinStockAlert : 10,
            NearExpiryDays = nearExpiryDays > 0 ? nearExpiryDays : 90,
            Batches = dto.Batches ?? new List<ProductBatchDto>(),
            SearchQuery = query
        };
    }
}

public partial class CartItemViewModel : ObservableObject
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string PackSizeDescription { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    private decimal _mrp;
    public decimal Mrp
    {
        get => _mrp;
        set
        {
            if (SetProperty(ref _mrp, value))
            {
                OnPropertyChanged(nameof(MrpDouble));
                if (_mrp > 0 && _unitPrice > _mrp)
                {
                    UnitPrice = _mrp;
                }
                OnPropertyChanged(nameof(GrossAmount));
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(TaxableAmount));
                OnPropertyChanged(nameof(GstAmount));
                OnPropertyChanged(nameof(NetAmount));
            }
        }
    }

    public double MrpDouble => Mrp > 0 ? (double)Mrp : double.MaxValue;
    public decimal GstRatePercent { get; set; }
    public bool IsColdChain { get; set; }
    public DrugSchedule Schedule { get; set; }

    [ObservableProperty]
    private int _lineNumber = 1;

    public int StripsPerBox { get; set; } = 1;
    public int TabsPerStrip { get; set; } = 10;
    public int TotalUnitsPerBox { get; set; } = 10;

    [ObservableProperty]
    private decimal _availableQuantity;

    private decimal _stripQuantity = 1;
    public decimal StripQuantity
    {
        get => _stripQuantity;
        set
        {
            var tabsPerStrip = TabsPerStrip > 1 ? TabsPerStrip : 10m;
            if (AvailableQuantity > 0 && (value * tabsPerStrip + _tabQuantity) > AvailableQuantity)
            {
                value = Math.Max(0, Math.Floor((AvailableQuantity - _tabQuantity) / tabsPerStrip));
            }
            if (SetProperty(ref _stripQuantity, value))
            {
                _quantity = (value * tabsPerStrip) + _tabQuantity;
                OnPropertyChanged(nameof(StripQuantityDouble));
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(QuantityDouble));
                OnPropertyChanged(nameof(GrossAmount));
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(TaxableAmount));
                OnPropertyChanged(nameof(GstAmount));
                OnPropertyChanged(nameof(NetAmount));
            }
        }
    }

    public double StripQuantityDouble
    {
        get => (double)StripQuantity;
        set
        {
            var numericVal = double.IsNaN(value) || value < 0 ? 0 : value;
            var val = (decimal)numericVal;
            var tabsPerStrip = TabsPerStrip > 1 ? TabsPerStrip : 10m;
            if (AvailableQuantity > 0 && (val * tabsPerStrip + _tabQuantity) > AvailableQuantity)
            {
                val = Math.Max(0, Math.Floor((AvailableQuantity - _tabQuantity) / tabsPerStrip));
            }
            _stripQuantity = val;
            _quantity = (val * tabsPerStrip) + _tabQuantity;
            OnPropertyChanged(nameof(StripQuantity));
            OnPropertyChanged(nameof(Quantity));
            OnPropertyChanged(nameof(QuantityDouble));
            OnPropertyChanged(nameof(GrossAmount));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(TaxableAmount));
            OnPropertyChanged(nameof(GstAmount));
            OnPropertyChanged(nameof(NetAmount));
        }
    }

    private decimal _tabQuantity = 0;
    public decimal TabQuantity
    {
        get => _tabQuantity;
        set
        {
            var tabsPerStrip = TabsPerStrip > 1 ? TabsPerStrip : 10m;
            if (AvailableQuantity > 0 && (_stripQuantity * tabsPerStrip + value) > AvailableQuantity)
            {
                value = Math.Max(0, AvailableQuantity - (_stripQuantity * tabsPerStrip));
            }
            if (SetProperty(ref _tabQuantity, value))
            {
                _quantity = (_stripQuantity * tabsPerStrip) + value;
                OnPropertyChanged(nameof(TabQuantityDouble));
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(QuantityDouble));
                OnPropertyChanged(nameof(GrossAmount));
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(TaxableAmount));
                OnPropertyChanged(nameof(GstAmount));
                OnPropertyChanged(nameof(NetAmount));
            }
        }
    }

    public double TabQuantityDouble
    {
        get => (double)TabQuantity;
        set
        {
            var numericVal = double.IsNaN(value) || value < 0 ? 0 : value;
            var val = (decimal)numericVal;
            var tabsPerStrip = TabsPerStrip > 1 ? TabsPerStrip : 10m;
            if (AvailableQuantity > 0 && (_stripQuantity * tabsPerStrip + val) > AvailableQuantity)
            {
                val = Math.Max(0, AvailableQuantity - (_stripQuantity * tabsPerStrip));
            }
            _tabQuantity = val;
            _quantity = (_stripQuantity * tabsPerStrip) + val;
            OnPropertyChanged(nameof(TabQuantity));
            OnPropertyChanged(nameof(Quantity));
            OnPropertyChanged(nameof(QuantityDouble));
            OnPropertyChanged(nameof(GrossAmount));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(TaxableAmount));
            OnPropertyChanged(nameof(GstAmount));
            OnPropertyChanged(nameof(NetAmount));
        }
    }

    private decimal _quantity = 1;
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (AvailableQuantity > 0 && value > AvailableQuantity)
            {
                value = AvailableQuantity;
            }
            if (SetProperty(ref _quantity, value))
            {
                _stripQuantity = value;
                _tabQuantity = 0;
                OnPropertyChanged(nameof(StripQuantity));
                OnPropertyChanged(nameof(StripQuantityDouble));
                OnPropertyChanged(nameof(TabQuantity));
                OnPropertyChanged(nameof(TabQuantityDouble));
                OnPropertyChanged(nameof(QuantityDouble));
                OnPropertyChanged(nameof(GrossAmount));
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(TaxableAmount));
                OnPropertyChanged(nameof(GstAmount));
                OnPropertyChanged(nameof(NetAmount));
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FreeQuantityDouble))]
    private decimal _freeQuantity = 0;

    public double FreeQuantityDouble
    {
        get => (double)FreeQuantity;
        set
        {
            if (value >= 0)
            {
                FreeQuantity = (decimal)value;
            }
        }
    }

    private decimal _unitPrice;
    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (Mrp > 0 && value > Mrp)
            {
                value = Mrp;
            }
            if (SetProperty(ref _unitPrice, value))
            {
                OnPropertyChanged(nameof(UnitPriceDouble));
                OnPropertyChanged(nameof(GrossAmount));
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(TaxableAmount));
                OnPropertyChanged(nameof(GstAmount));
                OnPropertyChanged(nameof(NetAmount));
            }
        }
    }

    public double UnitPriceDouble
    {
        get => (double)UnitPrice;
        set
        {
            if (value >= 0)
            {
                var val = (decimal)value;
                if (Mrp > 0 && val > Mrp)
                {
                    val = Mrp;
                }
                UnitPrice = val;
            }
        }
    }

    public double QuantityDouble
    {
        get => (double)Quantity;
        set
        {
            if (value >= 0)
            {
                Quantity = (decimal)value;
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountAmount))]
    [NotifyPropertyChangedFor(nameof(TaxableAmount))]
    [NotifyPropertyChangedFor(nameof(GstAmount))]
    [NotifyPropertyChangedFor(nameof(NetAmount))]
    [NotifyPropertyChangedFor(nameof(DiscountPercentDouble))]
    private decimal _discountPercent = 0;

    public double DiscountPercentDouble
    {
        get => (double)DiscountPercent;
        set
        {
            if (value >= 0 && value <= 100)
            {
                DiscountPercent = (decimal)value;
            }
        }
    }

    public decimal GrossAmount
    {
        get
        {
            if (StripQuantity <= 0 && TabQuantity <= 0)
            {
                return 0m;
            }

            var effectiveRate = Mrp > 0 ? Mrp : UnitPrice;
            var tabsPerStrip = TabsPerStrip > 1 ? (decimal)TabsPerStrip : 10m;
            var perTabRate = effectiveRate / tabsPerStrip;
            decimal gross = 0;

            if (StripQuantity > 0)
            {
                gross += StripQuantity * effectiveRate;
            }
            if (TabQuantity > 0)
            {
                gross += TabQuantity * perTabRate;
            }

            return Math.Round(gross, 2, MidpointRounding.AwayFromZero);
        }
    }

    public decimal DiscountAmount => Math.Round(GrossAmount * (DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal NetAmount => Math.Round(GrossAmount - DiscountAmount, 2, MidpointRounding.AwayFromZero);
    public decimal TaxableAmount => GstRatePercent > 0
        ? Math.Round((NetAmount * 100m) / (100m + GstRatePercent), 2, MidpointRounding.AwayFromZero)
        : NetAmount;
    public decimal GstAmount => NetAmount - TaxableAmount;
    public decimal CgstAmount => Math.Round(GstAmount / 2m, 2, MidpointRounding.AwayFromZero);
    public decimal SgstAmount => GstAmount - CgstAmount;
    public decimal IgstAmount => GstAmount;
}

public partial class InvoiceTabViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    [NotifyPropertyChangedFor(nameof(DisplayInvoiceNo))]
    private int _tabNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabBackgroundHex))]
    [NotifyPropertyChangedFor(nameof(TabBorderHex))]
    [NotifyPropertyChangedFor(nameof(TabForegroundHex))]
    [NotifyPropertyChangedFor(nameof(TabBadgeBackgroundHex))]
    [NotifyPropertyChangedFor(nameof(TabBadgeForegroundHex))]
    [NotifyPropertyChangedFor(nameof(TabCloseForegroundHex))]
    private bool _isActive;

    public string TabBackgroundHex => IsActive ? "#0D6EFD" : "#21262D";
    public string TabBorderHex => IsActive ? "#0B5ED7" : "#30363D";
    public string TabForegroundHex => IsActive ? "#FFFFFF" : "#94A3B8";
    public string TabBadgeBackgroundHex => IsActive ? "#FFFFFF" : "#161B22";
    public string TabBadgeForegroundHex => IsActive ? "#0D6EFD" : "#94A3B8";
    public string TabCloseForegroundHex => IsActive ? "#FFFFFF" : "#94A3B8";

    public string TabId { get; }
    public string TabTitle => $"#{TabNumber}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayInvoiceNo))]
    private string _customInvoiceNo = string.Empty;

    public string DisplayInvoiceNo => string.IsNullOrWhiteSpace(CustomInvoiceNo) ? TabTitle : CustomInvoiceNo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InvoiceDateString))]
    private DateTime _invoiceDate = DateTime.Today;

    public string InvoiceDateString
    {
        get => InvoiceDate.ToString("dd-MM-yyyy");
        set
        {
            if (DateTime.TryParse(value, out var dt))
            {
                InvoiceDate = dt;
            }
        }
    }

    [ObservableProperty]
    private string? _customerId;

    [ObservableProperty]
    private string _customerName = "WALK-IN CUSTOMER";

    partial void OnCustomerNameChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && value != value.ToUpperInvariant())
        {
            CustomerName = value.ToUpperInvariant();
        }
    }

    [ObservableProperty]
    private string _customerMobile = string.Empty;

    [ObservableProperty]
    private string _doctorName = string.Empty;

    partial void OnDoctorNameChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && value != value.ToUpperInvariant())
        {
            DoctorName = value.ToUpperInvariant();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaymentModeIndex))]
    private PaymentMode _paymentMode = PaymentMode.Cash;

    public int PaymentModeIndex
    {
        get => PaymentMode switch
        {
            PaymentMode.Cash => 0,
            PaymentMode.Upi => 1,
            PaymentMode.Card => 2,
            PaymentMode.Credit => 3,
            _ => 0
        };
        set
        {
            PaymentMode = value switch
            {
                0 => PaymentMode.Cash,
                1 => PaymentMode.Upi,
                2 => PaymentMode.Card,
                3 => PaymentMode.Credit,
                _ => PaymentMode.Cash
            };
            OnPropertyChanged(nameof(PaymentMode));
            OnPropertyChanged(nameof(PaymentModeIndex));
        }
    }

    [ObservableProperty]
    private bool _isInterstate = false;

    [ObservableProperty]
    private int _selectedCartIndex = -1;

    public ObservableCollection<CartItemViewModel> CartItems { get; } = new();

    [ObservableProperty]
    private decimal _subtotal = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalDiscountDisplay))]
    [NotifyPropertyChangedFor(nameof(EffectiveDiscountPercent))]
    private decimal _totalDiscount = 0;

    public decimal EffectiveDiscountPercent => Subtotal > 0 && TotalDiscount > 0
        ? Math.Round((TotalDiscount / Subtotal) * 100m, 1, MidpointRounding.AwayFromZero)
        : 0m;

    public string TotalDiscountDisplay
    {
        get
        {
            if (TotalDiscount <= 0) return "₹0.00 (0%)";
            var pct = EffectiveDiscountPercent;
            return pct > 0 ? $"-₹{TotalDiscount:N2} ({pct:0.#}%)" : $"-₹{TotalDiscount:N2}";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalGst))]
    private decimal _cgst = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalGst))]
    private decimal _sgst = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalGst))]
    private decimal _igst = 0;

    [ObservableProperty]
    private decimal _taxableAmount = 0;

    public decimal TotalGst => Cgst + Sgst + Igst;
    public decimal ItemDiscounts => CartItems.Sum(i => i.DiscountAmount);

    [ObservableProperty]
    private decimal _roundOff = 0;

    [ObservableProperty]
    private decimal _grandTotal = 0;

    [ObservableProperty]
    private int _totalItemsCount = 0;

    [ObservableProperty]
    private decimal _totalQuantity = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BalanceAmount))]
    [NotifyPropertyChangedFor(nameof(AmountReceivedDouble))]
    private decimal _amountReceived = 0;

    public double AmountReceivedDouble
    {
        get => (double)AmountReceived;
        set => AmountReceived = (decimal)value;
    }

    public decimal BalanceAmount => AmountReceived >= GrandTotal ? AmountReceived - GrandTotal : 0;

    public InvoiceTabViewModel(int tabNumber, string? tabId = null)
    {
        TabNumber = tabNumber;
        TabId = string.IsNullOrWhiteSpace(tabId) ? Ulid.NewUlid().ToString() : tabId;
        CartItems.CollectionChanged += CartItems_CollectionChanged;
    }

    private void CartItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (CartItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }
        }
        if (e.NewItems != null)
        {
            foreach (CartItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }
        }
        UpdateLineNumbers();
        RecalculateTotals();
    }

    private void UpdateLineNumbers()
    {
        for (int i = 0; i < CartItems.Count; i++)
        {
            CartItems[i].LineNumber = i + 1;
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CartItemViewModel.GrossAmount)
            or nameof(CartItemViewModel.DiscountAmount)
            or nameof(CartItemViewModel.NetAmount)
            or nameof(CartItemViewModel.StripQuantity)
            or nameof(CartItemViewModel.TabQuantity)
            or nameof(CartItemViewModel.Quantity)
            or nameof(CartItemViewModel.UnitPrice)
            or nameof(CartItemViewModel.DiscountPercent))
        {
            RecalculateTotals();
        }
    }

    private bool _isUpdatingDiscount = false;

    private decimal _billDiscountPercent = 0;
    public decimal BillDiscountPercent
    {
        get => _billDiscountPercent;
        set
        {
            if (SetProperty(ref _billDiscountPercent, value))
            {
                OnPropertyChanged(nameof(BillDiscountPercentDouble));
                if (!_isUpdatingDiscount)
                {
                    _isUpdatingDiscount = true;
                    var eligibleSubtotal = CartItems.Where(i => i.DiscountPercent == 0).Sum(i => i.GrossAmount);
                    _billDiscountAmount = eligibleSubtotal > 0
                        ? Math.Round(eligibleSubtotal * (value / 100m), 2, MidpointRounding.AwayFromZero)
                        : 0m;
                    OnPropertyChanged(nameof(BillDiscountAmount));
                    OnPropertyChanged(nameof(BillDiscountAmountDouble));
                    _isUpdatingDiscount = false;
                }
                RecalculateTotals();
            }
        }
    }

    public double BillDiscountPercentDouble
    {
        get => (double)BillDiscountPercent;
        set
        {
            if (value >= 0 && value <= 100)
            {
                BillDiscountPercent = (decimal)value;
            }
        }
    }

    private decimal _billDiscountAmount = 0;
    public decimal BillDiscountAmount
    {
        get => _billDiscountAmount;
        set
        {
            if (SetProperty(ref _billDiscountAmount, value))
            {
                OnPropertyChanged(nameof(BillDiscountAmountDouble));
                if (!_isUpdatingDiscount)
                {
                    _isUpdatingDiscount = true;
                    var eligibleSubtotal = CartItems.Where(i => i.DiscountPercent == 0).Sum(i => i.GrossAmount);
                    if (eligibleSubtotal > 0)
                    {
                        var clamped = Math.Min(value, eligibleSubtotal);
                        _billDiscountAmount = clamped;
                        _billDiscountPercent = Math.Round((clamped / eligibleSubtotal) * 100m, 2, MidpointRounding.AwayFromZero);
                    }
                    else
                    {
                        _billDiscountAmount = 0m;
                        _billDiscountPercent = 0m;
                    }
                    OnPropertyChanged(nameof(BillDiscountAmount));
                    OnPropertyChanged(nameof(BillDiscountAmountDouble));
                    OnPropertyChanged(nameof(BillDiscountPercent));
                    OnPropertyChanged(nameof(BillDiscountPercentDouble));
                    _isUpdatingDiscount = false;
                }
                RecalculateTotals();
            }
        }
    }

    public double BillDiscountAmountDouble
    {
        get => (double)BillDiscountAmount;
        set
        {
            if (value >= 0)
            {
                BillDiscountAmount = (decimal)value;
            }
        }
    }

    public void RecalculateTotals()
    {
        Subtotal = CartItems.Sum(i => i.GrossAmount);
        var itemDiscounts = CartItems.Sum(i => i.DiscountAmount);
        var eligibleSubtotal = CartItems.Where(i => i.DiscountPercent == 0).Sum(i => i.GrossAmount);

        if (!_isUpdatingDiscount)
        {
            if (BillDiscountPercent > 0)
            {
                _billDiscountAmount = Math.Round(eligibleSubtotal * (BillDiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
                OnPropertyChanged(nameof(BillDiscountAmount));
                OnPropertyChanged(nameof(BillDiscountAmountDouble));
            }
            else if (BillDiscountAmount > 0)
            {
                _billDiscountAmount = Math.Min(BillDiscountAmount, eligibleSubtotal);
                OnPropertyChanged(nameof(BillDiscountAmount));
                OnPropertyChanged(nameof(BillDiscountAmountDouble));
            }
        }

        // Total discount includes both individual product discounts and overall bill discount
        TotalDiscount = itemDiscounts + BillDiscountAmount;
        TotalItemsCount = CartItems.Count;
        TotalQuantity = CartItems.Sum(i => i.Quantity);

        // Net Payable is Subtotal minus Total Discount with standard rupee rounding
        var netPayable = Math.Max(0m, Subtotal - TotalDiscount);
        var roundedPayable = Math.Round(netPayable, 0, MidpointRounding.AwayFromZero);
        RoundOff = roundedPayable - netPayable;
        GrandTotal = roundedPayable;

        decimal totalCgst = 0m;
        decimal totalSgst = 0m;
        decimal totalIgst = 0m;

        foreach (var item in CartItems)
        {
            decimal itemDiscPct = item.DiscountPercent;
            if (itemDiscPct == 0)
            {
                if (BillDiscountPercent > 0)
                {
                    itemDiscPct = BillDiscountPercent;
                }
                else if (BillDiscountAmount > 0 && eligibleSubtotal > 0)
                {
                    itemDiscPct = Math.Round((BillDiscountAmount / eligibleSubtotal) * 100m, 2, MidpointRounding.AwayFromZero);
                }
            }

            var lineGross = item.GrossAmount;
            var lineDisc = Math.Round(lineGross * (itemDiscPct / 100m), 2, MidpointRounding.AwayFromZero);
            var lineNet = lineGross - lineDisc;
            var lineTaxable = item.GstRatePercent > 0
                ? Math.Round((lineNet * 100m) / (100m + item.GstRatePercent), 2, MidpointRounding.AwayFromZero)
                : lineNet;
            var lineGst = lineNet - lineTaxable;

            if (IsInterstate)
            {
                totalIgst += lineGst;
            }
            else
            {
                var cgst = Math.Round(lineGst / 2m, 2, MidpointRounding.AwayFromZero);
                var sgst = lineGst - cgst;
                totalCgst += cgst;
                totalSgst += sgst;
            }
        }

        Cgst = totalCgst;
        Sgst = totalSgst;
        Igst = totalIgst;
        TaxableAmount = Math.Max(0m, GrandTotal - (totalCgst + totalSgst + totalIgst));
        OnPropertyChanged(nameof(ItemDiscounts));

        if (AmountReceived < GrandTotal && PaymentMode == PaymentMode.Cash)
        {
            AmountReceived = GrandTotal;
        }
    }
}

public record HeldBillModel(
    string HoldId,
    string HoldNumber,
    DateTime HeldAt,
    string CustomerName,
    string CustomerMobile,
    string DoctorName,
    PaymentMode PaymentMode,
    bool IsInterstate,
    List<CartItemViewModel> Items,
    decimal GrandTotal
);

public partial class PosViewModel : ObservableObject
{
    private readonly IProductSearchService _searchService;
    private readonly IPosTransactionService _posTransactionService;
    private readonly IProductService _productService;
    private CancellationTokenSource? _searchCts;
    private int _tabCounter = 1;
    private int _holdCounter = 1;

    // Bill Hold & Recall Buffer (F8 / F9)
    public ObservableCollection<HeldBillModel> HeldBills { get; } = new();

    [ObservableProperty]
    private bool _isRecallBillModalOpen = false;

    [ObservableProperty]
    private bool _isCloseTabConfirmationOpen = false;

    [ObservableProperty]
    private bool _isExitAppConfirmationOpen = false;

    [ObservableProperty]
    private InvoiceTabViewModel? _tabPendingClose;

    [ObservableProperty]
    private HeldBillModel? _selectedHeldBill;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching = false;

    [ObservableProperty]
    private string _statusMessage = "Ready for billing. Press [F3] Search, [F2] Add Item, [F6] Settle.";

    [ObservableProperty]
    private string _counterName = "Counter-1";

    [ObservableProperty]
    private string _cashierName = "Cashier";

    [ObservableProperty]
    private string _warehouseId = "wh-1";

    [ObservableProperty]
    private string _orgId = "org-1";

    [ObservableProperty]
    private string _branchId = "branch-1";

    [ObservableProperty]
    private int _selectedSearchIndex = -1;

    [ObservableProperty]
    private bool _isShortcutHelpOpen = false;

    // Multi-tab invoicing
    public ObservableCollection<InvoiceTabViewModel> InvoiceTabs { get; } = new();

    [ObservableProperty]
    private InvoiceTabViewModel? _activeTab;

    partial void OnActiveTabChanged(InvoiceTabViewModel? value)
    {
        UpdateActiveTabState();
    }

    public void UpdateActiveTabState()
    {
        foreach (var tab in InvoiceTabs)
        {
            tab.IsActive = (tab == ActiveTab);
        }
    }

    [ObservableProperty]
    private int _activeTabIndex = 0;

    // Sale Error / Insufficient Stock Alert Modal
    [ObservableProperty]
    private bool _isSaleErrorModalOpen = false;

    [ObservableProperty]
    private string _saleErrorTitle = "Cannot Complete Sale";

    [ObservableProperty]
    private string _saleErrorMessage = string.Empty;

    [RelayCommand]
    public void CloseSaleErrorModal()
    {
        IsSaleErrorModalOpen = false;
    }

    public void ShowSaleError(string title, string message)
    {
        SaleErrorTitle = title;
        SaleErrorMessage = message;
        IsSaleErrorModalOpen = true;
    }

    // MARG ERP Line-Item Loop & Focus State
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingItem))]
    private ProductSearchItemViewModel? _pendingSelectedItem;

    public bool HasPendingItem => PendingSelectedItem != null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingGrossAmount))]
    [NotifyPropertyChangedFor(nameof(PendingDiscountAmount))]
    [NotifyPropertyChangedFor(nameof(PendingTaxableAmount))]
    [NotifyPropertyChangedFor(nameof(PendingGstAmount))]
    [NotifyPropertyChangedFor(nameof(PendingNetAmount))]
    private ProductBatchDto? _pendingSelectedBatch;

    private double _pendingStripQuantity = 1;
    public double PendingStripQuantity
    {
        get => _pendingStripQuantity;
        set
        {
            var sanitizedValue = double.IsNaN(value) || value < 0 ? 0 : value;
            if (SetProperty(ref _pendingStripQuantity, sanitizedValue))
            {
                var tabsPerStrip = 10;
                if (PendingSelectedItem != null)
                {
                    var breakdown = PackagingHelper.Parse(PendingSelectedItem.PackSizeDescription);
                    tabsPerStrip = breakdown.TabsPerStrip > 1 ? breakdown.TabsPerStrip : 10;
                }
                _pendingQuantity = (sanitizedValue * tabsPerStrip) + _pendingTabQuantity;
                OnPropertyChanged(nameof(PendingQuantity));
                OnPropertyChanged(nameof(PendingGrossAmount));
                OnPropertyChanged(nameof(PendingDiscountAmount));
                OnPropertyChanged(nameof(PendingTaxableAmount));
                OnPropertyChanged(nameof(PendingGstAmount));
                OnPropertyChanged(nameof(PendingNetAmount));
            }
        }
    }

    private double _pendingTabQuantity = 0;
    public double PendingTabQuantity
    {
        get => _pendingTabQuantity;
        set
        {
            var sanitizedValue = double.IsNaN(value) || value < 0 ? 0 : value;
            if (SetProperty(ref _pendingTabQuantity, sanitizedValue))
            {
                var tabsPerStrip = 10;
                if (PendingSelectedItem != null)
                {
                    var breakdown = PackagingHelper.Parse(PendingSelectedItem.PackSizeDescription);
                    tabsPerStrip = breakdown.TabsPerStrip > 1 ? breakdown.TabsPerStrip : 10;
                }
                _pendingQuantity = (_pendingStripQuantity * tabsPerStrip) + sanitizedValue;
                OnPropertyChanged(nameof(PendingQuantity));
                OnPropertyChanged(nameof(PendingGrossAmount));
                OnPropertyChanged(nameof(PendingDiscountAmount));
                OnPropertyChanged(nameof(PendingTaxableAmount));
                OnPropertyChanged(nameof(PendingGstAmount));
                OnPropertyChanged(nameof(PendingNetAmount));
            }
        }
    }

    private double _pendingQuantity = 1;
    public double PendingQuantity
    {
        get => _pendingQuantity;
        set
        {
            var sanitizedValue = double.IsNaN(value) || value < 0 ? 0 : value;
            if (SetProperty(ref _pendingQuantity, sanitizedValue))
            {
                _pendingStripQuantity = sanitizedValue;
                _pendingTabQuantity = 0;
                OnPropertyChanged(nameof(PendingStripQuantity));
                OnPropertyChanged(nameof(PendingTabQuantity));
                OnPropertyChanged(nameof(PendingGrossAmount));
                OnPropertyChanged(nameof(PendingDiscountAmount));
                OnPropertyChanged(nameof(PendingTaxableAmount));
                OnPropertyChanged(nameof(PendingGstAmount));
                OnPropertyChanged(nameof(PendingNetAmount));
            }
        }
    }

    [ObservableProperty]
    private double _pendingFreeQuantity = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingGrossAmount))]
    [NotifyPropertyChangedFor(nameof(PendingDiscountAmount))]
    [NotifyPropertyChangedFor(nameof(PendingTaxableAmount))]
    [NotifyPropertyChangedFor(nameof(PendingGstAmount))]
    [NotifyPropertyChangedFor(nameof(PendingNetAmount))]
    private double _pendingRate = 0;

    partial void OnPendingRateChanged(double value)
    {
        if (PendingSelectedItem != null)
        {
            var effectiveMrp = PendingSelectedBatch?.Mrp > 0 ? PendingSelectedBatch.Mrp : PendingSelectedItem.Mrp;
            if (effectiveMrp > 0 && (decimal)value > effectiveMrp)
            {
                PendingRate = (double)effectiveMrp;
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingDiscountAmount))]
    [NotifyPropertyChangedFor(nameof(PendingTaxableAmount))]
    [NotifyPropertyChangedFor(nameof(PendingGstAmount))]
    [NotifyPropertyChangedFor(nameof(PendingNetAmount))]
    private double _pendingDiscountPercent = 0;

    public decimal PendingGrossAmount
    {
        get
        {
            if (PendingStripQuantity <= 0 && PendingTabQuantity <= 0 && PendingQuantity <= 0)
            {
                return 0m;
            }

            var tabsPerStrip = 10;
            if (PendingSelectedItem != null)
            {
                var breakdown = PackagingHelper.Parse(PendingSelectedItem.PackSizeDescription);
                tabsPerStrip = breakdown.TabsPerStrip > 1 ? breakdown.TabsPerStrip : 10;
            }

            decimal gross = 0;
            var effectiveRate = (PendingSelectedBatch?.Mrp > 0 ? PendingSelectedBatch.Mrp : PendingSelectedItem?.Mrp) ?? (decimal)PendingRate;
            if (effectiveRate <= 0 && PendingRate > 0) effectiveRate = (decimal)PendingRate;

            var perTabRate = tabsPerStrip > 0 ? effectiveRate / tabsPerStrip : effectiveRate;

            if (PendingStripQuantity > 0)
            {
                gross += (decimal)PendingStripQuantity * effectiveRate;
            }
            if (PendingTabQuantity > 0)
            {
                gross += (decimal)PendingTabQuantity * perTabRate;
            }

            if (gross > 0)
            {
                return Math.Round(gross, 2, MidpointRounding.AwayFromZero);
            }

            if (PendingStripQuantity <= 0 && PendingTabQuantity <= 0 && PendingQuantity > 0)
            {
                return Math.Round((decimal)PendingQuantity * effectiveRate, 2, MidpointRounding.AwayFromZero);
            }

            return 0m;
        }
    }

    public decimal PendingDiscountAmount => Math.Round(PendingGrossAmount * ((decimal)PendingDiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal PendingTaxableAmount => PendingGrossAmount - PendingDiscountAmount;
    public decimal PendingGstAmount => Math.Round(PendingTaxableAmount * ((PendingSelectedItem?.GstRatePercent ?? 12.0m) / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal PendingNetAmount => PendingTaxableAmount;

    [ObservableProperty]
    private int? _insertSpliceIndex = null;

    [ObservableProperty]
    private bool _isSaveConfirmationOpen = false;

    // Amount Details Modal Dialog (Intermediary review modal before payment selection)
    [ObservableProperty]
    private bool _isAmountDetailsModalOpen = false;

    // Sale Type Selection Prompt (Cash, Credit, UPI, Card)
    [ObservableProperty]
    private bool _isSaleTypePromptOpen = false;

    [ObservableProperty]
    private int _selectedSaleTypeIndex = 0;

    // Full-Screen Print Preview Modal
    [ObservableProperty]
    private bool _isPrintPreviewOpen = false;

    // Print Receipt Confirmation Prompt Modal
    [ObservableProperty]
    private bool _isPrintPromptOpen = false;

    [ObservableProperty]
    private string _lastCompletedInvoiceNo = string.Empty;

    [ObservableProperty]
    private decimal _lastCompletedAmount = 0;

    [ObservableProperty]
    private string _lastCompletedCustomer = "Walk-in Customer";

    // Stage 1 Search Results
    [ObservableProperty]
    private bool _hasSearchResults = false;

    public ObservableCollection<ProductSearchItemViewModel> SearchResults { get; } = new();

    // Stage 2 Batch Picker Window
    [ObservableProperty]
    private bool _isBatchPickerOpen = false;

    [ObservableProperty]
    private ProductSearchItemViewModel? _selectedProductForBatches;

    public ObservableCollection<ProductBatchDto> SelectedProductBatches { get; } = new();

    [ObservableProperty]
    private int _selectedBatchIndex = -1;

    // On-The-Fly Item Creation Modal (F2)
    [ObservableProperty]
    private bool _isCreateProductModalOpen = false;

    [ObservableProperty]
    private string _newProductName = string.Empty;

    [ObservableProperty]
    private string _newBrandName = string.Empty;

    [ObservableProperty]
    private string _newGenericName = string.Empty;

    [ObservableProperty]
    private string _newComposition = string.Empty;

    [ObservableProperty]
    private DosageForm _newDosageForm = DosageForm.Tablet;

    [ObservableProperty]
    private string _newPackSizeText = "10x10";

    [ObservableProperty]
    private int _newPackUnits = 100;

    [ObservableProperty]
    private string _newBaseUnit = "TAB";

    [ObservableProperty]
    private string _newHsnCode = "3004";

    [ObservableProperty]
    private double _newGstPercent = 12.0;

    [ObservableProperty]
    private DrugSchedule _newSchedule = DrugSchedule.OTC;

    [ObservableProperty]
    private string _newManufacturerName = string.Empty;

    [ObservableProperty]
    private string _newBarcode = string.Empty;

    [ObservableProperty]
    private bool _newIsColdChain = false;

    [ObservableProperty]
    private string _newBatchNumber = "B101";

    [ObservableProperty]
    private DateTimeOffset _newExpiryDate = DateTimeOffset.UtcNow.AddMonths(18);

    [ObservableProperty]
    private double _newMrp = 100.0;

    [ObservableProperty]
    private double _newPurchaseRate = 70.0;

    [ObservableProperty]
    private double _newSaleRate = 90.0;

    [ObservableProperty]
    private double _newOpeningQty = 100.0;

    [ObservableProperty]
    private double _newMinStockAlert = 10.0;

    [ObservableProperty]
    private int _nearExpiryDays = 90;

    [ObservableProperty]
    private string _newProductNameError = string.Empty;

    [ObservableProperty]
    private string _newPackSizeTextError = string.Empty;

    [ObservableProperty]
    private string _newBatchNumberError = string.Empty;

    [ObservableProperty]
    private string _newMrpError = string.Empty;

    [ObservableProperty]
    private string _newSaleRateError = string.Empty;

    [ObservableProperty]
    private string _createProductFormError = string.Empty;

    // --- Party / Credit Customer Selection (Marg ERP Style) ---
    private readonly ICustomerService? _customerService;

    [ObservableProperty]
    private bool _isPartyPickerOpen = false;

    [ObservableProperty]
    private string _partySearchQuery = string.Empty;

    public ObservableCollection<CustomerDto> PartySearchResults { get; } = new();

    [ObservableProperty]
    private int _selectedPartyIndex = -1;

    [ObservableProperty]
    private CustomerDto? _selectedParty;

    // --- Customer Auto-Complete / Quick Pick Properties ---
    [ObservableProperty]
    private bool _isCustomerQuickPickOpen = false;

    public ObservableCollection<CustomerDto> CustomerSuggestions { get; } = new();

    [ObservableProperty]
    private int _selectedCustomerSuggestionIndex = -1;

    [ObservableProperty]
    private CustomerDto? _selectedCustomerSuggestion;

    // --- Fast Party Creation Form Properties ---
    private readonly IDocumentSequenceService? _sequenceService;

    [ObservableProperty]
    private bool _isCreatePartyModalOpen = false;

    [ObservableProperty]
    private string _newPartyName = string.Empty;

    [ObservableProperty]
    private string _newPartyPhone = string.Empty;

    [ObservableProperty]
    private string _newPartyAddress = string.Empty;

    [ObservableProperty]
    private string _newPartyCity = "DELHI";

    [ObservableProperty]
    private double _newPartyCreditLimit = 25000.0;

    [ObservableProperty]
    private double _newPartyOpeningBalance = 0.0;

    [ObservableProperty]
    private string _newPartyGstin = string.Empty;

    [ObservableProperty]
    private string _newPartyNameError = string.Empty;

    [ObservableProperty]
    private string _newPartyPhoneError = string.Empty;

    [ObservableProperty]
    private string _createPartyFormError = string.Empty;

    private readonly string? _draftStorageFilePath;

    public PosViewModel(
        IProductSearchService searchService,
        IPosTransactionService posTransactionService,
        IProductService productService,
        string draftStorageFilePath)
        : this(searchService, posTransactionService, productService, null, null, draftStorageFilePath)
    {
    }

    public PosViewModel(
        IProductSearchService searchService,
        IPosTransactionService posTransactionService,
        IProductService productService,
        ICustomerService? customerService,
        string? draftStorageFilePath)
        : this(searchService, posTransactionService, productService, customerService, null, draftStorageFilePath)
    {
    }

    public PosViewModel(
        IProductSearchService searchService,
        IPosTransactionService posTransactionService,
        IProductService productService,
        ICustomerService? customerService = null,
        IDocumentSequenceService? sequenceService = null,
        string? draftStorageFilePath = null)
    {
        _searchService = searchService;
        _posTransactionService = posTransactionService;
        _productService = productService;
        _customerService = customerService;
        _sequenceService = sequenceService;
        _draftStorageFilePath = draftStorageFilePath;

        NearExpiryDays = SettingsViewModel.GetNearExpiryDays();
        LoadDraftState();
        _ = SyncInitialTabNumberWithDbAsync();
    }

    private string GetDraftFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_draftStorageFilePath))
        {
            var customDir = Path.GetDirectoryName(_draftStorageFilePath);
            if (!string.IsNullOrEmpty(customDir) && !Directory.Exists(customDir))
            {
                Directory.CreateDirectory(customDir);
            }
            return _draftStorageFilePath;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var medistockDir = Path.Combine(appData, "Medistock", "Data");
        if (!Directory.Exists(medistockDir))
        {
            Directory.CreateDirectory(medistockDir);
        }
        return Path.Combine(medistockDir, "pos_draft_state.json");
    }

    private void RegisterTabListeners(InvoiceTabViewModel tab)
    {
        tab.PropertyChanged -= Tab_PropertyChanged;
        tab.PropertyChanged += Tab_PropertyChanged;
        tab.CartItems.CollectionChanged -= Tab_CartItems_CollectionChanged;
        tab.CartItems.CollectionChanged += Tab_CartItems_CollectionChanged;
    }

    private void Tab_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(InvoiceTabViewModel.SelectedCartIndex))
        {
            SaveDraftState();
        }
    }

    private void Tab_CartItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SaveDraftState();
    }

    public void SaveDraftState()
    {
        try
        {
            var filePath = GetDraftFilePath();
            var draft = new PosDraftStateDto
            {
                TabCounter = _tabCounter,
                ActiveTabIndex = ActiveTabIndex,
                Tabs = InvoiceTabs.Select(t => new InvoiceTabDraftDto
                {
                    TabNumber = t.TabNumber,
                    TabId = t.TabId,
                    CustomInvoiceNo = t.CustomInvoiceNo,
                    InvoiceDate = t.InvoiceDate.ToString("yyyy-MM-dd"),
                    CustomerName = t.CustomerName,
                    CustomerMobile = t.CustomerMobile,
                    DoctorName = t.DoctorName,
                    PaymentMode = t.PaymentMode,
                    IsInterstate = t.IsInterstate,
                    BillDiscountPercent = t.BillDiscountPercent,
                    BillDiscountAmount = t.BillDiscountAmount,
                    SelectedCartIndex = t.SelectedCartIndex,
                    CartItems = t.CartItems.Select(c => new CartItemDraftDto
                    {
                        ProductId = c.ProductId,
                        ProductName = c.ProductName,
                        PackSizeDescription = c.PackSizeDescription,
                        BatchId = c.BatchId,
                        BatchNumber = c.BatchNumber,
                        ExpiryDate = c.ExpiryDate,
                        Mrp = c.Mrp,
                        GstRatePercent = c.GstRatePercent,
                        IsColdChain = c.IsColdChain,
                        Schedule = c.Schedule,
                        StripsPerBox = c.StripsPerBox,
                        TabsPerStrip = c.TabsPerStrip,
                        TotalUnitsPerBox = c.TotalUnitsPerBox,
                        AvailableQuantity = c.AvailableQuantity,
                        StripQuantity = c.StripQuantity,
                        TabQuantity = c.TabQuantity,
                        Quantity = c.Quantity,
                        FreeQuantity = c.FreeQuantity,
                        UnitPrice = c.UnitPrice,
                        DiscountPercent = c.DiscountPercent
                    }).ToList()
                }).ToList()
            };

            var json = JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            // Non-fatal exception handling for draft auto-save
        }
    }

    public void LoadDraftState()
    {
        try
        {
            var filePath = GetDraftFilePath();
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var draft = JsonSerializer.Deserialize<PosDraftStateDto>(json);
                if (draft != null && draft.Tabs != null && draft.Tabs.Count > 0)
                {
                    InvoiceTabs.Clear();
                    _tabCounter = Math.Max(1, draft.TabCounter);

                    foreach (var tabDto in draft.Tabs)
                    {
                        var tab = new InvoiceTabViewModel(tabDto.TabNumber, tabDto.TabId)
                        {
                            CustomInvoiceNo = tabDto.CustomInvoiceNo ?? string.Empty,
                            CustomerName = tabDto.CustomerName ?? "Walk-in Customer",
                            CustomerMobile = tabDto.CustomerMobile ?? string.Empty,
                            DoctorName = tabDto.DoctorName ?? string.Empty,
                            PaymentMode = tabDto.PaymentMode,
                            IsInterstate = tabDto.IsInterstate,
                            BillDiscountPercent = tabDto.BillDiscountPercent,
                            BillDiscountAmount = tabDto.BillDiscountAmount
                        };

                        if (!string.IsNullOrWhiteSpace(tabDto.InvoiceDate) && DateTime.TryParse(tabDto.InvoiceDate, out var dt))
                        {
                            tab.InvoiceDate = dt;
                        }

                        if (tabDto.CartItems != null)
                        {
                            foreach (var itemDto in tabDto.CartItems)
                            {
                                var item = new CartItemViewModel
                                {
                                    ProductId = itemDto.ProductId,
                                    ProductName = itemDto.ProductName,
                                    PackSizeDescription = itemDto.PackSizeDescription,
                                    BatchId = itemDto.BatchId,
                                    BatchNumber = itemDto.BatchNumber,
                                    ExpiryDate = itemDto.ExpiryDate,
                                    Mrp = itemDto.Mrp,
                                    GstRatePercent = itemDto.GstRatePercent,
                                    IsColdChain = itemDto.IsColdChain,
                                    Schedule = itemDto.Schedule,
                                    StripsPerBox = itemDto.StripsPerBox,
                                    TabsPerStrip = itemDto.TabsPerStrip,
                                    TotalUnitsPerBox = itemDto.TotalUnitsPerBox,
                                    AvailableQuantity = itemDto.AvailableQuantity,
                                    StripQuantity = itemDto.StripQuantity,
                                    TabQuantity = itemDto.TabQuantity,
                                    Quantity = itemDto.Quantity,
                                    FreeQuantity = itemDto.FreeQuantity,
                                    UnitPrice = itemDto.UnitPrice,
                                    DiscountPercent = itemDto.DiscountPercent
                                };
                                tab.CartItems.Add(item);
                            }
                        }

                        tab.SelectedCartIndex = tabDto.SelectedCartIndex >= 0 && tabDto.SelectedCartIndex < tab.CartItems.Count
                            ? tabDto.SelectedCartIndex
                            : (tab.CartItems.Count > 0 ? 0 : -1);

                        tab.RecalculateTotals();
                        RegisterTabListeners(tab);
                        InvoiceTabs.Add(tab);
                    }

                    ActiveTabIndex = draft.ActiveTabIndex >= 0 && draft.ActiveTabIndex < InvoiceTabs.Count ? draft.ActiveTabIndex : 0;
                    ActiveTab = InvoiceTabs[ActiveTabIndex];
                    UpdateActiveTabState();
                    return;
                }
            }
        }
        catch
        {
            // If draft read fails, start with fresh default tab
        }

        InvoiceTabs.Clear();
        var initialTab = new InvoiceTabViewModel(_tabCounter++);
        RegisterTabListeners(initialTab);
        InvoiceTabs.Add(initialTab);
        ActiveTab = initialTab;
        ActiveTabIndex = 0;
        UpdateActiveTabState();
    }

    public async Task SyncInitialTabNumberWithDbAsync()
    {
        if (_sequenceService == null) return;
        try
        {
            var dbSeq = await _sequenceService.PeekNextSequenceNumberAsync(_orgId, _branchId, "INV");
            if (InvoiceTabs.Count == 1 && InvoiceTabs[0].CartItems.Count == 0 && (InvoiceTabs[0].TabNumber <= 1 || string.IsNullOrWhiteSpace(InvoiceTabs[0].CustomInvoiceNo)))
            {
                InvoiceTabs[0].TabNumber = dbSeq;
                _tabCounter = dbSeq + 1;
            }
            else
            {
                _tabCounter = Math.Max(_tabCounter, dbSeq + 1);
            }
        }
        catch
        {
            // Non-fatal background peek
        }
    }

    partial void OnActiveTabIndexChanged(int value)
    {
        if (value >= 0 && value < InvoiceTabs.Count)
        {
            ActiveTab = InvoiceTabs[value];
            SaveDraftState();
        }
    }

    [RelayCommand]
    public async Task AddNewTabAsync()
    {
        int nextNum = _tabCounter++;
        if (_sequenceService != null)
        {
            try
            {
                var dbSeq = await _sequenceService.PeekNextSequenceNumberAsync(_orgId, _branchId, "INV");
                var maxCurrent = InvoiceTabs.Count > 0 ? InvoiceTabs.Max(t => t.TabNumber) : 0;
                nextNum = Math.Max(dbSeq, maxCurrent + 1);
                _tabCounter = nextNum + 1;
            }
            catch
            {
            }
        }

        var newTab = new InvoiceTabViewModel(nextNum);
        RegisterTabListeners(newTab);
        InvoiceTabs.Add(newTab);
        ActiveTab = newTab;
        ActiveTabIndex = InvoiceTabs.Count - 1;
        SaveDraftState();
        SelectedSaleTypeIndex = 0;
        IsSaleTypePromptOpen = false;
        StatusMessage = $"Opened {newTab.TabTitle}. Ready for billing.";
    }

    public void AddNewTab()
    {
        _ = AddNewTabAsync();
    }

    public void NewTab() => AddNewTab();

    [RelayCommand]
    public void RequestCloseTab(InvoiceTabViewModel? tab)
    {
        var targetTab = tab ?? ActiveTab;
        if (targetTab == null) return;

        // If tab has no items in cart, close or clear immediately without popup
        if (targetTab.CartItems.Count == 0)
        {
            CloseTabInternal(targetTab);
            return;
        }

        // If tab has items, switch to it and open confirmation
        var targetIdx = InvoiceTabs.IndexOf(targetTab);
        if (targetIdx >= 0)
        {
            ActiveTabIndex = targetIdx;
            ActiveTab = targetTab;
        }

        TabPendingClose = targetTab;
        IsCloseTabConfirmationOpen = true;
        StatusMessage = $"Confirm discard of unsaved bill? '{targetTab.TabTitle}' has {targetTab.CartItems.Count} item(s) worth ₹{targetTab.GrandTotal:N2}.";
    }

    [RelayCommand]
    public void ConfirmCloseTab()
    {
        if (TabPendingClose != null)
        {
            CloseTabInternal(TabPendingClose);
            TabPendingClose = null;
        }
        IsCloseTabConfirmationOpen = false;
    }

    [RelayCommand]
    public void CancelCloseTab()
    {
        TabPendingClose = null;
        IsCloseTabConfirmationOpen = false;
        StatusMessage = "Kept bill.";
    }

    [RelayCommand]
    public void OpenExitAppConfirmation()
    {
        IsExitAppConfirmationOpen = true;
    }

    [RelayCommand]
    public void CloseExitAppConfirmation()
    {
        IsExitAppConfirmationOpen = false;
    }

    private void CloseTabInternal(InvoiceTabViewModel targetTab)
    {
        if (InvoiceTabs.Count <= 1)
        {
            targetTab.CartItems.Clear();
            targetTab.CustomerName = "WALK-IN CUSTOMER";
            targetTab.CustomerMobile = string.Empty;
            targetTab.DoctorName = string.Empty;
            targetTab.RecalculateTotals();
            SaveDraftState();
            StatusMessage = "Cleared active bill.";
            return;
        }

        var idx = InvoiceTabs.IndexOf(targetTab);
        InvoiceTabs.Remove(targetTab);

        var nextIdx = Math.Clamp(idx, 0, InvoiceTabs.Count - 1);
        ActiveTabIndex = nextIdx;
        ActiveTab = InvoiceTabs[nextIdx];

        SaveDraftState();
        StatusMessage = "Closed invoice tab.";
    }

    [RelayCommand]
    public void CloseTab(InvoiceTabViewModel? tab)
    {
        RequestCloseTab(tab);
    }

    [RelayCommand]
    public void NextTab()
    {
        if (InvoiceTabs.Count <= 1) return;
        ActiveTabIndex = (ActiveTabIndex + 1) % InvoiceTabs.Count;
        ActiveTab = InvoiceTabs[ActiveTabIndex];
        UpdateActiveTabState();
        SaveDraftState();
        StatusMessage = $"Switched to {ActiveTab.TabTitle}";
    }

    [RelayCommand]
    public void PreviousTab()
    {
        if (InvoiceTabs.Count <= 1) return;
        ActiveTabIndex = (ActiveTabIndex - 1 + InvoiceTabs.Count) % InvoiceTabs.Count;
        ActiveTab = InvoiceTabs[ActiveTabIndex];
        UpdateActiveTabState();
        SaveDraftState();
        StatusMessage = $"Switched to {ActiveTab.TabTitle}";
    }

    [RelayCommand]
    public void ToggleShortcutHelp()
    {
        IsShortcutHelpOpen = !IsShortcutHelpOpen;
    }

    [RelayCommand]
    public void CloseShortcutHelp()
    {
        IsShortcutHelpOpen = false;
    }

    // --- On-The-Fly Item Creation Modal (F2) ---
    [RelayCommand]
    public void OpenCreateProductModal()
    {
        HasSearchResults = false;
        SearchResults.Clear();
        NewProductName = !string.IsNullOrWhiteSpace(SearchQuery) ? SearchQuery.Trim() : string.Empty;
        NewBrandName = NewProductName;
        NewGenericName = string.Empty;
        NewComposition = string.Empty;
        NewDosageForm = DosageForm.Tablet;
        NewPackSizeText = "10x10";
        NewPackUnits = 100;
        NewBaseUnit = "TAB";
        NewHsnCode = "3004";
        NewGstPercent = 12.0;
        NewSchedule = DrugSchedule.OTC;
        NewManufacturerName = string.Empty;
        NewBarcode = string.Empty;
        NewIsColdChain = false;
        NewBatchNumber = $"B{DateTime.UtcNow:yyMM}";
        NewExpiryDate = DateTimeOffset.UtcNow.AddMonths(18);
        NewMrp = 100.0;
        NewPurchaseRate = 70.0;
        NewSaleRate = 90.0;
        NewOpeningQty = 50.0;
        NewMinStockAlert = 10.0;

        ClearCreateProductErrors();

        IsCreateProductModalOpen = true;
        StatusMessage = "Creating new item. Press [Enter] to Save, [Tab] to navigate fields, [Esc] to cancel.";
    }

    public void ClearCreateProductErrors()
    {
        NewProductNameError = string.Empty;
        NewPackSizeTextError = string.Empty;
        NewBatchNumberError = string.Empty;
        NewMrpError = string.Empty;
        NewSaleRateError = string.Empty;
        CreateProductFormError = string.Empty;
    }

    [RelayCommand]
    public void CloseCreateProductModal()
    {
        ClearCreateProductErrors();
        IsCreateProductModalOpen = false;
        StatusMessage = "Ready for billing.";
    }

    [RelayCommand]
    public async Task<bool> SaveCreateProductAsync()
    {
        ClearCreateProductErrors();
        bool hasError = false;

        if (string.IsNullOrWhiteSpace(NewProductName))
        {
            NewProductNameError = "Medicine / Product Name is required.";
            hasError = true;
        }

        if (string.IsNullOrWhiteSpace(NewPackSizeText))
        {
            NewPackSizeTextError = "Pack configuration is required (e.g. 10x10).";
            hasError = true;
        }

        if (string.IsNullOrWhiteSpace(NewBatchNumber))
        {
            NewBatchNumberError = "Batch Number is required.";
            hasError = true;
        }

        if (NewMrp <= 0)
        {
            NewMrpError = "MRP must be greater than ₹0.00.";
            hasError = true;
        }

        if (NewSaleRate <= 0 && NewMrp <= 0)
        {
            NewSaleRateError = "Sale Rate must be greater than ₹0.00.";
            hasError = true;
        }
        else if (NewMrp > 0 && NewSaleRate > NewMrp)
        {
            NewSaleRateError = "Sale Rate cannot be greater than MRP.";
            hasError = true;
        }

        if (hasError)
        {
            CreateProductFormError = "Please fill in all mandatory fields marked with an asterisk (*).";
            StatusMessage = "Cannot save: required fields are missing or invalid.";
            return false;
        }

        var packaging = PackagingHelper.Parse(NewPackSizeText, NewBaseUnit);

        var cmd = new CreateProductWithBatchCommand(
            OrgId: OrgId,
            WarehouseId: WarehouseId,
            Name: NewProductName.Trim(),
            BrandName: string.IsNullOrWhiteSpace(NewBrandName) ? NewProductName.Trim() : NewBrandName.Trim(),
            GenericName: NewGenericName.Trim(),
            Composition: NewComposition.Trim(),
            Strength: string.Empty,
            DosageForm: NewDosageForm,
            PackUnits: packaging.TotalUnitsPerBox > 0 ? packaging.TotalUnitsPerBox : 10,
            BaseUnit: string.IsNullOrWhiteSpace(NewBaseUnit) ? "TAB" : NewBaseUnit.Trim().ToUpperInvariant(),
            HsnCode: string.IsNullOrWhiteSpace(NewHsnCode) ? "3004" : NewHsnCode.Trim(),
            GstRatePercent: (decimal)NewGstPercent,
            Schedule: NewSchedule,
            PrimaryBarcode: string.IsNullOrWhiteSpace(NewBarcode) ? null : NewBarcode.Trim(),
            ManufacturerName: string.IsNullOrWhiteSpace(NewManufacturerName) ? null : NewManufacturerName.Trim(),
            IsColdChain: NewIsColdChain,
            BatchNumber: NewBatchNumber.Trim().ToUpperInvariant(),
            ExpiryDate: NewExpiryDate.DateTime,
            Mrp: (decimal)NewMrp,
            PurchaseRate: (decimal)NewPurchaseRate,
            SaleRate: (decimal)(NewSaleRate > 0 ? Math.Min(NewSaleRate, NewMrp) : NewMrp),
            OpeningQuantity: (decimal)NewOpeningQty,
            MinStockAlert: (decimal)(NewMinStockAlert > 0 ? NewMinStockAlert : 10.0)
        );

        var result = await _productService.CreateProductWithBatchAsync(cmd);

        if (result.Success && result.ProductId != null && result.BatchId != null)
        {
            ClearCreateProductErrors();
            IsCreateProductModalOpen = false;

            // Automatically add newly created item to active tab cart
            if (ActiveTab != null)
            {
                var cartItem = new CartItemViewModel
                {
                    ProductId = result.ProductId,
                    ProductName = cmd.Name,
                    PackSizeDescription = packaging.FormattedDescription,
                    StripsPerBox = packaging.StripsPerBox,
                    TabsPerStrip = packaging.TabsPerStrip,
                    TotalUnitsPerBox = packaging.TotalUnitsPerBox,
                    BatchId = result.BatchId,
                    BatchNumber = cmd.BatchNumber,
                    ExpiryDate = cmd.ExpiryDate,
                    UnitPrice = cmd.SaleRate > 0 ? cmd.SaleRate : cmd.Mrp,
                    Mrp = cmd.Mrp,
                    GstRatePercent = cmd.GstRatePercent,
                    IsColdChain = cmd.IsColdChain,
                    Schedule = cmd.Schedule,
                    StripQuantity = 1,
                    TabQuantity = 0,
                    Quantity = packaging.TabsPerStrip,
                    FreeQuantity = 0,
                    DiscountPercent = 0
                };

                ActiveTab.CartItems.Add(cartItem);
                ActiveTab.SelectedCartIndex = ActiveTab.CartItems.Count - 1;
                ActiveTab.RecalculateTotals();
            }

            SearchQuery = string.Empty;
            SearchResults.Clear();
            HasSearchResults = false;
            StatusMessage = $"Created & Added: {cmd.Name} (Batch {cmd.BatchNumber})";
            return true;
        }
        else
        {
            CreateProductFormError = $"Failed to save item: {result.ErrorMessage}";
            StatusMessage = $"Error adding item: {result.ErrorMessage}";
            return false;
        }
    }

    // --- Stage 1 & Stage 2 Search ---
    async partial void OnSearchQueryChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            SelectedSearchIndex = -1;
            HasSearchResults = false;
            IsSearching = false;
            return;
        }

        try
        {
            IsSearching = true;
            await Task.Delay(40, token); // 40ms instant debounce

            var results = await _searchService.SearchAsync(value, WarehouseId, 25, token);

            if (!token.IsCancellationRequested)
            {
                SearchResults.Clear();
                foreach (var r in results)
                {
                    SearchResults.Add(ProductSearchItemViewModel.FromDto(r, NearExpiryDays, value));
                }

                SelectedSearchIndex = SearchResults.Count > 0 ? 0 : -1;
                HasSearchResults = SearchResults.Count > 0;
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            IsSearching = false;
        }
    }

    public void MoveSearchSelectionDown()
    {
        if (SearchResults.Count == 0) return;
        if (SelectedSearchIndex < SearchResults.Count - 1)
        {
            SelectedSearchIndex++;
        }
    }

    public void MoveSearchSelectionUp()
    {
        if (SearchResults.Count == 0) return;
        if (SelectedSearchIndex > 0)
        {
            SelectedSearchIndex--;
        }
    }

    // --- Two-Stage Batch Selection Window & Line Loop ---
    public bool PrepareLineItem(ProductSearchItemViewModel product)
    {
        if (product == null) return false;

        SelectedProductForBatches = product;
        SelectedProductBatches.Clear();

        if (product.Batches != null && product.Batches.Count > 0)
        {
            foreach (var b in product.Batches)
            {
                b.NearExpiryDays = NearExpiryDays;
                b.MinStockAlert = product.MinStockAlert;
                SelectedProductBatches.Add(b);
            }
        }
        else if (!string.IsNullOrEmpty(product.BatchId))
        {
            SelectedProductBatches.Add(new ProductBatchDto
            {
                Id = product.BatchId,
                ProductId = product.Id,
                BatchNumber = product.BatchNumber ?? "DEFAULT",
                ExpiryDate = product.NearestExpiryDate ?? DateTime.UtcNow.AddYears(1),
                Mrp = product.Mrp,
                SaleRate = product.SaleRate > 0 ? product.SaleRate : product.Mrp,
                PurchaseRate = product.SaleRate * 0.8m,
                AvailableQuantity = product.AvailableQuantity,
                MinStockAlert = product.MinStockAlert,
                NearExpiryDays = NearExpiryDays
            });
        }

        // MARG ERP Rule: If exactly 1 batch, auto-select and skip straight to quantity!
        if (SelectedProductBatches.Count == 1)
        {
            PendingSelectedItem = product;
            PendingSelectedBatch = SelectedProductBatches[0];
            PendingStripQuantity = 1;
            PendingTabQuantity = 0;
            PendingFreeQuantity = 0;
            var effectiveMrp = PendingSelectedBatch.Mrp > 0 ? PendingSelectedBatch.Mrp : product.Mrp;
            PendingRate = (double)effectiveMrp;
            PendingDiscountPercent = 0;
            IsBatchPickerOpen = false;
            StatusMessage = $"{product.Name} (Batch {PendingSelectedBatch.BatchNumber}) selected. Enter Quantity.";
            return true; // Single batch, jump to Qty
        }
        else if (SelectedProductBatches.Count > 1)
        {
            SelectedBatchIndex = 0;
            IsBatchPickerOpen = true;
            PendingSelectedItem = product;
            PendingSelectedBatch = SelectedProductBatches[0];
            PendingStripQuantity = 1;
            PendingTabQuantity = 0;
            PendingFreeQuantity = 0;
            var effectiveMrp = SelectedProductBatches[0].Mrp > 0 ? SelectedProductBatches[0].Mrp : product.Mrp;
            PendingRate = (double)effectiveMrp;
            PendingDiscountPercent = 0;
            StatusMessage = $"Select Batch for {product.Name} [↑/↓ to choose, Enter to confirm]";
            return false; // Multiple batches, show batch picker
        }
        else
        {
            StatusMessage = $"No available batches for {product.Name}";
            return false;
        }
    }

    public void OpenBatchPicker(ProductSearchItemViewModel product)
    {
        PrepareLineItem(product);
    }

    [RelayCommand]
    public void SelectBatch(ProductBatchDto? batch)
    {
        if (batch == null || SelectedProductForBatches == null) return;

        PendingSelectedItem = SelectedProductForBatches;
        PendingSelectedBatch = batch;
        PendingStripQuantity = 1;
        PendingTabQuantity = 0;
        PendingFreeQuantity = 0;
        var effectiveMrp = batch.Mrp > 0 ? batch.Mrp : SelectedProductForBatches.Mrp;
        PendingRate = (double)effectiveMrp;
        PendingDiscountPercent = 0;
        CloseBatchPicker();
        StatusMessage = $"{PendingSelectedItem.Name} (Batch {batch.BatchNumber}) selected. Enter Quantity.";
    }

    [RelayCommand]
    public void CommitCurrentLine()
    {
        if (ActiveTab == null) return;

        if (PendingSelectedItem == null && SelectedProductForBatches != null)
        {
            PendingSelectedItem = SelectedProductForBatches;
        }

        if (PendingSelectedItem == null)
        {
            StatusMessage = "No item selected to add.";
            return;
        }

        var product = PendingSelectedItem;
        var batch = PendingSelectedBatch ?? (SelectedProductBatches.Count > 0 ? SelectedProductBatches[0] : null);

        var batchId = batch?.Id ?? product.BatchId ?? Guid.NewGuid().ToString("N");
        var batchNo = batch?.BatchNumber ?? product.BatchNumber ?? "DEFAULT";
        var expiry = batch?.ExpiryDate ?? product.NearestExpiryDate ?? DateTime.UtcNow.AddYears(1);
        var mrp = batch?.Mrp ?? product.Mrp;
        var unitPrice = mrp > 0 ? mrp : (PendingRate > 0 ? (decimal)PendingRate : (product.SaleRate > 0 ? product.SaleRate : mrp));

        var available = batch != null ? batch.AvailableQuantity : product.AvailableQuantity;
        if (available <= 0)
        {
            StatusMessage = $"⚠️ Cannot add {product.Name}: Batch {batchNo} is OUT OF STOCK (0 units available).";
            ShowSaleError("Out of Stock", $"Cannot add '{product.Name}' (Batch {batchNo}) because it is OUT OF STOCK (0 units available).");
            return;
        }

        var packaging = PackagingHelper.Parse(product.PackSizeDescription);
        var strips = (decimal)PendingStripQuantity;
        var tabs = (decimal)PendingTabQuantity;
        var freeQty = (decimal)PendingFreeQuantity;
        var disc = (decimal)PendingDiscountPercent;

        var tabsPerStrip = packaging.TabsPerStrip > 0 ? packaging.TabsPerStrip : 1;
        var requestedUnits = (strips * tabsPerStrip) + tabs;
        if (available > 0 && requestedUnits > available)
        {
            strips = Math.Max(0, Math.Floor(available / (decimal)tabsPerStrip));
            tabs = Math.Max(0, available - (strips * (decimal)tabsPerStrip));
            StatusMessage = $"⚠️ Stock limited: {product.Name} (Batch {batchNo}) has only {available:0.#} available units. Quantity adjusted to {strips:0.#} strips, {tabs:0.#} tabs.";
        }

        var item = new CartItemViewModel
        {
            ProductId = product.Id,
            ProductName = product.Name,
            PackSizeDescription = packaging.FormattedDescription,
            StripsPerBox = packaging.StripsPerBox,
            TabsPerStrip = packaging.TabsPerStrip,
            TotalUnitsPerBox = packaging.TotalUnitsPerBox,
            BatchId = batchId,
            BatchNumber = batchNo,
            ExpiryDate = expiry,
            Mrp = mrp,
            UnitPrice = unitPrice,
            GstRatePercent = product.GstRatePercent,
            IsColdChain = product.IsColdChain,
            Schedule = product.Schedule,
            AvailableQuantity = available,
            StripQuantity = strips,
            TabQuantity = tabs,
            FreeQuantity = freeQty,
            DiscountPercent = disc
        };

        if (strips == 0 && tabs == 0 && PendingQuantity > 0)
        {
            item.Quantity = (decimal)PendingQuantity;
        }

        if (InsertSpliceIndex.HasValue && InsertSpliceIndex.Value >= 0 && InsertSpliceIndex.Value < ActiveTab.CartItems.Count)
        {
            ActiveTab.CartItems.Insert(InsertSpliceIndex.Value, item);
            ActiveTab.SelectedCartIndex = InsertSpliceIndex.Value;
            InsertSpliceIndex = null;
        }
        else
        {
            ActiveTab.CartItems.Add(item);
            ActiveTab.SelectedCartIndex = ActiveTab.CartItems.Count - 1;
        }

        ActiveTab.RecalculateTotals();

        // Reset input state for next line item
        PendingSelectedItem = null;
        PendingSelectedBatch = null;
        PendingStripQuantity = 1;
        PendingTabQuantity = 0;
        PendingFreeQuantity = 0;
        PendingRate = 0;
        PendingDiscountPercent = 0;
        SearchQuery = string.Empty;
        SearchResults.Clear();
        HasSearchResults = false;
        CloseBatchPicker();

        StatusMessage = $"Added {product.Name} (Strips {strips:0.#}, Tabs {tabs:0.#}, Free {freeQty:0.#}). Ready for next item.";
    }

    [RelayCommand]
    public void CancelPendingLine()
    {
        PendingSelectedItem = null;
        PendingSelectedBatch = null;
        PendingStripQuantity = 1;
        PendingTabQuantity = 0;
        PendingFreeQuantity = 0;
        PendingRate = 0;
        PendingDiscountPercent = 0;
        CloseBatchPicker();
        StatusMessage = "Cancelled item entry. Ready for billing.";
    }

    [RelayCommand]
    public void SetInsertSpliceMode()
    {
        if (ActiveTab != null && ActiveTab.SelectedCartIndex >= 0 && ActiveTab.SelectedCartIndex < ActiveTab.CartItems.Count)
        {
            InsertSpliceIndex = ActiveTab.SelectedCartIndex;
            StatusMessage = $"[INSERT MODE] Next item will be inserted before row #{InsertSpliceIndex.Value + 1}. Scan/search medicine.";
        }
    }

    [RelayCommand]
    public void OpenSaveConfirmation()
    {
        if (ActiveTab == null || !ActiveTab.CartItems.Any())
        {
            StatusMessage = "Cannot save an empty bill.";
            return;
        }

        IsSaveConfirmationOpen = true;
        StatusMessage = $"Save Bill? Total ₹{ActiveTab.GrandTotal:N2}. [Enter: Yes / Save, Esc: Cancel]";
    }

    [RelayCommand]
    public void CloseSaveConfirmation()
    {
        IsSaveConfirmationOpen = false;
        StatusMessage = "Ready for billing.";
    }

    [RelayCommand]
    public void OpenAmountDetails()
    {
        if (ActiveTab == null || !ActiveTab.CartItems.Any())
        {
            StatusMessage = "Cart is empty. Scan barcode or type medicine name to begin.";
            return;
        }

        ActiveTab.RecalculateTotals();
        IsAmountDetailsModalOpen = true;
        StatusMessage = $"Bill Summary: Total ₹{ActiveTab.GrandTotal:N2}. Press [Enter] for Payment Mode, [Esc] to return.";
    }

    [RelayCommand]
    public void CloseAmountDetails()
    {
        IsAmountDetailsModalOpen = false;
        StatusMessage = "Ready for billing.";
    }

    [RelayCommand]
    public void OpenSaleTypePrompt()
    {
        SelectedSaleTypeIndex = ActiveTab?.PaymentModeIndex ?? 0;
        IsSaleTypePromptOpen = true;
        StatusMessage = "Select Sale Type: [1] Cash, [2] Credit, [3] UPI/QR, [4] Card";
    }

    [RelayCommand]
    public void SelectSaleType(int index)
    {
        if (ActiveTab != null)
        {
            ActiveTab.PaymentModeIndex = index;
        }
        SelectedSaleTypeIndex = index;
        IsSaleTypePromptOpen = false;

        if (index == 3) // Credit (Party Sale)
        {
            OpenPartyPicker();
        }
        else
        {
            if (ActiveTab != null && ActiveTab.CartItems.Any())
            {
                _ = FinalizeSaleInternalAsync(showPrintPrompt: true);
            }
            else
            {
                StatusMessage = $"Sale Type set to {ActiveTab?.PaymentMode}. Enter invoice details.";
            }
        }
    }

    [RelayCommand]
    public void OpenPartyPicker()
    {
        PartySearchQuery = string.Empty;
        SelectedPartyIndex = -1;
        SelectedParty = null;
        IsPartyPickerOpen = true;
        _ = SearchPartiesAsync(string.Empty);
        StatusMessage = "Credit Sale: Select Party / Debtor or press [F2] to create new.";
    }

    [RelayCommand]
    public void ClosePartyPicker()
    {
        IsPartyPickerOpen = false;
        PartySearchResults.Clear();
        SelectedPartyIndex = -1;
        SelectedParty = null;
    }

    public async Task SearchPartiesAsync(string query)
    {
        if (_customerService == null) return;
        var list = await _customerService.SearchCustomersAsync(OrgId, query);
        PartySearchResults.Clear();
        foreach (var c in list)
        {
            PartySearchResults.Add(c);
        }
        if (PartySearchResults.Count > 0)
        {
            SelectedPartyIndex = 0;
            SelectedParty = PartySearchResults[0];
        }
        else
        {
            SelectedPartyIndex = -1;
            SelectedParty = null;
        }
    }

    async partial void OnPartySearchQueryChanged(string value)
    {
        await SearchPartiesAsync(value);
    }

    public void MovePartySelectionDown()
    {
        if (PartySearchResults.Count == 0) return;
        if (SelectedPartyIndex < PartySearchResults.Count - 1)
        {
            SelectedPartyIndex++;
            SelectedParty = PartySearchResults[SelectedPartyIndex];
        }
    }

    public void MovePartySelectionUp()
    {
        if (PartySearchResults.Count == 0) return;
        if (SelectedPartyIndex > 0)
        {
            SelectedPartyIndex--;
            SelectedParty = PartySearchResults[SelectedPartyIndex];
        }
    }

    [RelayCommand]
    public void SelectParty(CustomerDto? party)
    {
        var target = party ?? SelectedParty;
        if (target != null && ActiveTab != null)
        {
            ActiveTab.CustomerId = target.Id;
            ActiveTab.CustomerName = target.Name;
            ActiveTab.CustomerMobile = target.Phone ?? string.Empty;
            ClosePartyPicker();

            if (ActiveTab.CartItems.Any())
            {
                _ = FinalizeSaleInternalAsync(showPrintPrompt: true);
            }
            else
            {
                StatusMessage = $"Party selected: {target.Name} (Bal: ₹{target.CurrentBalance:N2}). Ready for billing.";
            }
        }
    }

    public async Task SearchCustomerQuickPickAsync(string query)
    {
        if (_customerService == null) return;
        var trimmed = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length < 1)
        {
            CustomerSuggestions.Clear();
            IsCustomerQuickPickOpen = false;
            SelectedCustomerSuggestionIndex = -1;
            SelectedCustomerSuggestion = null;
            return;
        }

        var list = await _customerService.SearchCustomersAsync(OrgId, trimmed);
        CustomerSuggestions.Clear();
        foreach (var c in list)
        {
            CustomerSuggestions.Add(c);
        }

        if (CustomerSuggestions.Count > 0)
        {
            SelectedCustomerSuggestionIndex = 0;
            SelectedCustomerSuggestion = CustomerSuggestions[0];
            IsCustomerQuickPickOpen = true;
        }
        else
        {
            SelectedCustomerSuggestionIndex = -1;
            SelectedCustomerSuggestion = null;
            IsCustomerQuickPickOpen = false;
        }
    }

    public void MoveCustomerSuggestionDown()
    {
        if (CustomerSuggestions.Count == 0) return;
        if (SelectedCustomerSuggestionIndex < CustomerSuggestions.Count - 1)
        {
            SelectedCustomerSuggestionIndex++;
            SelectedCustomerSuggestion = CustomerSuggestions[SelectedCustomerSuggestionIndex];
        }
    }

    public void MoveCustomerSuggestionUp()
    {
        if (CustomerSuggestions.Count == 0) return;
        if (SelectedCustomerSuggestionIndex > 0)
        {
            SelectedCustomerSuggestionIndex--;
            SelectedCustomerSuggestion = CustomerSuggestions[SelectedCustomerSuggestionIndex];
        }
    }

    [RelayCommand]
    public void SelectCustomerQuickPick(CustomerDto? customer)
    {
        var target = customer ?? SelectedCustomerSuggestion;
        if (target != null && ActiveTab != null)
        {
            ActiveTab.CustomerId = target.Id;
            ActiveTab.CustomerName = target.Name;
            ActiveTab.CustomerMobile = target.Phone ?? string.Empty;
            IsCustomerQuickPickOpen = false;
            CustomerSuggestions.Clear();
            SelectedCustomerSuggestionIndex = -1;
            SelectedCustomerSuggestion = null;
            StatusMessage = $"Customer selected: {target.Name}";
        }
    }

    [RelayCommand]
    public void OpenCreatePartyModal()
    {
        NewPartyName = !string.IsNullOrWhiteSpace(PartySearchQuery) ? PartySearchQuery.Trim() : string.Empty;
        NewPartyPhone = string.Empty;
        NewPartyAddress = string.Empty;
        NewPartyCity = "DELHI";
        NewPartyCreditLimit = 25000.0;
        NewPartyOpeningBalance = 0.0;
        NewPartyGstin = string.Empty;

        ClearCreatePartyErrors();
        IsPartyPickerOpen = false;
        IsCreatePartyModalOpen = true;
        StatusMessage = "Create New Party / Customer. Press [Enter] to Save, [Esc] to cancel.";
    }

    public void ClearCreatePartyErrors()
    {
        NewPartyNameError = string.Empty;
        NewPartyPhoneError = string.Empty;
        CreatePartyFormError = string.Empty;
    }

    [RelayCommand]
    public void CloseCreatePartyModal()
    {
        ClearCreatePartyErrors();
        IsCreatePartyModalOpen = false;
        if (ActiveTab?.PaymentMode == PaymentMode.Credit && string.IsNullOrWhiteSpace(ActiveTab.CustomerId))
        {
            OpenPartyPicker();
        }
    }

    [RelayCommand]
    public async Task<bool> SaveCreatePartyAsync()
    {
        ClearCreatePartyErrors();
        bool hasError = false;

        if (string.IsNullOrWhiteSpace(NewPartyName))
        {
            NewPartyNameError = "Party / Customer Name is required.";
            hasError = true;
        }

        if (string.IsNullOrWhiteSpace(NewPartyPhone))
        {
            NewPartyPhoneError = "Mobile Number is required.";
            hasError = true;
        }
        else if (NewPartyPhone.Trim().Length < 10)
        {
            NewPartyPhoneError = "Please enter a valid 10-digit mobile number.";
            hasError = true;
        }

        if (hasError)
        {
            CreatePartyFormError = "Please correct the errors before saving.";
            StatusMessage = "Cannot save party: required fields are missing or invalid.";
            return false;
        }

        if (_customerService == null)
        {
            CreatePartyFormError = "Customer service unavailable.";
            return false;
        }

        var cmd = new CreateCustomerCommand(
            OrgId: OrgId,
            Name: NewPartyName.Trim(),
            Phone: NewPartyPhone.Trim(),
            Address: NewPartyAddress.Trim(),
            City: NewPartyCity.Trim(),
            State: "Delhi",
            Pincode: "110001",
            Gstin: NewPartyGstin.Trim(),
            DlNumber: null,
            CreditLimit: (decimal)NewPartyCreditLimit,
            OpeningBalance: (decimal)NewPartyOpeningBalance
        );

        var result = await _customerService.CreateCustomerAsync(cmd);
        if (result.Success && result.Customer != null)
        {
            ClearCreatePartyErrors();
            IsCreatePartyModalOpen = false;
            SelectParty(result.Customer);
            return true;
        }
        else
        {
            CreatePartyFormError = result.ErrorMessage ?? "Failed to create party.";
            StatusMessage = $"Error creating party: {result.ErrorMessage}";
            return false;
        }
    }

    [RelayCommand]
    public void OpenPrintPreview()
    {
        if (ActiveTab == null || !ActiveTab.CartItems.Any())
        {
            StatusMessage = "Cannot preview an empty bill.";
            return;
        }

        IsSaveConfirmationOpen = false;
        IsPrintPreviewOpen = true;
        StatusMessage = $"Tax Invoice Preview: {ActiveTab.DisplayInvoiceNo} — Total ₹{ActiveTab.GrandTotal:N2}. [Enter: Save & Print, Esc: Close]";
    }

    [RelayCommand]
    public void ClosePrintPreview()
    {
        IsPrintPreviewOpen = false;
        StatusMessage = "Ready for billing.";
    }

    [RelayCommand]
    public async Task ConfirmPrintPreviewAndSaveAsync()
    {
        IsPrintPreviewOpen = false;
        await FinalizeSaleInternalAsync(showPrintPrompt: false);
        await PrintReceiptAsync();
    }

    [RelayCommand]
    public async Task SaveAndNextInvoiceAsync()
    {
        if (ActiveTab == null || !ActiveTab.CartItems.Any())
        {
            StatusMessage = "Cannot save an empty bill.";
            return;
        }

        CloseSaveConfirmation();
        await FinalizeSaleInternalAsync(showPrintPrompt: false);
        StatusMessage = $"Sale {LastCompletedInvoiceNo} saved (₹{LastCompletedAmount:N2}). Ready for next invoice.";
    }

    [RelayCommand]
    public void SaveAndOpenPrintPreview()
    {
        if (ActiveTab == null || !ActiveTab.CartItems.Any())
        {
            StatusMessage = "Cannot preview an empty bill.";
            return;
        }

        CloseSaveConfirmation();
        OpenPrintPreview();
    }

    [RelayCommand]
    public void ConfirmAndFinalizeSale()
    {
        SaveAndOpenPrintPreview();
    }

    public async Task ConfirmAndFinalizeSaleAsync()
    {
        await SaveAndNextInvoiceAsync();
    }

    public void MoveBatchSelectionDown()
    {
        if (SelectedProductBatches.Count == 0) return;
        if (SelectedBatchIndex < SelectedProductBatches.Count - 1)
        {
            SelectedBatchIndex++;
        }
    }

    public void MoveBatchSelectionUp()
    {
        if (SelectedProductBatches.Count == 0) return;
        if (SelectedBatchIndex > 0)
        {
            SelectedBatchIndex--;
        }
    }

    [RelayCommand]
    public void CloseBatchPicker()
    {
        IsBatchPickerOpen = false;
        SelectedProductForBatches = null;
        SelectedProductBatches.Clear();
        SelectedBatchIndex = -1;
    }

    public void IncreaseSelectedCartQuantity()
    {
        if (ActiveTab == null || ActiveTab.CartItems.Count == 0) return;

        if (ActiveTab.SelectedCartIndex < 0 || ActiveTab.SelectedCartIndex >= ActiveTab.CartItems.Count)
        {
            ActiveTab.SelectedCartIndex = ActiveTab.CartItems.Count - 1;
        }

        ActiveTab.CartItems[ActiveTab.SelectedCartIndex].Quantity += 1;
        ActiveTab.RecalculateTotals();
    }

    public void DecreaseSelectedCartQuantity()
    {
        if (ActiveTab == null || ActiveTab.CartItems.Count == 0) return;

        if (ActiveTab.SelectedCartIndex < 0 || ActiveTab.SelectedCartIndex >= ActiveTab.CartItems.Count)
        {
            ActiveTab.SelectedCartIndex = ActiveTab.CartItems.Count - 1;
        }

        if (ActiveTab.CartItems[ActiveTab.SelectedCartIndex].Quantity > 1)
        {
            ActiveTab.CartItems[ActiveTab.SelectedCartIndex].Quantity -= 1;
            ActiveTab.RecalculateTotals();
        }
    }

    [RelayCommand]
    public async Task ProcessBarcodeScanAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;

        var product = await _searchService.ScanBarcodeAsync(barcode, WarehouseId);
        if (product == null)
        {
            StatusMessage = $"Barcode not found: {barcode}";
            return;
        }

        if (string.IsNullOrEmpty(product.BatchId))
        {
            StatusMessage = $"No available batch found for {product.ProductName}";
            return;
        }

        AddToCartFromBarcode(product);
    }

    [RelayCommand]
    public void AddToCart(ProductSearchItemViewModel product)
    {
        if (product == null) return;
        OpenBatchPicker(product);
    }

    private void AddToCartFromBarcode(BarcodeLookupDto product)
    {
        if (ActiveTab == null) return;

        var packaging = PackagingHelper.Parse(product.PackSizeDescription);
        var mrp = product.Mrp;
        var rate = mrp > 0 ? mrp : (product.SaleRate > 0 ? product.SaleRate : mrp);

        var item = new CartItemViewModel
        {
            ProductId = product.ProductId,
            ProductName = product.ProductName,
            PackSizeDescription = packaging.FormattedDescription,
            StripsPerBox = packaging.StripsPerBox,
            TabsPerStrip = packaging.TabsPerStrip,
            TotalUnitsPerBox = packaging.TotalUnitsPerBox,
            BatchId = product.BatchId,
            BatchNumber = product.BatchNumber,
            ExpiryDate = product.ExpiryDate,
            Mrp = mrp,
            UnitPrice = rate,
            GstRatePercent = product.GstRatePercent,
            IsColdChain = product.IsColdChain,
            Schedule = product.Schedule,
            StripQuantity = 1,
            TabQuantity = 0,
            Quantity = packaging.TabsPerStrip,
            FreeQuantity = 0,
            DiscountPercent = 0
        };

        ActiveTab.CartItems.Add(item);
        ActiveTab.SelectedCartIndex = ActiveTab.CartItems.Count - 1;

        ActiveTab.RecalculateTotals();
        SearchQuery = string.Empty;
        SearchResults.Clear();
        HasSearchResults = false;
        StatusMessage = $"Added {product.ProductName} to {ActiveTab.TabTitle}.";
    }

    [RelayCommand]
    public void RemoveCartItem(CartItemViewModel? item)
    {
        if (item == null || ActiveTab == null) return;

        if (ActiveTab.CartItems.Contains(item))
        {
            ActiveTab.CartItems.Remove(item);
            ActiveTab.RecalculateTotals();
        }
    }

    [RelayCommand]
    public async Task ClearBillAsync()
    {
        if (ActiveTab == null) return;
        int nextNum = _tabCounter++;
        if (_sequenceService != null)
        {
            try
            {
                var dbSeq = await _sequenceService.PeekNextSequenceNumberAsync(OrgId, BranchId, "INV");
                var maxOther = InvoiceTabs.Where(t => t != ActiveTab).Select(t => t.TabNumber).DefaultIfEmpty(0).Max();
                nextNum = Math.Max(dbSeq, maxOther + 1);
                _tabCounter = nextNum + 1;
            }
            catch
            {
            }
        }

        ActiveTab.TabNumber = nextNum;
        ActiveTab.CustomInvoiceNo = string.Empty;
        ActiveTab.CartItems.Clear();
        ActiveTab.CustomerId = null;
        ActiveTab.CustomerName = "WALK-IN CUSTOMER";
        ActiveTab.CustomerMobile = string.Empty;
        ActiveTab.DoctorName = string.Empty;
        ActiveTab.InvoiceDate = DateTime.Today;
        ActiveTab.PaymentMode = PaymentMode.Cash;
        ActiveTab.BillDiscountPercent = 0;
        ActiveTab.BillDiscountAmount = 0;
        ActiveTab.AmountReceived = 0;
        ActiveTab.RecalculateTotals();
        SearchQuery = string.Empty;
        SearchResults.Clear();
        HasSearchResults = false;
        CloseBatchPicker();
        SaveDraftState();
        SelectedSaleTypeIndex = 0;
        IsSaleTypePromptOpen = false;
        StatusMessage = $"Fresh sale ready ({ActiveTab.TabTitle}). Ready for billing.";
    }

    public void ClearBill()
    {
        _ = ClearBillAsync();
    }

    [RelayCommand]
    public void OpenPrintPrompt()
    {
        IsPrintPromptOpen = true;
    }

    [RelayCommand]
    public void ClosePrintPrompt()
    {
        IsPrintPromptOpen = false;
    }

    [RelayCommand]
    public void SkipPrint()
    {
        IsPrintPromptOpen = false;
        StatusMessage = $"Sale {LastCompletedInvoiceNo} saved. Fresh sale ready.";
    }

    [RelayCommand]
    public async Task PrintReceiptAsync()
    {
        IsPrintPromptOpen = false;
        StatusMessage = $"Receipt printed for {LastCompletedInvoiceNo} (₹{LastCompletedAmount:N2}). Ready for next sale.";
        await Task.CompletedTask;
    }

    [RelayCommand]
    public void HoldActiveBill()
    {
        if (ActiveTab == null || !ActiveTab.CartItems.Any())
        {
            StatusMessage = "No items in active bill to hold.";
            return;
        }

        var held = new HeldBillModel(
            HoldId: Ulid.NewUlid().ToString(),
            HoldNumber: $"H-{_holdCounter++:D3}",
            HeldAt: DateTime.UtcNow,
            CustomerName: string.IsNullOrWhiteSpace(ActiveTab.CustomerName) ? "WALK-IN CUSTOMER" : ActiveTab.CustomerName.Trim().ToUpperInvariant(),
            CustomerMobile: ActiveTab.CustomerMobile,
            DoctorName: string.IsNullOrWhiteSpace(ActiveTab.DoctorName) ? string.Empty : ActiveTab.DoctorName.Trim().ToUpperInvariant(),
            PaymentMode: ActiveTab.PaymentMode,
            IsInterstate: ActiveTab.IsInterstate,
            Items: ActiveTab.CartItems.ToList(),
            GrandTotal: ActiveTab.GrandTotal
        );

        HeldBills.Add(held);
        ActiveTab.CartItems.Clear();
        ActiveTab.CustomerName = "WALK-IN CUSTOMER";
        ActiveTab.CustomerMobile = string.Empty;
        ActiveTab.DoctorName = string.Empty;
        ActiveTab.InvoiceDate = DateTime.Today;
        ActiveTab.PaymentMode = PaymentMode.Cash;
        ActiveTab.BillDiscountPercent = 0;
        ActiveTab.BillDiscountAmount = 0;
        ActiveTab.RecalculateTotals();
        SaveDraftState();

        StatusMessage = $"Bill {held.HoldNumber} held for {held.CustomerName} ({held.Items.Count} items, ₹{held.GrandTotal:N2}). Press [F9] to recall.";
    }

    [RelayCommand]
    public void OpenRecallBillModal()
    {
        if (HeldBills.Count == 0)
        {
            StatusMessage = "No held bills to recall.";
            return;
        }

        SelectedHeldBill = HeldBills.FirstOrDefault();
        IsRecallBillModalOpen = true;
    }

    [RelayCommand]
    public void CloseRecallBillModal()
    {
        IsRecallBillModalOpen = false;
    }

    [RelayCommand]
    public void RestoreHeldBill(HeldBillModel? held)
    {
        var target = held ?? SelectedHeldBill;
        if (target == null || ActiveTab == null) return;

        ActiveTab.CustomerName = string.IsNullOrWhiteSpace(target.CustomerName) ? "WALK-IN CUSTOMER" : target.CustomerName.Trim().ToUpperInvariant();
        ActiveTab.CustomerMobile = target.CustomerMobile;
        ActiveTab.DoctorName = string.IsNullOrWhiteSpace(target.DoctorName) ? string.Empty : target.DoctorName.Trim().ToUpperInvariant();
        ActiveTab.PaymentMode = target.PaymentMode;
        ActiveTab.IsInterstate = target.IsInterstate;

        ActiveTab.CartItems.Clear();
        foreach (var item in target.Items)
        {
            ActiveTab.CartItems.Add(item);
        }

        HeldBills.Remove(target);
        ActiveTab.RecalculateTotals();
        IsRecallBillModalOpen = false;
        SaveDraftState();
        StatusMessage = $"Restored bill {target.HoldNumber} ({target.Items.Count} items, ₹{target.GrandTotal:N2}).";
    }

    [RelayCommand]
    public void DeleteHeldBill(HeldBillModel? held)
    {
        if (held != null)
        {
            HeldBills.Remove(held);
            StatusMessage = $"Deleted held bill {held.HoldNumber}.";
        }
    }

    [RelayCommand]
    public async Task FinalizeSaleAsync()
    {
        await FinalizeSaleInternalAsync(showPrintPrompt: false);
    }

    public async Task FinalizeSaleInternalAsync(bool showPrintPrompt = false)
    {
        if (ActiveTab == null || !ActiveTab.CartItems.Any())
        {
            StatusMessage = "Cannot pay an empty bill.";
            return;
        }

        var customerName = string.IsNullOrWhiteSpace(ActiveTab.CustomerName) ? "WALK-IN CUSTOMER" : ActiveTab.CustomerName.Trim().ToUpperInvariant();
        var grandTotal = ActiveTab.GrandTotal;
        var billDiscountPercent = ActiveTab.BillDiscountPercent;
        var billDiscountAmount = ActiveTab.BillDiscountAmount;
        var eligibleSubtotal = ActiveTab.CartItems.Where(i => i.DiscountPercent == 0).Sum(i => i.GrossAmount);

        var doctorFormatted = string.IsNullOrWhiteSpace(ActiveTab.DoctorName)
            ? null
            : (ActiveTab.DoctorName.Trim().StartsWith("DR.", StringComparison.OrdinalIgnoreCase)
                ? ActiveTab.DoctorName.Trim().ToUpperInvariant()
                : $"DR. {ActiveTab.DoctorName.Trim().ToUpperInvariant()}");

        var command = new CommitSaleCommand(
            OrgId: OrgId,
            BranchId: BranchId,
            CounterId: CounterName,
            WarehouseId: WarehouseId,
            UserId: CashierName,
            DeviceId: Environment.MachineName,
            CustomerId: string.IsNullOrWhiteSpace(ActiveTab.CustomerId) ? null : ActiveTab.CustomerId,
            CustomerName: customerName,
            IsInterstate: ActiveTab.IsInterstate,
            PrescriptionRef: doctorFormatted,
            Items: ActiveTab.CartItems.Select(c =>
            {
                decimal itemDiscPct = c.DiscountPercent;
                // If item has no item-level discount, apply the overall bill discount % or pro-rated amount %
                if (itemDiscPct == 0)
                {
                    if (billDiscountPercent > 0)
                    {
                        itemDiscPct = billDiscountPercent;
                    }
                    else if (billDiscountAmount > 0 && eligibleSubtotal > 0)
                    {
                        itemDiscPct = Math.Round((billDiscountAmount / eligibleSubtotal) * 100m, 2, MidpointRounding.AwayFromZero);
                    }
                }

                // If strips/tabs are specified, compute the effective bill quantity matching the unit price
                var billQty = (c.StripQuantity > 0 || c.TabQuantity > 0)
                    ? (c.StripQuantity + (c.TabsPerStrip > 0 ? (c.TabQuantity / (decimal)c.TabsPerStrip) : 0m))
                    : c.Quantity;

                return new CartItemInput(
                    c.ProductId,
                    c.ProductName,
                    c.BatchId,
                    c.BatchNumber,
                    c.ExpiryDate,
                    billQty,
                    c.Mrp > 0 ? c.Mrp : c.UnitPrice,
                    c.Mrp,
                    c.GstRatePercent,
                    itemDiscPct
                );
            }).ToList(),
            Payments: new List<SalePaymentInput>
            {
                new SalePaymentInput(ActiveTab.PaymentMode, grandTotal)
            }
        );

        var overstockItem = ActiveTab.CartItems.FirstOrDefault(c =>
        {
            var billQty = (c.StripQuantity > 0 || c.TabQuantity > 0)
                ? (c.StripQuantity + (c.TabsPerStrip > 0 ? (c.TabQuantity / (decimal)c.TabsPerStrip) : 0m))
                : c.Quantity;
            return c.AvailableQuantity > 0 && billQty > c.AvailableQuantity;
        });

        if (overstockItem != null)
        {
            var billQty = (overstockItem.StripQuantity > 0 || overstockItem.TabQuantity > 0)
                ? (overstockItem.StripQuantity + (overstockItem.TabsPerStrip > 0 ? (overstockItem.TabQuantity / (decimal)overstockItem.TabsPerStrip) : 0m))
                : overstockItem.Quantity;
            var msg = $"Cannot complete sale for '{overstockItem.ProductName}' (Batch {overstockItem.BatchNumber}).\n\nRequested: {billQty:0.##} units\nAvailable in Stock: {overstockItem.AvailableQuantity:0.##} units\n\nPlease reduce the quantity before completing the sale.";
            StatusMessage = $"⚠️ Insufficient stock for {overstockItem.ProductName}";
            ShowSaleError("Insufficient Stock", msg);
            return;
        }

        try
        {
            var result = await _posTransactionService.ProcessSaleAsync(command);

            if (result.IsSuccess)
            {
                LastCompletedInvoiceNo = result.InvoiceNo ?? $"INV-{DateTime.Now:yyMMddHHmmss}";
                LastCompletedAmount = result.TotalAmount > 0 ? result.TotalAmount : grandTotal;
                LastCompletedCustomer = customerName;

                StatusMessage = $"Sale Completed: {LastCompletedInvoiceNo} — ₹{LastCompletedAmount:N2}";
                ClearBill();
                if (showPrintPrompt) IsPrintPromptOpen = true;
            }
            else
            {
                StatusMessage = $"⚠️ Cannot save sale: {result.ErrorMessage}";
                ShowSaleError("Cannot Complete Sale", result.ErrorMessage ?? "Database rejected transaction.");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"⚠️ Error completing sale: {ex.Message}";
            ShowSaleError("Error Completing Sale", ex.Message);
        }
    }
}
