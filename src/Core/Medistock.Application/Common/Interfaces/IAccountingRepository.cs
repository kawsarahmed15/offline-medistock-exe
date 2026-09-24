using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Accounting.DTOs;
using Medistock.Domain.Accounting;

namespace Medistock.Application.Common.Interfaces;

public interface IAccountingRepository
{
    Task<IReadOnlyList<AccountHead>> GetAccountHeadsAsync(string orgId, CancellationToken cancellationToken = default);
    Task<AccountHead?> GetAccountHeadByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AccountHead?> GetAccountHeadByCodeAsync(string orgId, string code, CancellationToken cancellationToken = default);
    Task CreateAccountHeadAsync(AccountHead account, CancellationToken cancellationToken = default);
    Task<string> PostJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DayBookVoucherDto>> GetDayBookAsync(string orgId, string branchId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountLedgerLineDto>> GetAccountLedgerAsync(string accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrialBalanceItemDto>> GetTrialBalanceAsync(string orgId, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task<ProfitLossDto> GetProfitLossSummaryAsync(string orgId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
