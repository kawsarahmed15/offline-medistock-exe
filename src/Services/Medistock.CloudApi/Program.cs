using System;
using Medistock.Application;
using Medistock.Application.B2B.Services;
using Medistock.Application.Sync;
using Medistock.Contracts.Auth;
using Medistock.Contracts.B2B;
using Medistock.Contracts.Sync;
using Medistock.Contracts.Updates;
using Medistock.Contracts.Backup;
using Medistock.Domain.B2B;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Data.Migrations;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructureData();

// ─── JWT signing key for development ─────────────────────────────────────────
// PRODUCTION: Replace with RSA key pair — use RS256 and embed the public key in the desktop app.
// Generate: openssl genrsa -out private.pem 2048 && openssl rsa -in private.pem -pubout -out public.pem
// For now, using HMAC-SHA256 with a secret key (symmetric) for development simplicity.
//
// In production: load private key from environment / Azure Key Vault / HSM
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? "MEDISTOCK_DEV_SECRET_CHANGE_IN_PRODUCTION_MIN_32_CHARS";

var app = builder.Build();

// Run automated startup migrations
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    await migrator.MigrateAsync();
}

app.UseHttpsRedirection();

// ─────────────────────────────────────────────────────────────────────────────
// 1. Health & Status
// ─────────────────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Medistock.CloudApi",
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0-LTS"
}));

// ─────────────────────────────────────────────────────────────────────────────
// 2. Activation & Licensing
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Activate a device. Validates credentials and returns a signed license JWT.
///
/// DEVELOPMENT MODE: Accepts any non-empty email/password and issues a 30-day token.
/// PRODUCTION: Replace with real credential DB lookup and RSA-signed JWT.
/// </summary>
app.MapPost("/api/v1/auth/activate", ([FromBody] ActivationRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { Error = "Email and password are required." });

    // ── DEVELOPMENT MODE ── accept any credentials ────────────────────────────
    // TODO: Replace with real credential validation:
    //   var user = await db.ValidateCredentialsAsync(req.Email, req.Password);
    //   if (user == null) return Results.Unauthorized();
    // ─────────────────────────────────────────────────────────────────────────

    var orgId = $"ORG-{req.Email.GetHashCode():X8}";
    var orgName = "Development Pharmacy";
    var plan = "pharmacy_pro";

    // Issue license JWT (30 days)
    var now = DateTime.UtcNow;
    var expiry = now.AddDays(30);

    var claims = new[]
    {
        new Claim("sub", req.Email),
        new Claim("email", req.Email),
        new Claim("org_id", orgId),
        new Claim("org_name", orgName),
        new Claim("device_id", req.DeviceId),
        new Claim("plan", plan),
        new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        new Claim("exp", new DateTimeOffset(expiry).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(claims: claims, signingCredentials: creds,
        notBefore: now, expires: expiry);

    var jwt = new JwtSecurityTokenHandler().WriteToken(token);

    var response = new ActivationResponse
    {
        LicenseJwt = jwt,
        OrgName = orgName,
        Plan = plan
    };
    return Results.Ok(response);
});

/// <summary>
/// Check if a license is still valid (revocation check).
/// Called periodically by the desktop app in the background.
/// </summary>
app.MapGet("/api/v1/auth/license/status", ([FromHeader(Name = "Authorization")] string? authHeader) =>
{
    // DEVELOPMENT MODE: all tokens are valid
    // PRODUCTION: parse the JWT, look up device_id in revocation list
    var isValid = !string.IsNullOrWhiteSpace(authHeader);
    return Results.Ok(new LicenseStatusResponse { IsValid = isValid });
});

/// <summary>
/// Legacy login endpoint (kept for backward compat with existing sync infrastructure).
/// </summary>
app.MapPost("/api/v1/auth/login", ([FromBody] UserLoginRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { Error = "Username and password are required." });

    var token = $"AT_{Guid.NewGuid():N}";
    var response = new UserLoginResponse
    {
        AccessToken = token,
        RefreshToken = $"RT_{Guid.NewGuid():N}",
        ExpiresAtUtc = DateTime.UtcNow.AddHours(12),
        UserId = "USR-001",
        FullName = "Lead Pharmacist",
        Role = "Pharmacist",
        OrgId = string.IsNullOrWhiteSpace(req.OrgCode) ? "ORG-001" : req.OrgCode,
        BranchId = "BR-MAIN"
    };
    return Results.Ok(response);
});

