using System;
using System.Collections.Generic;
using System.Linq;

namespace Medistock.Domain.Accounting;

public enum AccountCategory
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum VoucherType
{
    Sales = 1,
    Purchase = 2,
    Payment = 3,
    Receipt = 4,
    Contra = 5,
    Journal = 6,
    CreditNote = 7,
    DebitNote = 8
}

public class AccountHead
{
    public string Id { get; private set; }
    public string OrgId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public AccountCategory Category { get; private set; }
    public string? ParentAccountId { get; private set; }
    public bool IsSystemAccount { get; private set; }
    public bool IsActive { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public AccountHead(
        string id,
        string orgId,
        string code,
        string name,
        AccountCategory category,
        string? parentAccountId = null,
        bool isSystemAccount = false,
        bool isActive = true,
        decimal currentBalance = 0m)
    {
        Id = id;
        OrgId = orgId;
        Code = code;
        Name = name;
        Category = category;
        ParentAccountId = parentAccountId;
        IsSystemAccount = isSystemAccount;
        IsActive = isActive;
        CurrentBalance = currentBalance;
        CreatedAt = DateTime.UtcNow;
    }

    public void ApplyBalanceDelta(decimal debit, decimal credit)
    {
        // For Assets & Expenses: normal balance is Debit (Debit increases, Credit decreases)
        // For Liabilities, Equity & Revenue: normal balance is Credit (Credit increases, Debit decreases)
        if (Category == AccountCategory.Asset || Category == AccountCategory.Expense)
        {
            CurrentBalance += (debit - credit);
        }
        else
        {
            CurrentBalance += (credit - debit);
        }
    }
}

public class JournalEntryLine
{
    public string Id { get; private set; }
    public string JournalEntryId { get; private set; }
    public string AccountId { get; private set; }
    public string AccountName { get; private set; }
    public decimal DebitAmount { get; private set; }
    public decimal CreditAmount { get; private set; }
    public string? Narration { get; private set; }

    public JournalEntryLine(
        string id,
        string journalEntryId,
        string accountId,
        string accountName,
        decimal debitAmount,
        decimal creditAmount,
        string? narration = null)
    {
        if (debitAmount < 0 || creditAmount < 0)
        {
            throw new ArgumentException("Debit and Credit amounts cannot be negative.");
        }

        if (debitAmount == 0 && creditAmount == 0)
        {
            throw new ArgumentException("A journal line must have either a debit or credit amount greater than 0.");
        }

        Id = id;
        JournalEntryId = journalEntryId;
        AccountId = accountId;
        AccountName = accountName;
        DebitAmount = debitAmount;
        CreditAmount = creditAmount;
        Narration = narration;
    }
}

public class JournalEntry
{
    public string Id { get; private set; }
    public string OrgId { get; private set; }
    public string BranchId { get; private set; }
    public string VoucherNumber { get; private set; }
    public VoucherType VoucherType { get; private set; }
    public DateTime VoucherDate { get; private set; }
    public string? Narration { get; private set; }
    public string? ReferenceId { get; private set; }
    public string? ReferenceType { get; private set; }
    public string CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<JournalEntryLine> _lines = new();
    public IReadOnlyList<JournalEntryLine> Lines => _lines.AsReadOnly();

    public decimal TotalDebit => _lines.Sum(l => l.DebitAmount);
    public decimal TotalCredit => _lines.Sum(l => l.CreditAmount);
    public bool IsBalanced => Math.Abs(TotalDebit - TotalCredit) < 0.001m;

    public JournalEntry(
        string id,
        string orgId,
        string branchId,
        string voucherNumber,
        VoucherType voucherType,
        DateTime voucherDate,
        string? narration,
        string? referenceId,
        string? referenceType,
        string createdByUserId)
    {
        Id = id;
        OrgId = orgId;
        BranchId = branchId;
        VoucherNumber = voucherNumber;
        VoucherType = voucherType;
        VoucherDate = voucherDate;
        Narration = narration;
        ReferenceId = referenceId;
        ReferenceType = referenceType;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public void AddLine(string accountId, string accountName, decimal debitAmount, decimal creditAmount, string? narration = null)
    {
        var lineId = $"jl_{Guid.NewGuid():N}";
        _lines.Add(new JournalEntryLine(lineId, Id, accountId, accountName, debitAmount, creditAmount, narration));
    }

    public void ValidateBalance()
    {
        if (_lines.Count < 2)
        {
            throw new InvalidOperationException("A journal entry must contain at least two lines for double-entry bookkeeping.");
        }

        if (!IsBalanced)
        {
            throw new InvalidOperationException($"Double-entry imbalance! Total Debit (₹{TotalDebit:N2}) != Total Credit (₹{TotalCredit:N2})");
        }
    }
}
