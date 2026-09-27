using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Sync;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Sync;

public interface ICloudSyncClient
{
    Task<SyncPushResponse> PushEventsAsync(SyncPushRequest request, CancellationToken cancellationToken = default);
    Task<SyncPullResponse> PullEventsAsync(SyncPullRequest request, CancellationToken cancellationToken = default);
}

public class CloudSyncClient : ICloudSyncClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudSyncClient>? _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CloudSyncClient(HttpClient httpClient, ILogger<CloudSyncClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    public async Task<SyncPushResponse> PushEventsAsync(SyncPushRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/sync/push", request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogWarning("Sync push returned non-success code {StatusCode}: {Error}", response.StatusCode, errorContent);
                return new SyncPushResponse
                {
                    Success = false,
                    ProcessedCount = 0,
                    FailedCount = request.Events.Count,
                    Results = request.Events.ConvertAll(e => new SyncItemResultDto
                    {
                        EventId = e.EventId,
                        IdempotencyKey = e.IdempotencyKey,
                        Status = SyncItemStatus.Failed,
                        ErrorMessage = $"HTTP {(int)response.StatusCode}: {errorContent}"
                    })
                };
            }

            var result = await response.Content.ReadFromJsonAsync<SyncPushResponse>(JsonOptions, cancellationToken);
            return result ?? new SyncPushResponse { Success = false };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to push sync events to server.");
            return new SyncPushResponse
            {
                Success = false,
                ProcessedCount = 0,
                FailedCount = request.Events.Count,
                Results = request.Events.ConvertAll(e => new SyncItemResultDto
                {
                    EventId = e.EventId,
                    IdempotencyKey = e.IdempotencyKey,
                    Status = SyncItemStatus.Failed,
                    ErrorMessage = ex.Message
                })
            };
        }
    }

    public async Task<SyncPullResponse> PullEventsAsync(SyncPullRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/sync/pull", request, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SyncPullResponse>(JsonOptions, cancellationToken);
            return result ?? new SyncPullResponse
            {
                OrgId = request.OrgId,
                BranchId = request.BranchId,
                LatestSequenceNumber = request.SinceSequenceNumber,
                HasMore = false
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to pull sync events from server.");
            throw;
        }
    }
}
