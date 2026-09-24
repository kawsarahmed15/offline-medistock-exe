using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Inventory.DTOs;
using Medistock.Domain.Compliance;

namespace Medistock.Application.Compliance.Services;

public interface IScheduleDrugService
{
    Task<IReadOnlyList<ScheduleDrugRegisterDto>> GetRegisterAsync(
        ScheduleDrugFilter filter,
        CancellationToken cancellationToken = default);

    Task RecordDispenseLogAsync(
        IReadOnlyList<ScheduleDrugRegisterEntry> entries,
        CancellationToken cancellationToken = default);
}

public class ScheduleDrugService : IScheduleDrugService
{
    private readonly IScheduleDrugRepository _repository;

    public ScheduleDrugService(IScheduleDrugRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<ScheduleDrugRegisterDto>> GetRegisterAsync(
        ScheduleDrugFilter filter,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetScheduleRegisterAsync(filter, cancellationToken);
    }

    public Task RecordDispenseLogAsync(
        IReadOnlyList<ScheduleDrugRegisterEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0) return Task.CompletedTask;
        return _repository.RecordScheduleDrugEntriesAsync(entries, null, cancellationToken);
    }
}
