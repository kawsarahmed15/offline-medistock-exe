#pragma warning disable MVVMTK0045

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Products.Commands;
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
    public string? ManufacturerName { get; set; }
    public string? Barcode { get; set; }
    public string? BatchId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? NearestExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal SaleRate { get; set; }
    public decimal AvailableQuantity { get; set; }
    public List<ProductBatchDto> Batches { get; set; } = new();

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
            ManufacturerName = dto.ManufacturerName,
            Barcode = dto.Barcode,
            BatchId = dto.BatchId,
            BatchNumber = dto.BatchNumber,
            NearestExpiryDate = dto.NearestExpiryDate,
            Mrp = dto.Mrp,
            SaleRate = dto.SaleRate,
            AvailableQuantity = dto.AvailableQuantity,
            Batches = dto.Batches ?? new List<ProductBatchDto>()
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

    public decimal GrossAmount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public decimal DiscountAmount => Math.Round(GrossAmount * (DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal TaxableAmount => GrossAmount - DiscountAmount;
    public decimal GstAmount => Math.Round(TaxableAmount * (GstRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
    public decimal NetAmount => TaxableAmount + GstAmount;
}

public partial class InvoiceTabViewModel : ObservableObject
{
    public int TabNumber { get; }
    public string TabId { get; }
    public string TabTitle => $"Bill #{TabNumber}";

    [ObservableProperty]
    private string _customerName = "Walk-in Customer";

    [ObservableProperty]
    private string _customerMobile = string.Empty;

    [ObservableProperty]
    private string _doctorName = string.Empty;

    [ObservableProperty]
    private PaymentMode _paymentMode = PaymentMode.Cash;

    [ObservableProperty]
    private bool _isInterstate = false;

    [ObservableProperty]
    private int _selectedCartIndex = -1;

    public ObservableCollection<CartItemViewModel> CartItems { get; } = new();

    [ObservableProperty]
    private decimal _subtotal = 0;

    [ObservableProperty]
    private decimal _totalDiscount = 0;

    [ObservableProperty]
    private decimal _cgst = 0;

    [ObservableProperty]
    private decimal _sgst = 0;

    [ObservableProperty]
    private decimal _igst = 0;

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

    public InvoiceTabViewModel(int tabNumber)
    {
        TabNumber = tabNumber;
        TabId = Ulid.NewUlid().ToString();
    }

    public void RecalculateTotals()
    {
        Subtotal = CartItems.Sum(i => i.GrossAmount);
        TotalDiscount = CartItems.Sum(i => i.DiscountAmount);
        TotalItemsCount = CartItems.Count;
        TotalQuantity = CartItems.Sum(i => i.Quantity);

        var totalGst = CartItems.Sum(i => i.GstAmount);
        if (IsInterstate)
        {
            Igst = totalGst;
            Cgst = 0;
            Sgst = 0;
        }
        else
        {
            Igst = 0;
            Cgst = Math.Round(totalGst / 2m, 2, MidpointRounding.AwayFromZero);
            Sgst = totalGst - Cgst;
        }

        var rawTotal = (Subtotal - TotalDiscount) + totalGst;
        var rounded = Math.Round(rawTotal, 0, MidpointRounding.AwayFromZero);
        RoundOff = rounded - rawTotal;
        GrandTotal = rounded;

        if (AmountReceived < GrandTotal && PaymentMode == PaymentMode.Cash)
        {
            AmountReceived = GrandTotal;
        }
    }
}

public partial class PosViewModel : ObservableObject
{
    private readonly IProductSearchService _searchService;
    private readonly IPosTransactionService _posTransactionService;
    private readonly IProductService _productService;
    private CancellationTokenSource? _searchCts;
    private int _tabCounter = 1;

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

    [ObservableProperty]
    private int _activeTabIndex = 0;

    // Stage 1 Search Results
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
    private int _newPackUnits = 10;

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

    public PosViewModel(
        IProductSearchService searchService,
        IPosTransactionService posTransactionService,
        IProductService productService)
    {
        _searchService = searchService;
        _posTransactionService = posTransactionService;
        _productService = productService;

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

    // --- On-The-Fly Item Creation Modal (F2) ---
    [RelayCommand]
    public void OpenCreateProductModal()
    {
        NewProductName = !string.IsNullOrWhiteSpace(SearchQuery) ? SearchQuery.Trim() : string.Empty;
        NewBrandName = NewProductName;
        NewGenericName = string.Empty;
        NewComposition = string.Empty;
        NewDosageForm = DosageForm.Tablet;
        NewPackUnits = 10;
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

        IsCreateProductModalOpen = true;
        StatusMessage = "Creating new item. Press [Enter/F2] to Save and insert to bill, [Esc] to cancel.";
    }

    [RelayCommand]
    public void CloseCreateProductModal()
    {
        IsCreateProductModalOpen = false;
        StatusMessage = "Ready for billing.";
    }

    [RelayCommand]
    public async Task SaveCreateProductAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProductName))
        {
            StatusMessage = "Product Name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewBatchNumber))
        {
            StatusMessage = "Batch Number is required.";
            return;
        }

        if (NewMrp <= 0)
        {
            StatusMessage = "MRP must be greater than zero.";
            return;
        }

        var cmd = new CreateProductWithBatchCommand(
            OrgId: OrgId,
            WarehouseId: WarehouseId,
            Name: NewProductName.Trim(),
            BrandName: string.IsNullOrWhiteSpace(NewBrandName) ? NewProductName.Trim() : NewBrandName.Trim(),
            GenericName: NewGenericName.Trim(),
            Composition: NewComposition.Trim(),
            Strength: string.Empty,
            DosageForm: NewDosageForm,
            PackUnits: NewPackUnits > 0 ? NewPackUnits : 10,
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
            SaleRate: (decimal)(NewSaleRate > 0 ? NewSaleRate : NewMrp),
            OpeningQuantity: (decimal)NewOpeningQty
        );

        var result = await _productService.CreateProductWithBatchAsync(cmd);

        if (result.Success && result.ProductId != null && result.BatchId != null)
        {
            IsCreateProductModalOpen = false;

            // Automatically add newly created item to active tab cart
            if (ActiveTab != null)
            {
                var cartItem = new CartItemViewModel
                {
                    ProductId = result.ProductId,
                    ProductName = cmd.Name,
                    PackSizeDescription = $"{cmd.PackUnits} {cmd.BaseUnit}/Pack",
                    BatchId = result.BatchId,
                    BatchNumber = cmd.BatchNumber,
                    ExpiryDate = cmd.ExpiryDate,
                    UnitPrice = cmd.SaleRate,
                    Mrp = cmd.Mrp,
                    GstRatePercent = cmd.GstRatePercent,
                    IsColdChain = cmd.IsColdChain,
                    Schedule = cmd.Schedule,
                    Quantity = 1
                };

                ActiveTab.CartItems.Add(cartItem);
                ActiveTab.SelectedCartIndex = ActiveTab.CartItems.Count - 1;
                ActiveTab.RecalculateTotals();
            }

            SearchQuery = string.Empty;
            SearchResults.Clear();
            StatusMessage = $"Created & Added: {cmd.Name} (Batch {cmd.BatchNumber})";
        }
        else
        {
            StatusMessage = $"Error adding item: {result.ErrorMessage}";
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
            IsSearching = false;
            CloseBatchPicker();
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

    // --- Two-Stage Batch Selection Window ---
    public void OpenBatchPicker(ProductSearchItemViewModel product)
    {
        if (product == null) return;

        SelectedProductForBatches = product;
        SelectedProductBatches.Clear();

        if (product.Batches != null && product.Batches.Count > 0)
        {
            foreach (var b in product.Batches)
            {
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
                AvailableQuantity = product.AvailableQuantity
            });
        }

        if (SelectedProductBatches.Count > 1)
        {
            SelectedBatchIndex = 0;
            IsBatchPickerOpen = true;
            StatusMessage = $"Select Batch for {product.Name} [↑/↓ to choose, Enter to confirm]";
        }
        else if (SelectedProductBatches.Count == 1)
        {
            SelectBatch(SelectedProductBatches[0]);
        }
        else
        {
            StatusMessage = $"No available batches for {product.Name}";
        }
    }

    [RelayCommand]
    public void SelectBatch(ProductBatchDto? batch)
    {
        if (batch == null || SelectedProductForBatches == null || ActiveTab == null) return;

        var product = SelectedProductForBatches;
        var existing = ActiveTab.CartItems.FirstOrDefault(c => c.BatchId == batch.Id);
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
                BatchId = batch.Id,
                BatchNumber = batch.BatchNumber,
                ExpiryDate = batch.ExpiryDate,
                UnitPrice = batch.SaleRate > 0 ? batch.SaleRate : batch.Mrp,
                Mrp = batch.Mrp,
                GstRatePercent = product.GstRatePercent,
                IsColdChain = product.IsColdChain,
                Schedule = product.Schedule,
                Quantity = 1
            };

            ActiveTab.CartItems.Add(item);
            ActiveTab.SelectedCartIndex = ActiveTab.CartItems.Count - 1;
        }

        ActiveTab.RecalculateTotals();
        CloseBatchPicker();
        SearchQuery = string.Empty;
        SearchResults.Clear();
        StatusMessage = $"Added {product.Name} (Batch {batch.BatchNumber}) to {ActiveTab.TabTitle}.";
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
                PackSizeDescription = "Pack",
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
        SearchQuery = string.Empty;
        SearchResults.Clear();
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
    public void ClearBill()
    {
        if (ActiveTab == null) return;
        ActiveTab.CartItems.Clear();
        SearchQuery = string.Empty;
        SearchResults.Clear();
        CloseBatchPicker();
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
