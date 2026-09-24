using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Purchases.DTOs;
using Medistock.Application.Purchases.Services;

namespace Medistock.Desktop.ViewModels;

public partial class PurchaseItemRowViewModel : ObservableObject
{
#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private string _productId = string.Empty;

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _hsnCode = "30049099";

    [ObservableProperty]
    private string _batchNumber = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _expiryDate = DateTimeOffset.UtcNow.AddMonths(18);

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
    private decimal _gstRatePercent = 12.0m;
#pragma warning restore MVVMTK0045

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
        set { Mrp = value; OnPropertyChanged(); Recalculate(); }
    }

    public decimal DiscountPctDouble
    {
        get => DiscountPct;
        set { DiscountPct = value; OnPropertyChanged(); Recalculate(); }
    }

    public decimal TaxableAmount => Math.Round((Quantity * UnitPrice) * (1 - (DiscountPct / 100m)), 2);
    public decimal GstAmount => Math.Round(TaxableAmount * (GstRatePercent / 100m), 2);
    public decimal NetAmount => TaxableAmount + GstAmount;

    public void Recalculate()
    {
        OnPropertyChanged(nameof(TaxableAmount));
        OnPropertyChanged(nameof(GstAmount));
        OnPropertyChanged(nameof(NetAmount));
    }
}

public partial class PurchaseEntryViewModel : ObservableObject
{
    private readonly IPurchaseService _purchaseService;

#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private string _selectedSupplierId = string.Empty;

    [ObservableProperty]
    private string _selectedSupplierName = "Apex Pharma Wholesalers";

    [ObservableProperty]
    private string _supplierGstin = "29AABCU9603R1ZM";

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

    [ObservableProperty]
    private decimal _taxableSubtotal;

    [ObservableProperty]
    private decimal _cgstTotal;

    [ObservableProperty]
    private decimal _sgstTotal;

    [ObservableProperty]
    private decimal _igstTotal;

    [ObservableProperty]
    private decimal _roundOff;

    [ObservableProperty]
    private decimal _grandTotal;
#pragma warning restore MVVMTK0045

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<PurchaseItemRowViewModel> LineItems { get; } = new();

    public PurchaseEntryViewModel(IPurchaseService purchaseService)
    {
        _purchaseService = purchaseService;
        AddNewRow();
    }

    [RelayCommand]
    public async Task LoadSuppliersAsync()
    {
        var list = await _purchaseService.GetSuppliersAsync("org-1");
        Suppliers.Clear();
        foreach (var s in list)
        {
            Suppliers.Add(s);
        }

        if (Suppliers.Count > 0 && string.IsNullOrEmpty(SelectedSupplierId))
        {
            SelectedSupplierId = Suppliers[0].Id;
            SelectedSupplierName = Suppliers[0].Name;
            SupplierGstin = Suppliers[0].Gstin ?? "";
        }
    }

    [RelayCommand]
    public void AddNewRow()
    {
        var row = new PurchaseItemRowViewModel
        {
            ProductId = "p_dolo",
            ProductName = "Dolo 650mg Tablet",
            BatchNumber = "DL" + DateTime.UtcNow.ToString("yyMM"),
            UnitPrice = 22.00m,
            Mrp = 30.50m,
            SaleRate = 30.00m,
            Quantity = 50,
            GstRatePercent = 12.0m
        };
        row.PropertyChanged += (s, e) => RecalculateTotals();
        LineItems.Add(row);
        RecalculateTotals();
    }

    [RelayCommand]
    public void RemoveRow(PurchaseItemRowViewModel row)
    {
        if (LineItems.Count > 1)
        {
            LineItems.Remove(row);
            RecalculateTotals();
        }
    }

    public void RecalculateTotals()
    {
        TaxableSubtotal = LineItems.Sum(i => i.TaxableAmount);

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
            StatusMessage = "Please enter Supplier Invoice Number.";
            return;
        }

        if (LineItems.Count == 0)
        {
            StatusMessage = "Add at least one line item.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Posting purchase invoice & updating stock...";

        try
        {
            var command = new CreatePurchaseInvoiceCommand(
                OrgId: "org-1",
                BranchId: "br-1",
                WarehouseId: "wh-1",
                SupplierId: string.IsNullOrEmpty(SelectedSupplierId) ? "sup-apex" : SelectedSupplierId,
                SupplierName: SelectedSupplierName,
                SupplierGstin: SupplierGstin,
                SupplierInvoiceNo: SupplierInvoiceNo,
                SupplierInvoiceDate: SupplierInvoiceDate.UtcDateTime,
                IsInterstate: IsInterstate,
                CreatedByUserId: "USER-STOREKEEPER",
                Notes: Notes,
                Items: LineItems.Select(i => new PurchaseInvoiceItemInputDto(
                    ProductId: i.ProductId,
                    ProductName: i.ProductName,
                    HsnCode: i.HsnCode,
                    BatchNumber: i.BatchNumber,
                    ExpiryDate: i.ExpiryDate.UtcDateTime,
                    ManufacturingDate: null,
                    Quantity: i.Quantity,
                    FreeQuantity: i.FreeQuantity,
                    UnitPrice: i.UnitPrice,
                    Mrp: i.Mrp,
                    SaleRate: i.SaleRate,
                    DiscountPct: i.DiscountPct,
                    GstRatePercent: i.GstRatePercent
                )).ToList()
            );

            var result = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(command);
            if (result.Success)
            {
                StatusMessage = $"✓ Invoice {SupplierInvoiceNo} posted successfully! Added {result.TotalStockAdded} units across {result.BatchesCreatedOrUpdated} batches.";
                LineItems.Clear();
                AddNewRow();
                SupplierInvoiceNo = string.Empty;
            }
            else
            {
                StatusMessage = $"Error: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
