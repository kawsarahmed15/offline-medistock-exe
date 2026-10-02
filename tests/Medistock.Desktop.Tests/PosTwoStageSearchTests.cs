using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Products.Commands;
using Medistock.Application.Products.Queries;
using Medistock.Application.Sales.Commands;
using Medistock.Desktop.ViewModels;
using Medistock.Domain.Common;
using Xunit;

namespace Medistock.Desktop.Tests;

public class FakeProductSearchService : IProductSearchService
{
    public List<ProductSearchDto> StubbedResults { get; set; } = new();

    public Task<IReadOnlyList<ProductSearchDto>> SearchAsync(string query, string warehouseId, int limit = 20, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProductSearchDto>>(StubbedResults);
    }

    public Task<BarcodeLookupDto?> ScanBarcodeAsync(string barcode, string warehouseId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<BarcodeLookupDto?>(null);
    }
}

public class FakePosTransactionService : IPosTransactionService
{
    public CommitSaleCommand? LastCommand { get; private set; }

    public Task<CommitSaleResult> ProcessSaleAsync(CommitSaleCommand command, CancellationToken cancellationToken = default)
    {
        LastCommand = command;
        return Task.FromResult(new CommitSaleResult(true, "sale-1", "INV-1001", 100, 100, DateTime.UtcNow, null));
    }
}

public class FakeProductService : IProductService
{
    public CreateProductWithBatchCommand? LastCommand { get; private set; }

    public Task<CreateProductResult> CreateProductWithBatchAsync(CreateProductWithBatchCommand command, CancellationToken cancellationToken = default)
    {
        LastCommand = command;
        return Task.FromResult(new CreateProductResult(true, "prod-new-1", "batch-new-1", null));
    }
}

public class PosTwoStageSearchTests
{
    private readonly FakeProductSearchService _fakeSearch = new();
    private readonly FakePosTransactionService _fakePos = new();
    private readonly FakeProductService _fakeProduct = new();

    private PosViewModel CreateVm(string? draftPath = null)
    {
        var path = draftPath ?? Path.Combine(Path.GetTempPath(), $"pos_draft_{Guid.NewGuid():N}.json");
        return new PosViewModel(_fakeSearch, _fakePos, _fakeProduct, path);
    }

    [Fact]
    public void PrepareLineItem_WithMultipleBatches_OpensPickerWindow()
    {
        var vm = CreateVm();

        var product = new ProductSearchItemViewModel
        {
            Id = "p1",
            Name = "Augmentin 625",
            PackSizeDescription = "10 TAB/Pack",
            GstRatePercent = 12,
            Batches = new List<ProductBatchDto>
            {
                new() { Id = "b1", BatchNumber = "AUG-1", ExpiryDate = DateTime.UtcNow.AddMonths(6), Mrp = 200, SaleRate = 180, AvailableQuantity = 20 },
                new() { Id = "b2", BatchNumber = "AUG-2", ExpiryDate = DateTime.UtcNow.AddMonths(12), Mrp = 210, SaleRate = 190, AvailableQuantity = 30 }
            }
        };

        var skipped = vm.PrepareLineItem(product);

        Assert.False(skipped);
        Assert.True(vm.IsBatchPickerOpen);
        Assert.Equal(2, vm.SelectedProductBatches.Count);
        Assert.Equal(0, vm.SelectedBatchIndex);

        // Select the second batch
        vm.SelectBatch(vm.SelectedProductBatches[1]);

        Assert.False(vm.IsBatchPickerOpen);
        Assert.NotNull(vm.PendingSelectedItem);
        Assert.Equal("AUG-2", vm.PendingSelectedBatch?.BatchNumber);

        // Enter quantity and commit line
        vm.PendingQuantity = 3;
        vm.CommitCurrentLine();

        Assert.NotNull(vm.ActiveTab);
        Assert.Single(vm.ActiveTab.CartItems);
        Assert.Equal("AUG-2", vm.ActiveTab.CartItems[0].BatchNumber);
        Assert.Equal(210m, vm.ActiveTab.CartItems[0].UnitPrice);
        Assert.Equal(3m, vm.ActiveTab.CartItems[0].StripQuantity);
        Assert.Equal(30m, vm.ActiveTab.CartItems[0].Quantity);
    }

