using System.Threading;
using System.Threading.Tasks;
using Medistock.Infrastructure.Identity.Models;

namespace Medistock.Infrastructure.Identity.Services;

/// <summary>
/// Describes the current activation status of this device.
/// </summary>
public enum ActivationStatus
{
    /// <summary>Token valid and not expired. App is fully unlocked.</summary>
    Activated,

    /// <summary>No license token found at all. Must activate online.</summary>
    NotActivated,

    /// <summary>Token expired AND past the 30-day offline grace period. Must reconnect.</summary>
    Expired,

    /// <summary>Token technically expired but within the 30-day offline grace window. Allow usage, show warning.</summary>
    GracePeriod,

    /// <summary>Server has revoked this device's license. Block immediately.</summary>
    Revoked
}

public record ActivationResult(bool Success, string? ErrorMessage, LicenseToken? Token);

public interface IActivationService
{
    /// <summary>
    /// Checks the stored license token and returns the current activation status.
    /// This is an offline check (no network call) using local JWT signature validation.
    /// </summary>
    Task<ActivationStatus> CheckActivationStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Performs online activation: validates credentials with the server and stores the issued license JWT.
    /// </summary>
    Task<ActivationResult> ActivateAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// Checks with the server whether the stored license is still valid (revocation check).
    /// Called periodically in the background. Returns false if the server reports revocation.
    /// </summary>
    Task<bool> CheckOnlineLicenseStatusAsync(CancellationToken ct = default);

    /// <summary>Removes the stored license token and activation metadata from this device.</summary>
    Task DeactivateAsync(CancellationToken ct = default);

    /// <summary>Returns the currently cached license token, or null if not activated.</summary>
    LicenseToken? GetCurrentToken();

    /// <summary>Gets the device ID for this installation (generates and persists one on first call).</summary>
    string GetOrCreateDeviceId();
}
