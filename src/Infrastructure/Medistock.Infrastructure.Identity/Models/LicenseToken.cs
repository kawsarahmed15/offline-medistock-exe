using System;

namespace Medistock.Infrastructure.Identity.Models;

/// <summary>
/// Represents a validated and decoded license token for this device.
/// </summary>
public record LicenseToken
{
    public required string OrgId { get; init; }
    public required string OrgName { get; init; }
    public required string UserId { get; init; }
    public required string UserEmail { get; init; }
    public required string DeviceId { get; init; }
    public required string Plan { get; init; }       // "pharmacy_starter" | "pharmacy_pro"
    public required DateTime IssuedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string RawJwt { get; init; }

    /// <summary>True if the token is within its primary validity window.</summary>
    public bool IsValid => DateTime.UtcNow <= ExpiresAt;

    /// <summary>True if expired but within the 30-day offline grace period.</summary>
    public bool IsInGracePeriod =>
        !IsValid && DateTime.UtcNow <= ExpiresAt.AddDays(30);
}
