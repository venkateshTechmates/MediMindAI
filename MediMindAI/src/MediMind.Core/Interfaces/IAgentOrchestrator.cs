using MediMind.Core.Models;

namespace MediMind.Core.Interfaces;

/// <summary>
/// Orchestrates multi-agent query processing using Semantic Kernel.
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Process a clinical query through the multi-agent pipeline.
    /// Returns the synthesized response with citations and agent traces.
    /// </summary>
    Task<ClinicalResponse> ProcessQueryAsync(ClinicalQuery query, CancellationToken ct = default);

    /// <summary>
    /// Stream the response tokens for a clinical query through SignalR-compatible async enumerable.
    /// </summary>
    IAsyncEnumerable<string> StreamQueryAsync(ClinicalQuery query, CancellationToken ct = default);
}
