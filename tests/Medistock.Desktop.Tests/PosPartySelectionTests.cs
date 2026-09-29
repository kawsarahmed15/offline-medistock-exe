using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Customers.DTOs;
using Medistock.Application.Customers.Services;
using Medistock.Desktop.ViewModels;
using Medistock.Domain.Common;
using Xunit;

namespace Medistock.Desktop.Tests;

public class FakeCustomerService : ICustomerService
{
    public List<CustomerDto> StubbedCustomers { get; set; } = new();
    public CreateCustomerCommand? LastCreatedCommand { get; private set; }

    public Task<IReadOnlyList<CustomerDto>> SearchCustomersAsync(string orgId, string query, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<CustomerDto>>(StubbedCustomers);
        }

        var filtered = StubbedCustomers.Where(c =>
            c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            c.Phone.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            c.City.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult<IReadOnlyList<CustomerDto>>(filtered);
    }

    public Task<CustomerDto?> GetCustomerByIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var found = StubbedCustomers.FirstOrDefault(c => c.Id == customerId);
        return Task.FromResult<CustomerDto?>(found);
    }

    public Task<CreateCustomerResult> CreateCustomerAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        LastCreatedCommand = command;
        var newCustomer = new CustomerDto(
            Id: $"cust-{Guid.NewGuid():N}",
            OrgId: command.OrgId,
            Name: command.Name,
            Phone: command.Phone,
            Address: command.Address,
            City: command.City,
            State: command.State,
            Pincode: command.Pincode,
            Gstin: command.Gstin,
            DlNumber: command.DlNumber,
            CreditLimit: command.CreditLimit,
            CurrentBalance: command.OpeningBalance,
            IsActive: true,
            CreatedAt: DateTime.UtcNow
        );
        return Task.FromResult(new CreateCustomerResult(true, newCustomer.Id, newCustomer, null));
    }
}

public class PosPartySelectionTests
{
    private readonly FakeProductSearchService _fakeSearch = new();
    private readonly FakePosTransactionService _fakePos = new();
    private readonly FakeProductService _fakeProduct = new();
    private readonly FakeCustomerService _fakeCustomer = new();

