using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Desktop.ViewModels;
using Xunit;

namespace Medistock.Desktop.Tests;

public class PosCreateProductModalTests
{
    private readonly FakeProductSearchService _fakeSearch = new();
    private readonly FakePosTransactionService _fakePos = new();
    private readonly FakeProductService _fakeProduct = new();

    private PosViewModel CreateVm()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pos_modal_draft_{Guid.NewGuid():N}.json");
        return new PosViewModel(_fakeSearch, _fakePos, _fakeProduct, path);
    }

    [Fact]
    public void OpenCreateProductModal_InitializesDefaultsAndClearsErrors()
    {
        var vm = CreateVm();
        vm.NewProductNameError = "Previous error";
        vm.CreateProductFormError = "Banner error";

        vm.OpenCreateProductModal();

        Assert.True(vm.IsCreateProductModalOpen);
        Assert.Empty(vm.NewProductNameError);
        Assert.Empty(vm.CreateProductFormError);
        Assert.StartsWith("B", vm.NewBatchNumber);
        Assert.Equal("10x10", vm.NewPackSizeText);
        Assert.Equal(100.0, vm.NewMrp);
        Assert.Equal(90.0, vm.NewSaleRate);
    }

    [Fact]
    public async Task SaveCreateProductAsync_FailsValidation_WhenRequiredFieldsAreEmpty()
    {
        var vm = CreateVm();
        vm.OpenCreateProductModal();

        vm.NewProductName = "   ";
        vm.NewPackSizeText = "";
        vm.NewBatchNumber = " ";
        vm.NewMrp = 0;
        vm.NewSaleRate = 0;

        var result = await vm.SaveCreateProductAsync();

        Assert.False(result);
        Assert.True(vm.IsCreateProductModalOpen);
        Assert.NotEmpty(vm.NewProductNameError);
        Assert.NotEmpty(vm.NewPackSizeTextError);
        Assert.NotEmpty(vm.NewBatchNumberError);
        Assert.NotEmpty(vm.NewMrpError);
        Assert.NotEmpty(vm.NewSaleRateError);
        Assert.NotEmpty(vm.CreateProductFormError);
    }

    [Fact]
    public async Task SaveCreateProductAsync_SucceedsAndAddsToCart_WhenValid()
    {
        var vm = CreateVm();
        vm.OpenCreateProductModal();

        vm.NewProductName = "Augmentin 625 Duo";
        vm.NewGenericName = "Amoxicillin and Clavulanate Potassium";
        vm.NewPackSizeText = "10x10";
        vm.NewBatchNumber = "AUG2026";
        vm.NewMrp = 220.0;
        vm.NewPurchaseRate = 160.0;
        vm.NewSaleRate = 200.0;
        vm.NewOpeningQty = 100.0;

        var result = await vm.SaveCreateProductAsync();

        Assert.True(result);
        Assert.False(vm.IsCreateProductModalOpen);
        Assert.Empty(vm.NewProductNameError);
        Assert.Empty(vm.CreateProductFormError);

        Assert.NotNull(vm.ActiveTab);
        Assert.Single(vm.ActiveTab.CartItems);

        var item = vm.ActiveTab.CartItems.First();
        Assert.Equal("Augmentin 625 Duo", item.ProductName);
        Assert.Equal("AUG2026", item.BatchNumber);
        Assert.Equal(200m, item.UnitPrice);
        Assert.Equal(220m, item.Mrp);
    }

    [Fact]
    public async Task SaveCreateProductAsync_FailsValidation_WhenSaleRateGreaterThanMrp()
    {
        var vm = CreateVm();
        vm.OpenCreateProductModal();

        vm.NewProductName = "Test Product";
        vm.NewPackSizeText = "10x10";
        vm.NewBatchNumber = "BATCH01";
        vm.NewMrp = 100.0;
        vm.NewPurchaseRate = 80.0;
        vm.NewSaleRate = 120.0; // Greater than MRP!

        var result = await vm.SaveCreateProductAsync();

        Assert.False(result);
        Assert.True(vm.IsCreateProductModalOpen);
        Assert.Equal("Sale Rate cannot be greater than MRP.", vm.NewSaleRateError);
    }

    [Fact]
    public void CartItemViewModel_UnitPrice_NeverExceedsMrp()
    {
        var item = new CartItemViewModel
        {
            ProductName = "Test Medicine",
            Mrp = 100m,
            UnitPrice = 80m
        };

        Assert.Equal(80m, item.UnitPrice);
        Assert.Equal(80.0, item.UnitPriceDouble);

        // Attempt to set UnitPrice above MRP
        item.UnitPrice = 150m;
        Assert.Equal(100m, item.UnitPrice);
        Assert.Equal(100.0, item.UnitPriceDouble);

        // Lower MRP below current UnitPrice -> UnitPrice should automatically clamp down
        item.Mrp = 75m;
        Assert.Equal(75m, item.UnitPrice);
        Assert.Equal(75.0, item.UnitPriceDouble);

        // Attempt to set UnitPriceDouble above MRP
        item.UnitPriceDouble = 999.0;
        Assert.Equal(75m, item.UnitPrice);
        Assert.Equal(75.0, item.UnitPriceDouble);

        // When MRP is 0, UnitPrice should not be restricted
        item.Mrp = 0m;
        Assert.Equal(double.MaxValue, item.MrpDouble);
        item.UnitPrice = 250m;
        Assert.Equal(250m, item.UnitPrice);
        Assert.Equal(250.0, item.UnitPriceDouble);
    }
}
