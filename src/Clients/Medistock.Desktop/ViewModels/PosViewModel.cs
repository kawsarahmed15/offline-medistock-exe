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
    public string BatchId { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Mrp { get; set; }
    public decimal GstRatePercent { get; set; }
    public bool IsColdChain { get; set; }
    public DrugSchedule Schedule { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaxableAmount))]
    [NotifyPropertyChangedFor(nameof(GstAmount))]
    [NotifyPropertyChangedFor(nameof(NetAmount))]
    [NotifyPropertyChangedFor(nameof(QuantityDouble))]
    private decimal _quantity = 1;

    public double QuantityDouble
    {
        get => (double)Quantity;
        set => Quantity = (decimal)value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaxableAmount))]
    [NotifyPropertyChangedFor(nameof(GstAmount))]
    [NotifyPropertyChangedFor(nameof(NetAmount))]
    private decimal _discountPercent = 0;

    public decimal GrossAmount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public decimal DiscountAmount => Math.Round(GrossAmount * (DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal TaxableAmount => GrossAmount - DiscountAmount;
    public decimal GstAmount => Math.Round(TaxableAmount * (GstRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal NetAmount => TaxableAmount + GstAmount;
}

public partial class PosViewModel : ObservableObject
{
    private readonly IProductSearchService _searchService;
    private readonly IPosTransactionService _posTransactionService;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusMessage = "Ready for billing. Scan barcode or type [F3] to search.";

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
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _taxAmount;

    [ObservableProperty]
    private decimal _roundOff;

    [ObservableProperty]
    private decimal _grandTotal;

    [ObservableProperty]
    private decimal _amountReceived;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeAmount))]
    private decimal _cashPaymentAmount;

    public decimal ChangeAmount => Math.Max(0, AmountReceived - GrandTotal);

    [ObservableProperty]
    private int _selectedSearchIndex = -1;

    [ObservableProperty]
    private int _selectedCartIndex = -1;

    public ObservableCollection<ProductSearchItemViewModel> SearchResults { get; } = new();
    public ObservableCollection<CartItemViewModel> CartItems { get; } = new();
    public ObservableCollection<string> HeldBills { get; } = new();

    public PosViewModel(
        IProductSearchService searchService,
        IPosTransactionService posTransactionService)
    {
        _searchService = searchService;
        _posTransactionService = posTransactionService;
        CartItems.CollectionChanged += (_, _) => RecalculateBillTotals();
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
            await Task.Delay(50, token); // Fast 50ms debounce for sub-millisecond instant search

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
        if (CartItems.Count == 0) return;

        if (SelectedCartIndex < 0 || SelectedCartIndex >= CartItems.Count)
        {
            SelectedCartIndex = CartItems.Count - 1;
        }

        CartItems[SelectedCartIndex].Quantity += 1;
        RecalculateBillTotals();
    }

    public void DecreaseSelectedCartQuantity()
    {
        if (CartItems.Count == 0) return;

        if (SelectedCartIndex < 0 || SelectedCartIndex >= CartItems.Count)
        {
            SelectedCartIndex = CartItems.Count - 1;
        }

        if (CartItems[SelectedCartIndex].Quantity > 1)
        {
            CartItems[SelectedCartIndex].Quantity -= 1;
            RecalculateBillTotals();
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
        if (string.IsNullOrEmpty(product.BatchId))
        {
            StatusMessage = $"No active stock batch available for {product.Name}";
            return;
        }

        var existing = CartItems.FirstOrDefault(c => c.BatchId == product.BatchId);
        if (existing != null)
        {
            existing.Quantity += 1;
            SelectedCartIndex = CartItems.IndexOf(existing);
        }
        else
        {
            var item = new CartItemViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
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
            CartItems.Add(item);
            SelectedCartIndex = CartItems.Count - 1;
        }

        SearchQuery = string.Empty;
        SearchResults.Clear();
        SelectedSearchIndex = -1;
        StatusMessage = $"Added {product.Name} to cart.";
        RecalculateBillTotals();
    }

    private void AddToCartFromBarcode(BarcodeLookupDto product)
    {
        var existing = CartItems.FirstOrDefault(c => c.BatchId == product.BatchId);
        if (existing != null)
        {
            existing.Quantity += 1;
            SelectedCartIndex = CartItems.IndexOf(existing);
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
            CartItems.Add(item);
            SelectedCartIndex = CartItems.Count - 1;
        }

        StatusMessage = $"Scanned & Added: {product.ProductName}";
        RecalculateBillTotals();
    }

    [RelayCommand]
    public void RemoveCartItem(CartItemViewModel item)
    {
        if (item != null && CartItems.Contains(item))
        {
            CartItems.Remove(item);
            RecalculateBillTotals();
        }
    }

    [RelayCommand]
    public void ClearBill()
    {
        CartItems.Clear();
        SearchQuery = string.Empty;
        SearchResults.Clear();
        AmountReceived = 0;
        RecalculateBillTotals();
        StatusMessage = "New bill started.";
    }

    [RelayCommand]
    public async Task FinalizeSaleAsync()
    {
        if (!CartItems.Any())
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
            CustomerName: "Walk-in Customer",
            IsInterstate: false,
            PrescriptionRef: null,
            Items: CartItems.Select(c => new CartItemInput(
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
                new SalePaymentInput(PaymentMode.Cash, GrandTotal)
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

    public void RecalculateBillTotals()
    {
        Subtotal = CartItems.Sum(c => c.TaxableAmount);
        TaxAmount = CartItems.Sum(c => c.GstAmount);

        var rawTotal = Subtotal + TaxAmount;
        var rounded = Math.Round(rawTotal, 0, MidpointRounding.AwayFromZero);
        RoundOff = rounded - rawTotal;
        GrandTotal = rounded;
    }
}
