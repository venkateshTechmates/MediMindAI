using MediMind.API.Diagnostics;

namespace MediMind.API.Endpoints;

/// <summary>
/// Minimal API endpoints for viewing distributed traces in the browser.
/// </summary>
public static class TraceEndpoints
{
    public static void MapTraceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/traces").WithTags("Traces");

        // GET /api/traces — list recent trace groups
        group.MapGet("/", (InMemoryTraceCollector collector, int? count) =>
        {
            var traces = collector.GetRecentTraces(count ?? 50);
            return Results.Ok(traces);
        })
        .WithName("GetRecentTraces")
        .WithDescription("Returns recent trace groups ordered by most recent first.");

        // GET /api/traces/{traceId} — get all spans for a specific trace
        group.MapGet("/{traceId}", (InMemoryTraceCollector collector, string traceId) =>
        {
            var spans = collector.GetByTraceId(traceId);
            if (spans.Count == 0)
                return Results.NotFound(new { message = "No spans found for this trace ID." });
            return Results.Ok(spans);
        })
        .WithName("GetTraceById")
        .WithDescription("Returns all spans for a specific trace ID.");

        // GET /api/traces/spans — list all recent individual spans
        group.MapGet("/spans", (InMemoryTraceCollector collector, int? count) =>
        {
            var spans = collector.GetRecentSpans(count ?? 200);
            return Results.Ok(spans);
        })
        .WithName("GetRecentSpans")
        .WithDescription("Returns recent individual spans.");

        // DELETE /api/traces — clear all collected traces
        group.MapDelete("/", (InMemoryTraceCollector collector) =>
        {
            collector.Clear();
            return Results.Ok(new { message = "Traces cleared." });
        })
        .WithName("ClearTraces")
        .WithDescription("Clears all collected traces from memory.");
    }
}