/// <summary>Device registration (existing endpoint — kept for compat).</summary>
app.MapPost("/api/v1/auth/register-device", ([FromBody] DeviceRegistrationRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.DeviceId) || string.IsNullOrWhiteSpace(req.OrgId))
        return Results.BadRequest(new { Error = "DeviceId and OrgId are required." });

    var token = $"DVTKN_{req.OrgId}_{req.BranchId}_{req.DeviceId}_{Guid.NewGuid():N}";
    var response = new DeviceRegistrationResponse
    {
        DeviceToken = token,
        ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
        OrgName = "Medistock Central Network",
        BranchName = req.BranchId,
        ServerTimeUtc = DateTime.UtcNow.ToString("o")
    };
    return Results.Ok(response);
});

// 3. Outbox Synchronization Hub (Push & Pull)
app.MapPost("/api/v1/sync/push", async (
    [FromBody] SyncPushRequest req,
    [FromHeader(Name = "X-Org-Id")] string? headerOrgId,
    [FromServices] ISyncEngineService syncEngine) =>
{
    var effectiveOrgId = !string.IsNullOrWhiteSpace(headerOrgId) ? headerOrgId : req.OrgId;
    var result = await syncEngine.ProcessPushBatchAsync(req, effectiveOrgId);
    return Results.Ok(result);
});

app.MapPost("/api/v1/sync/pull", async (
    [FromBody] SyncPullRequest req,
    [FromHeader(Name = "X-Org-Id")] string? headerOrgId,
    [FromServices] ISyncEngineService syncEngine) =>
{
    var effectiveOrgId = !string.IsNullOrWhiteSpace(headerOrgId) ? headerOrgId : req.OrgId;
    var result = await syncEngine.ProcessPullBatchAsync(req, effectiveOrgId);
    return Results.Ok(result);
});

// 4. B2B Wholesaler Commerce API
app.MapGet("/api/v1/b2b/wholesalers", async ([FromServices] IB2bCommerceService b2bService) =>
{
    var wholesalers = await b2bService.GetWholesalersAsync();
    return Results.Ok(wholesalers);
});

app.MapGet("/api/v1/b2b/catalog", async (
    [FromQuery] string? wholesalerId,
    [FromQuery] string? query,
    [FromServices] IB2bCommerceService b2bService) =>
{
    var items = await b2bService.SearchCatalogAsync(wholesalerId, query);
    return Results.Ok(items);
});

app.MapGet("/api/v1/b2b/orders", async (
    [FromHeader(Name = "X-Org-Id")] string? orgId,
    [FromQuery] string? branchId,
    [FromQuery] string? status,
    [FromServices] IB2bCommerceService b2bService) =>
{
    var effectiveOrgId = orgId ?? "ORG-001";
    B2bOrderStatus? filterStatus = Enum.TryParse<B2bOrderStatus>(status, true, out var s) ? s : null;
    var orders = await b2bService.GetOrdersAsync(effectiveOrgId, branchId ?? "BR-MAIN", filterStatus);
    return Results.Ok(orders);
});

app.MapPost("/api/v1/b2b/orders", async (
    [FromBody] CreateB2bOrderRequest req,
    [FromHeader(Name = "X-Org-Id")] string? orgId,
    [FromHeader(Name = "X-Branch-Id")] string? branchId,
    [FromServices] IB2bCommerceService b2bService) =>
{
    var effectiveOrgId = orgId ?? "ORG-001";
    var effectiveBranchId = branchId ?? "BR-MAIN";
    var summary = await b2bService.CreateAndSubmitOrderAsync(req, effectiveOrgId, effectiveBranchId, "USR-001", "API-01");
    return Results.Created($"/api/v1/b2b/orders/{summary.OrderId}", summary);
});

