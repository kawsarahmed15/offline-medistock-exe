#pragma warning disable MVVMTK0045

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Products.Queries;
using Medistock.Application.Sales.Commands;
using Medistock.Domain.Common;

namespace Medistock.Desktop.ViewModels;

public class ProductSearchItemViewModel
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
    public string? Barcode { get; set; }
    public string? BatchId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? NearestExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal SaleRate { get; set; }
    public decimal AvailableQuantity { get; set; }

    public static ProductSearchItemViewModel FromDto(ProductSearchDto dto)
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
            Barcode = dto.Barcode,
            BatchId = dto.BatchId,
            BatchNumber = dto.BatchNumber,
            NearestExpiryDate = dto.NearestExpiryDate,
            Mrp = dto.Mrp,
            SaleRate = dto.SaleRate,
            AvailableQuantity = dto.AvailableQuantity
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
    public decimal UnitPrice { get; set; }
    public decimal Mrp { get; set; }
    public decimal GstRatePercent { get; set; }
    public bool IsColdChain { get; set; }
    public DrugSchedule Schedule { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GrossAmount))]
    [NotifyPropertyChangedFor(nameof(TaxableAmount))]
    [NotifyPropertyChangedFor(nameof(DiscountAmount))]
    [NotifyPropertyChangedFor(nameof(GstAmount))]
    [NotifyPropertyChangedFor(nameof(NetAmount))]
    [NotifyPropertyChangedFor(nameof(QuantityDouble))]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _freeQuantity = 0;

    public double QuantityDouble
    {
        get => (double)Quantity;
        set
        {
            if (value >= 1)
            {
                Quantity = (decimal)value;
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaxableAmount))]
    [NotifyPropertyChangedFor(nameof(DiscountAmount))]
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

    public decimal GrossAmount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public decimal DiscountAmount => Math.Round(GrossAmount * (DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal TaxableAmount => GrossAmount - DiscountAmount;
    public decimal GstAmount => Math.Round(TaxableAmount * (GstRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal NetAmount => TaxableAmount + GstAmount;
}

public partial class InvoiceTabViewModel : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private int _tabNumber = 1;

    [ObservableProperty]
    private string _invoiceNo = "INV-DRAFT";

    [ObservableProperty]
    private DateTime _invoiceDate = DateTime.UtcNow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private string _customerName = "Walk-in Customer";

    [ObservableProperty]
    private string _doctorName = string.Empty;

    [ObservableProperty]
    private string _prescriptionRef = string.Empty;

    [ObservableProperty]
    private PaymentMode _paymentMode = PaymentMode.Cash;

    [ObservableProperty]
    private bool _isInterstate = false;

    [ObservableProperty]
    private int _selectedCartIndex = -1;

    public ObservableCollection<CartItemViewModel> CartItems { get; } = new();

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private decimal _taxAmount;

    [ObservableProperty]
    private decimal _roundOff;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    [NotifyPropertyChangedFor(nameof(ChangeAmount))]
    private decimal _grandTotal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeAmount))]
    private decimal _amountReceived;

    public decimal ChangeAmount => Math.Max(0, AmountReceived - GrandTotal);

    public int TotalItemsCount => CartItems.Count;
    public decimal TotalQuantity => CartItems.Sum(c => c.Quantity + c.FreeQuantity);

    public string TabTitle => $"Bill #{TabNumber} (₹{GrandTotal:N0})";

    public InvoiceTabViewModel(int tabNumber)
    {
        TabNumber = tabNumber;
        CartItems.CollectionChanged += (_, _) => RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        Subtotal = CartItems.Sum(c => c.TaxableAmount);
        DiscountAmount = CartItems.Sum(c => c.DiscountAmount);
        TaxAmount = CartItems.Sum(c => c.GstAmount);

        var rawTotal = Subtotal + TaxAmount;
        var rounded = Math.Round(rawTotal, 0, MidpointRounding.AwayFromZero);
        RoundOff = rounded - rawTotal;
        GrandTotal = rounded;

        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(TotalItemsCount));
        OnPropertyChanged(nameof(TotalQuantity));
    }
}

public partial class PosViewModel : ObservableObject
{
    private readonly IProductSearchService _searchService;
    private readonly IPosTransactionService _posTransactionService;
    private CancellationTokenSource? _searchCts;
    private int _tabCounter = 1;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusMessage = "Ready for billing. Scan barcode or type [F3] to search. [F1] for all shortcuts.";

