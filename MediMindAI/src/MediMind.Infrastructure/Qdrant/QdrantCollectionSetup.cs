using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace MediMind.Infrastructure.Qdrant;

/// <summary>
/// Initializes Qdrant collections and indexes on startup.
/// </summary>
public class QdrantCollectionSetup
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantCollectionSetup> _logger;

    public QdrantCollectionSetup(QdrantClient client, ILogger<QdrantCollectionSetup> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Create all required collections if they don't exist.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var collections = new Dictionary<string, int>
        {
            { "medimind_clinical", 1024 },      // Clinical guidelines
            { "medimind_drug_formulary", 1024 }, // Drug database
            { "medimind_research", 1024 }        // Research literature
        };

        foreach (var (name, dimension) in collections)
        {
            try
            {
                var existing = await _client.ListCollectionsAsync(ct);
                if (existing.Any(c => c == name))
                {
                    _logger.LogInformation("Collection '{Name}' exists.", name);
                    continue;
                }

                await _client.CreateCollectionAsync(
                    name,
                    new VectorParams
                    {
                        Size = (ulong)dimension,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);

                // Create payload indexes for filtered search
                await _client.CreatePayloadIndexAsync(name, "category", PayloadSchemaType.Keyword, cancellationToken: ct);
                await _client.CreatePayloadIndexAsync(name, "source", PayloadSchemaType.Keyword, cancellationToken: ct);
                await _client.CreatePayloadIndexAsync(name, "drug_class", PayloadSchemaType.Keyword, cancellationToken: ct);
                await _client.CreatePayloadIndexAsync(name, "guideline_type", PayloadSchemaType.Keyword, cancellationToken: ct);

                _logger.LogInformation("Created and indexed collection '{Name}'.", name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize collection '{Name}'.", name);
            }
        }
    }
}
