using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using MediMind.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.Agents;

/// <summary>
/// Specialist agent for differential diagnosis and clinical guideline retrieval.
/// </summary>
public class DiagnosisAgent : BaseAgent
{
    private readonly ClinicalGuidelinePlugin _plugin;

    public DiagnosisAgent(ILLMClient llmClient, ClinicalGuidelinePlugin plugin, ILogger<DiagnosisAgent> logger)
        : base(llmClient, logger)
    {
        _plugin = plugin;
    }

    public override string AgentName => "DiagnosisAgent";
    public override string SystemPrompt =>
        "You are a clinical diagnostician. Using retrieved clinical guidelines and the patient's chief complaint, suggest a ranked differential diagnosis with supporting evidence. Reference the specific guideline and version.";

    public async Task<AgentResult> GetDifferentialAsync(string symptoms, string? findings = null, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(symptoms, async () =>
            await _plugin.GetDifferentialDiagnosisAsync(symptoms, findings, ct));
    }

    public async Task<AgentResult> GetTreatmentAsync(string diagnosis, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(diagnosis, async () =>
            await _plugin.GetTreatmentProtocolAsync(diagnosis, ct));
    }
}
