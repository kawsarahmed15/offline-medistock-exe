#pragma warning disable MVVMTK0045

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Accounting.DTOs;
using Medistock.Application.Accounting.Services;
using Medistock.Domain.Accounting;

namespace Medistock.Desktop.ViewModels;

public partial class AccountingViewModel : ObservableObject
{
    private readonly IAccountingService _accountingService;

    [ObservableProperty]
    private string _statusMessage = "Ready. Double-entry financial accounting initialized.";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private decimal _totalDebits;

    [ObservableProperty]
    private decimal _totalCredits;

    [ObservableProperty]
    private decimal _totalRevenue;

    [ObservableProperty]
    private decimal _totalExpenses;

    [ObservableProperty]
    private decimal _netProfit;

    [ObservableProperty]
    private AccountHeadDto? _selectedAccount;

    [ObservableProperty]
    private bool _isVoucherDialogOpen;

    [ObservableProperty]
    private int _voucherTypeIndex; // 0: Receipt, 1: Payment, 2: Expense

    [ObservableProperty]
    private double _voucherAmount = 0;

    [ObservableProperty]
    private string _voucherPartyName = string.Empty;

    [ObservableProperty]
    private string _voucherNarration = string.Empty;

    [ObservableProperty]
    private bool _isCashPayment = true;

    [ObservableProperty]
    private AccountHeadDto? _selectedExpenseAccount;

    public ObservableCollection<AccountHeadDto> Accounts { get; } = new();
    public ObservableCollection<AccountHeadDto> ExpenseAccounts { get; } = new();
    public ObservableCollection<DayBookVoucherDto> DayBookEntries { get; } = new();
    public ObservableCollection<AccountLedgerLineDto> LedgerLines { get; } = new();
    public ObservableCollection<TrialBalanceItemDto> TrialBalanceItems { get; } = new();

    public AccountingViewModel(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }

    [RelayCommand]
    public void OpenReceiptDialog()
    {
        VoucherTypeIndex = 0;
        VoucherAmount = 0;
        VoucherPartyName = string.Empty;
        VoucherNarration = "Customer receipt settlement";
        IsCashPayment = true;
        IsVoucherDialogOpen = true;
    }

    [RelayCommand]
    public void OpenPaymentDialog()
    {
        VoucherTypeIndex = 1;
        VoucherAmount = 0;
        VoucherPartyName = string.Empty;
        VoucherNarration = "Supplier payment settlement";
        IsCashPayment = true;
        IsVoucherDialogOpen = true;
    }

    [RelayCommand]
    public void OpenExpenseDialog()
    {
        VoucherTypeIndex = 2;
        VoucherAmount = 0;
        VoucherPartyName = string.Empty;
        VoucherNarration = "Operating expense";
        IsCashPayment = true;
        IsVoucherDialogOpen = true;
    }

    [RelayCommand]
    public void CloseVoucherDialog()
    {
        IsVoucherDialogOpen = false;
    }

