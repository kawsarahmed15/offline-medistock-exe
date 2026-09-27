using System;

namespace Medistock.Contracts.Auth;

public record DeviceRegistrationRequest
{
    public required string OrgId { get; init; }
    public required string BranchId { get; init; }
    public required string DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public required string DeviceType { get; init; } // POS_COUNTER, MANAGER_WORKSTATION, MOBILE_SCANNER
    public required string AppVersion { get; init; }
}

public record DeviceRegistrationResponse
{
    public required string DeviceToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required string OrgName { get; init; }
    public required string BranchName { get; init; }
    public required string ServerTimeUtc { get; init; }
}

public record UserLoginRequest
{
    public required string OrgCode { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string DeviceId { get; init; }
}

public record UserLoginResponse
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required string UserId { get; init; }
    public required string FullName { get; init; }
    public required string Role { get; init; } // Owner, Pharmacist, Cashier, Accountant
    public required string OrgId { get; init; }
    public required string BranchId { get; init; }
}
