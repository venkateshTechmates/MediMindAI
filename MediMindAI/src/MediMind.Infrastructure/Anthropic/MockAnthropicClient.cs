using System.Runtime.CompilerServices;
using MediMind.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Anthropic;

/// <summary>
/// Mock Anthropic client for local testing without API key (FR-45).
/// Returns deterministic, clinically-themed responses.
/// </summary>
public class MockAnthropicClient : ILLMClient
{
    private readonly ILogger<MockAnthropicClient> _logger;

    public MockAnthropicClient(ILogger<MockAnthropicClient> logger)
    {
        _logger = logger;
    }

    public Task<LLMResponse> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK LLM] CompleteAsync called with prompt length: {Len}", userPrompt.Length);

        var response = new LLMResponse
        {
            Content = GenerateMockResponse(userPrompt),
            InputTokens = userPrompt.Length / 4,
            OutputTokens = 150,
            StopReason = "end_turn"
        };

        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK LLM] StreamAsync called with prompt length: {Len}", userPrompt.Length);

        var response = GenerateMockResponse(userPrompt);
        var words = response.Split(' ');

        foreach (var word in words)
        {
            if (ct.IsCancellationRequested) break;
            yield return word + " ";
            await Task.Delay(50, ct);
        }
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK LLM] EmbedAsync called for text of length: {Len}", text.Length);

        // Generate a deterministic pseudo-embedding based on text hash
        var hash = text.GetHashCode();
        var random = new Random(hash);
        var embedding = new float[1024];
        for (var i = 0; i < embedding.Length; i++)
            embedding[i] = (float)(random.NextDouble() * 2 - 1);

        // Normalize to unit vector
        var magnitude = MathF.Sqrt(embedding.Sum(x => x * x));
        for (var i = 0; i < embedding.Length; i++)
            embedding[i] /= magnitude;

        return Task.FromResult(embedding);
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            results.Add(await EmbedAsync(text, ct));
        }
        return results;
    }

    private static string GenerateMockResponse(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        if (lowerQuery.Contains("drug") || lowerQuery.Contains("interaction") || lowerQuery.Contains("medication"))
        {
            return "[MOCK] Based on the retrieved drug formulary data, the following interactions were identified:\n\n" +
                   "1. **Metformin + ACE Inhibitors**: Generally safe combination. Monitor renal function.\n" +
                   "2. **Aspirin + Warfarin**: Increased bleeding risk. Use with caution.\n\n" +
                   "**Source**: Mock Drug Formulary v2024, Section 4.2 (Confidence: 0.92)\n\n" +
                   "*Note: This is a mock response for testing purposes.*";
        }

        if (lowerQuery.Contains("diagnosis") || lowerQuery.Contains("symptom") || lowerQuery.Contains("differential"))
        {
            return "[MOCK] Based on the clinical guidelines retrieved, the differential diagnosis includes:\n\n" +
                   "1. **Acute Coronary Syndrome** (High probability) — Chest pain on exertion with ECG changes\n" +
                   "2. **Stable Angina** (Moderate probability) — Consistent with exertional pattern\n" +
                   "3. **GERD** (Low probability) — Consider if cardiac workup negative\n\n" +
                   "**Recommended**: Troponin levels, serial ECG, stress test\n\n" +
                   "**Source**: Mock Clinical Guidelines v2024.1, Chapter 7 (Confidence: 0.88)\n\n" +
                   "*Note: This is a mock response for testing purposes.*";
        }

        if (lowerQuery.Contains("lab") || lowerQuery.Contains("result") || lowerQuery.Contains("abnormal"))
        {
            return "[MOCK] Lab result interpretation:\n\n" +
                   "- **HbA1c 8.2%**: Above target (< 7.0%). Indicates suboptimal glycemic control over past 3 months.\n" +
                   "- **Fasting Glucose 186 mg/dL**: Elevated (normal: 70-100 mg/dL).\n\n" +
                   "**Recommendation**: Consider medication adjustment and dietary counseling.\n\n" +
                   "**Source**: Mock Lab Reference Guide v2024 (Confidence: 0.95)\n\n" +
                   "*Note: This is a mock response for testing purposes.*";
        }

        return "[MOCK] Based on the retrieved clinical knowledge base, here is a synthesized response to your query:\n\n" +
               "The clinical evidence suggests following standard-of-care protocols. " +
               "Please consult the relevant specialist for patient-specific guidance.\n\n" +
               "**Sources**: Mock Clinical Database v2024 (Confidence: 0.85)\n\n" +
               "*Note: This is a mock response for testing purposes. " +
               "In production, this would be grounded in real clinical guidelines and patient data.*";
    }
}
