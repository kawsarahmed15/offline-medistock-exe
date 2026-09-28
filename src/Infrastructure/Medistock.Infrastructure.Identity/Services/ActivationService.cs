using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Auth;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Identity.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Medistock.Infrastructure.Identity.Services;

/// <summary>
/// Handles all license activation logic for the desktop client.
/// - Offline check: validates the locally stored JWT signature using the embedded public key
/// - Online activation: calls /api/v1/auth/activate and stores the returned JWT in Windows Credential Manager
/// - Grace period: 30-day offline tolerance after token expiry
/// </summary>
public class ActivationService : IActivationService
{
    // ─── EMBEDDED RSA PUBLIC KEY ──────────────────────────────────────────────
    // This key matches the private key stored ONLY on your CloudApi server.
    // Replace the content with your actual RSA-2048 public key (PEM format).
    // Generate: openssl genrsa -out private.pem 2048 && openssl rsa -in private.pem -pubout -out public.pem
    //
    // IMPORTANT: Keep the private key secret on your server. The public key here
    // is safe to embed — it can only verify tokens, not issue them.
    private const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0PLACEHOLDER0000000000
        00000000000000000000000000000000000000000000000000000000000000000000
        00000000000000000000000000000000000000000000000000000000000000000000
        00000000000000000000PLACEHOLDER0PLACEHOLDER0000000000000000000000000
        00000000000000000000000000000000000000000000000000000000000000000000
        0000000000000000000000000000000000000000000000000000000000000AQAB
        -----END PUBLIC KEY-----
        """;

    private const string CredentialVaultResource = "MedistockLicense_v1";
    private const string CredentialVaultUsername = "license_jwt";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private LicenseToken? _cachedToken;

    public ActivationService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public LicenseToken? GetCurrentToken() => _cachedToken;

    /// <inheritdoc/>
    public string GetOrCreateDeviceId()
    {
        var path = MedistockPaths.ActivationMetadataFile;
        if (File.Exists(path))
        {
            try
            {
                var meta = JsonSerializer.Deserialize<ActivationMetadata>(File.ReadAllText(path), JsonOpts);
                if (!string.IsNullOrWhiteSpace(meta?.DeviceId))
                    return meta.DeviceId;
            }
            catch { /* fall through to generate */ }
        }

        // Generate a stable device GUID — save it immediately so it's the same on next call
        var deviceId = Guid.NewGuid().ToString("N");
        try
        {
            MedistockPaths.EnsureAllDirectoriesExist();
            var stub = new ActivationMetadata
            {
                DeviceId = deviceId,
                OrgId = string.Empty,
                OrgName = string.Empty,
                ActivatedAt = string.Empty,
                AppVersion = string.Empty
            };
            File.WriteAllText(path, JsonSerializer.Serialize(stub, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* non-fatal — the ID will be regenerated on next launch if save failed */ }

        return deviceId;
    }

    /// <inheritdoc/>
    public Task<ActivationStatus> CheckActivationStatusAsync(CancellationToken ct = default)
    {
        var rawJwt = ReadFromCredentialVault();
        if (rawJwt == null) return Task.FromResult(ActivationStatus.NotActivated);

        var token = ValidateJwtLocally(rawJwt);
        if (token == null) return Task.FromResult(ActivationStatus.NotActivated);

        _cachedToken = token;

        if (token.IsValid)       return Task.FromResult(ActivationStatus.Activated);
        if (token.IsInGracePeriod) return Task.FromResult(ActivationStatus.GracePeriod);
        return Task.FromResult(ActivationStatus.Expired);
    }

    /// <inheritdoc/>
    public async Task<ActivationResult> ActivateAsync(string email, string password, CancellationToken ct = default)
    {
        var deviceId = GetOrCreateDeviceId();

        var request = new ActivationRequest
        {
            Email = email,
            Password = password,
            DeviceId = deviceId,
            DeviceFingerprint = ComputeDeviceFingerprint(),
            DeviceName = Environment.MachineName,
            AppVersion = GetAppVersion()
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/activate", request, JsonOpts, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var msg = TryExtractError(body) ?? $"Server returned {(int)response.StatusCode}";
                return new ActivationResult(false, msg, null);
            }

            var result = await response.Content.ReadFromJsonAsync<ActivationResponse>(JsonOpts, ct);
            if (result?.LicenseJwt == null)
                return new ActivationResult(false, "Invalid response from activation server", null);

            // Validate the returned JWT locally before trusting it
            var token = ValidateJwtLocally(result.LicenseJwt);
            if (token == null)
                return new ActivationResult(false, "License token failed local validation", null);

            // Persist to Windows Credential Manager
            StoreInCredentialVault(result.LicenseJwt);

            // Persist metadata to activation.json
            var meta = new ActivationMetadata
            {
                DeviceId = deviceId,
                OrgId = token.OrgId,
                OrgName = result.OrgName,
                ActivatedAt = DateTime.UtcNow.ToString("O"),
                AppVersion = GetAppVersion()
            };
            File.WriteAllText(MedistockPaths.ActivationMetadataFile,
                JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

            _cachedToken = token;
            return new ActivationResult(true, null, token);
        }
        catch (HttpRequestException)
        {
            return new ActivationResult(false,
                "Cannot connect to Medistock servers. Please check your internet connection.", null);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return new ActivationResult(false, "Activation was cancelled.", null);
        }
        catch (Exception ex)
        {
            return new ActivationResult(false, $"Unexpected error: {ex.Message}", null);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CheckOnlineLicenseStatusAsync(CancellationToken ct = default)
    {
        var rawJwt = ReadFromCredentialVault();
        if (rawJwt == null) return false;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/license/status");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rawJwt);
            using var response = await _httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return true; // Network error: keep grace period, don't revoke

            var result = await response.Content.ReadFromJsonAsync<LicenseStatusResponse>(JsonOpts, ct);
            return result?.IsValid ?? true;
        }
        catch
        {
            return true; // Offline or error: don't revoke
        }
    }

    /// <inheritdoc/>
    public Task DeactivateAsync(CancellationToken ct = default)
    {
        try
        {
            var vault = new Windows.Security.Credentials.PasswordVault();
            try
            {
                var cred = vault.Retrieve(CredentialVaultResource, CredentialVaultUsername);
                vault.Remove(cred);
            }
            catch { /* credential may not exist */ }

            if (File.Exists(MedistockPaths.ActivationMetadataFile))
                File.Delete(MedistockPaths.ActivationMetadataFile);

            _cachedToken = null;
        }
        catch { /* best-effort cleanup */ }

        return Task.CompletedTask;
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────

    private LicenseToken? ValidateJwtLocally(string rawJwt)
    {
        try
        {
            // In development mode (when public key is the placeholder), bypass signature validation
            // Remove this block when you replace the placeholder with your real key
            if (PublicKeyPem.Contains("PLACEHOLDER"))
            {
                return ParseJwtUnsafe(rawJwt);
            }

            var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);
            var securityKey = new RsaSecurityKey(rsa);

            var handler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,
                ValidateIssuer = false,    // Issuer validation optional — add if you set iss claim
                ValidateAudience = false,  // Audience validation optional
                ValidateLifetime = false,  // We handle expiry ourselves (grace period logic)
                ClockSkew = TimeSpan.Zero
            };

            var principal = handler.ValidateToken(rawJwt, validationParams, out var validatedToken);
            return ExtractLicenseToken(principal, rawJwt);
        }
        catch
        {
            return null; // Invalid signature or malformed token
        }
    }

    /// <summary>
    /// Used only when PublicKeyPem is the placeholder (dev mode).
    /// DO NOT USE IN PRODUCTION — this skips signature verification!
    /// </summary>
    private static LicenseToken? ParseJwtUnsafe(string rawJwt)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(rawJwt)) return null;
            var token = handler.ReadJwtToken(rawJwt);

            return new LicenseToken
            {
                OrgId = token.Claims.FirstOrDefault(c => c.Type == "org_id")?.Value ?? "DEV-ORG",
                OrgName = token.Claims.FirstOrDefault(c => c.Type == "org_name")?.Value ?? "Development",
                UserId = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "dev-user",
                UserEmail = token.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "dev@medistock.local",
                DeviceId = token.Claims.FirstOrDefault(c => c.Type == "device_id")?.Value ?? "dev-device",
                Plan = token.Claims.FirstOrDefault(c => c.Type == "plan")?.Value ?? "pharmacy_pro",
                IssuedAt = token.IssuedAt,
                ExpiresAt = token.ValidTo,
                RawJwt = rawJwt
            };
        }
        catch { return null; }
    }

    private static LicenseToken? ExtractLicenseToken(ClaimsPrincipal principal, string rawJwt)
    {
        try
        {
            var expUnix = long.Parse(principal.FindFirst("exp")?.Value ?? "0");
            var iatUnix = long.Parse(principal.FindFirst("iat")?.Value ?? "0");

            return new LicenseToken
            {
                OrgId = principal.FindFirst("org_id")?.Value ?? string.Empty,
                OrgName = principal.FindFirst("org_name")?.Value ?? string.Empty,
                UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value ?? string.Empty,
                UserEmail = principal.FindFirst(ClaimTypes.Email)?.Value
                         ?? principal.FindFirst("email")?.Value ?? string.Empty,
                DeviceId = principal.FindFirst("device_id")?.Value ?? string.Empty,
                Plan = principal.FindFirst("plan")?.Value ?? string.Empty,
                IssuedAt = DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime,
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime,
                RawJwt = rawJwt
            };
        }
        catch { return null; }
    }

    private static string? ReadFromCredentialVault()
    {
        try
        {
            var vault = new Windows.Security.Credentials.PasswordVault();
            var cred = vault.Retrieve(CredentialVaultResource, CredentialVaultUsername);
            cred.RetrievePassword();
            return cred.Password;
        }
        catch { return null; }
    }

    private static void StoreInCredentialVault(string jwt)
    {
        var vault = new Windows.Security.Credentials.PasswordVault();
        // Remove any existing credential first to avoid duplicates
        try
        {
            var existing = vault.Retrieve(CredentialVaultResource, CredentialVaultUsername);
            vault.Remove(existing);
        }
        catch { /* didn't exist */ }

        vault.Add(new Windows.Security.Credentials.PasswordCredential(
            CredentialVaultResource, CredentialVaultUsername, jwt));
    }

    private static string ComputeDeviceFingerprint()
    {
        // Simple fingerprint using machine name + processor count
        // For production, augment with WMI CPU/Motherboard serial (needs Windows-specific API)
        var raw = $"{Environment.MachineName}|{Environment.ProcessorCount}|{Environment.OSVersion}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static string GetAppVersion()
    {
        return System.Reflection.Assembly.GetEntryAssembly()
            ?.GetName().Version?.ToString() ?? "1.0.0";
    }

    private static string? TryExtractError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var e)) return e.GetString();
            if (doc.RootElement.TryGetProperty("Error", out var E)) return E.GetString();
            if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString();
        }
        catch { }
        return null;
    }
}
