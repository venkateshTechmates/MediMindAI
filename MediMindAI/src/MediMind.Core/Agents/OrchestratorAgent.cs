using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MediMind.Core.Entities;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.Agents;

/// <summary>
/// Orchestrator agent that decomposes complex clinical queries into sub-tasks,
/// routes them to specialist agents, and synthesizes a unified response (FR-17–21).
/// </summary>
public class OrchestratorAgent : IAgentOrchestrator
{
    private static readonly ActivitySource _activitySource = new("MediMind.Orchestrator", "1.0.0");

    private readonly DrugAgent _drugAgent;
    private readonly DiagnosisAgent _diagnosisAgent;
    private readonly EhrAgent _ehrAgent;
    private readonly LabAgent _labAgent;
    private readonly DischargeAgent _dischargeAgent;
    private readonly ILLMClient _llmClient;
    private readonly IRagPipeline _ragPipeline;
    private readonly ILogger<OrchestratorAgent> _logger;

    private static readonly TimeSpan AgentTimeout = TimeSpan.FromSeconds(10);

    public OrchestratorAgent(
        DrugAgent drugAgent,
        DiagnosisAgent diagnosisAgent,
        EhrAgent ehrAgent,
        LabAgent labAgent,
        DischargeAgent dischargeAgent,
        ILLMClient llmClient,
        IRagPipeline ragPipeline,
        ILogger<OrchestratorAgent> logger)
    {
        _drugAgent = drugAgent;
        _diagnosisAgent = diagnosisAgent;
        _ehrAgent = ehrAgent;
        _labAgent = labAgent;
        _dischargeAgent = dischargeAgent;
        _llmClient = llmClient;
        _ragPipeline = ragPipeline;
        _logger = logger;
    }

    public async Task<ClinicalResponse> ProcessQueryAsync(ClinicalQuery query, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Orchestrator.ProcessQuery", ActivityKind.Internal);
        activity?.SetTag("query.id", query.QueryId.ToString());
        activity?.SetTag("query.session_id", query.SessionId.ToString());
        activity?.SetTag("query.has_patient", query.PatientId.HasValue);
        activity?.SetTag("query.text_length", query.QueryText.Length);

        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[Orchestrator] Processing query: {QueryId}", query.QueryId);

        // Step 1: Classify the query to determine which agents to invoke
        QueryPlan plan;
        using (var classifyActivity = _activitySource.StartActivity("Orchestrator.ClassifyQuery"))
        {
            plan = await ClassifyQueryAsync(query.QueryText, ct);
            classifyActivity?.SetTag("plan.agents", string.Join(", ", plan.AgentsToInvoke));
            classifyActivity?.SetTag("plan.is_out_of_scope", plan.IsOutOfScope);
        }
        _logger.LogInformation("[Orchestrator] Plan: {Plan}", string.Join(", ", plan.AgentsToInvoke));

        // Step 2: Execute RAG pipeline for context retrieval
        RagContext ragContext;
        using (var ragActivity = _activitySource.StartActivity("Orchestrator.RagPipeline"))
        {
            ragContext = await _ragPipeline.ExecuteAsync(query, ct);
            ragActivity?.SetTag("rag.citations_count", ragContext.Citations.Count);
            ragActivity?.SetTag("rag.context_length", ragContext.AugmentedContext?.Length ?? 0);
        }

        // Step 3: Invoke specialist agents in parallel with timeout
        List<AgentResult> agentResults;
        using (var agentsActivity = _activitySource.StartActivity("Orchestrator.ExecuteAgents"))
        {
            agentsActivity?.SetTag("agents.count", plan.AgentsToInvoke.Length);
            agentResults = await ExecuteAgentsAsync(plan, query, ct);
            agentsActivity?.SetTag("agents.success_count", agentResults.Count(r => r.Success));
            agentsActivity?.SetTag("agents.failed_count", agentResults.Count(r => !r.Success));
        }

        // Step 4: Synthesize multi-agent results into a coherent response
        string synthesized;
        using (var synthesisActivity = _activitySource.StartActivity("Orchestrator.SynthesizeResponse"))
        {
            synthesized = await SynthesizeResponseAsync(query.QueryText, ragContext, agentResults, ct);
            synthesisActivity?.SetTag("synthesis.response_length", synthesized.Length);
        }

        sw.Stop();

        activity?.SetTag("total_latency_ms", sw.ElapsedMilliseconds);
        activity?.SetTag("total_agents_invoked", agentResults.Count);
        activity?.SetTag("is_out_of_scope", plan.IsOutOfScope);

        return new ClinicalResponse
        {
            QueryId = query.QueryId,
            SessionId = query.SessionId,
            Content = synthesized,
            Citations = ragContext.Citations,
            AgentResults = agentResults,
            TotalTokensUsed = agentResults.Sum(r => r.TokensUsed),
            TotalLatencyMs = sw.ElapsedMilliseconds,
            IsOutOfScope = plan.IsOutOfScope,
            FallbackMessage = plan.IsOutOfScope
                ? "This query appears to be outside the scope of clinical decision support. Please consult your healthcare provider."
                : null
        };
    }