    [Fact]
    public void PrepareLineItem_WithSingleBatch_SkipsBatchPickerAndDirectlyPreparesQuantity()
    {
        var vm = CreateVm();

        var product = new ProductSearchItemViewModel
        {
            Id = "p2",
            Name = "Dolo 650",
            PackSizeDescription = "15 TAB/Pack",
            BatchId = "b10",
            BatchNumber = "DL-100",
            Mrp = 30,
            SaleRate = 28,
            GstRatePercent = 12,
            Batches = new List<ProductBatchDto>
            {
                new() { Id = "b10", BatchNumber = "DL-100", ExpiryDate = DateTime.UtcNow.AddMonths(12), Mrp = 30, SaleRate = 28, AvailableQuantity = 100 }
            }
        };

        // MARG ERP Auto-select & Skip rule
        var skipped = vm.PrepareLineItem(product);

        Assert.True(skipped); // true indicates batch picker was auto-skipped
        Assert.False(vm.IsBatchPickerOpen);
        Assert.NotNull(vm.PendingSelectedItem);
        Assert.Equal("DL-100", vm.PendingSelectedBatch?.BatchNumber);

        vm.PendingQuantity = 2;
        vm.CommitCurrentLine();

        Assert.NotNull(vm.ActiveTab);
        Assert.Single(vm.ActiveTab.CartItems);
        Assert.Equal("DL-100", vm.ActiveTab.CartItems[0].BatchNumber);
        Assert.Equal(2m, vm.ActiveTab.CartItems[0].StripQuantity);
        Assert.Equal(30m, vm.ActiveTab.CartItems[0].Quantity);
    }

    [Fact]
    public void InsertSpliceMode_InsertsItemBeforeSelectedRow()
    {
        var vm = CreateVm();
        Assert.NotNull(vm.ActiveTab);

        // Add Item 1 (Row 0)
        vm.ActiveTab.CartItems.Add(new CartItemViewModel { ProductId = "p1", ProductName = "Item A", BatchId = "b1", BatchNumber = "B1", UnitPrice = 10, Quantity = 1 });
        // Add Item 2 (Row 1)
        vm.ActiveTab.CartItems.Add(new CartItemViewModel { ProductId = "p2", ProductName = "Item B", BatchNumber = "B2", BatchId = "b2", UnitPrice = 20, Quantity = 1 });
        // Add Item 3 (Row 2)
        vm.ActiveTab.CartItems.Add(new CartItemViewModel { ProductId = "p3", ProductName = "Item C", BatchNumber = "B3", BatchId = "b3", UnitPrice = 30, Quantity = 1 });

        // Select Row 1 (Item B) and press Insert (set insert splice mode)
        vm.ActiveTab.SelectedCartIndex = 1;
        vm.SetInsertSpliceMode();
        Assert.Equal(1, vm.InsertSpliceIndex);

        // Prepare and commit new Item D
        var itemD = new ProductSearchItemViewModel
        {
            Id = "p4",
            Name = "Item D",
            BatchId = "b4",
            BatchNumber = "B4",
            Mrp = 40,
            SaleRate = 40,
            Batches = new List<ProductBatchDto>
            {
                new() { Id = "b4", BatchNumber = "B4", ExpiryDate = DateTime.UtcNow.AddYears(1), Mrp = 40, SaleRate = 40, AvailableQuantity = 50 }
            }
        };

        vm.PrepareLineItem(itemD);
        vm.CommitCurrentLine();

        Assert.Equal(4, vm.ActiveTab.CartItems.Count);
        Assert.Equal("Item A", vm.ActiveTab.CartItems[0].ProductName);
        Assert.Equal("Item D", vm.ActiveTab.CartItems[1].ProductName); // Spliced before Item B!
        Assert.Equal("Item B", vm.ActiveTab.CartItems[2].ProductName);
        Assert.Equal("Item C", vm.ActiveTab.CartItems[3].ProductName);
        Assert.Null(vm.InsertSpliceIndex); // Reset after insertion
    }