    private PosViewModel CreateVm()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pos_party_draft_{Guid.NewGuid():N}.json");
        return new PosViewModel(_fakeSearch, _fakePos, _fakeProduct, _fakeCustomer, path);
    }

    [Fact]
    public void SelectSaleType_Credit_OpensPartyPicker()
    {
        var vm = CreateVm();
        vm.IsSaleTypePromptOpen = true;

        vm.SelectSaleType(3); // 3 = Credit (Party Sale)

        Assert.False(vm.IsSaleTypePromptOpen);
        Assert.True(vm.IsPartyPickerOpen);
        Assert.NotNull(vm.ActiveTab);
        Assert.Equal(PaymentMode.Credit, vm.ActiveTab.PaymentMode);
    }

    [Fact]
    public async Task SearchPartiesAsync_PopulatesResultsAndSelectsFirst()
    {
        var vm = CreateVm();
        _fakeCustomer.StubbedCustomers = new List<CustomerDto>
        {
            new("c1", "org-1", "Dr. Sharma Clinic", "9876543210", "Civil Lines", "Delhi", "Delhi", "110001", "", "", 50000, 1200, true, DateTime.UtcNow),
            new("c2", "org-1", "City Hospital", "9811122233", "Ring Road", "Delhi", "Delhi", "110002", "", "", 100000, 4500, true, DateTime.UtcNow)
        };

        await vm.SearchPartiesAsync("Sharma");

        Assert.Single(vm.PartySearchResults);
        Assert.Equal("Dr. Sharma Clinic", vm.PartySearchResults[0].Name);
        Assert.Equal(0, vm.SelectedPartyIndex);
        Assert.Equal("Dr. Sharma Clinic", vm.SelectedParty?.Name);
    }

    [Fact]
    public void PartySelection_Navigation_MovesIndexCorrectly()
    {
        var vm = CreateVm();
        vm.PartySearchResults.Add(new("c1", "org-1", "Party 1", "111", "", "Delhi", "", "", "", "", 1000, 0, true, DateTime.UtcNow));
        vm.PartySearchResults.Add(new("c2", "org-1", "Party 2", "222", "", "Delhi", "", "", "", "", 2000, 0, true, DateTime.UtcNow));
        vm.SelectedPartyIndex = 0;
        vm.SelectedParty = vm.PartySearchResults[0];

        vm.MovePartySelectionDown();
        Assert.Equal(1, vm.SelectedPartyIndex);
        Assert.Equal("Party 2", vm.SelectedParty?.Name);

        vm.MovePartySelectionUp();
        Assert.Equal(0, vm.SelectedPartyIndex);
        Assert.Equal("Party 1", vm.SelectedParty?.Name);
    }

    [Fact]
    public void SelectParty_AssignsCustomerToActiveTabAndClosesPicker()
    {
        var vm = CreateVm();
        var party = new CustomerDto("c10", "org-1", "Apollo Pharmacy", "9998887776", "South Ex", "Delhi", "Delhi", "110049", "07AAAAA0000A1Z5", "", 75000, 3200, true, DateTime.UtcNow);

        vm.IsPartyPickerOpen = true;
        vm.SelectParty(party);

        Assert.False(vm.IsPartyPickerOpen);
        Assert.NotNull(vm.ActiveTab);
        Assert.Equal("c10", vm.ActiveTab.CustomerId);
        Assert.Equal("APOLLO PHARMACY", vm.ActiveTab.CustomerName);
        Assert.Equal("9998887776", vm.ActiveTab.CustomerMobile);
    }

    [Fact]
    public void OpenCreatePartyModal_SetsDefaultsAndOpensDialog()
    {
        var vm = CreateVm();
        vm.PartySearchQuery = "New Medicos";

        vm.OpenCreatePartyModal();

        Assert.False(vm.IsPartyPickerOpen);
        Assert.True(vm.IsCreatePartyModalOpen);
        Assert.Equal("New Medicos", vm.NewPartyName);
        Assert.Empty(vm.NewPartyPhone);
        Assert.Equal(25000.0, vm.NewPartyCreditLimit);
    }

    [Fact]
    public async Task SaveCreatePartyAsync_ValidationFails_WhenNameOrPhoneMissing()
    {
        var vm = CreateVm();
        vm.OpenCreatePartyModal();

        vm.NewPartyName = "";
        vm.NewPartyPhone = "";

        var success = await vm.SaveCreatePartyAsync();

        Assert.False(success);
        Assert.True(vm.IsCreatePartyModalOpen);
        Assert.NotEmpty(vm.NewPartyNameError);
        Assert.NotEmpty(vm.NewPartyPhoneError);
        Assert.NotEmpty(vm.CreatePartyFormError);
    }

    [Fact]
    public async Task SaveCreatePartyAsync_Succeeds_CreatesAndSelectsParty()
    {
        var vm = CreateVm();
        vm.OpenCreatePartyModal();

        vm.NewPartyName = "Green Park Care Clinic";
        vm.NewPartyPhone = "9876543210";
        vm.NewPartyCity = "Delhi";
        vm.NewPartyAddress = "Main Market";
        vm.NewPartyCreditLimit = 30000.0;
        vm.NewPartyOpeningBalance = 0.0;

        var success = await vm.SaveCreatePartyAsync();

        Assert.True(success);
        Assert.False(vm.IsCreatePartyModalOpen);
        Assert.NotNull(vm.ActiveTab);
        Assert.Equal("GREEN PARK CARE CLINIC", vm.ActiveTab.CustomerName);
        Assert.Equal("9876543210", vm.ActiveTab.CustomerMobile);
        Assert.NotNull(_fakeCustomer.LastCreatedCommand);
        Assert.Equal("Green Park Care Clinic", _fakeCustomer.LastCreatedCommand.Name);
    }

    [Fact]
    public void OpenAmountDetails_WhenCartHasItems_OpensModalAndCalculatesSummary()
    {
        var vm = CreateVm();
        var tab = vm.ActiveTab!;
        tab.CartItems.Add(new CartItemViewModel
        {
            ProductId = "p1",
            ProductName = "Paracetamol 500mg",
            Quantity = 10,
            UnitPrice = 20m, // Gross = 200
            DiscountPercent = 10m, // Line Disc = 20, Net = 180
            GstRatePercent = 12m
        });
        tab.BillDiscountPercent = 5m;
        tab.RecalculateTotals();

        vm.OpenAmountDetails();

        Assert.True(vm.IsAmountDetailsModalOpen);
        Assert.True(tab.GrandTotal > 0);
        Assert.True(tab.TotalGst > 0);
        Assert.True(tab.TaxableAmount > 0);
        Assert.Contains("Bill Summary", vm.StatusMessage);

        vm.CloseAmountDetails();
        Assert.False(vm.IsAmountDetailsModalOpen);
    }

    [Fact]
    public void OpenAmountDetails_WhenCartEmpty_DoesNotOpenModal()
    {
        var vm = CreateVm();
        vm.ActiveTab!.CartItems.Clear();

        vm.OpenAmountDetails();

        Assert.False(vm.IsAmountDetailsModalOpen);
        Assert.Contains("Cart is empty", vm.StatusMessage);
    }
}
