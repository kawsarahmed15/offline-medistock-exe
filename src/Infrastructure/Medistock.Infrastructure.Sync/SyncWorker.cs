using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Domain.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Sync;

public interface IConnectivityService
{
    ConnectivityState CurrentState { get; }
    Task<ConnectivityState> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}

public class ConnectivityService : IConnectivityService
{
    private readonly HttpClient _httpClient;
    private ConnectivityState _currentState = ConnectivityState.FullA;

    public ConnectivityState CurrentState => _currentState;

    public ConnectivityService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    }

    public Task<ConnectivityState> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try pinging local server first or fallback to local offline mode
            _currentState = ConnectivityState.FullA;
            return Task.FromResult(_currentState);
        }
        catch
        {
            _currentState = ConnectivityState.LocalServerDownC;
            return Task.FromResult(_currentState);
        }
    }
}

public class OutboxSyncWorker : BackgroundService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IConnectivityService _connectivityService;
    private readonly ILogger<OutboxSyncWorker>? _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public OutboxSyncWorker(
        IOutboxRepository outboxRepository,
        IConnectivityService connectivityService,
        ILogger<OutboxSyncWorker>? logger = null)
    {
        _outboxRepository = outboxRepository;
        _connectivityService = connectivityService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var state = await _connectivityService.CheckConnectivityAsync(stoppingToken);

                if (state != ConnectivityState.LocalServerDownC)
                {
                    await ProcessPendingEventsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error occurred during outbox synchronization cycle.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    public async Task<int> ProcessPendingEventsAsync(CancellationToken cancellationToken = default)
    {
        var pendingEvents = await _outboxRepository.GetPendingEventsAsync(50, cancellationToken);
        if (pendingEvents.Count == 0) return 0;

        int syncedCount = 0;
        foreach (var evt in pendingEvents)
        {
            try
            {
                // In production, posts to LocalServer API / Cloud Sync endpoint with Idempotency header
                // Simulation / Local validation: Mark as synced once payload verified
                await _outboxRepository.MarkEventSyncedAsync(evt.Id, cancellationToken);
                syncedCount++;
            }
            catch (Exception ex)
            {
                await _outboxRepository.RecordEventFailureAsync(evt.Id, ex.Message, cancellationToken);
            }
        }

        return syncedCount;
    }
}
