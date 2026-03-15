using System.Diagnostics;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace MediMind.Infrastructure.Qdrant;

/// <summary>
/// Qdrant vector store implementation for clinical document embeddings.
/// </summary>
public class QdrantVectorStore : IVectorStore
{
    private static readonly ActivitySource _activitySource = new("MediMind.Data", "1.0.0");

    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStore> _logger;
    private readonly string _defaultCollection;

    public QdrantVectorStore(QdrantClient client, ILogger<QdrantVectorStore> logger, string defaultCollection = "medimind_clinical")
    {
        _client = client;
        _logger = logger;
        _defaultCollection = defaultCollection;
    }

    public async Task EnsureCollectionAsync(string collectionName, int vectorDimension = 1024, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.Qdrant.EnsureCollection", ActivityKind.Client);
        activity?.SetTag("qdrant.collection", collectionName);
        activity?.SetTag("qdrant.vector_dimension", vectorDimension);

        try
        {
            var collections = await _client.ListCollectionsAsync(ct);
            if (collections.Any(c => c == collectionName))
            {
                activity?.SetTag("qdrant.result", "already_exists");
                activity?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogInformation("Qdrant collection '{Collection}' already exists.", collectionName);
                return;
            }

            await _client.CreateCollectionAsync(
                collectionName,
                new VectorParams
                {
                    Size = (ulong)vectorDimension,
                    Distance = Distance.Cosine
                },
                cancellationToken: ct);

            activity?.SetTag("qdrant.result", "created");
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Created Qdrant collection '{Collection}' with dimension {Dim}.", collectionName, vectorDimension);
        }
        catch (Exception ex)
        {
            activity?.SetTag("qdrant.error", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to ensure Qdrant collection '{Collection}'.", collectionName);
            throw;
        }
    }

    public async Task UpsertChunksAsync(IEnumerable<DocumentChunk> chunks, string collectionName, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.Qdrant.Upsert", ActivityKind.Client);
        activity?.SetTag("qdrant.collection", collectionName);

        var points = chunks.Select(chunk => new PointStruct
        {
            Id = new PointId { Uuid = chunk.ChunkId.ToString() },
            Vectors = chunk.Embedding!,
            Payload =
            {
                ["content"] = chunk.Content,
                ["document_name"] = chunk.DocumentName,
                ["chunk_index"] = chunk.ChunkIndex,
                ["source"] = chunk.Metadata.Source,
                ["category"] = chunk.Metadata.Category,
                ["version"] = chunk.Metadata.Version ?? "",
                ["drug_class"] = chunk.Metadata.DrugClass ?? "",
                ["guideline_type"] = chunk.Metadata.GuidelineType ?? "",
                ["page"] = chunk.Page ?? 0
            }
        }).ToList();

        await _client.UpsertAsync(collectionName, points, cancellationToken: ct);

        activity?.SetTag("qdrant.points_upserted", points.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);
        _logger.LogInformation("Upserted {Count} chunks into '{Collection}', TraceId: {TraceId}",
            points.Count, collectionName, Activity.Current?.TraceId.ToString() ?? "none");
    }

    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        float[] queryVector,
        int topK = 8,
        Dictionary<string, string>? filters = null,
        string? collectionName = null,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.Qdrant.Search", ActivityKind.Client);
        collectionName ??= _defaultCollection;

        activity?.SetTag("qdrant.collection", collectionName);
        activity?.SetTag("qdrant.top_k", topK);
        activity?.SetTag("qdrant.has_filters", filters is { Count: > 0 });
        activity?.SetTag("qdrant.vector_dim", queryVector.Length);

        var sw = Stopwatch.StartNew();
        Filter? qdrantFilter = null;

        if (filters is { Count: > 0 })
        {
            var conditions = filters.Select(f =>
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = f.Key,
                        Match = new Match { Keyword = f.Value }
                    }
                }).ToList();

            qdrantFilter = new Filter();
            qdrantFilter.Must.AddRange(conditions);
        }

        var results = await _client.SearchAsync(
            collectionName,
            queryVector,
            limit: (ulong)topK,
            filter: qdrantFilter,
            cancellationToken: ct);

        sw.Stop();
        var resultList = results.Select(r => new DocumentChunk
        {
            ChunkId = Guid.Parse(r.Id.Uuid),
            Content = r.Payload.TryGetValue("content", out var content) ? content.StringValue : "",
            DocumentName = r.Payload.TryGetValue("document_name", out var docName) ? docName.StringValue : "",
            ChunkIndex = r.Payload.TryGetValue("chunk_index", out var idx) ? (int)idx.IntegerValue : 0,
            Page = r.Payload.TryGetValue("page", out var page) ? (int)page.IntegerValue : null,
            Metadata = new DocumentChunkMetadata
            {
                Source = r.Payload.TryGetValue("source", out var src) ? src.StringValue : "",
                Category = r.Payload.TryGetValue("category", out var cat) ? cat.StringValue : "",
                Version = r.Payload.TryGetValue("version", out var ver) ? ver.StringValue : null,
                DrugClass = r.Payload.TryGetValue("drug_class", out var dc) ? dc.StringValue : null,
                GuidelineType = r.Payload.TryGetValue("guideline_type", out var gt) ? gt.StringValue : null
            }
        }).ToList();

        activity?.SetTag("qdrant.results_count", resultList.Count);
        activity?.SetTag("qdrant.latency_ms", sw.ElapsedMilliseconds);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogDebug("Qdrant search returned {Count} results in {Ms}ms from '{Collection}', TraceId: {TraceId}",
            resultList.Count, sw.ElapsedMilliseconds, collectionName, Activity.Current?.TraceId.ToString() ?? "none");

        return resultList;
    }

    public async Task DeleteBySourceAsync(string source, string collectionName, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.Qdrant.DeleteBySource", ActivityKind.Client);
        activity?.SetTag("qdrant.collection", collectionName);
        activity?.SetTag("qdrant.source", source);

        var filter = new Filter();
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = "source",
                Match = new Match { Keyword = source }
            }
        });

        await _client.DeleteAsync(collectionName, filter, cancellationToken: ct);

        activity?.SetStatus(ActivityStatusCode.Ok);
        _logger.LogInformation("Deleted vectors for source '{Source}' from '{Collection}', TraceId: {TraceId}",
            source, collectionName, Activity.Current?.TraceId.ToString() ?? "none");
    }
}
