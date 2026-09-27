#pragma warning disable MVVMTK0045

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.B2B.Services;
using Medistock.Contracts.B2B;
using Medistock.Domain.B2B;

namespace Medistock.Desktop.ViewModels;

public partial class B2bCartItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _catalogId = string.Empty;

    [ObservableProperty]
    private string _productCode = string.Empty;

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _wholesalerName = string.Empty;

    [ObservableProperty]
    private double _quantity = 1;

    [ObservableProperty]
    private int _freeQuantity = 0;

    [ObservableProperty]
    private decimal _unitRate;

    [ObservableProperty]
    private decimal _gstRate;

    public decimal TaxableAmount => (decimal)Quantity * UnitRate;
    public decimal TaxAmount => Math.Round(TaxableAmount * (GstRate / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal TotalAmount => TaxableAmount + TaxAmount;

    public string FormattedTotal => $"₹{TotalAmount:N2}";
    public string FormattedRate => $"₹{UnitRate:N2}";
}

public partial class B2bCommerceViewModel : ObservableObject
{
    private readonly IB2bCommerceService _b2bService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private Wholesaler? _selectedWholesaler;

    [ObservableProperty]
    private WholesalerCatalogItemDto? _selectedCatalogItem;

    [ObservableProperty]
    private B2bOrderSummaryDto? _selectedOrder;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    // KPI Counters
    [ObservableProperty]
    private int _totalOrdersCount;

    [ObservableProperty]
    private int _pendingDeliveryCount;

    [ObservableProperty]
    private decimal _totalOrderSpend;

    [ObservableProperty]
    private int _activeWholesalersCount;

    // PO Cart Totals
    [ObservableProperty]
    private decimal _cartSubtotal;

    [ObservableProperty]
    private decimal _cartTax;

    [ObservableProperty]
    private decimal _cartGrandTotal;

    [ObservableProperty]
    private int _cartTotalItems;

    [ObservableProperty]
    private string _deliveryAddress = "Main Pharmacy Store, Counter 1, Mumbai";

    [ObservableProperty]
    private string _orderNotes = "Please dispatch standard FEFO batches.";

    public ObservableCollection<Wholesaler> Wholesalers { get; } = new();
    public ObservableCollection<WholesalerCatalogItemDto> CatalogItems { get; } = new();
    public ObservableCollection<B2bOrderSummaryDto> PurchaseOrders { get; } = new();
    public ObservableCollection<B2bCartItemViewModel> CartItems { get; } = new();

    public B2bCommerceViewModel(IB2bCommerceService b2bService)
    {
        _b2bService = b2bService;
    }

    public async Task InitializeAsync()
    {
        await LoadWholesalersAsync();
        await RefreshCatalogAsync();
        await LoadOrdersAsync();
    }

    [RelayCommand]
    public async Task LoadWholesalersAsync()
    {
        try
        {
            var list = await _b2bService.GetWholesalersAsync();
            Wholesalers.Clear();
            foreach (var w in list) Wholesalers.Add(w);
            ActiveWholesalersCount = Wholesalers.Count;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading wholesalers: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RefreshCatalogAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _b2bService.SearchCatalogAsync(
                SelectedWholesaler?.Id,
                string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery);

            CatalogItems.Clear();
            foreach (var item in items) CatalogItems.Add(item);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error fetching catalog: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadOrdersAsync()
    {
        try
        {
            var orders = await _b2bService.GetOrdersAsync("ORG-001", "BR-MAIN");
            PurchaseOrders.Clear();
            foreach (var o in orders) PurchaseOrders.Add(o);

            TotalOrdersCount = PurchaseOrders.Count;
            PendingDeliveryCount = PurchaseOrders.Count(o => o.Status == "Submitted" || o.Status == "Confirmed" || o.Status == "Dispatched");
            TotalOrderSpend = PurchaseOrders.Sum(o => o.TotalAmount);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading orders: {ex.Message}";
        }
    }

    [RelayCommand]
    public void AddToCart(WholesalerCatalogItemDto? item)
    {
        if (item == null) return;

        var existing = CartItems.FirstOrDefault(c => c.CatalogId == item.CatalogId);
        if (existing != null)
        {
            existing.Quantity += 1;
            RecalculateCart();
            return;
        }

        int freeQty = 0;
        if (item.FreeRatioBuy > 0 && item.FreeRatioGet > 0)
        {
            freeQty = ((int)item.MinimumOrderQuantity / item.FreeRatioBuy) * item.FreeRatioGet;
        }

        var cartItem = new B2bCartItemViewModel
        {
            CatalogId = item.CatalogId,
            ProductCode = item.ProductCode,
            ProductName = item.BrandName,
            WholesalerName = item.WholesalerName,
            Quantity = item.MinimumOrderQuantity > 0 ? item.MinimumOrderQuantity : 1,
            FreeQuantity = freeQty,
            UnitRate = item.WholesaleRate,
            GstRate = item.GstRate
        };

        CartItems.Add(cartItem);
        RecalculateCart();
        StatusMessage = $"Added {item.BrandName} to PO Cart";
    }

    [RelayCommand]
    public void RemoveFromCart(B2bCartItemViewModel? item)
    {
        if (item != null)
        {
            CartItems.Remove(item);
            RecalculateCart();
        }
    }

    [RelayCommand]
    public void ClearCart()
    {
        CartItems.Clear();
        RecalculateCart();
    }

    public void RecalculateCart()
    {
        decimal subtotal = 0;
        decimal tax = 0;
        int count = 0;

        foreach (var item in CartItems)
        {
            subtotal += item.TaxableAmount;
            tax += item.TaxAmount;
            count += (int)item.Quantity + item.FreeQuantity;
        }

        CartSubtotal = subtotal;
        CartTax = tax;
        CartGrandTotal = subtotal + tax;
        CartTotalItems = count;
    }

    [RelayCommand]
    public async Task SubmitPurchaseOrderAsync()
    {
        if (CartItems.Count == 0)
        {
            StatusMessage = "Cart is empty. Please add items to place an order.";
            return;
        }

        var firstItem = CartItems.First();
        var catalogItem = CatalogItems.FirstOrDefault(c => c.CatalogId == firstItem.CatalogId);
        var wholesalerId = catalogItem?.WholesalerId ?? Wholesalers.FirstOrDefault()?.Id ?? "w_apex";

        IsLoading = true;
        try
        {
            var req = new CreateB2bOrderRequest
            {
                WholesalerId = wholesalerId,
                DeliveryAddress = DeliveryAddress,
                Notes = OrderNotes,
                Items = CartItems.Select(c => new CreateB2bOrderItemDto
                {
                    CatalogId = c.CatalogId,
                    ProductCode = c.ProductCode,
                    ProductName = c.ProductName,
                    OrderQuantity = (int)c.Quantity,
                    UnitWholesaleRate = c.UnitRate,
                    GstRate = c.GstRate,
                    FreeQuantity = c.FreeQuantity
                }).ToList()
            };

            var summary = await _b2bService.CreateAndSubmitOrderAsync(
                req,
                "ORG-001",
                "BR-MAIN",
                "USR-001",
                "DESKTOP-POS-01");

            CartItems.Clear();
            RecalculateCart();
            await LoadOrdersAsync();
            SelectedTabIndex = 1; // Switch to Orders tab
            StatusMessage = $"Order #{summary.OrderNumber} submitted successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to submit order: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ReceiveAndStockOrderAsync(B2bOrderSummaryDto? order)
    {
        if (order == null) return;

        IsLoading = true;
        try
        {
            var result = await _b2bService.ReceiveOrderAndConvertToPurchaseInvoiceAsync(
                order.OrderId,
                "WH-MAIN",
                "USR-001",
                "DESKTOP-POS-01");

            if (result.Success)
            {
                StatusMessage = $"Order #{order.OrderNumber} received & converted to Purchase Invoice #{result.PurchaseInvoiceId}! Inventory restocked.";
                await LoadOrdersAsync();
            }
            else
            {
                StatusMessage = $"Receive failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error processing delivery: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
