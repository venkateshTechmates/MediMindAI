using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using MediMind.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.Agents;

/// <summary>
/// Specialist agent for patient EHR data retrieval and summary.
/// </summary>
public class EhrAgent : BaseAgent
{
    private readonly PatientRecordPlugin _plugin;

    public EhrAgent(ILLMClient llmClient, PatientRecordPlugin plugin, ILogger<EhrAgent> logger)
        : base(llmClient, logger)
    {
        _plugin = plugin;
    }

    public override string AgentName => "EHRAgent";
    public override string SystemPrompt =>
        "You are an EHR data specialist. Retrieve and summarize patient records including demographics, encounters, medications, allergies, and lab results. Present data clearly and flag clinically relevant items.";

    public async Task<AgentResult> GetPatientSummaryAsync(string patientId, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(patientId, async () =>
            await _plugin.GetPatientSummaryAsync(patientId, ct));
    }

    public async Task<AgentResult> GetAllergiesAsync(string patientId, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(patientId, async () =>
            await _plugin.GetPatientAllergiesAsync(patientId, ct));
    }

    public async Task<AgentResult> GetActiveMedicationsAsync(string patientId, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(patientId, async () =>
            await _plugin.GetActiveMedicationsAsync(patientId, ct));
    }
}
