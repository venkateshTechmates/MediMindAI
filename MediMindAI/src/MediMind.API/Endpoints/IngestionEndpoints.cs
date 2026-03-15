using System.Diagnostics;
using MediMind.Core.Entities;
using MediMind.Core.Models;
using MediMind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediMind.API.Endpoints;

/// <summary>
/// Document ingestion endpoints — ingest clinical documents into the RAG vector store (FR-01–07).
/// </summary>
public static class IngestionEndpoints
{
    private static readonly ActivitySource _activitySource = new("MediMind.API", "1.0.0");

    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ingestion")
            .WithTags("Document Ingestion");

        // Start a new ingestion job
        group.MapPost("/", async (
            [FromBody] IngestionJobRequest request,
            [FromServices] MediMindDbContext db,
            CancellationToken ct) =>
        {
            using var activity = _activitySource.StartActivity("API.Ingestion.Create", ActivityKind.Server);
            activity?.SetTag("api.endpoint", "POST /api/v1/ingestion");
            activity?.SetTag("ingestion.document_name", request.DocumentName);
            activity?.SetTag("ingestion.document_type", request.DocumentType);

            var job = new IngestionJob
            {
                DocumentName = request.DocumentName,
                DocumentType = request.DocumentType,
                Status = IngestionStatus.Pending
            };

            await db.IngestionJobs.AddAsync(job, ct);
            await db.SaveChangesAsync(ct);

            activity?.SetTag("ingestion.job_id", job.Id.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            return Results.Created($"/api/v1/ingestion/{job.Id}", new IngestionStatusResponse
            {
                JobId = job.Id,
                DocumentName = job.DocumentName,
                Status = job.Status.ToString(),
                StartedAt = job.StartedAt
            });
        })
        .WithName("CreateIngestionJob")
        .WithSummary("Start a new document ingestion job")
        .Produces<IngestionStatusResponse>(201);

        // Get ingestion job status
        group.MapGet("/{jobId:guid}", async (
            Guid jobId,
            [FromServices] MediMindDbContext db,
            CancellationToken ct) =>
        {
            var job = await db.IngestionJobs.FindAsync(new object[] { jobId }, ct);
            if (job is null)
                return Results.NotFound();

            return Results.Ok(new IngestionStatusResponse
            {
                JobId = job.Id,
                DocumentName = job.DocumentName,
                Status = job.Status.ToString(),
                ChunksIngested = job.ChunksIngested,
                ErrorMessage = job.ErrorMessage,
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt
            });
        })
        .WithName("GetIngestionStatus")
        .WithSummary("Get the status of an ingestion job");

        // List all ingestion jobs
        group.MapGet("/", async (
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromServices] MediMindDbContext db = null!,
            CancellationToken ct = default) =>
        {
            var jobs = await db.IngestionJobs
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new IngestionStatusResponse
                {
                    JobId = j.Id,
                    DocumentName = j.DocumentName,
                    Status = j.Status.ToString(),
                    ChunksIngested = j.ChunksIngested,
                    ErrorMessage = j.ErrorMessage,
                    StartedAt = j.StartedAt,
                    CompletedAt = j.CompletedAt
                })
                .ToListAsync(ct);

            return Results.Ok(jobs);
        })
        .WithName("ListIngestionJobs")
        .WithSummary("List all ingestion jobs with pagination");

        return app;
    }
}

public record IngestionJobRequest(
    string DocumentName,
    string DocumentType,
    string? Category = null,
    string? Version = null);
