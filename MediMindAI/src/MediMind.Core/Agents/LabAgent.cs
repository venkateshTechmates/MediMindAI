using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using MediMind.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.Agents;

/// <summary>
/// Specialist agent for lab result interpretation and trend analysis.
/// </summary>
public class LabAgent : BaseAgent
{
    private readonly LabResultPlugin _plugin;

    public LabAgent(ILLMClient llmClient, LabResultPlugin plugin, ILogger<LabAgent> logger)
        : base(llmClient, logger)
    {
        _plugin = plugin;
    }

    public override string AgentName => "LabAgent";
    public override string SystemPrompt =>
        "You are a clinical laboratory medicine specialist. Interpret lab results, identify abnormalities, detect trends, and recommend follow-up testing. Always reference normal ranges.";

    public async Task<AgentResult> GetRecentLabsAsync(string patientId, bool abnormalOnly = false, int days = 30, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(patientId, async () =>
            await _plugin.GetRecentLabResultsAsync(patientId, abnormalOnly, days, ct));
    }

    public async Task<AgentResult> InterpretLabsAsync(string patientId, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(patientId, async () =>
            await _plugin.InterpretLabResultsAsync(patientId, ct));
    }
}
