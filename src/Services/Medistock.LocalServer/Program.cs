using Medistock.Application;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Sync;

var builder = WebApplication.CreateBuilder(args);

// Add Medistock Core & Infrastructure Services
builder.Services.AddApplication();
builder.Services.AddInfrastructureData();
builder.Services.AddInfrastructureSync();

var app = builder.Build();

app.UseHttpsRedirection();

// Health & Connectivity Check Endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0-alpha",
    service = "Medistock.LocalServer"
}));

// Outbox Sync Ingestion Endpoint
app.MapPost("/api/sync/events", (object payload) =>
{
    // Idempotent ingestion of synced events from edge terminals
    return Results.Ok(new { processed = true, receivedAt = DateTime.UtcNow });
});

app.Run();
