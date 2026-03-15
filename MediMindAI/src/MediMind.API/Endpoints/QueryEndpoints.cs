using System.Diagnostics;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Endpoints;

/// <summary>
/// Clinical query endpoints — accepts natural language queries and returns RAG-grounded responses (FR-08).
/// </summary>
public static class QueryEndpoints
{
    private static readonly ActivitySource _activitySource = new("MediMind.API", "1.0.0");

    public static IEndpointRouteBuilder MapQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/query")
            .WithTags("Clinical Query");

        group.MapPost("/", async (
            [FromBody] QueryRequest request,
            [FromServices] IAgentOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            using var activity = _activitySource.StartActivity("API.Query.Process", ActivityKind.Server);
            activity?.SetTag("api.endpoint", "POST /api/v1/query");
            activity?.SetTag("query.user_id", request.UserId ?? "anonymous");
            activity?.SetTag("query.has_patient", request.PatientId.HasValue);
            activity?.SetTag("query.text_length", request.QueryText.Length);

            var query = new ClinicalQuery
            {
                SessionId = request.SessionId ?? Guid.NewGuid(),
                UserId = request.UserId ?? "anonymous",
                UserRole = request.UserRole ?? "Clinician",
                QueryText = request.QueryText,
                PatientId = request.PatientId,
                Filters = request.Filters
            };

            activity?.SetTag("query.session_id", query.SessionId.ToString());

            var sw = Stopwatch.StartNew();
            var response = await orchestrator.ProcessQueryAsync(query, ct);
            sw.Stop();

            activity?.SetTag("query.latency_ms", sw.ElapsedMilliseconds);
            activity?.SetTag("query.agents_used", response.AgentResults?.Count ?? 0);
            activity?.SetTag("query.is_out_of_scope", response.IsOutOfScope);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return Results.Ok(response);
        })
        .WithName("ProcessClinicalQuery")
        .WithSummary("Process a clinical query through the multi-agent RAG pipeline")
        .Produces<ClinicalResponse>(200)
        .Produces(400);

        return app;
    }
}

public record QueryRequest(
    string QueryText,
    Guid? SessionId = null,
    string? UserId = null,
    string? UserRole = null,
    Guid? PatientId = null,
    Dictionary<string, string>? Filters = null);