    [Fact]
    public async Task BlankRowCommit_TriggersSaveConfirmation_AndConfirmsSale()
    {
        var vm = CreateVm();
        Assert.NotNull(vm.ActiveTab);

        // Empty cart cannot be saved
        vm.OpenSaveConfirmation();
        Assert.False(vm.IsSaveConfirmationOpen);

        // Add item
        vm.ActiveTab.CartItems.Add(new CartItemViewModel
        {
            ProductId = "p1",
            ProductName = "Paracetamol 500",
            BatchId = "b1",
            BatchNumber = "B100",
            UnitPrice = 50,
            Mrp = 50,
            Quantity = 2
        });
        vm.ActiveTab.RecalculateTotals();

        // Trigger save confirmation
        vm.OpenSaveConfirmation();
        Assert.True(vm.IsSaveConfirmationOpen);

        // Confirm and finalize
        await vm.ConfirmAndFinalizeSaleAsync();
        Assert.False(vm.IsSaveConfirmationOpen);
        Assert.Empty(vm.ActiveTab.CartItems); // Bill cleared after successful sale
    }

    [Fact]
    public void CartQuantityHotkeys_IncreasesAndDecreasesSelectedRowQuantity()
    {
        var vm = CreateVm();
        Assert.NotNull(vm.ActiveTab);

        vm.ActiveTab.CartItems.Add(new CartItemViewModel
        {
            ProductId = "p1",
            ProductName = "Paracetamol 500",
            BatchId = "b1",
            BatchNumber = "B100",
            UnitPrice = 50,
            Mrp = 50,
            Quantity = 1
        });
        vm.ActiveTab.SelectedCartIndex = 0;

        vm.IncreaseSelectedCartQuantity();
        Assert.Equal(2m, vm.ActiveTab.CartItems[0].Quantity);

        vm.DecreaseSelectedCartQuantity();
        Assert.Equal(1m, vm.ActiveTab.CartItems[0].Quantity);

        // Cannot decrease below 1 with hotkey
        vm.DecreaseSelectedCartQuantity();
        Assert.Equal(1m, vm.ActiveTab.CartItems[0].Quantity);
    }

    [Fact]
    public async Task OnTheFlyProductCreation_F2_SavesAndAddsToActiveCart()
    {
        var vm = CreateVm();
        vm.SearchQuery = "Calpol 500";

        vm.OpenCreateProductModal();
        Assert.True(vm.IsCreateProductModalOpen);
        Assert.Equal("Calpol 500", vm.NewProductName);

        vm.NewBatchNumber = "CP-99";
        vm.NewMrp = 45.0;
        vm.NewSaleRate = 40.0;
        vm.NewOpeningQty = 25.0;

        await vm.SaveCreateProductAsync();

        Assert.False(vm.IsCreateProductModalOpen);
        Assert.NotNull(_fakeProduct.LastCommand);
        Assert.Equal("Calpol 500", _fakeProduct.LastCommand.Name);
        Assert.Equal("CP-99", _fakeProduct.LastCommand.BatchNumber);

        Assert.NotNull(vm.ActiveTab);
        Assert.Single(vm.ActiveTab.CartItems);
        Assert.Equal("Calpol 500", vm.ActiveTab.CartItems[0].ProductName);
        Assert.Equal("CP-99", vm.ActiveTab.CartItems[0].BatchNumber);
        Assert.Equal(40m, vm.ActiveTab.CartItems[0].UnitPrice);
    }

    [Fact]
    public void MultiTabInvoicing_SupportsAddingAndSwitchingTabs()
    {
        var vm = CreateVm();
        Assert.Single(vm.InvoiceTabs);
        Assert.Equal("#1", vm.ActiveTab?.TabTitle);

        vm.AddNewTab();
        Assert.Equal(2, vm.InvoiceTabs.Count);
        Assert.Equal("#2", vm.ActiveTab?.TabTitle);

        vm.PreviousTab();
        Assert.Equal("#1", vm.ActiveTab?.TabTitle);

        vm.NextTab();
        Assert.Equal("#2", vm.ActiveTab?.TabTitle);
    }

