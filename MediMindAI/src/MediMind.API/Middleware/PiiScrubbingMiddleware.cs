using System.Diagnostics;
using MediMind.Core.Interfaces;

namespace MediMind.API.Middleware;

/// <summary>
/// ASP.NET Core middleware that scrubs PII from request bodies before
/// they are processed by downstream handlers (NFR-4, NFR-5).
/// Only applies to JSON request bodies on mutation endpoints (POST/PUT/PATCH).
/// </summary>
public sealed class PiiScrubbingMiddleware
{
    private static readonly ActivitySource _activitySource = new("MediMind.Middleware", "1.0.0");

    private readonly RequestDelegate _next;
    private readonly ILogger<PiiScrubbingMiddleware> _logger;

    public PiiScrubbingMiddleware(RequestDelegate next, ILogger<PiiScrubbingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IPiiScrubber piiScrubber)
    {
        if (!IsMutationRequest(context.Request))
        {
            await _next(context);
            return;
        }

        using var activity = _activitySource.StartActivity("Middleware.PiiScrubbing", ActivityKind.Internal);
        activity?.SetTag("pii.method", context.Request.Method);
        activity?.SetTag("pii.path", context.Request.Path.ToString());

        // Enable request body buffering so it can be read multiple times
        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(body))
        {
            activity?.SetTag("pii.body_empty", true);
            activity?.SetTag("pii.entities_detected", 0);
            await _next(context);
            return;
        }

        activity?.SetTag("pii.body_length", body.Length);

        var result = await piiScrubber.ScrubAsync(body);

        activity?.SetTag("pii.entities_detected", result.EntitiesDetected);

        if (result.EntitiesDetected > 0)
        {
            var entityTypes = string.Join(", ", result.DetectedEntities.Select(e => e.Type));
            activity?.SetTag("pii.entity_types", entityTypes);
            activity?.SetTag("pii.scrubbed", true);

            _logger.LogWarning(
                "PII detected and scrubbed from {Method} {Path}: {Count} entities removed ({Types}), TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                result.DetectedEntities.Count,
                entityTypes,
                Activity.Current?.TraceId.ToString() ?? "none");

            // Replace the request body with the scrubbed version
            var scrubbedBytes = System.Text.Encoding.UTF8.GetBytes(result.ScrubedText);
            context.Request.Body = new MemoryStream(scrubbedBytes);
            context.Request.ContentLength = scrubbedBytes.Length;
        }
        else
        {
            activity?.SetTag("pii.scrubbed", false);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        await _next(context);
    }

    private static bool IsMutationRequest(HttpRequest request) =>
        request.Method is "POST" or "PUT" or "PATCH" &&
        (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);
}
