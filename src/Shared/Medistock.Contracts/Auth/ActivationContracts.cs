using System;

namespace Medistock.Contracts.Auth;

/// <summary>First-time activation request sent to /api/v1/auth/activate</summary>
public record ActivationRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string DeviceId { get; init; }
    public required string DeviceFingerprint { get; init; }
    public required string DeviceName { get; init; }
    public required string AppVersion { get; init; }
}

/// <summary>Activation response containing a signed license JWT</summary>
public record ActivationResponse
{
    public required string LicenseJwt { get; init; }
    public required string OrgName { get; init; }
    public required string Plan { get; init; }
}

/// <summary>Background license status check — used to detect server-side revocation</summary>
public record LicenseStatusResponse
{
    public required bool IsValid { get; init; }
    public string? RevokeReason { get; init; }
}

/// <summary>Metadata persisted to activation.json (non-sensitive)</summary>
public record ActivationMetadata
{
    public required string DeviceId { get; init; }
    public required string OrgId { get; init; }
    public required string OrgName { get; init; }
    public required string ActivatedAt { get; init; }
    public required string AppVersion { get; init; }
}