    [Fact]
    public void BillHoldAndRecall_F8_F9_SavesAndRestoresCartItems()
    {
        var vm = CreateVm();
        Assert.NotNull(vm.ActiveTab);

        vm.ActiveTab.CustomerName = "Rahul Sharma";
        vm.ActiveTab.DoctorName = "Dr. Gupta";
        vm.ActiveTab.CartItems.Add(new CartItemViewModel
        {
            ProductId = "p1",
            ProductName = "Dolo 650",
            BatchId = "b1",
            BatchNumber = "B100",
            ExpiryDate = DateTime.UtcNow.AddMonths(12),
            UnitPrice = 30,
            Mrp = 30,
            Quantity = 2
        });
        vm.ActiveTab.RecalculateTotals();

        // Hold active bill (F8)
        vm.HoldActiveBill();

        Assert.Single(vm.HeldBills);
        Assert.Equal("RAHUL SHARMA", vm.HeldBills[0].CustomerName);
        Assert.Equal(60m, vm.HeldBills[0].GrandTotal);
        // Active tab should now be cleared
        Assert.Empty(vm.ActiveTab.CartItems);

        // Open recall modal (F9)
        vm.OpenRecallBillModal();
        Assert.True(vm.IsRecallBillModalOpen);

        // Restore held bill
        vm.RestoreHeldBill(vm.HeldBills[0]);
        Assert.False(vm.IsRecallBillModalOpen);
        Assert.Empty(vm.HeldBills);
        Assert.Single(vm.ActiveTab.CartItems);
        Assert.Equal("Dolo 650", vm.ActiveTab.CartItems[0].ProductName);
        Assert.Equal("RAHUL SHARMA", vm.ActiveTab.CustomerName);
        Assert.Equal(60m, vm.ActiveTab.GrandTotal);
    }

    [Fact]
    public void DualStripAndTabSelection_CalculatesGrossAmountAndQuantityAccurately()
    {
        var item = new CartItemViewModel
        {
            ProductName = "Augmentin 625",
            UnitPrice = 100m,
            TabsPerStrip = 10,
            StripsPerBox = 1,
            StripQuantity = 2,
            TabQuantity = 5
        };

        // 2 strips * 100 = 200, 5 tabs * 10 = 50 => Total Gross = 250
        Assert.Equal(250m, item.GrossAmount);
        // Total base quantity in tabs = 2 * 10 + 5 = 25
        Assert.Equal(25m, item.Quantity);
    }

