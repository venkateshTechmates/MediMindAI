using MediMind.Core.Models;

namespace MediMind.Core.Interfaces;

/// <summary>
/// RAG pipeline orchestration — embedding, search, reranking, context building.
/// </summary>
public interface IRagPipeline
{
    /// <summary>
    /// Execute the full RAG pipeline: embed query → search vectors → rerank → build context.
    /// </summary>
    Task<RagContext> ExecuteAsync(ClinicalQuery query, CancellationToken ct = default);
}

/// <summary>
/// Embeds text into vector representations.
/// </summary>
public interface IQueryEmbedder
{
    Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default);
}

/// <summary>
/// Reranks retrieved document chunks by relevance.
/// </summary>
public interface IReranker
{
    Task<IReadOnlyList<DocumentChunk>> RerankAsync(string query, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
}

/// <summary>
/// Builds the augmented context prompt from retrieved chunks and patient data.
/// </summary>
public interface IContextBuilder
{
    string BuildContext(IReadOnlyList<DocumentChunk> chunks, string? patientContext = null);
}

/// <summary>
/// The assembled RAG context ready for LLM prompt construction.
/// </summary>
public class RagContext
{
    public string AugmentedContext { get; set; } = string.Empty;
    public IReadOnlyList<DocumentChunk> RetrievedChunks { get; set; } = Array.Empty<DocumentChunk>();
    public List<SourceCitation> Citations { get; set; } = new();
    public long SearchLatencyMs { get; set; }
    public long RerankLatencyMs { get; set; }
}
