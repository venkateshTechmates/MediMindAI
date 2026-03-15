using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using MediMind.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.Agents;

/// <summary>
/// Specialist agent for drug interaction checking, dosage lookups, and contraindication detection.
/// </summary>
public class DrugAgent : BaseAgent
{
    private readonly DrugInteractionPlugin _plugin;

    public DrugAgent(ILLMClient llmClient, DrugInteractionPlugin plugin, ILogger<DrugAgent> logger)
        : base(llmClient, logger)
    {
        _plugin = plugin;
    }

    public override string AgentName => "DrugAgent";
    public override string SystemPrompt =>
        "You are a clinical pharmacology specialist. Given drug information retrieved from the formulary vector store and the patient's current medication list, identify interactions, contraindications, and safe dosage adjustments. Always cite the drug database source.";

    public async Task<AgentResult> CheckInteractionsAsync(string drugList, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(drugList, async () =>
            await _plugin.CheckInteractionsAsync(drugList, ct));
    }

    public async Task<AgentResult> LookupDosageAsync(string drugName, string? patientContext = null, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(drugName, async () =>
            await _plugin.LookupDosageAsync(drugName, patientContext, ct));
    }
}