    [Fact]
    public void DraftStatePersistence_SavesAndRestoresAllActiveTabsAndCartItems()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"pos_draft_test_{Guid.NewGuid():N}.json");
        try
        {
            var vm1 = CreateVm(tempFile);
            vm1.ActiveTab!.CustomerName = "Anil Verma";
            vm1.ActiveTab.CustomerMobile = "9876543210";
            vm1.ActiveTab.DoctorName = "Dr. Gupta";
            vm1.ActiveTab.CartItems.Add(new CartItemViewModel
            {
                ProductId = "p-test-1",
                ProductName = "Azithromycin 500mg",
                BatchId = "b-test-1",
                BatchNumber = "AZ500",
                UnitPrice = 120m,
                Mrp = 130m,
                StripQuantity = 3,
                TabQuantity = 2,
                DiscountPercent = 10m
            });
            vm1.ActiveTab.RecalculateTotals();

            // Add a 2nd bill tab
            vm1.AddNewTab();
            vm1.ActiveTab.CustomerName = "Pooja Patel";
            vm1.ActiveTab.CartItems.Add(new CartItemViewModel
            {
                ProductId = "p-test-2",
                ProductName = "Pan-D Capsule",
                BatchId = "b-test-2",
                BatchNumber = "PD100",
                UnitPrice = 85m,
                Mrp = 90m,
                StripQuantity = 1,
                DiscountPercent = 5m
            });
            vm1.ActiveTab.RecalculateTotals();

            // Explicitly trigger save
            vm1.SaveDraftState();

            // Simulate new ViewModel instance (e.g. app restart or page navigation)
            var vm2 = CreateVm(tempFile);

            Assert.Equal(2, vm2.InvoiceTabs.Count);
            Assert.Equal("ANIL VERMA", vm2.InvoiceTabs[0].CustomerName);
            Assert.Equal("9876543210", vm2.InvoiceTabs[0].CustomerMobile);
            Assert.Equal("DR. GUPTA", vm2.InvoiceTabs[0].DoctorName);
            Assert.Single(vm2.InvoiceTabs[0].CartItems);
            Assert.Equal("Azithromycin 500mg", vm2.InvoiceTabs[0].CartItems[0].ProductName);
            Assert.Equal(3m, vm2.InvoiceTabs[0].CartItems[0].StripQuantity);
            Assert.Equal(2m, vm2.InvoiceTabs[0].CartItems[0].TabQuantity);
            Assert.Equal(10m, vm2.InvoiceTabs[0].CartItems[0].DiscountPercent);

            Assert.Equal("POOJA PATEL", vm2.InvoiceTabs[1].CustomerName);
            Assert.Single(vm2.InvoiceTabs[1].CartItems);
            Assert.Equal("Pan-D Capsule", vm2.InvoiceTabs[1].CartItems[0].ProductName);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void BillDiscount_Percent_ExcludesItemsWithExistingDiscount_AndShowsTotalDiscount()
    {
        var vm = CreateVm();
        var tab = vm.ActiveTab!;

        // Item 1: Gross 100, Item Discount 10% (=10), Net = 90
        tab.CartItems.Add(new CartItemViewModel
        {
            ProductId = "p1",
            ProductName = "Item 1 With 10% Disc",
            UnitPrice = 100m,
            StripQuantity = 1,
            DiscountPercent = 10m
        });

        // Item 2: Gross 200, Item Discount 0%, Net = 200
        tab.CartItems.Add(new CartItemViewModel
        {
            ProductId = "p2",
            ProductName = "Item 2 Without Disc",
            UnitPrice = 200m,
            StripQuantity = 1,
            DiscountPercent = 0m
        });

        tab.RecalculateTotals();
        Assert.Equal(300m, tab.Subtotal);
        Assert.Equal(10m, tab.TotalDiscount);
        Assert.Equal(290m, tab.GrandTotal);

        // Apply 10% Bill Discount: should apply ONLY to Item 2 (Gross 200), i.e. 200 * 10% = 20
        tab.BillDiscountPercent = 10m;

        Assert.Equal(20m, tab.BillDiscountAmount);
        // Total Discount = Item 1 discount (10) + Bill discount on Item 2 (20) = 30
        Assert.Equal(30m, tab.TotalDiscount);
        // Grand Total = 300 - 30 = 270
        Assert.Equal(270m, tab.GrandTotal);
    }

    [Fact]
    public void BillDiscount_FlatAmount_SynchronizesPercentAndUpdatesTotalDiscount()
    {
        var vm = CreateVm();
        var tab = vm.ActiveTab!;

        // Item 1: Gross 100, Item Discount 10% (=10) -> Excluded from bill discount
        tab.CartItems.Add(new CartItemViewModel
        {
            ProductId = "p1",
            ProductName = "Item 1",
            UnitPrice = 100m,
            StripQuantity = 1,
            DiscountPercent = 10m
        });

        // Item 2: Gross 200, Item Discount 0% -> Eligible for bill discount
        tab.CartItems.Add(new CartItemViewModel
        {
            ProductId = "p2",
            ProductName = "Item 2",
            UnitPrice = 200m,
            StripQuantity = 1,
            DiscountPercent = 0m
        });

        tab.RecalculateTotals();

        // Enter Flat ₹50 discount on eligible amount (200)
        tab.BillDiscountAmount = 50m;

        // 50 / 200 * 100 = 25%
        Assert.Equal(25m, tab.BillDiscountPercent);
        // Total discount = 10 + 50 = 60
        Assert.Equal(60m, tab.TotalDiscount);
        // Grand Total = 300 - 60 = 240
        Assert.Equal(240m, tab.GrandTotal);
    }
}
