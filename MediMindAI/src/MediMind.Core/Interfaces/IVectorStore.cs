using MediMind.Core.Models;

namespace MediMind.Core.Interfaces;

/// <summary>
/// Abstraction for vector store operations (Qdrant).
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Upsert a batch of document chunks with their embeddings and metadata.
    /// </summary>
    Task UpsertChunksAsync(IEnumerable<DocumentChunk> chunks, string collectionName, CancellationToken ct = default);

    /// <summary>
    /// Perform semantic search using a query vector, returning top-K results with metadata filters.
    /// </summary>
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        float[] queryVector,
        int topK = 8,
        Dictionary<string, string>? filters = null,
        string? collectionName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Initialize a collection with the specified vector dimension.
    /// </summary>
    Task EnsureCollectionAsync(string collectionName, int vectorDimension = 1024, CancellationToken ct = default);

    /// <summary>
    /// Delete all vectors for a given document source.
    /// </summary>
    Task DeleteBySourceAsync(string source, string collectionName, CancellationToken ct = default);
}
