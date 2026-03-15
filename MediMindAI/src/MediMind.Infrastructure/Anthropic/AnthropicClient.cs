using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediMind.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediMind.Infrastructure.Anthropic;

/// <summary>
/// Anthropic Claude API client implementing ILLMClient.
/// Supports both full completions and streaming via the Messages API.
/// </summary>
public class AnthropicClient : ILLMClient
{
    private static readonly ActivitySource _activitySource = new("MediMind.LLM", "1.0.0");

    private readonly HttpClient _httpClient;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicClient> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AnthropicClient(HttpClient httpClient, IOptions<AnthropicOptions> options, ILogger<AnthropicClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.anthropic.com/");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<LLMResponse> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("LLM.Complete", ActivityKind.Client);
        activity?.SetTag("llm.model", _options.Model);
        activity?.SetTag("llm.max_tokens", _options.MaxOutputTokens);
        activity?.SetTag("llm.system_prompt_length", systemPrompt.Length);
        activity?.SetTag("llm.user_prompt_length", userPrompt.Length);

        var request = new
        {
            model = _options.Model,
            max_tokens = _options.MaxOutputTokens,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("v1/messages", request, _jsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AnthropicMessagesResponse>(_jsonOptions, ct);

        activity?.SetTag("llm.input_tokens", result?.Usage?.InputTokens ?? 0);
        activity?.SetTag("llm.output_tokens", result?.Usage?.OutputTokens ?? 0);
        activity?.SetTag("llm.stop_reason", result?.StopReason);

        return new LLMResponse
        {
            Content = result?.Content?.FirstOrDefault()?.Text ?? string.Empty,
            InputTokens = result?.Usage?.InputTokens ?? 0,
            OutputTokens = result?.Usage?.OutputTokens ?? 0,
            StopReason = result?.StopReason
        };
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("LLM.Stream", ActivityKind.Client);
        activity?.SetTag("llm.model", _options.Model);
        activity?.SetTag("llm.max_tokens", _options.MaxOutputTokens);
        activity?.SetTag("llm.system_prompt_length", systemPrompt.Length);
        activity?.SetTag("llm.user_prompt_length", userPrompt.Length);
        activity?.SetTag("llm.streaming", true);

        var request = new
        {
            model = _options.Model,
            max_tokens = _options.MaxOutputTokens,
            stream = true,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        int chunkCount = 0;
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            string? textChunk = null;
            try
            {
                var evt = JsonSerializer.Deserialize<StreamEvent>(data, _jsonOptions);
                if (evt?.Type == "content_block_delta" && evt.Delta?.Text is not null)
                {
                    textChunk = evt.Delta.Text;
                }
            }
            catch (JsonException)
            {
                // Skip malformed events
            }

            if (textChunk is not null)
            {
                chunkCount++;
                yield return textChunk;
            }
        }
        activity?.SetTag("llm.stream_chunks", chunkCount);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("LLM.Embed", ActivityKind.Client);
        activity?.SetTag("llm.embedding_model", _options.EmbeddingModel);
        activity?.SetTag("llm.input_length", text.Length);

        // Anthropic Messages API does not serve embeddings.
        // Voyage-3 embeddings require a separate Voyage AI API key & endpoint.
        // Until a Voyage API key is configured, return a deterministic hash-based
        // vector so RAG still returns *some* results via cosine similarity.
        if (string.IsNullOrWhiteSpace(_options.EmbeddingApiKey))
        {
            activity?.SetTag("llm.embed_fallback", "hash_vector");
            _logger.LogDebug("No embedding API key configured — returning hash-based vector for text of length {Len}.", text.Length);
            return GenerateHashVector(text);
        }

        // If a Voyage API key IS configured, call the Voyage AI endpoint.
        using var voyageClient = new HttpClient();
        voyageClient.BaseAddress = new Uri("https://api.voyageai.com/");
        voyageClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.EmbeddingApiKey}");

        var request = new
        {
            model = _options.EmbeddingModel,
            input = text,
            input_type = "search_document"
        };

        var response = await voyageClient.PostAsJsonAsync("v1/embeddings", request, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Embedding API call failed with status {Status}. Using hash vector fallback.", response.StatusCode);
            activity?.SetTag("llm.embed_fallback", "api_error");
            return GenerateHashVector(text);
        }

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(_jsonOptions, ct);
        activity?.SetTag("llm.embed_dimensions", result?.Data?.FirstOrDefault()?.Embedding?.Length ?? 0);
        return result?.Data?.FirstOrDefault()?.Embedding ?? GenerateHashVector(text);
    }

    /// <summary>
    /// Generates a deterministic 1024-dimension vector from text using a hash,
    /// so that identical inputs always produce identical embeddings.
    /// </summary>
    private static float[] GenerateHashVector(string text, int dimensions = 1024)
    {
        var vector = new float[dimensions];
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(text));
        
        var rng = new Random(BitConverter.ToInt32(hash, 0));
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)(rng.NextDouble() * 2 - 1); // range [-1, 1]
        }

        // Normalize to unit vector
        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < dimensions; i++)
                vector[i] /= magnitude;
        }

        return vector;
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            var embedding = await EmbedAsync(text, ct);
            results.Add(embedding);
        }
        return results;
    }

    // ── Internal DTOs ──

    private class AnthropicMessagesResponse
    {
        public List<ContentBlock>? Content { get; set; }
        public UsageInfo? Usage { get; set; }
        public string? StopReason { get; set; }
    }

    private class ContentBlock
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }

    private class UsageInfo
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

    private class StreamEvent
    {
        public string? Type { get; set; }
        public StreamDelta? Delta { get; set; }
    }

    private class StreamDelta
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }

    private class EmbeddingResponse
    {
        public List<EmbeddingData>? Data { get; set; }
    }

    private class EmbeddingData
    {
        public float[]? Embedding { get; set; }
    }
}

/// <summary>
/// Configuration options for Anthropic API.
/// </summary>
public class AnthropicOptions
{
    public const string SectionName = "Anthropic";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-20250514";
    public string EmbeddingModel { get; set; } = "voyage-3";
    public string? EmbeddingApiKey { get; set; }
    public int MaxOutputTokens { get; set; } = 4096;
    public bool UseMock { get; set; } = false;
}