app.MapPost("/api/v1/b2b/orders/{orderId}/status", async (
    string orderId,
    [FromQuery] string status,
    [FromQuery] string? trackingNumber,
    [FromServices] IB2bCommerceService b2bService) =>
{
    if (!Enum.TryParse<B2bOrderStatus>(status, true, out var newStatus))
    {
        return Results.BadRequest(new { Error = $"Invalid status {status}" });
    }

    await b2bService.UpdateOrderStatusAsync(orderId, newStatus, trackingNumber);
    return Results.Ok(new { Success = true, OrderId = orderId, Status = newStatus.ToString() });
});

app.MapPost("/api/v1/b2b/orders/{orderId}/receive", async (
    string orderId,
    [FromQuery] string? warehouseId,
    [FromServices] IB2bCommerceService b2bService) =>
{
    var result = await b2bService.ReceiveOrderAndConvertToPurchaseInvoiceAsync(
        orderId,
        warehouseId ?? "WH-MAIN",
        "USR-001",
        "API-01");

    return Results.Ok(result);
});

// 5. Update Distribution Endpoints & Storage Hub
var updatesStorageDir = Path.Combine(AppContext.BaseDirectory, "Storage", "Updates");
Directory.CreateDirectory(updatesStorageDir);

var publishedUpdates = new System.Collections.Concurrent.ConcurrentDictionary<string, PublishUpdateRequest>();

app.MapPost("/api/v1/updates/check", ([FromBody] UpdateCheckRequest req) =>
{
    var latest = publishedUpdates.Values
        .Where(u => string.Equals(u.Channel, req.Channel, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(u => Version.TryParse(u.Version, out var v) ? v : new Version(1, 0, 0))
        .FirstOrDefault();

    if (latest != null && Version.TryParse(latest.Version, out var latestVer) &&
        Version.TryParse(req.CurrentVersion, out var currentVer) && latestVer > currentVer)
    {
        return Results.Ok(new UpdateCheckResponse
        {
            UpdateAvailable = true,
            Version = latest.Version,
            IsMandatory = latest.IsMandatory,
            DownloadUrl = latest.DownloadUrl,
            Sha256Hash = latest.Sha256Hash,
            SizeBytes = latest.SizeBytes,
            ReleaseNotes = latest.ReleaseNotes,
            MinVersionRequired = latest.MinVersionRequired
        });
    }

    return Results.Ok(new UpdateCheckResponse
    {
        UpdateAvailable = false
    });
});

app.MapGet("/api/v1/updates/download/{fileName}", (string fileName) =>
{
    var safeFile = Path.GetFileName(fileName);
    var filePath = Path.Combine(updatesStorageDir, safeFile);
    if (!File.Exists(filePath))
    {
        return Results.NotFound(new { Error = "Update package file not found." });
    }

    return Results.File(filePath, "application/octet-stream", safeFile);
});

app.MapPost("/api/v1/admin/updates/upload", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { Error = "Expected multipart form content." });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("updateFile");
    var version = form["version"].ToString().Trim();
    var channel = string.IsNullOrWhiteSpace(form["channel"]) ? "stable" : form["channel"].ToString().Trim();
    var isMandatory = form["isMandatory"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
    var releaseNotes = form["releaseNotes"].ToString();

    if (file == null || file.Length == 0)
        return Results.BadRequest(new { Error = "Installer .exe file is required." });

    if (string.IsNullOrWhiteSpace(version))
        return Results.BadRequest(new { Error = "Version number (e.g. 1.0.1) is required." });

    var fileName = $"Medistock-Setup-v{version.Replace('.', '_')}.exe";
    var targetPath = Path.Combine(updatesStorageDir, fileName);

    await using (var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
    {
        await file.CopyToAsync(stream);
    }

    // Compute SHA-256
    string sha256;
    await using (var readStream = File.OpenRead(targetPath))
    {
        var hashBytes = await System.Security.Cryptography.SHA256.HashDataAsync(readStream);
        sha256 = Convert.ToHexString(hashBytes);
    }

    var scheme = request.Scheme;
    var host = request.Host.Value;
    var downloadUrl = $"{scheme}://{host}/api/v1/updates/download/{fileName}";

    var updateInfo = new PublishUpdateRequest
    {
        Version = version,
        Channel = channel,
        IsMandatory = isMandatory,
        DownloadUrl = downloadUrl,
        Sha256Hash = sha256,
        SizeBytes = file.Length,
        ReleaseNotes = releaseNotes
    };

    publishedUpdates[version] = updateInfo;

    return Results.Ok(new
    {
        Success = true,
        Message = $"Update v{version} published successfully! All pharmacy clients will now download and install it.",
        Update = updateInfo
    });
});

app.MapPost("/api/v1/admin/updates/publish", ([FromBody] PublishUpdateRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Version) || string.IsNullOrWhiteSpace(req.DownloadUrl))
    {
        return Results.BadRequest(new { Error = "Version and DownloadUrl are required." });
    }

    publishedUpdates[req.Version] = req;
    return Results.Ok(new { Success = true, Message = $"Version {req.Version} published successfully." });
});

