using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Accounting.DTOs;
using Medistock.Application.Common.Interfaces;
using Medistock.Domain.Accounting;

namespace Medistock.Application.Accounting.Services;

public class AccountingService : IAccountingService
{
    private readonly IAccountingRepository _accountingRepository;

    public AccountingService(IAccountingRepository accountingRepository)
    {
        _accountingRepository = accountingRepository;
    }

    public async Task<IReadOnlyList<AccountHeadDto>> GetChartOfAccountsAsync(string orgId, CancellationToken cancellationToken = default)
    {
        var accounts = await _accountingRepository.GetAccountHeadsAsync(orgId, cancellationToken);
        return accounts.Select(a => new AccountHeadDto(
            a.Id,
            a.OrgId,
            a.Code,
            a.Name,
            a.Category,
            a.ParentAccountId,
            a.IsSystemAccount,
            a.IsActive,
            a.CurrentBalance
        )).ToList();
    }

    public async Task<string> PostJournalEntryAsync(PostJournalEntryCommand command, CancellationToken cancellationToken = default)
    {
        var entryId = $"je_{Guid.NewGuid():N}";
        var voucherNumber = $"JV-{DateTime.UtcNow:yyyyMM}-{Random.Shared.Next(10000, 99999)}";

        var entry = new JournalEntry(
            id: entryId,
            orgId: command.OrgId,
            branchId: command.BranchId,
            voucherNumber: voucherNumber,
            voucherType: command.VoucherType,
            voucherDate: command.VoucherDate,
            narration: command.Narration,
            referenceId: command.ReferenceId,
            referenceType: command.ReferenceType,
            createdByUserId: command.CreatedByUserId
        );

        foreach (var line in command.Lines)
        {
            entry.AddLine(line.AccountId, line.AccountName, line.DebitAmount, line.CreditAmount, line.Narration);
        }

        entry.ValidateBalance();

        return await _accountingRepository.PostJournalEntryAsync(entry, cancellationToken);
    }

    public Task<IReadOnlyList<DayBookVoucherDto>> GetDayBookAsync(string orgId, string branchId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return _accountingRepository.GetDayBookAsync(orgId, branchId, startDate, endDate, cancellationToken);
    }

    public Task<IReadOnlyList<AccountLedgerLineDto>> GetAccountLedgerAsync(string accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return _accountingRepository.GetAccountLedgerAsync(accountId, startDate, endDate, cancellationToken);
    }

    public Task<IReadOnlyList<TrialBalanceItemDto>> GetTrialBalanceAsync(string orgId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        return _accountingRepository.GetTrialBalanceAsync(orgId, asOfDate, cancellationToken);
    }

    public Task<ProfitLossDto> GetProfitLossStatementAsync(string orgId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return _accountingRepository.GetProfitLossSummaryAsync(orgId, startDate, endDate, cancellationToken);
    }

