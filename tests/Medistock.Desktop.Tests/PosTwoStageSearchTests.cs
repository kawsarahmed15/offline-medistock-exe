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
    public Task<CommitSaleResult> ProcessSaleAsync(CommitSaleCommand command, CancellationToken cancellationToken = default)
    {
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

    private PosViewModel CreateVm() => new(_fakeSearch, _fakePos, _fakeProduct);

    [Fact]
    public void OpenBatchPicker_WithMultipleBatches_OpensPickerWindow()
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

        vm.OpenBatchPicker(product);

        Assert.True(vm.IsBatchPickerOpen);
        Assert.Equal(2, vm.SelectedProductBatches.Count);
        Assert.Equal(0, vm.SelectedBatchIndex);

        // Select the second batch
        vm.SelectBatch(vm.SelectedProductBatches[1]);

        Assert.False(vm.IsBatchPickerOpen);
        Assert.NotNull(vm.ActiveTab);
        Assert.Single(vm.ActiveTab.CartItems);
        Assert.Equal("AUG-2", vm.ActiveTab.CartItems[0].BatchNumber);
        Assert.Equal(190m, vm.ActiveTab.CartItems[0].UnitPrice);
    }

    [Fact]
    public void OpenBatchPicker_WithSingleBatch_DirectlyAddsToCart()
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

        vm.OpenBatchPicker(product);

        Assert.False(vm.IsBatchPickerOpen);
        Assert.NotNull(vm.ActiveTab);
        Assert.Single(vm.ActiveTab.CartItems);
        Assert.Equal("DL-100", vm.ActiveTab.CartItems[0].BatchNumber);
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
        Assert.Equal("Bill #1", vm.ActiveTab?.TabTitle);

        vm.AddNewTab();
        Assert.Equal(2, vm.InvoiceTabs.Count);
        Assert.Equal("Bill #2", vm.ActiveTab?.TabTitle);

        vm.PreviousTab();
        Assert.Equal("Bill #1", vm.ActiveTab?.TabTitle);

        vm.NextTab();
        Assert.Equal("Bill #2", vm.ActiveTab?.TabTitle);
    }
}
