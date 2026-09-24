using System;
using System.Collections.Generic;
using Medistock.Domain.Accounting;

namespace Medistock.Application.Accounting.DTOs;

public record AccountHeadDto(
    string Id,
    string OrgId,
    string Code,
    string Name,
    AccountCategory Category,
    string? ParentAccountId,
    bool IsSystemAccount,
    bool IsActive,
    decimal CurrentBalance
);

public record JournalLineInputDto(
    string AccountId,
    string AccountName,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Narration
);

public record PostJournalEntryCommand(
    string OrgId,
    string BranchId,
    VoucherType VoucherType,
    DateTime VoucherDate,
    string? Narration,
    string? ReferenceId,
    string? ReferenceType,
    string CreatedByUserId,
    List<JournalLineInputDto> Lines
);

public record JournalEntryDto(
    string Id,
    string VoucherNumber,
    VoucherType VoucherType,
    DateTime VoucherDate,
    string? Narration,
    string? ReferenceId,
    string? ReferenceType,
    string CreatedByUserId,
    decimal TotalDebit,
    decimal TotalCredit,
    List<JournalLineDto> Lines
);

public record JournalLineDto(
    string Id,
    string AccountId,
    string AccountName,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Narration
);

public record AccountLedgerLineDto(
    string VoucherNumber,
    VoucherType VoucherType,
    DateTime VoucherDate,
    string Particulars,
    string? Narration,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal RunningBalance
);

public record DayBookVoucherDto(
    string Id,
    string VoucherNumber,
    VoucherType VoucherType,
    DateTime VoucherDate,
    string Narration,
    string PrimaryAccount,
    decimal TotalAmount,
    string CreatedByUserId
);

public record TrialBalanceItemDto(
    string AccountId,
    string AccountCode,
    string AccountName,
    AccountCategory Category,
    decimal DebitBalance,
    decimal CreditBalance
);

public record ProfitLossDto(
    decimal TotalRevenue,
    decimal TotalPurchases,
    decimal GrossProfit,
    decimal TotalOperatingExpenses,
    decimal NetProfit,
    List<TrialBalanceItemDto> RevenueAccounts,
    List<TrialBalanceItemDto> ExpenseAccounts
);

public record CreateReceiptVoucherCommand(
    string OrgId,
    string BranchId,
    DateTime VoucherDate,
    string PaymentAccountId,
    string DebtorAccountId,
    decimal Amount,
    string? CustomerName,
    string? Narration,
    string CreatedByUserId
);

public record CreatePaymentVoucherCommand(
    string OrgId,
    string BranchId,
    DateTime VoucherDate,
    string PaymentAccountId,
    string CreditorAccountId,
    decimal Amount,
    string? SupplierName,
    string? Narration,
    string CreatedByUserId
);

public record CreateExpenseVoucherCommand(
    string OrgId,
    string BranchId,
    DateTime VoucherDate,
    string PaymentAccountId,
    string ExpenseAccountId,
    decimal Amount,
    string? Narration,
    string CreatedByUserId
);

