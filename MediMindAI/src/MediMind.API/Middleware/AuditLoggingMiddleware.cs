using System.Diagnostics;
using MediMind.Core.Entities;
using MediMind.Infrastructure.Persistence;

namespace MediMind.API.Middleware;

/// <summary>
/// Request/response audit-logging middleware. Captures every HTTP request
/// as an immutable <see cref="AuditLog"/> entry (NFR-6).
/// </summary>
public sealed class AuditLoggingMiddleware
{
    private static readonly ActivitySource _activitySource = new("MediMind.Middleware", "1.0.0");

    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, MediMindDbContext dbContext)
    {
        using var activity = _activitySource.StartActivity("Middleware.AuditLogging", ActivityKind.Internal);
        var sw = Stopwatch.StartNew();

        // Capture request metadata
        var userId = context.User?.Identity?.Name ?? "anonymous";
        var action = $"{context.Request.Method} {context.Request.Path}";

        activity?.SetTag("audit.user_id", userId);
        activity?.SetTag("audit.action", action);
        activity?.SetTag("audit.method", context.Request.Method);
        activity?.SetTag("audit.path", context.Request.Path.ToString());

        _logger.LogDebug("[AuditLog] Processing {Action} for user {UserId}, TraceId: {TraceId}",
            action, userId, Activity.Current?.TraceId.ToString() ?? "none");

        try
        {
            await _next(context);
            sw.Stop();

            activity?.SetTag("audit.status_code", context.Response.StatusCode);
            activity?.SetTag("audit.duration_ms", sw.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation("[AuditLog] {Action} → {StatusCode} in {Duration}ms",
                action, context.Response.StatusCode, sw.ElapsedMilliseconds);

            var audit = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = "HttpRequest",
                EntityId = context.TraceIdentifier,
                OldValue = $"Status={context.Response.StatusCode}, Duration={sw.ElapsedMilliseconds}ms",
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                Timestamp = DateTime.UtcNow
            };

            dbContext.AuditLogs.Add(audit);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            sw.Stop();
            activity?.SetTag("audit.status_code", 500);
            activity?.SetTag("audit.duration_ms", sw.ElapsedMilliseconds);
            activity?.SetTag("audit.error", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex, "[AuditLog] Request failed: {Action} after {Duration}ms, TraceId: {TraceId}",
                action, sw.ElapsedMilliseconds, Activity.Current?.TraceId.ToString() ?? "none");

            // Still log the failed request
            try
            {
                var audit = new AuditLog
                {
                    UserId = userId,
                    Action = action,
                    EntityType = "HttpRequest",
                    EntityId = context.TraceIdentifier,
                    OldValue = $"FAILED: {ex.Message}, Duration={sw.ElapsedMilliseconds}ms",
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    Timestamp = DateTime.UtcNow
                };

                dbContext.AuditLogs.Add(audit);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx, "Failed to write audit log for failed request");
            }

            throw;
        }
    }
}
