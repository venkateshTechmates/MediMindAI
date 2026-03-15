using System.Diagnostics;
using MediMind.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.RAG;

/// <summary>
/// Embeds user queries into vector representations for Qdrant similarity search (FR-10).
/// </summary>
public class QueryEmbedder : IQueryEmbedder
{
    private static readonly ActivitySource _activitySource = new("MediMind.RAG", "1.0.0");

    private readonly ILLMClient _llmClient;
    private readonly ILogger<QueryEmbedder> _logger;

    public QueryEmbedder(ILLMClient llmClient, ILogger<QueryEmbedder> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("RAG.QueryEmbedder.Embed", ActivityKind.Internal);
        activity?.SetTag("query.text_length", text.Length);

        _logger.LogDebug("Embedding query of length {Len}, TraceId: {TraceId}",
            text.Length, Activity.Current?.TraceId.ToString() ?? "none");

        var sw = Stopwatch.StartNew();
        var vector = await _llmClient.EmbedAsync(text, ct);
        sw.Stop();

        activity?.SetTag("embed.vector_dim", vector.Length);
        activity?.SetTag("embed.latency_ms", sw.ElapsedMilliseconds);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogDebug("Query embedded: dim={Dim}, latency={Ms}ms", vector.Length, sw.ElapsedMilliseconds);
        return vector;
    }
}