    [RelayCommand]
    public async Task SaveVoucherAsync()
    {
        if (VoucherAmount <= 0)
        {
            StatusMessage = "Voucher amount must be greater than zero.";
            return;
        }

        var payAccountId = IsCashPayment ? "acc_cash" : "acc_bank_hdfc";
        var decimalAmount = (decimal)VoucherAmount;

        try
        {
            IsLoading = true;
            string voucherNo = string.Empty;

            if (VoucherTypeIndex == 0) // Receipt
            {
                voucherNo = await _accountingService.CreateReceiptVoucherAsync(new CreateReceiptVoucherCommand(
                    OrgId: "org-1",
                    BranchId: "branch-1",
                    VoucherDate: DateTime.UtcNow,
                    PaymentAccountId: payAccountId,
                    DebtorAccountId: "acc_debtors",
                    Amount: decimalAmount,
                    CustomerName: string.IsNullOrWhiteSpace(VoucherPartyName) ? "Cash Customer" : VoucherPartyName,
                    Narration: VoucherNarration,
                    CreatedByUserId: "USER-ADMIN"
                ));
            }
            else if (VoucherTypeIndex == 1) // Supplier Payment
            {
                voucherNo = await _accountingService.CreatePaymentVoucherAsync(new CreatePaymentVoucherCommand(
                    OrgId: "org-1",
                    BranchId: "branch-1",
                    VoucherDate: DateTime.UtcNow,
                    PaymentAccountId: payAccountId,
                    CreditorAccountId: "acc_creditors",
                    Amount: decimalAmount,
                    SupplierName: string.IsNullOrWhiteSpace(VoucherPartyName) ? "Supplier" : VoucherPartyName,
                    Narration: VoucherNarration,
                    CreatedByUserId: "USER-ADMIN"
                ));
            }
            else if (VoucherTypeIndex == 2) // Expense
            {
                var expAccountId = SelectedExpenseAccount?.Id ?? "acc_rent_exp";
                voucherNo = await _accountingService.CreateExpenseVoucherAsync(new CreateExpenseVoucherCommand(
                    OrgId: "org-1",
                    BranchId: "branch-1",
                    VoucherDate: DateTime.UtcNow,
                    PaymentAccountId: payAccountId,
                    ExpenseAccountId: expAccountId,
                    Amount: decimalAmount,
                    Narration: VoucherNarration,
                    CreatedByUserId: "USER-ADMIN"
                ));
            }

            IsVoucherDialogOpen = false;
            StatusMessage = $"Voucher {voucherNo} posted successfully! Ledgers updated.";
            await LoadInitialDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to post voucher: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }


    [RelayCommand]
    public async Task LoadInitialDataAsync()
    {
        IsLoading = true;
        try
        {
            await LoadChartOfAccountsAsync();
            await LoadDayBookAsync();
            await LoadTrialBalanceAndPlAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Accounting Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadChartOfAccountsAsync()
    {
        Accounts.Clear();
        ExpenseAccounts.Clear();
        var accounts = await _accountingService.GetChartOfAccountsAsync("org-1");
        foreach (var a in accounts)
        {
            Accounts.Add(a);
            if (a.Category == AccountCategory.Expense)
            {
                ExpenseAccounts.Add(a);
            }
        }

        if (Accounts.Any() && SelectedAccount == null)
        {
            SelectedAccount = Accounts.First();
        }

        if (ExpenseAccounts.Any() && SelectedExpenseAccount == null)
        {
            SelectedExpenseAccount = ExpenseAccounts.First();
        }
    }

    [RelayCommand]
    public async Task LoadDayBookAsync()
    {
        DayBookEntries.Clear();
        var entries = await _accountingService.GetDayBookAsync(
            "org-1", "branch-1",
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(1));

        foreach (var e in entries)
        {
            DayBookEntries.Add(e);
        }
    }

    [RelayCommand]
    public async Task LoadTrialBalanceAndPlAsync()
    {
        TrialBalanceItems.Clear();
        var tb = await _accountingService.GetTrialBalanceAsync("org-1", DateTime.UtcNow);
        foreach (var item in tb)
        {
            TrialBalanceItems.Add(item);
        }

        TotalDebits = TrialBalanceItems.Sum(t => t.DebitBalance);
        TotalCredits = TrialBalanceItems.Sum(t => t.CreditBalance);

        var pl = await _accountingService.GetProfitLossStatementAsync(
            "org-1",
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(1));

        TotalRevenue = pl.TotalRevenue;
        TotalExpenses = pl.TotalOperatingExpenses + pl.TotalPurchases;
        NetProfit = pl.NetProfit;
    }

    async partial void OnSelectedAccountChanged(AccountHeadDto? value)
    {
        if (value == null) return;

        LedgerLines.Clear();
        var lines = await _accountingService.GetAccountLedgerAsync(
            value.Id,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(1));

        foreach (var l in lines)
        {
            LedgerLines.Add(l);
        }

        StatusMessage = $"Ledger loaded for {value.Name} ({value.Code}). Balance: ₹{value.CurrentBalance:N2}";
    }
}

