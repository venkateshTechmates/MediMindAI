namespace MediMind.Core.Interfaces;

/// <summary>
/// Abstraction for LLM client operations (Anthropic Claude).
/// Supports both full completion and streaming.
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Generate a full completion for the given prompt with system instruction.
    /// </summary>
    Task<LLMResponse> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);

    /// <summary>
    /// Stream completion tokens for the given prompt.
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);

    /// <summary>
    /// Generate a vector embedding for the given text.
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Generate vector embeddings for a batch of texts.
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default);
}

/// <summary>
/// Response from the LLM including token usage.
/// </summary>
public class LLMResponse
{
    public string Content { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public string? StopReason { get; set; }
}
