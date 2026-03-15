using System.Diagnostics;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.RAG;

/// <summary>
/// Reranks retrieved document chunks using cross-encoder scoring (FR-12).
/// For local/mock mode, uses a simple heuristic reranker based on keyword overlap.
/// In production, this would use a dedicated cross-encoder model.
/// </summary>
public class Reranker : IReranker
{
    private static readonly ActivitySource _activitySource = new("MediMind.RAG", "1.0.0");

    private readonly ILogger<Reranker> _logger;

    public Reranker(ILogger<Reranker> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<DocumentChunk>> RerankAsync(
        string query,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("RAG.Reranker.Rerank", ActivityKind.Internal);
        activity?.SetTag("rerank.input_chunks", chunks.Count);
        activity?.SetTag("rerank.query_length", query.Length);

        _logger.LogDebug("Reranking {Count} chunks for query, TraceId: {TraceId}",
            chunks.Count, Activity.Current?.TraceId.ToString() ?? "none");

        var sw = Stopwatch.StartNew();
        var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var scored = chunks.Select(chunk =>
        {
            var contentLower = chunk.Content.ToLowerInvariant();
            var matchCount = queryTerms.Count(term => contentLower.Contains(term));
            var overlapRatio = (double)matchCount / Math.Max(queryTerms.Length, 1);
            if (contentLower.Contains(query.ToLowerInvariant()))
                overlapRatio += 0.5;
            return (Chunk: chunk, Score: overlapRatio);
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Chunk)
        .ToList();
        sw.Stop();

        activity?.SetTag("rerank.output_chunks", scored.Count);
        activity?.SetTag("rerank.latency_ms", sw.ElapsedMilliseconds);
        activity?.SetTag("rerank.strategy", "keyword_overlap");
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogDebug("Reranked {In}→{Out} chunks in {Ms}ms", chunks.Count, scored.Count, sw.ElapsedMilliseconds);

        IReadOnlyList<DocumentChunk> result = scored;
        return Task.FromResult(result);
    }
}