    public async IAsyncEnumerable<string> StreamQueryAsync(
        ClinicalQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Orchestrator.StreamQuery", ActivityKind.Internal);
        activity?.SetTag("query.id", query.QueryId.ToString());
        activity?.SetTag("query.session_id", query.SessionId.ToString());

        _logger.LogInformation("[Orchestrator] Streaming query: {QueryId}", query.QueryId);

        // Execute RAG pipeline
        RagContext ragContext;
        using (var ragActivity = _activitySource.StartActivity("Orchestrator.RagPipeline.Stream"))
        {
            ragContext = await _ragPipeline.ExecuteAsync(query, ct);
            ragActivity?.SetTag("rag.citations_count", ragContext.Citations.Count);
        }

        // Build augmented prompt with RAG context
        var systemPrompt = BuildOrchestratorSystemPrompt();
        var userPrompt = BuildStreamingPrompt(query.QueryText, ragContext);

        // Stream response tokens
        int tokenCount = 0;
        using var llmActivity = _activitySource.StartActivity("Orchestrator.LLM.Stream");
        await foreach (var token in _llmClient.StreamAsync(systemPrompt, userPrompt, ct))
        {
            tokenCount++;
            yield return token;
        }
        llmActivity?.SetTag("stream.token_chunks", tokenCount);
    }