    [ObservableProperty]
    private string _counterName = "Counter 1";

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

    public ObservableCollection<InvoiceTabViewModel> InvoiceTabs { get; } = new();

    [ObservableProperty]
    private InvoiceTabViewModel? _activeTab;

    [ObservableProperty]
    private int _activeTabIndex = 0;

    public ObservableCollection<ProductSearchItemViewModel> SearchResults { get; } = new();

    public PosViewModel(
        IProductSearchService searchService,
        IPosTransactionService posTransactionService)
    {
        _searchService = searchService;
        _posTransactionService = posTransactionService;

        // Initialize with default Bill #1 tab
        var initialTab = new InvoiceTabViewModel(_tabCounter++);
        InvoiceTabs.Add(initialTab);
        ActiveTab = initialTab;
        ActiveTabIndex = 0;
    }

    partial void OnActiveTabIndexChanged(int value)
    {
        if (value >= 0 && value < InvoiceTabs.Count)
        {
            ActiveTab = InvoiceTabs[value];
        }
    }

    [RelayCommand]
    public void AddNewTab()
    {
        var newTab = new InvoiceTabViewModel(_tabCounter++);
        InvoiceTabs.Add(newTab);
        ActiveTab = newTab;
        ActiveTabIndex = InvoiceTabs.Count - 1;
        StatusMessage = $"Opened {newTab.TabTitle}. Press [Ctrl+Tab] or click tabs to switch.";
    }

    [RelayCommand]
    public void CloseTab(InvoiceTabViewModel? tab)
    {
        var targetTab = tab ?? ActiveTab;
        if (targetTab == null) return;

        if (InvoiceTabs.Count <= 1)
        {
            // If only 1 tab, clear it rather than removing
            targetTab.CartItems.Clear();
            targetTab.CustomerName = "Walk-in Customer";
            targetTab.DoctorName = string.Empty;
            targetTab.RecalculateTotals();
            StatusMessage = "Cleared active bill.";
            return;
        }

        var idx = InvoiceTabs.IndexOf(targetTab);
        InvoiceTabs.Remove(targetTab);

        if (ActiveTab == targetTab)
        {
            var nextIdx = Math.Min(idx, InvoiceTabs.Count - 1);
            ActiveTabIndex = nextIdx;
            ActiveTab = InvoiceTabs[nextIdx];
        }

        StatusMessage = "Closed invoice tab.";
    }

    [RelayCommand]
    public void NextTab()
    {
        if (InvoiceTabs.Count <= 1) return;
        ActiveTabIndex = (ActiveTabIndex + 1) % InvoiceTabs.Count;
        ActiveTab = InvoiceTabs[ActiveTabIndex];
        StatusMessage = $"Switched to {ActiveTab.TabTitle}";
    }

    [RelayCommand]
    public void PreviousTab()
    {
        if (InvoiceTabs.Count <= 1) return;
        ActiveTabIndex = (ActiveTabIndex - 1 + InvoiceTabs.Count) % InvoiceTabs.Count;
        ActiveTab = InvoiceTabs[ActiveTabIndex];
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

    async partial void OnSearchQueryChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            SelectedSearchIndex = -1;
            IsSearching = false;
            return;
        }

        try
        {
            IsSearching = true;
            await Task.Delay(50, token); // Instant 50ms prefix debounce

            var results = await _searchService.SearchAsync(value, WarehouseId, 25, token);

            if (!token.IsCancellationRequested)
            {
                SearchResults.Clear();
                foreach (var r in results)
                {
                    SearchResults.Add(ProductSearchItemViewModel.FromDto(r));
                }

                SelectedSearchIndex = SearchResults.Count > 0 ? 0 : -1;
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
        if (product == null || ActiveTab == null) return;
        if (string.IsNullOrEmpty(product.BatchId))
        {
            StatusMessage = $"No active stock batch available for {product.Name}";
            return;
        }

        var existing = ActiveTab.CartItems.FirstOrDefault(c => c.BatchId == product.BatchId);
        if (existing != null)
        {
            existing.Quantity += 1;
            ActiveTab.SelectedCartIndex = ActiveTab.CartItems.IndexOf(existing);
        }
        else
        {
            var item = new CartItemViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                PackSizeDescription = product.PackSizeDescription,
                BatchId = product.BatchId,
                BatchNumber = product.BatchNumber ?? "DEFAULT",
                ExpiryDate = product.NearestExpiryDate ?? DateTime.UtcNow.AddDays(365),
                UnitPrice = product.SaleRate > 0 ? product.SaleRate : product.Mrp,
                Mrp = product.Mrp,
                GstRatePercent = product.GstRatePercent,
                IsColdChain = product.IsColdChain,
                Schedule = product.Schedule,
                Quantity = 1
            };
            ActiveTab.CartItems.Add(item);
            ActiveTab.SelectedCartIndex = ActiveTab.CartItems.Count - 1;
        }

        SearchQuery = string.Empty;
        SearchResults.Clear();
        SelectedSearchIndex = -1;
        ActiveTab.RecalculateTotals();
        StatusMessage = $"Added {product.Name} (Batch: {product.BatchNumber}) to {ActiveTab.TabTitle}.";
    }

