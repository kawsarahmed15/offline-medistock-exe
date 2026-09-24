using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Accounting.DTOs;
using Medistock.Application.Common.Interfaces;
using Medistock.Domain.Accounting;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteAccountingRepository : IAccountingRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IOutboxRepository _outboxRepository;

    public SqliteAccountingRepository(
        ISqliteConnectionFactory connectionFactory,
        IOutboxRepository outboxRepository)
    {
        _connectionFactory = connectionFactory;
        _outboxRepository = outboxRepository;
    }

    public async Task<IReadOnlyList<AccountHead>> GetAccountHeadsAsync(string orgId, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, org_id, code, name, category, parent_account_id, is_system_account, is_active, current_balance
            FROM account_heads
            WHERE org_id = @orgId
            ORDER BY code ASC;
        ";

        var rows = await conn.QueryAsync<dynamic>(sql, new { orgId });
        var result = new List<AccountHead>();

        foreach (var r in rows)
        {
            result.Add(new AccountHead(
                id: (string)r.id,
                orgId: (string)r.org_id,
                code: (string)r.code,
                name: (string)r.name,
                category: (AccountCategory)(long)r.category,
                parentAccountId: (string?)r.parent_account_id,
                isSystemAccount: ((long)r.is_system_account) == 1,
                isActive: ((long)r.is_active) == 1,
                currentBalance: Convert.ToDecimal(r.current_balance)
            ));
        }

        return result;
    }

    public async Task<AccountHead?> GetAccountHeadByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, org_id, code, name, category, parent_account_id, is_system_account, is_active, current_balance
            FROM account_heads
            WHERE id = @id;
        ";

        var r = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { id });
        if (r == null) return null;

        return new AccountHead(
            id: (string)r.id,
            orgId: (string)r.org_id,
            code: (string)r.code,
            name: (string)r.name,
            category: (AccountCategory)(long)r.category,
            parentAccountId: (string?)r.parent_account_id,
            isSystemAccount: ((long)r.is_system_account) == 1,
            isActive: ((long)r.is_active) == 1,
            currentBalance: Convert.ToDecimal(r.current_balance)
        );
    }

    public async Task<AccountHead?> GetAccountHeadByCodeAsync(string orgId, string code, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, org_id, code, name, category, parent_account_id, is_system_account, is_active, current_balance
            FROM account_heads
            WHERE org_id = @orgId AND code = @code;
        ";

        var r = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { orgId, code });
        if (r == null) return null;

        return new AccountHead(
            id: (string)r.id,
            orgId: (string)r.org_id,
            code: (string)r.code,
            name: (string)r.name,
            category: (AccountCategory)(long)r.category,
            parentAccountId: (string?)r.parent_account_id,
            isSystemAccount: ((long)r.is_system_account) == 1,
            isActive: ((long)r.is_active) == 1,
            currentBalance: Convert.ToDecimal(r.current_balance)
        );
    }

    public async Task CreateAccountHeadAsync(AccountHead account, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO account_heads (id, org_id, code, name, category, parent_account_id, is_system_account, is_active, current_balance, created_at)
            VALUES (@Id, @OrgId, @Code, @Name, @Category, @ParentAccountId, @IsSystemAccount, @IsActive, @CurrentBalance, datetime('now'));
        ";

        await conn.ExecuteAsync(sql, new
        {
            account.Id,
            account.OrgId,
            account.Code,
            account.Name,
            Category = (int)account.Category,
            account.ParentAccountId,
            IsSystemAccount = account.IsSystemAccount ? 1 : 0,
            IsActive = account.IsActive ? 1 : 0,
            account.CurrentBalance
        });
    }

    public async Task<string> PostJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var tx = conn.BeginTransaction();

        try
        {
            // 1. Insert Header
            const string insertHeaderSql = @"
                INSERT INTO journal_entries (
                    id, org_id, branch_id, voucher_number, voucher_type, voucher_date,
                    narration, reference_id, reference_type, created_by_user_id, created_at
                ) VALUES (
                    @Id, @OrgId, @BranchId, @VoucherNumber, @VoucherType, @VoucherDate,
                    @Narration, @ReferenceId, @ReferenceType, @CreatedByUserId, datetime('now')
                );
            ";

            await conn.ExecuteAsync(insertHeaderSql, new
            {
                entry.Id,
                entry.OrgId,
                entry.BranchId,
                entry.VoucherNumber,
                VoucherType = (int)entry.VoucherType,
                VoucherDate = entry.VoucherDate.ToString("o"),
                entry.Narration,
                entry.ReferenceId,
                entry.ReferenceType,
                entry.CreatedByUserId
            }, tx);

            // 2. Insert Lines & Update Account Balances
            const string insertLineSql = @"
                INSERT INTO journal_lines (id, journal_entry_id, account_id, account_name, debit_amount, credit_amount, narration)
                VALUES (@Id, @JournalEntryId, @AccountId, @AccountName, @DebitAmount, @CreditAmount, @Narration);
            ";

            foreach (var line in entry.Lines)
            {
                await conn.ExecuteAsync(insertLineSql, new
                {
                    line.Id,
                    line.JournalEntryId,
                    line.AccountId,
                    line.AccountName,
                    line.DebitAmount,
                    line.CreditAmount,
                    line.Narration
                }, tx);

                // Update Account Current Balance based on category
                var category = await conn.ExecuteScalarAsync<int>(
                    "SELECT category FROM account_heads WHERE id = @accountId;",
                    new { accountId = line.AccountId }, tx);

                if (category == 1 || category == 5) // Asset or Expense: Debit increases, Credit decreases
                {
                    await conn.ExecuteAsync(@"
                        UPDATE account_heads 
                        SET current_balance = current_balance + (@debit - @credit)
                        WHERE id = @accountId;
                    ", new { debit = line.DebitAmount, credit = line.CreditAmount, accountId = line.AccountId }, tx);
                }
                else // Liability, Equity, Revenue: Credit increases, Debit decreases
                {
                    await conn.ExecuteAsync(@"
                        UPDATE account_heads 
                        SET current_balance = current_balance + (@credit - @debit)
                        WHERE id = @accountId;
                    ", new { debit = line.DebitAmount, credit = line.CreditAmount, accountId = line.AccountId }, tx);
                }
            }

            // 3. Outbox Event
            var payloadJson = JsonSerializer.Serialize(new
            {
                VoucherId = entry.Id,
                entry.VoucherNumber,
                VoucherType = entry.VoucherType.ToString(),
                entry.TotalDebit,
                entry.TotalCredit,
                entry.VoucherDate
            });

            var outboxEvent = Medistock.Domain.Common.OutboxEvent.Create(
                id: $"evt_{Guid.NewGuid():N}",
                aggregateType: "JournalEntry",
                aggregateId: entry.Id,
                eventType: "JOURNAL_POSTED",
                payloadJson: payloadJson,
                deviceId: Environment.MachineName,
                operationId: entry.Id
            );

            await _outboxRepository.EnqueueEventAsync(
                outboxEvent: outboxEvent,
                transaction: tx,
                cancellationToken: cancellationToken);

            tx.Commit();
            return entry.VoucherNumber;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<IReadOnlyList<DayBookVoucherDto>> GetDayBookAsync(string orgId, string branchId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                je.id,
                je.voucher_number,
                je.voucher_type,
                je.voucher_date,
                IFNULL(je.narration, '') AS narration,
                je.created_by_user_id,
                IFNULL((SELECT account_name FROM journal_lines WHERE journal_entry_id = je.id ORDER BY debit_amount DESC LIMIT 1), 'General Journal') AS primary_account,
                IFNULL((SELECT SUM(debit_amount) FROM journal_lines WHERE journal_entry_id = je.id), 0.0) AS total_amount
            FROM journal_entries je
            WHERE je.org_id = @orgId 
              AND (je.branch_id = @branchId OR @branchId = '' OR @branchId IS NULL)
              AND date(je.voucher_date) BETWEEN date(@start) AND date(@end)
            ORDER BY date(je.voucher_date) DESC, je.voucher_number DESC;
        ";

        var rows = await conn.QueryAsync<dynamic>(sql, new
        {
            orgId,
            branchId,
            start = startDate.ToString("yyyy-MM-dd"),
            end = endDate.ToString("yyyy-MM-dd")
        });

        return rows.Select(r => new DayBookVoucherDto(
            Id: (string)r.id,
            VoucherNumber: (string)r.voucher_number,
            VoucherType: (VoucherType)(long)r.voucher_type,
            VoucherDate: DateTime.Parse((string)r.voucher_date),
            Narration: (string)r.narration,
            PrimaryAccount: (string)r.primary_account,
            TotalAmount: Convert.ToDecimal(r.total_amount),
            CreatedByUserId: (string)r.created_by_user_id
        )).ToList();
    }

    public async Task<IReadOnlyList<AccountLedgerLineDto>> GetAccountLedgerAsync(string accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var account = await GetAccountHeadByIdAsync(accountId, cancellationToken);
        if (account == null) return Array.Empty<AccountLedgerLineDto>();

        // 1. Calculate Opening Balance before startDate
        const string openingSql = @"
            SELECT 
                CAST(IFNULL(SUM(jl.debit_amount), 0.0) AS REAL) AS total_debit,
                CAST(IFNULL(SUM(jl.credit_amount), 0.0) AS REAL) AS total_credit
            FROM journal_lines jl
            JOIN journal_entries je ON je.id = jl.journal_entry_id
            WHERE jl.account_id = @accountId AND date(je.voucher_date) < date(@start);
        ";

        var op = await conn.QueryFirstOrDefaultAsync<dynamic>(openingSql, new
        {
            accountId,
            start = startDate.ToString("yyyy-MM-dd")
        });

        decimal openingDebit = Convert.ToDecimal(op?.total_debit ?? 0);
        decimal openingCredit = Convert.ToDecimal(op?.total_credit ?? 0);
        decimal running = (account.Category == AccountCategory.Asset || account.Category == AccountCategory.Expense)
            ? (openingDebit - openingCredit)
            : (openingCredit - openingDebit);

        var list = new List<AccountLedgerLineDto>
        {
            new AccountLedgerLineDto(
                VoucherNumber: "OPENING",
                VoucherType: VoucherType.Journal,
                VoucherDate: startDate,
                Particulars: "Opening Balance",
                Narration: "Balance brought forward",
                DebitAmount: openingDebit,
                CreditAmount: openingCredit,
                RunningBalance: running
            )
        };

        // 2. Fetch lines within date range
        const string linesSql = @"
            SELECT 
                je.voucher_number,
                je.voucher_type,
                je.voucher_date,
                jl.account_name,
                jl.narration,
                CAST(jl.debit_amount AS REAL) AS debit_amount,
                CAST(jl.credit_amount AS REAL) AS credit_amount
            FROM journal_lines jl
            JOIN journal_entries je ON je.id = jl.journal_entry_id
            WHERE jl.account_id = @accountId AND date(je.voucher_date) BETWEEN date(@start) AND date(@end)
            ORDER BY date(je.voucher_date) ASC, je.voucher_number ASC;
        ";

        var rows = await conn.QueryAsync<dynamic>(linesSql, new
        {
            accountId,
            start = startDate.ToString("yyyy-MM-dd"),
            end = endDate.ToString("yyyy-MM-dd")
        });

        foreach (var r in rows)
        {
            decimal d = Convert.ToDecimal(r.debit_amount);
            decimal c = Convert.ToDecimal(r.credit_amount);

            if (account.Category == AccountCategory.Asset || account.Category == AccountCategory.Expense)
            {
                running += (d - c);
            }
            else
            {
                running += (c - d);
            }

            list.Add(new AccountLedgerLineDto(
                VoucherNumber: (string)r.voucher_number,
                VoucherType: (VoucherType)(long)r.voucher_type,
                VoucherDate: DateTime.Parse((string)r.voucher_date),
                Particulars: (string)r.account_name,
                Narration: (string?)r.narration,
                DebitAmount: d,
                CreditAmount: c,
                RunningBalance: running
            ));
        }

        return list;
    }

    public async Task<IReadOnlyList<TrialBalanceItemDto>> GetTrialBalanceAsync(string orgId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ah.id,
                ah.code,
                ah.name,
                ah.category,
                CAST(IFNULL(SUM(jl.debit_amount), 0.0) AS REAL) AS total_debit,
                CAST(IFNULL(SUM(jl.credit_amount), 0.0) AS REAL) AS total_credit
            FROM account_heads ah
            LEFT JOIN journal_lines jl ON jl.account_id = ah.id
            LEFT JOIN journal_entries je ON je.id = jl.journal_entry_id AND date(je.voucher_date) <= date(@asOf)
            WHERE ah.org_id = @orgId AND ah.is_active = 1
            GROUP BY ah.id, ah.code, ah.name, ah.category
            ORDER BY ah.code ASC;
        ";

        var rows = await conn.QueryAsync<dynamic>(sql, new
        {
            orgId,
            asOf = asOfDate.ToString("yyyy-MM-dd")
        });

        var results = new List<TrialBalanceItemDto>();

        foreach (var r in rows)
        {
            var cat = (AccountCategory)(long)r.category;
            decimal d = Convert.ToDecimal(r.total_debit);
            decimal c = Convert.ToDecimal(r.total_credit);

            decimal netDebit = 0m;
            decimal netCredit = 0m;

            if (cat == AccountCategory.Asset || cat == AccountCategory.Expense)
            {
                var net = d - c;
                if (net >= 0) netDebit = net;
                else netCredit = -net;
            }
            else
            {
                var net = c - d;
                if (net >= 0) netCredit = net;
                else netDebit = -net;
            }

            results.Add(new TrialBalanceItemDto(
                AccountId: (string)r.id,
                AccountCode: (string)r.code,
                AccountName: (string)r.name,
                Category: cat,
                DebitBalance: netDebit,
                CreditBalance: netCredit
            ));
        }

        return results;
    }

    public async Task<ProfitLossDto> GetProfitLossSummaryAsync(string orgId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var trialBalance = await GetTrialBalanceAsync(orgId, endDate, cancellationToken);

        var revenueAccounts = trialBalance.Where(a => a.Category == AccountCategory.Revenue).ToList();
        var expenseAccounts = trialBalance.Where(a => a.Category == AccountCategory.Expense).ToList();

        decimal totalRevenue = revenueAccounts.Sum(r => r.CreditBalance - r.DebitBalance);
        decimal purchases = expenseAccounts.Where(e => e.AccountCode == "5001").Sum(e => e.DebitBalance - e.CreditBalance);
        decimal otherExpenses = expenseAccounts.Where(e => e.AccountCode != "5001").Sum(e => e.DebitBalance - e.CreditBalance);

        decimal grossProfit = totalRevenue - purchases;
        decimal netProfit = grossProfit - otherExpenses;

        return new ProfitLossDto(
            TotalRevenue: Math.Max(0, totalRevenue),
            TotalPurchases: purchases,
            GrossProfit: grossProfit,
            TotalOperatingExpenses: otherExpenses,
            NetProfit: netProfit,
            RevenueAccounts: revenueAccounts,
            ExpenseAccounts: expenseAccounts
        );
    }
}