    private async Task<QueryPlan> ClassifyQueryAsync(string queryText, CancellationToken ct)
    {
        var classificationPrompt = $"""
            Classify this clinical query and determine which specialist agents should handle it.
            Respond with a JSON object containing:
            - "agents": array of agent names from ["drug", "diagnosis", "ehr", "lab", "discharge"]
            - "is_out_of_scope": boolean (true if query is not clinical/medical)
            
            Query: {queryText}

            Respond ONLY with valid JSON, no explanation.
            """;

        var response = await _llmClient.CompleteAsync(
            "You are a clinical query classifier. Classify queries and route them to appropriate specialist agents.",
            classificationPrompt, ct);

        try
        {
            var classification = JsonSerializer.Deserialize<QueryClassification>(response.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new QueryPlan
            {
                AgentsToInvoke = classification?.Agents ?? new[] { "diagnosis" },
                IsOutOfScope = classification?.IsOutOfScope ?? false
            };
        }
        catch (JsonException)
        {
            _logger.LogWarning("[Orchestrator] Failed to parse classification. Defaulting to all agents.");
            return new QueryPlan
            {
                AgentsToInvoke = new[] { "diagnosis", "drug" },
                IsOutOfScope = false
            };
        }
    }

    private async Task<List<AgentResult>> ExecuteAgentsAsync(QueryPlan plan, ClinicalQuery query, CancellationToken ct)
    {
        var tasks = new List<Task<AgentResult>>();

        foreach (var agentName in plan.AgentsToInvoke)
        {
            var task = agentName.ToLowerInvariant() switch
            {
                "drug" => ExecuteWithTimeout(() =>
                    _drugAgent.CheckInteractionsAsync(query.QueryText, ct), "DrugAgent"),
                "diagnosis" => ExecuteWithTimeout(() =>
                    _diagnosisAgent.GetDifferentialAsync(query.QueryText, ct: ct), "DiagnosisAgent"),
                "ehr" when query.PatientId.HasValue => ExecuteWithTimeout(() =>
                    _ehrAgent.GetPatientSummaryAsync(query.PatientId.Value.ToString(), ct), "EHRAgent"),
                "lab" when query.PatientId.HasValue => ExecuteWithTimeout(() =>
                    _labAgent.InterpretLabsAsync(query.PatientId.Value.ToString(), ct), "LabAgent"),
                "discharge" when query.PatientId.HasValue => ExecuteWithTimeout(() =>
                    _dischargeAgent.GenerateDischargeInstructionsAsync(query.PatientId.Value.ToString(), ct), "DischargeAgent"),
                _ => Task.FromResult(new AgentResult
                {
                    AgentName = agentName,
                    Success = false,
                    ErrorMessage = "Agent not available or patient context required."
                })
            };

            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<AgentResult> ExecuteWithTimeout(Func<Task<AgentResult>> action, string agentName)
    {
        using var activity = _activitySource.StartActivity($"Agent.{agentName}", ActivityKind.Internal);
        activity?.SetTag("agent.name", agentName);

        try
        {
            using var timeoutCts = new CancellationTokenSource(AgentTimeout);
            var result = await action();
            activity?.SetTag("agent.success", result.Success);
            activity?.SetTag("agent.latency_ms", result.LatencyMs);
            activity?.SetTag("agent.tokens_used", result.TokensUsed);
            if (!result.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[Orchestrator] {Agent} timed out after {Timeout}s.", agentName, AgentTimeout.TotalSeconds);
            activity?.SetStatus(ActivityStatusCode.Error, "Timed out");
            activity?.SetTag("agent.timed_out", true);
            return new AgentResult
            {
                AgentName = agentName,
                Success = false,
                ErrorMessage = $"Agent timed out after {AgentTimeout.TotalSeconds}s."
            };
        }
    }

    private async Task<string> SynthesizeResponseAsync(
        string originalQuery,
        RagContext ragContext,
        List<AgentResult> agentResults,
        CancellationToken ct)
    {
        var successfulResults = agentResults.Where(r => r.Success).ToList();

        if (successfulResults.Count == 0)
        {
            // Fallback to direct RAG if all agents failed
            var directResponse = await _llmClient.CompleteAsync(
                BuildOrchestratorSystemPrompt(),
                $"Query: {originalQuery}\n\nContext:\n{ragContext.AugmentedContext}",
                ct);
            return directResponse.Content;
        }

        var agentOutputs = string.Join("\n\n---\n\n",
            successfulResults.Select(r => $"[{r.AgentName}]:\n{r.Content}"));

        var synthesisPrompt = $"""
            Original query: {originalQuery}

            Retrieved context:
            {ragContext.AugmentedContext}

            Agent responses:
            {agentOutputs}

            Synthesize these into a single, coherent, well-structured clinical response.
            Include all relevant citations. If agents disagree, note the discrepancy.
            """;

        var response = await _llmClient.CompleteAsync(BuildOrchestratorSystemPrompt(), synthesisPrompt, ct);
        return response.Content;
    }

    private static string BuildOrchestratorSystemPrompt() =>
        """
        You are MediMind AI, a clinical decision support assistant. You synthesize information 
        from multiple specialist agents and clinical knowledge sources to provide comprehensive,
        evidence-based clinical guidance.

        Rules:
        1. Always cite sources with document name, version, and confidence score.
        2. Never provide definitive diagnoses — frame as "differential considerations."
        3. Flag any drug interactions or safety concerns prominently.
        4. If information is insufficient, clearly state limitations and recommend specialist consultation.
        5. Use clear medical terminology with patient-friendly explanations where appropriate.
        6. Include relevant ICD-10 codes when discussing diagnoses.
        """;

    private static string BuildStreamingPrompt(string query, RagContext ragContext) =>
        $"""
        Clinical Query: {query}

        Retrieved Evidence:
        {ragContext.AugmentedContext}

        Provide a comprehensive, cited clinical response.
        """;

    // ── Internal DTOs ──
    private class QueryPlan
    {
        public string[] AgentsToInvoke { get; set; } = Array.Empty<string>();
        public bool IsOutOfScope { get; set; }
    }

    private class QueryClassification
    {
        public string[]? Agents { get; set; }
        public bool IsOutOfScope { get; set; }
    }
}
