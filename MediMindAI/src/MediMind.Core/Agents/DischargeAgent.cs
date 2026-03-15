using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.Agents;

/// <summary>
/// Specialist agent for generating post-discharge instructions and medication plans.
/// Combines data from Qdrant (post-care protocols) and SQL Server (patient records).
/// </summary>
public class DischargeAgent : BaseAgent
{
    private readonly IVectorStore _vectorStore;
    private readonly IUnitOfWork _unitOfWork;

    public DischargeAgent(
        ILLMClient llmClient,
        IVectorStore vectorStore,
        IUnitOfWork unitOfWork,
        ILogger<DischargeAgent> logger)
        : base(llmClient, logger)
    {
        _vectorStore = vectorStore;
        _unitOfWork = unitOfWork;
    }

    public override string AgentName => "DischargeAgent";
    public override string SystemPrompt =>
        "You are a discharge planning specialist. Using the patient's diagnosis, medications, and retrieved post-care protocols, generate clear, patient-friendly discharge instructions. Include medication schedule, warning signs, and follow-up timeline.";

    public async Task<AgentResult> GenerateDischargeInstructionsAsync(string patientId, CancellationToken ct = default)
    {
        return await ExecuteWithTracing(patientId, async () =>
        {
            if (!Guid.TryParse(patientId, out var id))
                return "Invalid patient ID.";

            // Get patient data from SQL
            var patient = await _unitOfWork.Patients.GetFullProfileAsync(id, ct);
            if (patient is null)
                return $"Patient {patientId} not found.";

            var latestEncounter = patient.Encounters.FirstOrDefault();
            var activeMeds = patient.Medications.Where(m => m.IsActive).ToList();

            var diagnosis = latestEncounter?.Diagnosis ?? "Unknown";
            var chiefComplaint = latestEncounter?.ChiefComplaint ?? "Not specified";

            // Search for relevant discharge protocols in Qdrant
            var queryVector = await LlmClient.EmbedAsync(
                $"discharge instructions for {diagnosis} {chiefComplaint}", ct);

            var chunks = await _vectorStore.SearchAsync(queryVector, topK: 5, ct: ct);

            var protocolContext = string.Join("\n\n", chunks.Select(c =>
                $"[{c.Metadata.Source}]\n{c.Content}"));

            var medicationList = string.Join("\n", activeMeds.Select(m =>
                $"- {m.DrugName} {m.Dosage} {m.Frequency}"));

            var prompt = $"""
                Patient: {patient.FullName}
                Diagnosis: {diagnosis}
                Chief Complaint: {chiefComplaint}
                Allergies: {patient.Allergies ?? "NKDA"}
                
                Current Medications:
                {medicationList}
                
                Retrieved Discharge Protocols:
                {protocolContext}

                Generate comprehensive, patient-friendly discharge instructions.
                """;

            var response = await LlmClient.CompleteAsync(SystemPrompt, prompt, ct);
            return response.Content;
        });
    }
}
