using System.Diagnostics;
using System.Runtime.CompilerServices;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace MediMind.API.Hubs;

/// <summary>
/// SignalR hub for real-time clinical chat streaming (FR-3).
/// Clients invoke "SendQuery" and receive a token-by-token stream via "ReceiveToken",
/// followed by "ReceiveComplete" with the full response and citations.
/// </summary>
public sealed class ClinicalChatHub : Hub
{
    private static readonly ActivitySource _activitySource = new("MediMind.Hub", "1.0.0");

    private readonly IAgentOrchestrator _orchestrator;
    private readonly IPiiScrubber _piiScrubber;
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<ClinicalChatHub> _logger;

    public ClinicalChatHub(
        IAgentOrchestrator orchestrator,
        IPiiScrubber piiScrubber,
        ISessionStore sessionStore,
        ILogger<ClinicalChatHub> logger)
    {
        _orchestrator = orchestrator;
        _piiScrubber = piiScrubber;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, Error: {Error}",
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Primary streaming entry-point. The client sends a <see cref="ClinicalQuery"/> and
    /// receives tokens as they are generated via the "ReceiveToken" callback, then a final
    /// "ReceiveComplete" callback with the full <see cref="ClinicalResponse"/>.
    /// </summary>
    public async IAsyncEnumerable<string> StreamQuery(
        ClinicalQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Hub.StreamQuery", ActivityKind.Server);
        activity?.SetTag("hub.connection_id", Context.ConnectionId);
        activity?.SetTag("hub.session_id", query.SessionId.ToString());
        activity?.SetTag("hub.query_length", query.QueryText.Length);

        _logger.LogInformation("StreamQuery from {ConnectionId}, SessionId: {SessionId}",
            Context.ConnectionId, query.SessionId);

        // Scrub PII from the query text
        using (var piiActivity = _activitySource.StartActivity("Hub.PiiScrubbing"))
        {
            var scrubResult = await _piiScrubber.ScrubAsync(query.QueryText, cancellationToken);
            if (scrubResult.EntitiesDetected > 0)
            {
                _logger.LogWarning("PII scrubbed from query: {Count} entities removed", scrubResult.DetectedEntities.Count);
                query.QueryText = scrubResult.ScrubedText;
                piiActivity?.SetTag("pii.entities_detected", scrubResult.EntitiesDetected);
            }
            piiActivity?.SetTag("pii.scrubbed", scrubResult.EntitiesDetected > 0);
        }

        // Stream tokens via the orchestrator
        int tokenCount = 0;
        await foreach (var token in _orchestrator.StreamQueryAsync(query, cancellationToken))
        {
            tokenCount++;
            yield return token;
        }
        activity?.SetTag("hub.stream_token_chunks", tokenCount);

        // Persist session context
        await _sessionStore.SetAsync(
            $"session:{query.SessionId}:last_query",
            query,
            TimeSpan.FromHours(8),
            cancellationToken);
    }

    /// <summary>
    /// Non-streaming query — returns the full response at once.
    /// </summary>
    public async Task<ClinicalResponse> SendQuery(ClinicalQuery query)
    {
        using var activity = _activitySource.StartActivity("Hub.SendQuery", ActivityKind.Server);
        activity?.SetTag("hub.connection_id", Context.ConnectionId);
        activity?.SetTag("hub.session_id", query.SessionId.ToString());

        _logger.LogInformation("SendQuery from {ConnectionId}, SessionId: {SessionId}",
            Context.ConnectionId, query.SessionId);

        var scrubResult = await _piiScrubber.ScrubAsync(query.QueryText);
        if (scrubResult.EntitiesDetected > 0)
        {
            query.QueryText = scrubResult.ScrubedText;
        }

        var result = await _orchestrator.ProcessQueryAsync(query, Context.ConnectionAborted);

        activity?.SetTag("hub.response_length", result.Content.Length);
        activity?.SetTag("hub.total_latency_ms", result.TotalLatencyMs);
        activity?.SetTag("hub.agents_used", result.AgentResults.Count);

        // Send the full response
        await Clients.Caller.SendAsync("ReceiveComplete", result, Context.ConnectionAborted);

        // Persist session
        await _sessionStore.SetAsync(
            $"session:{query.SessionId}:last_response",
            result,
            TimeSpan.FromHours(8),
            Context.ConnectionAborted);

        return result;
    }

    /// <summary>
    /// Set the active patient for the session, so that subsequent queries
    /// are automatically scoped to that patient.
    /// </summary>
    public async Task SetActivePatient(string sessionId, Guid patientId)
    {
        await _sessionStore.SetAsync(
            $"session:{sessionId}:active_patient",
            patientId,
            TimeSpan.FromHours(8));

        await Clients.Caller.SendAsync("PatientContextUpdated", patientId);
        _logger.LogInformation("Active patient set: {PatientId} for session {SessionId}", patientId, sessionId);
    }

    /// <summary>
    /// Clears session history and resets conversation context.
    /// </summary>
    public async Task ClearSession(string sessionId)
    {
        await _sessionStore.RemoveAsync($"session:{sessionId}:last_query");
        await _sessionStore.RemoveAsync($"session:{sessionId}:last_response");
        await _sessionStore.RemoveAsync($"session:{sessionId}:active_patient");

        await Clients.Caller.SendAsync("SessionCleared", sessionId);
        _logger.LogInformation("Session cleared: {SessionId}", sessionId);
    }
}
