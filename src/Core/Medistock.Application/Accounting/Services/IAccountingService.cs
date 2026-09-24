using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Accounting.DTOs;
using Medistock.Domain.Accounting;

namespace Medistock.Application.Accounting.Services;

public interface IAccountingService
{
    Task<IReadOnlyList<AccountHeadDto>> GetChartOfAccountsAsync(string orgId, CancellationToken cancellationToken = default);
    Task<string> PostJournalEntryAsync(PostJournalEntryCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DayBookVoucherDto>> GetDayBookAsync(string orgId, string branchId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountLedgerLineDto>> GetAccountLedgerAsync(string accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrialBalanceItemDto>> GetTrialBalanceAsync(string orgId, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task<ProfitLossDto> GetProfitLossStatementAsync(string orgId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<string> CreateReceiptVoucherAsync(CreateReceiptVoucherCommand command, CancellationToken cancellationToken = default);
    Task<string> CreatePaymentVoucherAsync(CreatePaymentVoucherCommand command, CancellationToken cancellationToken = default);
    Task<string> CreateExpenseVoucherAsync(CreateExpenseVoucherCommand command, CancellationToken cancellationToken = default);
}
