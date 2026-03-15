namespace MediMind.Core.Interfaces;

/// <summary>
/// PII scrubbing service — strips personally identifiable health information
/// before sending data to external services (LLM, vector DB).
/// </summary>
public interface IPiiScrubber
{
    /// <summary>
    /// Scrub PII from text, replacing detected entities with placeholders.
    /// </summary>
    Task<PiiScrubResult> ScrubAsync(string text, CancellationToken ct = default);
}

public class PiiScrubResult
{
    public string ScrubedText { get; set; } = string.Empty;
    public int EntitiesDetected { get; set; }
    public List<PiiEntity> DetectedEntities { get; set; } = new();
}

public class PiiEntity
{
    public string Type { get; set; } = string.Empty;  // e.g., PERSON, SSN, PHONE
    public string OriginalValue { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
}