    private void AddToCartFromBarcode(BarcodeLookupDto product)
    {
        if (ActiveTab == null) return;
        var existing = ActiveTab.CartItems.FirstOrDefault(c => c.BatchId == product.BatchId);
        if (existing != null)
        {
            existing.Quantity += 1;
            ActiveTab.SelectedCartIndex = ActiveTab.CartItems.IndexOf(existing);
        }
        else
        {
            var item = new CartItemViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                BatchId = product.BatchId,
                BatchNumber = product.BatchNumber,
                ExpiryDate = product.ExpiryDate,
                UnitPrice = product.SaleRate > 0 ? product.SaleRate : product.Mrp,
                Mrp = product.Mrp,
                GstRatePercent = product.GstRatePercent,
                IsColdChain = product.IsColdChain,
                Schedule = product.Schedule,
                Quantity = 1
            };
            ActiveTab.CartItems.Add(item);
            ActiveTab.SelectedCartIndex = ActiveTab.CartItems.Count - 1;
        }

        ActiveTab.RecalculateTotals();
        StatusMessage = $"Scanned & Added: {product.ProductName}";
    }

    [RelayCommand]
    public void RemoveCartItem(CartItemViewModel item)
    {
        if (ActiveTab != null && item != null && ActiveTab.CartItems.Contains(item))
        {
            ActiveTab.CartItems.Remove(item);
            ActiveTab.RecalculateTotals();
        }
    }

    [RelayCommand]
    public void ClearBill()
    {
        if (ActiveTab == null) return;
        ActiveTab.CartItems.Clear();
        SearchQuery = string.Empty;
        SearchResults.Clear();
        ActiveTab.AmountReceived = 0;
        ActiveTab.RecalculateTotals();
        StatusMessage = "Active bill cleared.";
    }

    [RelayCommand]
    public async Task FinalizeSaleAsync()
    {
        if (ActiveTab == null || !ActiveTab.CartItems.Any())
        {
            StatusMessage = "Cannot pay an empty bill.";
            return;
        }

        var command = new CommitSaleCommand(
            OrgId: OrgId,
            BranchId: BranchId,
            CounterId: CounterName,
            WarehouseId: WarehouseId,
            UserId: CashierName,
            DeviceId: Environment.MachineName,
            CustomerId: null,
            CustomerName: string.IsNullOrWhiteSpace(ActiveTab.CustomerName) ? "Walk-in Customer" : ActiveTab.CustomerName,
            IsInterstate: ActiveTab.IsInterstate,
            PrescriptionRef: string.IsNullOrWhiteSpace(ActiveTab.DoctorName) ? null : $"Dr. {ActiveTab.DoctorName}",
            Items: ActiveTab.CartItems.Select(c => new CartItemInput(
                c.ProductId,
                c.ProductName,
                c.BatchId,
                c.BatchNumber,
                c.ExpiryDate,
                c.Quantity,
                c.UnitPrice,
                c.Mrp,
                c.GstRatePercent,
                c.DiscountPercent
            )).ToList(),
            Payments: new List<SalePaymentInput>
            {
                new SalePaymentInput(ActiveTab.PaymentMode, ActiveTab.GrandTotal)
            }
        );

        var result = await _posTransactionService.ProcessSaleAsync(command);

        if (result.IsSuccess)
        {
            StatusMessage = $"Sale Completed: {result.InvoiceNo} — ₹{result.TotalAmount:N2}";
            ClearBill();
        }
        else
        {
            StatusMessage = $"Sale Error: {result.ErrorMessage}";
        }
    }
}

