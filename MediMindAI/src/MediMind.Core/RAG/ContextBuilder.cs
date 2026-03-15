using System.Diagnostics;
using System.Text;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.RAG;

/// <summary>
/// Builds the augmented context prompt from retrieved chunks and patient data (FR-13).
/// </summary>
public class ContextBuilder : IContextBuilder
{
    private static readonly ActivitySource _activitySource = new("MediMind.RAG", "1.0.0");

    private readonly ILogger<ContextBuilder> _logger;

    public ContextBuilder(ILogger<ContextBuilder> logger)
    {
        _logger = logger;
    }

    public string BuildContext(IReadOnlyList<DocumentChunk> chunks, string? patientContext = null)
    {
        using var activity = _activitySource.StartActivity("RAG.ContextBuilder.Build", ActivityKind.Internal);
        activity?.SetTag("context.chunks_count", chunks.Count);
        activity?.SetTag("context.has_patient_context", patientContext != null);

        var sb = new StringBuilder();

        sb.AppendLine("=== RETRIEVED CLINICAL EVIDENCE ===");
        sb.AppendLine();

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            sb.AppendLine($"--- Source [{i + 1}]: {chunk.DocumentName} ---");
            
            if (!string.IsNullOrEmpty(chunk.Metadata.Category))
                sb.AppendLine($"Category: {chunk.Metadata.Category}");
            if (!string.IsNullOrEmpty(chunk.Metadata.Version))
                sb.AppendLine($"Version: {chunk.Metadata.Version}");
            if (!string.IsNullOrEmpty(chunk.Metadata.GuidelineType))
                sb.AppendLine($"Type: {chunk.Metadata.GuidelineType}");
            if (chunk.Page.HasValue)
                sb.AppendLine($"Page: {chunk.Page}");
            
            sb.AppendLine();
            sb.AppendLine(chunk.Content);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(patientContext))
        {
            sb.AppendLine("=== PATIENT CONTEXT ===");
            sb.AppendLine(patientContext);
            sb.AppendLine();
        }

        var context = sb.ToString();

        activity?.SetTag("context.output_length", context.Length);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogDebug("Built context of {Len} characters from {Count} chunks, TraceId: {TraceId}",
            context.Length, chunks.Count, Activity.Current?.TraceId.ToString() ?? "none");
        return context;
    }
}
