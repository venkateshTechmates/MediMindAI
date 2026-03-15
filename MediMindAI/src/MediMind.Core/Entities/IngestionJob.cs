namespace MediMind.Core.Entities;

/// <summary>
/// Tracks document ingestion jobs for the RAG vector store pipeline.
/// </summary>
public class IngestionJob : BaseEntity
{
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public IngestionStatus Status { get; set; } = IngestionStatus.Pending;
    public int ChunksIngested { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum IngestionStatus
{
    Pending,
    Processing,
    Done,
    Failed
}