// Admin Web Console for Push Updates
app.MapGet("/admin/updates", () => Results.Content(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Medistock Cloud - Release Update Hub</title>
    <style>
        :root { --bg: #0f172a; --card: #1e293b; --accent: #22c55e; --text: #f8fafc; --muted: #94a3b8; --border: #334155; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: var(--bg); color: var(--text); padding: 30px 20px; display: flex; justify-content: center; }
        .container { max-width: 680px; width: 100%; background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 28px; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5); }
        h1 { margin: 0 0 8px 0; font-size: 22px; color: #fff; display: flex; align-items: center; gap: 10px; }
        p.subtitle { color: var(--muted); font-size: 13px; margin: 0 0 24px 0; }
        .form-group { margin-bottom: 18px; }
        label { display: block; font-size: 13px; font-weight: 600; margin-bottom: 6px; color: #e2e8f0; }
        input[type=""text""], input[type=""file""], textarea, select { width: 100%; box-sizing: border-box; padding: 10px 12px; background: #0f172a; border: 1px solid var(--border); border-radius: 6px; color: #fff; font-size: 13px; }
        input[type=""file""] { padding: 8px; border-style: dashed; }
        textarea { height: 80px; resize: vertical; }
        .checkbox-group { display: flex; align-items: center; gap: 8px; margin-top: 6px; }
        button { background: var(--accent); color: #000; border: none; padding: 12px 20px; border-radius: 6px; font-weight: 700; font-size: 14px; cursor: pointer; width: 100%; transition: opacity 0.2s; margin-top: 10px; }
        button:hover { opacity: 0.9; }
        #status { margin-top: 20px; padding: 12px; border-radius: 6px; display: none; font-size: 13px; font-weight: 600; line-height: 1.5; }
        .status-success { background: rgba(34, 197, 94, 0.15); border: 1px solid #22c55e; color: #4ade80; }
        .status-error { background: rgba(239, 68, 68, 0.15); border: 1px solid #ef4444; color: #f87171; }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>🚀 Medistock Update Release Center</h1>
        <p class=""subtitle"">Upload a new build (.exe installer) and hit the button to push the update to all pharmacy terminals instantly.</p>
        
        <form id=""uploadForm"">
            <div class=""form-group"">
                <label>Select Installer Executable (.exe)</label>
                <input type=""file"" id=""updateFile"" name=""updateFile"" accept="".exe"" required>
            </div>
            
            <div class=""form-group"">
                <label>New Version Number (e.g. 1.0.1, 1.1.0)</label>
                <input type=""text"" id=""version"" name=""version"" placeholder=""1.0.1"" required>
            </div>

            <div class=""form-group"">
                <label>Release Channel</label>
                <select id=""channel"" name=""channel"">
                    <option value=""stable"" selected>Stable (All Pharmacies)</option>
                    <option value=""beta"">Beta (Preview Testers)</option>
                </select>
            </div>

            <div class=""form-group"">
                <label>Release Notes / Changelog</label>
                <textarea id=""releaseNotes"" name=""releaseNotes"" placeholder=""• Added automatic 7-day backup pruning&#10;• Enhanced billing & GST speed&#10;• Bug fixes""></textarea>
            </div>

            <div class=""form-group"">
                <div class=""checkbox-group"">
                    <input type=""checkbox"" id=""isMandatory"" name=""isMandatory"">
                    <label for=""isMandatory"" style=""margin-bottom:0; font-weight: normal;"">Mandatory Update (Pharmacies must apply immediately)</label>
                </div>
            </div>

            <button type=""submit"" id=""submitBtn"">🚀 Push Update to All Pharmacies</button>
        </form>

        <div id=""status""></div>
    </div>

    <script>
        document.getElementById('uploadForm').addEventListener('submit', async function(e) {
            e.preventDefault();
            const btn = document.getElementById('submitBtn');
            const status = document.getElementById('status');
            btn.disabled = true;
            btn.innerText = '⏳ Uploading & Publishing Update...';
            status.style.display = 'none';

            const formData = new FormData();
            formData.append('updateFile', document.getElementById('updateFile').files[0]);
            formData.append('version', document.getElementById('version').value);
            formData.append('channel', document.getElementById('channel').value);
            formData.append('releaseNotes', document.getElementById('releaseNotes').value);
            formData.append('isMandatory', document.getElementById('isMandatory').checked);

            try {
                const res = await fetch('/api/v1/admin/updates/upload', {
                    method: 'POST',
                    body: formData
                });
                const data = await res.json();
                if (res.ok && data.Success) {
                    status.className = 'status-success';
                    status.innerHTML = '✓ ' + data.Message + '<br><small>SHA256: ' + data.Update.Sha256Hash + '</small>';
                    status.style.display = 'block';
                } else {
                    status.className = 'status-error';
                    status.innerText = '✗ Error: ' + (data.Error || 'Failed to publish');
                    status.style.display = 'block';
                }
            } catch (err) {
                status.className = 'status-error';
                status.innerText = '✗ Network Error: ' + err.message;
                status.style.display = 'block';
            } finally {
                btn.disabled = false;
                btn.innerText = '🚀 Push Update to All Pharmacies';
            }
        });
    </script>
</body>
</html>", "text/html"));

// 6. Cloud Backup Storage Hub
var cloudBackups = new System.Collections.Concurrent.ConcurrentDictionary<string, (CloudBackupInfo Info, byte[] Data)>();

app.MapPost("/api/v1/backups/upload", async (
    HttpRequest request,
    [FromHeader(Name = "X-Org-Id")] string? orgId,
    [FromHeader(Name = "X-Device-Id")] string? deviceId,
    [FromHeader(Name = "X-App-Version")] string? appVer) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { Error = "Expected multipart form content." });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("backupFile");
    if (file == null || file.Length == 0)
        return Results.BadRequest(new { Error = "Missing backupFile." });

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    var data = ms.ToArray();

    var backupId = $"BCK_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
    var info = new CloudBackupInfo
    {
        BackupId = backupId,
        OrgId = orgId ?? "ORG-001",
        DeviceId = deviceId ?? "DEV-01",
        CreatedAtUtc = DateTime.UtcNow,
        SizeBytes = data.Length,
        AppVersion = appVer ?? "1.0.0"
    };

    cloudBackups[backupId] = (info, data);
    return Results.Ok(new CloudBackupUploadResponse { Success = true, BackupId = backupId });
});

app.MapGet("/api/v1/backups", ([FromHeader(Name = "X-Org-Id")] string? orgId) =>
{
    var effectiveOrg = orgId ?? "ORG-001";
    var list = cloudBackups.Values
        .Where(b => b.Info.OrgId == effectiveOrg)
        .Select(b => b.Info)
        .OrderByDescending(b => b.CreatedAtUtc)
        .ToList();

    return Results.Ok(list);
});

app.MapGet("/api/v1/backups/{backupId}/download", (string backupId) =>
{
    if (cloudBackups.TryGetValue(backupId, out var item))
    {
        return Results.File(item.Data, "application/octet-stream", $"backup_{backupId}.enc");
    }
    return Results.NotFound(new { Error = "Backup not found." });
});

app.Run();

public partial class Program { }

