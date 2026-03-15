using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using MediMind.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MediMind.Core.Plugins;

/// <summary>
/// SK Plugin for interpreting lab results, flagging abnormalities, and providing clinical context.
/// Data source: SQL Server via EF Core.
/// </summary>
public class LabResultPlugin
{
    private static readonly ActivitySource _activitySource = new("MediMind.Plugins", "1.0.0");

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILLMClient _llmClient;
    private readonly ILogger<LabResultPlugin> _logger;

    public LabResultPlugin(IUnitOfWork unitOfWork, ILLMClient llmClient, ILogger<LabResultPlugin> logger)
    {
        _unitOfWork = unitOfWork;
        _llmClient = llmClient;
        _logger = logger;
    }

    [KernelFunction("get_recent_lab_results")]
    [Description("Retrieve recent lab results for a patient, optionally filtering to abnormal results only.")]
    public async Task<string> GetRecentLabResultsAsync(
        [Description("The patient's unique identifier (GUID)")] string patientId,
        [Description("If true, return only abnormal results")] bool abnormalOnly = false,
        [Description("Number of past days to include (default: 30)")] int days = 30,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Plugin.LabResult.GetRecent", ActivityKind.Internal);
        activity?.SetTag("plugin.name", "LabResultPlugin");
        activity?.SetTag("plugin.function", "get_recent_lab_results");
        activity?.SetTag("patient.id", patientId);
        activity?.SetTag("abnormal_only", abnormalOnly);
        activity?.SetTag("days", days);

        _logger.LogInformation("LabResultPlugin: Fetching labs for patient {Id}, abnormalOnly={AO}, days={D}, TraceId: {TraceId}",
            patientId, abnormalOnly, days, Activity.Current?.TraceId.ToString() ?? "none");

        if (!Guid.TryParse(patientId, out var id))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid patient ID");
            return "Invalid patient ID format.";
        }

        var sw = Stopwatch.StartNew();
        var results = abnormalOnly
            ? await _unitOfWork.LabResults.GetAbnormalByPatientIdAsync(id, ct)
            : await _unitOfWork.LabResults.GetRecentByPatientIdAsync(id, days, ct);

        if (results.Count == 0)
        {
            activity?.SetTag("result", "no_data");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return abnormalOnly
                ? "No abnormal lab results found for this patient."
                : "No recent lab results found for this patient.";
        }

        var labData = results.Select(l => new
        {
            l.TestName,
            l.Value,
            l.Unit,
            l.ReferenceRange,
            l.IsAbnormal,
            Collected = l.CollectedAt.ToString("yyyy-MM-dd HH:mm")
        });

        var json = JsonSerializer.Serialize(labData, new JsonSerializerOptions { WriteIndented = true });
        sw.Stop();

        activity?.SetTag("result.count", results.Count);
        activity?.SetTag("total_latency_ms", sw.ElapsedMilliseconds);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return json;
    }

    [KernelFunction("interpret_lab_results")]
    [Description("Interpret a patient's lab results in clinical context, identifying concerning trends and recommending follow-up.")]
    public async Task<string> InterpretLabResultsAsync(
        [Description("The patient's unique identifier (GUID)")] string patientId,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Plugin.LabResult.Interpret", ActivityKind.Internal);
        activity?.SetTag("plugin.name", "LabResultPlugin");
        activity?.SetTag("plugin.function", "interpret_lab_results");
        activity?.SetTag("patient.id", patientId);

        if (!Guid.TryParse(patientId, out var id))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid patient ID");
            return "Invalid patient ID format.";
        }

        var results = await _unitOfWork.LabResults.GetRecentByPatientIdAsync(id, 90, ct);
        if (results.Count == 0)
        {
            activity?.SetTag("result", "no_data");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return "No lab results available for interpretation.";
        }

        var labSummary = string.Join("\n", results.Select(l =>
            $"- {l.TestName}: {l.Value} {l.Unit} (Ref: {l.ReferenceRange}) {(l.IsAbnormal ? "⚠️ ABNORMAL" : "✓")} [{l.CollectedAt:yyyy-MM-dd}]"));

        var response = await _llmClient.CompleteAsync(
            "You are a clinical laboratory medicine specialist. Interpret the following lab results, identify concerning values, note trends, and recommend follow-up testing or actions.",
            $"Patient lab results (last 90 days):\n{labSummary}",
            ct);

        activity?.SetTag("result.length", response.Content.Length);
        activity?.SetTag("lab_results.count", results.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return response.Content;
    }
}