    public async Task<string> CreateReceiptVoucherAsync(CreateReceiptVoucherCommand command, CancellationToken cancellationToken = default)
    {
        var payAcc = await _accountingRepository.GetAccountHeadByIdAsync(command.PaymentAccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment account '{command.PaymentAccountId}' not found.");
        var debtorAcc = await _accountingRepository.GetAccountHeadByIdAsync(command.DebtorAccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Debtor account '{command.DebtorAccountId}' not found.");

        var entryId = $"je_{Guid.NewGuid():N}";
        var voucherNumber = $"REC-{DateTime.UtcNow:yyyyMM}-{Random.Shared.Next(10000, 99999)}";
        var narration = command.Narration ?? $"Receipt from customer {command.CustomerName ?? "Cash Customer"}";

        var entry = new JournalEntry(
            id: entryId,
            orgId: command.OrgId,
            branchId: command.BranchId,
            voucherNumber: voucherNumber,
            voucherType: VoucherType.Receipt,
            voucherDate: command.VoucherDate,
            narration: narration,
            referenceId: command.CustomerName,
            referenceType: "CUSTOMER_RECEIPT",
            createdByUserId: command.CreatedByUserId
        );

        // Debit Cash/Bank, Credit Sundry Debtors
        entry.AddLine(payAcc.Id, payAcc.Name, debitAmount: command.Amount, creditAmount: 0m, narration: narration);
        entry.AddLine(debtorAcc.Id, debtorAcc.Name, debitAmount: 0m, creditAmount: command.Amount, narration: narration);

        entry.ValidateBalance();
        return await _accountingRepository.PostJournalEntryAsync(entry, cancellationToken);
    }

    public async Task<string> CreatePaymentVoucherAsync(CreatePaymentVoucherCommand command, CancellationToken cancellationToken = default)
    {
        var payAcc = await _accountingRepository.GetAccountHeadByIdAsync(command.PaymentAccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment account '{command.PaymentAccountId}' not found.");
        var creditorAcc = await _accountingRepository.GetAccountHeadByIdAsync(command.CreditorAccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Creditor account '{command.CreditorAccountId}' not found.");

        var entryId = $"je_{Guid.NewGuid():N}";
        var voucherNumber = $"PAY-{DateTime.UtcNow:yyyyMM}-{Random.Shared.Next(10000, 99999)}";
        var narration = command.Narration ?? $"Payment to supplier {command.SupplierName ?? "Supplier"}";

        var entry = new JournalEntry(
            id: entryId,
            orgId: command.OrgId,
            branchId: command.BranchId,
            voucherNumber: voucherNumber,
            voucherType: VoucherType.Payment,
            voucherDate: command.VoucherDate,
            narration: narration,
            referenceId: command.SupplierName,
            referenceType: "SUPPLIER_PAYMENT",
            createdByUserId: command.CreatedByUserId
        );

        // Debit Sundry Creditors, Credit Cash/Bank
        entry.AddLine(creditorAcc.Id, creditorAcc.Name, debitAmount: command.Amount, creditAmount: 0m, narration: narration);
        entry.AddLine(payAcc.Id, payAcc.Name, debitAmount: 0m, creditAmount: command.Amount, narration: narration);

        entry.ValidateBalance();
        return await _accountingRepository.PostJournalEntryAsync(entry, cancellationToken);
    }

    public async Task<string> CreateExpenseVoucherAsync(CreateExpenseVoucherCommand command, CancellationToken cancellationToken = default)
    {
        var payAcc = await _accountingRepository.GetAccountHeadByIdAsync(command.PaymentAccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment account '{command.PaymentAccountId}' not found.");
        var expenseAcc = await _accountingRepository.GetAccountHeadByIdAsync(command.ExpenseAccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Expense account '{command.ExpenseAccountId}' not found.");

        var entryId = $"je_{Guid.NewGuid():N}";
        var voucherNumber = $"EXP-{DateTime.UtcNow:yyyyMM}-{Random.Shared.Next(10000, 99999)}";
        var narration = command.Narration ?? $"Expense: {expenseAcc.Name}";

        var entry = new JournalEntry(
            id: entryId,
            orgId: command.OrgId,
            branchId: command.BranchId,
            voucherNumber: voucherNumber,
            voucherType: VoucherType.Payment,
            voucherDate: command.VoucherDate,
            narration: narration,
            referenceId: expenseAcc.Id,
            referenceType: "EXPENSE_VOUCHER",
            createdByUserId: command.CreatedByUserId
        );

        // Debit Expense, Credit Cash/Bank
        entry.AddLine(expenseAcc.Id, expenseAcc.Name, debitAmount: command.Amount, creditAmount: 0m, narration: narration);
        entry.AddLine(payAcc.Id, payAcc.Name, debitAmount: 0m, creditAmount: command.Amount, narration: narration);

        entry.ValidateBalance();
        return await _accountingRepository.PostJournalEntryAsync(entry, cancellationToken);
    }
}

