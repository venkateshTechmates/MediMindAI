using System.ComponentModel;
using System.Diagnostics;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MediMind.Core.Plugins;

/// <summary>
/// SK Plugin for drug interaction checking, dosage lookup, and contraindication detection.
/// Data source: Qdrant (drug formulary vectors).
/// </summary>
public class DrugInteractionPlugin
{
    private static readonly ActivitySource _activitySource = new("MediMind.Plugins", "1.0.0");

    private readonly IVectorStore _vectorStore;
    private readonly ILLMClient _llmClient;
    private readonly ILogger<DrugInteractionPlugin> _logger;
    private const string Collection = "medimind_drug_formulary";

    public DrugInteractionPlugin(IVectorStore vectorStore, ILLMClient llmClient, ILogger<DrugInteractionPlugin> logger)
    {
        _vectorStore = vectorStore;
        _llmClient = llmClient;
        _logger = logger;
    }

    [KernelFunction("check_drug_interactions")]
    [Description("Check for drug-drug interactions given a list of medications. Returns interaction details, severity, and recommendations.")]
    public async Task<string> CheckInteractionsAsync(
        [Description("Comma-separated list of drug names to check for interactions")] string drugNames,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Plugin.DrugInteraction.CheckInteractions", ActivityKind.Internal);
        activity?.SetTag("plugin.name", "DrugInteractionPlugin");
        activity?.SetTag("plugin.function", "check_drug_interactions");
        activity?.SetTag("drug.names", drugNames);

        _logger.LogInformation("DrugInteractionPlugin: Checking interactions for: {Drugs}, TraceId: {TraceId}",
            drugNames, Activity.Current?.TraceId.ToString() ?? "none");

        var sw = Stopwatch.StartNew();
        var queryVector = await _llmClient.EmbedAsync($"drug interactions between {drugNames}", ct);

        var chunks = await _vectorStore.SearchAsync(
            queryVector,
            topK: 6,
            filters: new Dictionary<string, string> { { "category", "drug-formulary" } },
            collectionName: Collection,
            ct: ct);

        activity?.SetTag("search.results_count", chunks.Count);

        if (chunks.Count == 0)
        {
            activity?.SetTag("result", "no_data");
            activity?.SetStatus(ActivityStatusCode.Ok, "No interaction data found");
            return $"No drug interaction data found for: {drugNames}. Consult a clinical pharmacist.";
        }

        var context = string.Join("\n\n", chunks.Select(c =>
            $"[Source: {c.Metadata.Source}, Drug Class: {c.Metadata.DrugClass}]\n{c.Content}"));

        var systemPrompt = "You are a clinical pharmacology specialist. Given drug information retrieved from the formulary vector store and the patient's current medication list, identify interactions, contraindications, and safe dosage adjustments. Always cite the drug database source.";
        var userPrompt = $"Check for interactions between these drugs: {drugNames}\n\nRetrieved formulary data:\n{context}";

        var response = await _llmClient.CompleteAsync(systemPrompt, userPrompt, ct);
        sw.Stop();

        activity?.SetTag("result.length", response.Content.Length);
        activity?.SetTag("total_latency_ms", sw.ElapsedMilliseconds);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogInformation("DrugInteractionPlugin: Completed in {Ms}ms", sw.ElapsedMilliseconds);
        return response.Content;
    }

    [KernelFunction("lookup_drug_dosage")]
    [Description("Look up recommended dosage, frequency, and administration route for a specific drug.")]
    public async Task<string> LookupDosageAsync(
        [Description("The drug name to look up dosage for")] string drugName,
        [Description("Patient context such as age, weight, renal function (optional)")] string? patientContext = null,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Plugin.DrugInteraction.LookupDosage", ActivityKind.Internal);
        activity?.SetTag("plugin.name", "DrugInteractionPlugin");
        activity?.SetTag("plugin.function", "lookup_drug_dosage");
        activity?.SetTag("drug.name", drugName);

        _logger.LogInformation("DrugInteractionPlugin: Looking up dosage for: {Drug}, TraceId: {TraceId}",
            drugName, Activity.Current?.TraceId.ToString() ?? "none");

        var sw = Stopwatch.StartNew();
        var queryVector = await _llmClient.EmbedAsync($"dosage and administration for {drugName}", ct);

        var chunks = await _vectorStore.SearchAsync(
            queryVector,
            topK: 4,
            filters: new Dictionary<string, string> { { "category", "drug-formulary" } },
            collectionName: Collection,
            ct: ct);

        var context = string.Join("\n\n", chunks.Select(c => $"[{c.Metadata.Source}]\n{c.Content}"));

        var prompt = $"What is the recommended dosage for {drugName}?";
        if (!string.IsNullOrEmpty(patientContext))
            prompt += $"\nPatient context: {patientContext}";
        prompt += $"\n\nFormulary data:\n{context}";

        var response = await _llmClient.CompleteAsync(
            "You are a clinical pharmacology specialist. Provide dosage recommendations with citations.",
            prompt, ct);
        sw.Stop();

        activity?.SetTag("result.length", response.Content.Length);
        activity?.SetTag("total_latency_ms", sw.ElapsedMilliseconds);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return response.Content;
    }
}
