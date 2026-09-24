using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Application.Accounting.DTOs;
using Medistock.Application.Accounting.Services;
using Medistock.Domain.Accounting;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class AccountingTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteOutboxRepository _outboxRepository;
    private readonly SqliteAccountingRepository _accountingRepository;
    private readonly AccountingService _accountingService;

    public AccountingTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_acc_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);

        var migrator = new DatabaseMigrator(_connectionFactory);
        migrator.MigrateAsync().GetAwaiter().GetResult();

        _outboxRepository = new SqliteOutboxRepository(_connectionFactory);
        _accountingRepository = new SqliteAccountingRepository(_connectionFactory, _outboxRepository);
        _accountingService = new AccountingService(_accountingRepository);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            var wal = _dbPath + "-wal";
            var shm = _dbPath + "-shm";
            if (File.Exists(wal)) File.Delete(wal);
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }

    [Fact]
    public async Task ChartOfAccounts_InitializedWithStandardPharmacyAccounts()
    {
        var accounts = await _accountingService.GetChartOfAccountsAsync("org-1");

        Assert.NotEmpty(accounts);
        Assert.Contains(accounts, a => a.Code == "1001" && a.Name == "Cash on Hand" && a.Category == AccountCategory.Asset);
        Assert.Contains(accounts, a => a.Code == "4001" && a.Name == "Pharmacy Medicine Sales A/c" && a.Category == AccountCategory.Revenue);
        Assert.Contains(accounts, a => a.Code == "5001" && a.Name == "Pharmacy Medicine Purchases A/c" && a.Category == AccountCategory.Expense);
        Assert.Contains(accounts, a => a.Code == "2002" && a.Name == "Output CGST A/c" && a.Category == AccountCategory.Liability);
    }

    [Fact]
    public async Task PostBalancedJournalEntry_UpdatesAccountBalancesAndCreatesOutboxEvent()
    {
        // Sale of ₹1120 (₹1000 base + ₹60 CGST + ₹60 SGST received in Cash)
        var command = new PostJournalEntryCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            VoucherType: VoucherType.Sales,
            VoucherDate: DateTime.UtcNow,
            Narration: "Daily retail pharmacy counter collection",
            ReferenceId: "INV-2026-001",
            ReferenceType: "SaleInvoice",
            CreatedByUserId: "USER-CASHIER-1",
            Lines: new List<JournalLineInputDto>
            {
                new("acc_cash", "Cash on Hand", 1120.00m, 0.00m, "Cash received from sale"),
                new("acc_sales", "Pharmacy Medicine Sales A/c", 0.00m, 1000.00m, "Retail sale"),
                new("acc_output_cgst", "Output CGST A/c", 0.00m, 60.00m, "CGST Output 6%"),
                new("acc_output_sgst", "Output SGST A/c", 0.00m, 60.00m, "SGST Output 6%")
            }
        );

        var voucherNumber = await _accountingService.PostJournalEntryAsync(command);

        Assert.NotNull(voucherNumber);
        Assert.StartsWith("JV-", voucherNumber);

        // Verify account balances
        var cash = await _accountingRepository.GetAccountHeadByIdAsync("acc_cash");
        var sales = await _accountingRepository.GetAccountHeadByIdAsync("acc_sales");
        var cgst = await _accountingRepository.GetAccountHeadByIdAsync("acc_output_cgst");

        Assert.NotNull(cash);
        Assert.NotNull(sales);
        Assert.NotNull(cgst);

        Assert.Equal(1120.00m, cash.CurrentBalance); // Asset increased by debit
        Assert.Equal(1000.00m, sales.CurrentBalance); // Revenue increased by credit
        Assert.Equal(60.00m, cgst.CurrentBalance);   // Liability increased by credit

        // Verify Day Book
        var dayBook = await _accountingService.GetDayBookAsync("org-1", "branch-1", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Single(dayBook);
        Assert.Equal(voucherNumber, dayBook[0].VoucherNumber);
        Assert.Equal(1120.00m, dayBook[0].TotalAmount);
    }

    [Fact]
    public async Task PostImbalancedJournalEntry_ThrowsInvalidOperationException()
    {
        var command = new PostJournalEntryCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            VoucherType: VoucherType.Payment,
            VoucherDate: DateTime.UtcNow,
            Narration: "Store rent payment",
            ReferenceId: null,
            ReferenceType: null,
            CreatedByUserId: "USER-ADMIN",
            Lines: new List<JournalLineInputDto>
            {
                new("acc_rent_exp", "Store Rent Expense", 25000.00m, 0.00m, "Monthly rent"),
                new("acc_cash", "Cash on Hand", 0.00m, 20000.00m, "Paid cash") // Imbalance of 5000!
            }
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _accountingService.PostJournalEntryAsync(command));
    }

    [Fact]
    public async Task AccountLedger_ComputesRunningBalanceCorrectly()
    {
        // 1. Post Sale of ₹5000 in cash
        await _accountingService.PostJournalEntryAsync(new PostJournalEntryCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            VoucherType: VoucherType.Sales,
            VoucherDate: DateTime.UtcNow.AddDays(-2),
            Narration: "Sale 1",
            ReferenceId: null,
            ReferenceType: null,
            CreatedByUserId: "USER-1",
            Lines: new List<JournalLineInputDto>
            {
                new("acc_cash", "Cash on Hand", 5000m, 0m, "Cash in"),
                new("acc_sales", "Pharmacy Medicine Sales A/c", 0m, 5000m, "Revenue")
            }
        ));

        // 2. Post Rent payment of ₹1500 from cash
        await _accountingService.PostJournalEntryAsync(new PostJournalEntryCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            VoucherType: VoucherType.Payment,
            VoucherDate: DateTime.UtcNow.AddDays(-1),
            Narration: "Rent payment",
            ReferenceId: null,
            ReferenceType: null,
            CreatedByUserId: "USER-1",
            Lines: new List<JournalLineInputDto>
            {
                new("acc_rent_exp", "Store Rent Expense", 1500m, 0m, "Rent"),
                new("acc_cash", "Cash on Hand", 0m, 1500m, "Cash out")
            }
        ));

        // 3. Post Utility payment of ₹500 from cash
        await _accountingService.PostJournalEntryAsync(new PostJournalEntryCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            VoucherType: VoucherType.Payment,
            VoucherDate: DateTime.UtcNow,
            Narration: "Electricity bill",
            ReferenceId: null,
            ReferenceType: null,
            CreatedByUserId: "USER-1",
            Lines: new List<JournalLineInputDto>
            {
                new("acc_electricity", "Electricity & Utilities", 500m, 0m, "Power bill"),
                new("acc_cash", "Cash on Hand", 0m, 500m, "Cash out")
            }
        ));

        // Get Cash Ledger
        var ledger = await _accountingService.GetAccountLedgerAsync("acc_cash", DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(1));

        Assert.Equal(4, ledger.Count); // Opening + 3 transactions
        Assert.Equal(3000.00m, ledger.Last().RunningBalance); // 5000 - 1500 - 500 = 3000

        // Get Trial Balance & Profit and Loss
        var tb = await _accountingService.GetTrialBalanceAsync("org-1", DateTime.UtcNow);
        var totalDebits = tb.Sum(t => t.DebitBalance);
        var totalCredits = tb.Sum(t => t.CreditBalance);
        Assert.Equal(totalDebits, totalCredits); // Trial balance must balance perfectly!

        var pl = await _accountingService.GetProfitLossStatementAsync("org-1", DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(1));
        Assert.Equal(5000m, pl.TotalRevenue);
        Assert.Equal(2000m, pl.TotalOperatingExpenses); // 1500 rent + 500 electricity
        Assert.Equal(3000m, pl.NetProfit);
    }

    [Fact]
    public async Task CustomerReceiptAndSupplierPaymentVouchers_ReconcileCorrectly()
    {
        // 1. Post Customer Receipt of ₹2500 in Cash
        var recNo = await _accountingService.CreateReceiptVoucherAsync(new CreateReceiptVoucherCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            VoucherDate: DateTime.UtcNow,
            PaymentAccountId: "acc_cash",
            DebtorAccountId: "acc_debtors",
            Amount: 2500m,
            CustomerName: "Dr. Sharma Clinic",
            Narration: "Clearance of invoice #1029",
            CreatedByUserId: "USER-ADMIN"
        ));

        Assert.NotNull(recNo);
        Assert.StartsWith("REC-", recNo);

        var cashAcc = await _accountingRepository.GetAccountHeadByIdAsync("acc_cash");
        var debtorsAcc = await _accountingRepository.GetAccountHeadByIdAsync("acc_debtors");

        Assert.Equal(2500m, cashAcc!.CurrentBalance);
        Assert.Equal(-2500m, debtorsAcc!.CurrentBalance); // Credited

        // 2. Post Supplier Payment of ₹1500 via Bank
        var payNo = await _accountingService.CreatePaymentVoucherAsync(new CreatePaymentVoucherCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            VoucherDate: DateTime.UtcNow,
            PaymentAccountId: "acc_bank_hdfc",
            CreditorAccountId: "acc_creditors",
            Amount: 1500m,
            SupplierName: "Apex Pharma Distributors",
            Narration: "Part payment for batch consignment",
            CreatedByUserId: "USER-ADMIN"
        ));

        Assert.NotNull(payNo);
        Assert.StartsWith("PAY-", payNo);

        var bankAcc = await _accountingRepository.GetAccountHeadByIdAsync("acc_bank_hdfc");
        var creditorsAcc = await _accountingRepository.GetAccountHeadByIdAsync("acc_creditors");

        Assert.Equal(-1500m, bankAcc!.CurrentBalance);
        Assert.Equal(-1500m, creditorsAcc!.CurrentBalance); // Debited liability decreases

        // 3. Post Store Expense of ₹800 in Cash
        var expNo = await _accountingService.CreateExpenseVoucherAsync(new CreateExpenseVoucherCommand(
            OrgId: "org-1",
            BranchId: "branch-1",
            VoucherDate: DateTime.UtcNow,
            PaymentAccountId: "acc_cash",
            ExpenseAccountId: "acc_electricity",
            Amount: 800m,
            Narration: "Monthly store electricity bill",
            CreatedByUserId: "USER-ADMIN"
        ));

        Assert.NotNull(expNo);
        Assert.StartsWith("EXP-", expNo);

        var cashAfterExp = await _accountingRepository.GetAccountHeadByIdAsync("acc_cash");
        var elecExp = await _accountingRepository.GetAccountHeadByIdAsync("acc_electricity");

        Assert.Equal(1700m, cashAfterExp!.CurrentBalance); // 2500 - 800
        Assert.Equal(800m, elecExp!.CurrentBalance);

        // 4. Verify Day Book contains all 3 vouchers
        var dayBook = await _accountingService.GetDayBookAsync("org-1", "branch-1", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Equal(3, dayBook.Count);
    }
}

