using System.ComponentModel;
using System.Diagnostics;
using MediMind.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MediMind.Core.Plugins;

/// <summary>
/// SK Plugin for clinical guideline retrieval and differential diagnosis support.
/// Data source: Qdrant (clinical guideline vectors — WHO, CDC, NICE).
/// </summary>
public class ClinicalGuidelinePlugin
{
    private static readonly ActivitySource _activitySource = new("MediMind.Plugins", "1.0.0");

    private readonly IVectorStore _vectorStore;
    private readonly ILLMClient _llmClient;
    private readonly ILogger<ClinicalGuidelinePlugin> _logger;
    private const string Collection = "medimind_clinical";

    public ClinicalGuidelinePlugin(IVectorStore vectorStore, ILLMClient llmClient, ILogger<ClinicalGuidelinePlugin> logger)
    {
        _vectorStore = vectorStore;
        _llmClient = llmClient;
        _logger = logger;
    }

    [KernelFunction("get_differential_diagnosis")]
    [Description("Given symptoms and clinical findings, retrieve relevant guidelines and suggest a ranked differential diagnosis.")]
    public async Task<string> GetDifferentialDiagnosisAsync(
        [Description("Chief complaint and symptoms")] string symptoms,
        [Description("Relevant clinical findings (vitals, exam, etc.)")] string? clinicalFindings = null,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Plugin.ClinicalGuideline.DifferentialDiagnosis", ActivityKind.Internal);
        activity?.SetTag("plugin.name", "ClinicalGuidelinePlugin");
        activity?.SetTag("plugin.function", "get_differential_diagnosis");
        activity?.SetTag("symptoms.length", symptoms.Length);

        _logger.LogInformation("ClinicalGuidelinePlugin: Differential diagnosis for: {Symptoms}, TraceId: {TraceId}",
            symptoms, Activity.Current?.TraceId.ToString() ?? "none");

        var sw = Stopwatch.StartNew();

        var queryText = $"differential diagnosis for {symptoms}";
        if (!string.IsNullOrEmpty(clinicalFindings))
            queryText += $" with findings: {clinicalFindings}";

        var queryVector = await _llmClient.EmbedAsync(queryText, ct);

        var chunks = await _vectorStore.SearchAsync(
            queryVector,
            topK: 8,
            filters: new Dictionary<string, string> { { "category", "clinical-guideline" } },
            collectionName: Collection,
            ct: ct);

        var context = string.Join("\n\n", chunks.Select(c =>
            $"[Source: {c.Metadata.Source}, Guideline: {c.Metadata.GuidelineType}, Version: {c.Metadata.Version}]\n{c.Content}"));

        var systemPrompt = "You are a clinical diagnostician. Using retrieved clinical guidelines and the patient's chief complaint, suggest a ranked differential diagnosis with supporting evidence. Reference the specific guideline and version.";
        var userPrompt = $"Patient presents with: {symptoms}\n";
        if (!string.IsNullOrEmpty(clinicalFindings))
            userPrompt += $"Clinical findings: {clinicalFindings}\n";
        userPrompt += $"\nRetrieved guidelines:\n{context}";

        var response = await _llmClient.CompleteAsync(systemPrompt, userPrompt, ct);
        sw.Stop();

        activity?.SetTag("search.results_count", chunks.Count);
        activity?.SetTag("result.length", response.Content.Length);
        activity?.SetTag("total_latency_ms", sw.ElapsedMilliseconds);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogInformation("ClinicalGuidelinePlugin: DifferentialDiagnosis completed in {Ms}ms", sw.ElapsedMilliseconds);
        return response.Content;
    }

    [KernelFunction("get_treatment_protocol")]
    [Description("Retrieve evidence-based treatment protocol for a given diagnosis.")]
    public async Task<string> GetTreatmentProtocolAsync(
        [Description("The diagnosis to look up treatment for")] string diagnosis,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Plugin.ClinicalGuideline.TreatmentProtocol", ActivityKind.Internal);
        activity?.SetTag("plugin.name", "ClinicalGuidelinePlugin");
        activity?.SetTag("plugin.function", "get_treatment_protocol");
        activity?.SetTag("diagnosis", diagnosis);

        _logger.LogInformation("ClinicalGuidelinePlugin: Treatment protocol for: {Diagnosis}, TraceId: {TraceId}",
            diagnosis, Activity.Current?.TraceId.ToString() ?? "none");

        var sw = Stopwatch.StartNew();
        var queryVector = await _llmClient.EmbedAsync($"treatment protocol for {diagnosis}", ct);

        var chunks = await _vectorStore.SearchAsync(queryVector, topK: 6, collectionName: Collection, ct: ct);

        var context = string.Join("\n\n", chunks.Select(c =>
            $"[{c.Metadata.Source} — {c.Metadata.GuidelineType}]\n{c.Content}"));

        var response = await _llmClient.CompleteAsync(
            "You are a clinical guidelines specialist. Provide evidence-based treatment protocols with citations.",
            $"What is the recommended treatment protocol for {diagnosis}?\n\nGuideline data:\n{context}",
            ct);
        sw.Stop();

        activity?.SetTag("result.length", response.Content.Length);
        activity?.SetTag("total_latency_ms", sw.ElapsedMilliseconds);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return response.Content;
    }
}
