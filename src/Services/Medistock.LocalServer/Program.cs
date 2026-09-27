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
using Medistock.Infrastructure.Sync;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Register Medistock Core & Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructureData();
builder.Services.AddInfrastructureSync();

var app = builder.Build();

// Run automated startup migrations on local branch SQLite/Postgres
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    await migrator.MigrateAsync();
}

app.UseHttpsRedirection();

// 1. Health & Heartbeat
app.MapGet("/api/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Medistock.LocalServer",
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0-LTS",
    BranchId = "BR-MAIN"
}));

// 2. Outbox Sync Ingestion (from local POS terminals)
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

app.Run();

public partial class Program { }
