using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qdrant.Client;

namespace MediMind.Infrastructure.Qdrant;

/// <summary>
/// Health check for the Qdrant vector database.
/// </summary>
public sealed class QdrantHealthCheck : IHealthCheck
{
    private readonly QdrantClient _client;

    public QdrantHealthCheck(QdrantClient client) => _client = client;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            return HealthCheckResult.Healthy($"Qdrant reachable — {collections.Count} collections");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Qdrant unreachable", ex);
        }
    }
}
