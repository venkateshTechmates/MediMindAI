using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using MediMind.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MediMind.Core.Plugins;

/// <summary>
/// SK Plugin for patient record retrieval from SQL Server via EF Core repositories.
/// </summary>
public class PatientRecordPlugin
{
    private static readonly ActivitySource _activitySource = new("MediMind.Plugins", "1.0.0");

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PatientRecordPlugin> _logger;

    public PatientRecordPlugin(IUnitOfWork unitOfWork, ILogger<PatientRecordPlugin> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [KernelFunction("get_patient_summary")]
    [Description("Retrieve a comprehensive patient summary including demographics, allergies, active medications, recent encounters, and lab results.")]
    public async Task<string> GetPatientSummaryAsync(
        [Description("The patient's unique identifier (GUID)")] string patientId,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Plugin.PatientRecord.GetSummary", ActivityKind.Internal);
        activity?.SetTag("plugin.name", "PatientRecordPlugin");
        activity?.SetTag("plugin.function", "get_patient_summary");
        activity?.SetTag("patient.id", patientId);

        _logger.LogInformation("PatientRecordPlugin: Fetching summary for patient {Id}, TraceId: {TraceId}",
            patientId, Activity.Current?.TraceId.ToString() ?? "none");

        if (!Guid.TryParse(patientId, out var id))
        {
            activity?.SetTag("error", "invalid_patient_id");
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid patient ID format");
            return "Invalid patient ID format.";
        }

        var sw = Stopwatch.StartNew();
        var patient = await _unitOfWork.Patients.GetFullProfileAsync(id, ct);
        if (patient is null)
        {
            activity?.SetTag("result", "not_found");
            activity?.SetStatus(ActivityStatusCode.Ok, "Patient not found");
            return $"Patient with ID {patientId} not found.";
        }

        var summary = new
        {
            patient.FullName,
            DateOfBirth = patient.DateOfBirth.ToString("yyyy-MM-dd"),
            patient.Gender,
            patient.BloodGroup,
            Allergies = patient.Allergies,
            ActiveMedications = patient.Medications
                .Where(m => m.IsActive)
                .Select(m => new { m.DrugName, m.Dosage, m.Frequency })
                .ToList(),
            RecentEncounters = patient.Encounters
                .Take(5)
                .Select(e => new
                {
                    Date = e.EncounterDate.ToString("yyyy-MM-dd"),
                    e.ChiefComplaint,
                    e.Diagnosis
                })
                .ToList(),
            RecentLabResults = patient.LabResults
                .Take(10)
                .Select(l => new
                {
                    l.TestName,
                    l.Value,
                    l.Unit,
                    l.ReferenceRange,
                    l.IsAbnormal,
                    Collected = l.CollectedAt.ToString("yyyy-MM-dd")
                })
                .ToList()
        };

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
    }

    [KernelFunction("get_patient_allergies")]
    [Description("Get the list of known allergies for a patient.")]
    public async Task<string> GetPatientAllergiesAsync(
        [Description("The patient's unique identifier (GUID)")] string patientId,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Plugin.PatientRecord.GetAllergies", ActivityKind.Internal);
        activity?.SetTag("plugin.name", "PatientRecordPlugin");
        activity?.SetTag("plugin.function", "get_patient_allergies");
        activity?.SetTag("patient.id", patientId);

        if (!Guid.TryParse(patientId, out var id))
            return "Invalid patient ID format.";

        var patient = await _unitOfWork.Patients.GetByIdAsync(id, ct);
        if (patient is null)
            return $"Patient {patientId} not found.";

        return patient.Allergies ?? "No known allergies.";
    }

    [KernelFunction("get_active_medications")]
    [Description("Get the list of currently active medications for a patient.")]
    public async Task<string> GetActiveMedicationsAsync(
        [Description("The patient's unique identifier (GUID)")] string patientId,
        CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Plugin.PatientRecord.GetActiveMedications", ActivityKind.Internal);
        activity?.SetTag("plugin.name", "PatientRecordPlugin");
        activity?.SetTag("plugin.function", "get_active_medications");
        activity?.SetTag("patient.id", patientId);

        if (!Guid.TryParse(patientId, out var id))
            return "Invalid patient ID format.";

        var medications = await _unitOfWork.Medications.GetActiveByPatientIdAsync(id, ct);

        if (medications.Count == 0)
            return "No active medications found.";

        var result = medications.Select(m => new
        {
            m.DrugName,
            m.Dosage,
            m.Frequency,
            Since = m.StartDate.ToString("yyyy-MM-dd")
        });

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
