using System.Diagnostics;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.RAG;

/// <summary>
/// Orchestrates the full RAG pipeline: embed query → Qdrant search → rerank → build context (FR-08–15).
/// </summary>
public class RagOrchestrator : IRagPipeline
{
    private static readonly ActivitySource _activitySource = new("MediMind.RAG", "1.0.0");

    private readonly IQueryEmbedder _embedder;
    private readonly IVectorStore _vectorStore;
    private readonly IReranker _reranker;
    private readonly IContextBuilder _contextBuilder;
    private readonly ILogger<RagOrchestrator> _logger;

    public RagOrchestrator(
        IQueryEmbedder embedder,
        IVectorStore vectorStore,
        IReranker reranker,
        IContextBuilder contextBuilder,
        ILogger<RagOrchestrator> logger)
    {
        _embedder = embedder;
        _vectorStore = vectorStore;
        _reranker = reranker;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<RagContext> ExecuteAsync(ClinicalQuery query, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("RAG.Pipeline", ActivityKind.Internal);
        activity?.SetTag("query.id", query.QueryId);
        activity?.SetTag("query.length", query.QueryText.Length);
        activity?.SetTag("has_filters", query.Filters?.Count > 0);

        _logger.LogInformation("[RAG] Starting pipeline for query: {QueryId}, TraceId: {TraceId}",
            query.QueryId, Activity.Current?.TraceId.ToString() ?? "none");
        var totalSw = Stopwatch.StartNew();

        // Step 1: Embed the query
        float[] queryVector;
        using (var embedActivity = _activitySource.StartActivity("RAG.EmbedQuery", ActivityKind.Internal))
        {
            var searchSw = Stopwatch.StartNew();
            queryVector = await _embedder.EmbedQueryAsync(query.QueryText, ct);
            searchSw.Stop();
            embedActivity?.SetTag("embed.latency_ms", searchSw.ElapsedMilliseconds);
            embedActivity?.SetTag("embed.vector_dim", queryVector.Length);
            _logger.LogDebug("[RAG] Query embedded in {Ms}ms, vector dim={Dim}", searchSw.ElapsedMilliseconds, queryVector.Length);
        }

        // Step 2: Semantic search on Qdrant (Top-K = 8, cosine similarity)
        IReadOnlyList<DocumentChunk> chunks;
        using (var searchActivity = _activitySource.StartActivity("RAG.VectorSearch", ActivityKind.Internal))
        {
            searchActivity?.SetTag("search.top_k", 8);
            var searchSw = Stopwatch.StartNew();
            chunks = await _vectorStore.SearchAsync(
                queryVector,
                topK: 8,
                filters: query.Filters,
                ct: ct);
            searchSw.Stop();
            searchActivity?.SetTag("search.results_count", chunks.Count);
            searchActivity?.SetTag("search.latency_ms", searchSw.ElapsedMilliseconds);
            _logger.LogInformation("[RAG] Search returned {Count} chunks in {Ms}ms.", chunks.Count, searchSw.ElapsedMilliseconds);
        }

        // Step 3: Rerank chunks
        IReadOnlyList<DocumentChunk> rerankedChunks;
        using (var rerankActivity = _activitySource.StartActivity("RAG.Rerank", ActivityKind.Internal))
        {
            rerankActivity?.SetTag("rerank.input_count", chunks.Count);
            var rerankSw = Stopwatch.StartNew();
            rerankedChunks = await _reranker.RerankAsync(query.QueryText, chunks, ct);
            rerankSw.Stop();
            rerankActivity?.SetTag("rerank.output_count", rerankedChunks.Count);
            rerankActivity?.SetTag("rerank.latency_ms", rerankSw.ElapsedMilliseconds);
            _logger.LogInformation("[RAG] Reranking completed in {Ms}ms.", rerankSw.ElapsedMilliseconds);
        }

        // Step 4: Build augmented context
        string context;
        using (var buildActivity = _activitySource.StartActivity("RAG.BuildContext", ActivityKind.Internal))
        {
            context = _contextBuilder.BuildContext(rerankedChunks);
            buildActivity?.SetTag("context.length", context.Length);
            buildActivity?.SetTag("context.chunks_used", rerankedChunks.Count);
        }

        // Step 5: Extract citations
        var citations = rerankedChunks.Select((c, i) => new SourceCitation
        {
            DocumentName = c.DocumentName,
            Section = c.Metadata.GuidelineType,
            Page = c.Page,
            ConfidenceScore = 1.0 - (i * 0.05),
            Category = c.Metadata.Category
        }).ToList();

        totalSw.Stop();
        activity?.SetTag("pipeline.total_latency_ms", totalSw.ElapsedMilliseconds);
        activity?.SetTag("pipeline.citations_count", citations.Count);
        activity?.SetTag("pipeline.chunks_retrieved", chunks.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogInformation("[RAG] Pipeline completed in {Ms}ms total, {Citations} citations.",
            totalSw.ElapsedMilliseconds, citations.Count);

        return new RagContext
        {
            AugmentedContext = context,
            RetrievedChunks = rerankedChunks,
            Citations = citations,
            SearchLatencyMs = totalSw.ElapsedMilliseconds,
            RerankLatencyMs = 0
        };
    }
}
