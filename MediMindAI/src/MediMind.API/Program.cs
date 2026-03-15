using System.Diagnostics;
using MediMind.API.Diagnostics;
using MediMind.API.Endpoints;
using MediMind.API.Hubs;
using MediMind.API.Middleware;
using MediMind.Core;
using MediMind.Infrastructure;
using MediMind.Infrastructure.Persistence;
using MediMind.Infrastructure.Qdrant;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────
// Serilog
// ──────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ──────────────────────────────────────────────────────────────
// OpenTelemetry — Distributed Tracing & Metrics
// ──────────────────────────────────────────────────────────────
var otlpEndpoint = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint")
    ?? "http://localhost:4317";

// In-memory trace collector for the built-in trace viewer UI
var traceCollector = new InMemoryTraceCollector();
builder.Services.AddSingleton(traceCollector);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "MediMind.API",
            serviceVersion: "1.0.0",
            serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing => tracing
        .AddSource("MediMind.Orchestrator")
        .AddSource("MediMind.Agents")
        .AddSource("MediMind.LLM")
        .AddSource("MediMind.Hub")
        .AddSource("MediMind.RAG")
        .AddSource("MediMind.Plugins")
        .AddSource("MediMind.Middleware")
        .AddSource("MediMind.Data")
        .AddSource("MediMind.API")
        .AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
            o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation(o => o.RecordException = true)
        .AddProcessor(new InMemoryExportProcessor(traceCollector))
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint))
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

// ──────────────────────────────────────────────────────────────
// Configuration
// ──────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "MediMind AI API",
        Version = "v1",
        Description = "Real-Time Clinical Intelligence & Multi-Agent Platform"
    });
});

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 128 * 1024; // 128 KB
    options.StreamBufferCapacity = 32;
});

// CORS (allow Blazor UI in development)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration.GetValue<string>("BlazorUI:BaseUrl") ?? "https://localhost:5002")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ──────────────────────────────────────────────────────────────
// Dependency Injection — layer registration
// ──────────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCore();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<QdrantHealthCheck>("qdrant");

// ──────────────────────────────────────────────────────────────
// Build
// ──────────────────────────────────────────────────────────────
var app = builder.Build();

// ──────────────────────────────────────────────────────────────
// Middleware pipeline
// ──────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediMind AI v1"));
}

app.UseCors("AllowBlazor");
app.UseSerilogRequestLogging();
app.UseMiddleware<PiiScrubbingMiddleware>();
app.UseMiddleware<AuditLoggingMiddleware>();

// ──────────────────────────────────────────────────────────────
// Map endpoints & hubs
// ──────────────────────────────────────────────────────────────
app.MapQueryEndpoints();
app.MapIngestionEndpoints();
app.MapPatientEndpoints();
app.MapTraceEndpoints();
app.MapHub<ClinicalChatHub>("/hubs/clinical-chat");
app.MapHealthChecks("/health");

// ──────────────────────────────────────────────────────────────
// Startup tasks — run migrations, seed data, ensure Qdrant collections
// ──────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Database migrations
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<MediMindDbContext>();
        logger.LogInformation("Applying database migrations…");
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied.");

        // Seed data
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration/seeding failed — continuing startup.");
    }

    // Qdrant collections
    try
    {
        var qdrant = scope.ServiceProvider.GetRequiredService<QdrantCollectionSetup>();
        logger.LogInformation("Ensuring Qdrant collections…");
        await qdrant.InitializeAsync();
        logger.LogInformation("Qdrant collections ready.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Qdrant collection setup failed — continuing startup.");
    }
}

// ──────────────────────────────────────────────────────────────
// Run
// ──────────────────────────────────────────────────────────────
app.Run();

// Expose partial class for integration-test WebApplicationFactory
public partial class Program;
