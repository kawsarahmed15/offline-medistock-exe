using System;
using Medistock.Application;
using Medistock.Application.B2B.Services;
using Medistock.Application.Sync;
using Medistock.Contracts.Auth;
using Medistock.Contracts.B2B;
using Medistock.Contracts.Sync;
using Medistock.Domain.B2B;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Data.Migrations;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Register Clean Architecture Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructureData();

var app = builder.Build();

// Run automated startup migrations
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    await migrator.MigrateAsync();
}

app.UseHttpsRedirection();

// 1. Health & Status
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Medistock.CloudApi",
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0-LTS"
}));

// 2. Authentication & Device Onboarding
app.MapPost("/api/v1/auth/register-device", ([FromBody] DeviceRegistrationRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.DeviceId) || string.IsNullOrWhiteSpace(req.OrgId))
    {
        return Results.BadRequest(new { Error = "DeviceId and OrgId are required." });
    }

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

app.MapPost("/api/v1/auth/login", ([FromBody] UserLoginRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new { Error = "Username and password are required." });
    }

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

app.Run();

public partial class Program { }
