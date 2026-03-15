using MediMind.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MediMind.Infrastructure.Persistence;

/// <summary>
/// Seeds the database with anonymized synthetic patient data for local testing (FR-43).
/// All data is fictional and generated for development/testing purposes only.
/// </summary>
public class DataSeeder
{
    private readonly MediMindDbContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(MediMindDbContext context, ILogger<DataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (await _context.Patients.AnyAsync())
        {
            _logger.LogInformation("Database already seeded. Skipping.");
            return;
        }

        _logger.LogInformation("Seeding database with synthetic clinical data...");

        var patients = CreatePatients();
        await _context.Patients.AddRangeAsync(patients);

        var encounters = CreateEncounters(patients);
        await _context.Encounters.AddRangeAsync(encounters);

        var medications = CreateMedications(patients, encounters);
        await _context.Medications.AddRangeAsync(medications);

        var labResults = CreateLabResults(patients);
        await _context.LabResults.AddRangeAsync(labResults);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {PatientCount} patients, {EncounterCount} encounters, " +
                             "{MedicationCount} medications, {LabCount} lab results.",
            patients.Count, encounters.Count, medications.Count, labResults.Count);
    }

    private static List<Patient> CreatePatients()
    {
        return new List<Patient>
        {
            new()
            {
                Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000001"),
                FullName = "Synthetic Patient Alpha",
                DateOfBirth = new DateTime(1985, 3, 15),
                Gender = "Male",
                BloodGroup = "A+",
                Allergies = JsonSerializer.Serialize(new[] { "Penicillin", "Sulfa drugs" }),
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000002"),
                FullName = "Synthetic Patient Beta",
                DateOfBirth = new DateTime(1992, 7, 22),
                Gender = "Female",
                BloodGroup = "O-",
                Allergies = JsonSerializer.Serialize(new[] { "Aspirin" }),
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000003"),
                FullName = "Synthetic Patient Gamma",
                DateOfBirth = new DateTime(1970, 11, 8),
                Gender = "Male",
                BloodGroup = "B+",
                Allergies = JsonSerializer.Serialize(new[] { "Latex", "Iodine" }),
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000004"),
                FullName = "Synthetic Patient Delta",
                DateOfBirth = new DateTime(2001, 1, 30),
                Gender = "Female",
                BloodGroup = "AB+",
                Allergies = JsonSerializer.Serialize(Array.Empty<string>()),
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000005"),
                FullName = "Synthetic Patient Epsilon",
                DateOfBirth = new DateTime(1958, 6, 12),
                Gender = "Male",
                BloodGroup = "O+",
                Allergies = JsonSerializer.Serialize(new[] { "NSAIDs", "ACE inhibitors" }),
                IsActive = true
            }
        };
    }

    private static List<Encounter> CreateEncounters(List<Patient> patients)
    {
        var encounters = new List<Encounter>();
        var baseDate = DateTime.UtcNow;

        // Patient Alpha — Type 2 Diabetes management
        encounters.Add(new Encounter
        {
            Id = Guid.Parse("b1b2c3d4-0002-0001-0001-000000000001"),
            PatientId = patients[0].Id,
            ClinicianId = "DR-SMITH-001",
            EncounterDate = baseDate.AddDays(-30),
            ChiefComplaint = "Routine diabetes follow-up, reports occasional dizziness",
            Diagnosis = JsonSerializer.Serialize(new { Primary = "Type 2 Diabetes Mellitus — E11.9", Secondary = "Essential Hypertension — I10" }),
            Notes = "HbA1c elevated at 8.2%. Adjusted metformin dosage. BP 142/88. Added lisinopril.",
            DischargeInstructions = "Continue metformin 1000mg BID. Start lisinopril 10mg daily. Recheck HbA1c in 3 months."
        });

        // Patient Beta — Post-surgical follow-up
        encounters.Add(new Encounter
        {
            Id = Guid.Parse("b1b2c3d4-0002-0001-0001-000000000002"),
            PatientId = patients[1].Id,
            ClinicianId = "DR-JOHNSON-002",
            EncounterDate = baseDate.AddDays(-7),
            ChiefComplaint = "Post-appendectomy follow-up, mild incisional pain",
            Diagnosis = JsonSerializer.Serialize(new { Primary = "Post-operative follow-up — Z09", Secondary = "Acute appendicitis status — K35.80" }),
            Notes = "Incision healing well. No signs of infection. Pain managed with acetaminophen.",
            DischargeInstructions = "Keep incision clean and dry. Take acetaminophen 500mg PRN for pain. Return if fever > 101°F or increasing redness."
        });

        // Patient Gamma — Cardiac assessment
        encounters.Add(new Encounter
        {
            Id = Guid.Parse("b1b2c3d4-0002-0001-0001-000000000003"),
            PatientId = patients[2].Id,
            ClinicianId = "DR-PATEL-003",
            EncounterDate = baseDate.AddDays(-14),
            ChiefComplaint = "Chest pain on exertion, shortness of breath",
            Diagnosis = JsonSerializer.Serialize(new { Primary = "Stable Angina Pectoris — I20.8", Secondary = "Hyperlipidemia — E78.5" }),
            Notes = "ECG shows ST depression in leads V4–V6. Stress test scheduled. Started on aspirin and atorvastatin.",
            DischargeInstructions = "Take aspirin 81mg daily and atorvastatin 40mg at bedtime. Avoid strenuous activity until stress test results. Call 911 if chest pain at rest."
        });

        // Patient Delta — Routine wellness
        encounters.Add(new Encounter
        {
            Id = Guid.Parse("b1b2c3d4-0002-0001-0001-000000000004"),
            PatientId = patients[3].Id,
            ClinicianId = "DR-SMITH-001",
            EncounterDate = baseDate.AddDays(-60),
            ChiefComplaint = "Annual wellness examination",
            Diagnosis = JsonSerializer.Serialize(new { Primary = "Routine health examination — Z00.00" }),
            Notes = "All vitals within normal limits. Labs ordered for CBC, CMP, lipid panel.",
            DischargeInstructions = "No acute issues. Follow up on lab results. Continue healthy lifestyle."
        });

        // Patient Epsilon — COPD exacerbation
        encounters.Add(new Encounter
        {
            Id = Guid.Parse("b1b2c3d4-0002-0001-0001-000000000005"),
            PatientId = patients[4].Id,
            ClinicianId = "DR-JOHNSON-002",
            EncounterDate = baseDate.AddDays(-3),
            ChiefComplaint = "Worsening cough, increased sputum production, dyspnea",
            Diagnosis = JsonSerializer.Serialize(new { Primary = "Acute exacerbation of COPD — J44.1", Secondary = "Chronic obstructive pulmonary disease — J44.9" }),
            Notes = "SpO2 91% on room air. CXR shows hyperinflation, no infiltrates. Started on prednisone taper and azithromycin.",
            DischargeInstructions = "Prednisone 40mg x5 days taper. Azithromycin 500mg day 1 then 250mg x4 days. Albuterol nebulizer Q4H PRN. Return if SpO2 < 90% or worsening dyspnea."
        });

        return encounters;
    }

    private static List<Medication> CreateMedications(List<Patient> patients, List<Encounter> encounters)
    {
        return new List<Medication>
        {
            // Patient Alpha medications
            new() { PatientId = patients[0].Id, EncounterId = encounters[0].Id, DrugName = "Metformin", Dosage = "1000mg", Frequency = "BID", StartDate = DateTime.UtcNow.AddMonths(-6), IsActive = true },
            new() { PatientId = patients[0].Id, EncounterId = encounters[0].Id, DrugName = "Lisinopril", Dosage = "10mg", Frequency = "Daily", StartDate = DateTime.UtcNow.AddDays(-30), IsActive = true },
            new() { PatientId = patients[0].Id, DrugName = "Glipizide", Dosage = "5mg", Frequency = "Daily", StartDate = DateTime.UtcNow.AddMonths(-12), EndDate = DateTime.UtcNow.AddMonths(-6), IsActive = false },

            // Patient Beta medications
            new() { PatientId = patients[1].Id, EncounterId = encounters[1].Id, DrugName = "Acetaminophen", Dosage = "500mg", Frequency = "Q6H PRN", StartDate = DateTime.UtcNow.AddDays(-7), IsActive = true },

            // Patient Gamma medications
            new() { PatientId = patients[2].Id, EncounterId = encounters[2].Id, DrugName = "Aspirin", Dosage = "81mg", Frequency = "Daily", StartDate = DateTime.UtcNow.AddDays(-14), IsActive = true },
            new() { PatientId = patients[2].Id, EncounterId = encounters[2].Id, DrugName = "Atorvastatin", Dosage = "40mg", Frequency = "QHS", StartDate = DateTime.UtcNow.AddDays(-14), IsActive = true },
            new() { PatientId = patients[2].Id, DrugName = "Metoprolol", Dosage = "50mg", Frequency = "BID", StartDate = DateTime.UtcNow.AddMonths(-3), IsActive = true },

            // Patient Epsilon medications
            new() { PatientId = patients[4].Id, EncounterId = encounters[4].Id, DrugName = "Prednisone", Dosage = "40mg", Frequency = "Daily (taper)", StartDate = DateTime.UtcNow.AddDays(-3), IsActive = true },
            new() { PatientId = patients[4].Id, EncounterId = encounters[4].Id, DrugName = "Azithromycin", Dosage = "250mg", Frequency = "Daily", StartDate = DateTime.UtcNow.AddDays(-3), IsActive = true },
            new() { PatientId = patients[4].Id, DrugName = "Tiotropium", Dosage = "18mcg", Frequency = "Daily", StartDate = DateTime.UtcNow.AddMonths(-24), IsActive = true },
            new() { PatientId = patients[4].Id, DrugName = "Albuterol", Dosage = "2.5mg/3mL", Frequency = "Q4H PRN", StartDate = DateTime.UtcNow.AddDays(-3), IsActive = true },
        };
    }

    private static List<LabResult> CreateLabResults(List<Patient> patients)
    {
        var now = DateTime.UtcNow;
        return new List<LabResult>
        {
            // Patient Alpha labs
            new() { PatientId = patients[0].Id, TestName = "HbA1c", Value = "8.2", Unit = "%", ReferenceRange = "4.0-5.6", IsAbnormal = true, CollectedAt = now.AddDays(-30), ReportedAt = now.AddDays(-29) },
            new() { PatientId = patients[0].Id, TestName = "Fasting Glucose", Value = "186", Unit = "mg/dL", ReferenceRange = "70-100", IsAbnormal = true, CollectedAt = now.AddDays(-30), ReportedAt = now.AddDays(-29) },
            new() { PatientId = patients[0].Id, TestName = "Creatinine", Value = "1.1", Unit = "mg/dL", ReferenceRange = "0.7-1.3", IsAbnormal = false, CollectedAt = now.AddDays(-30), ReportedAt = now.AddDays(-29) },
            new() { PatientId = patients[0].Id, TestName = "eGFR", Value = "78", Unit = "mL/min/1.73m²", ReferenceRange = ">60", IsAbnormal = false, CollectedAt = now.AddDays(-30), ReportedAt = now.AddDays(-29) },

            // Patient Gamma labs
            new() { PatientId = patients[2].Id, TestName = "Total Cholesterol", Value = "268", Unit = "mg/dL", ReferenceRange = "<200", IsAbnormal = true, CollectedAt = now.AddDays(-14), ReportedAt = now.AddDays(-13) },
            new() { PatientId = patients[2].Id, TestName = "LDL", Value = "178", Unit = "mg/dL", ReferenceRange = "<100", IsAbnormal = true, CollectedAt = now.AddDays(-14), ReportedAt = now.AddDays(-13) },
            new() { PatientId = patients[2].Id, TestName = "HDL", Value = "38", Unit = "mg/dL", ReferenceRange = ">40", IsAbnormal = true, CollectedAt = now.AddDays(-14), ReportedAt = now.AddDays(-13) },
            new() { PatientId = patients[2].Id, TestName = "Triglycerides", Value = "260", Unit = "mg/dL", ReferenceRange = "<150", IsAbnormal = true, CollectedAt = now.AddDays(-14), ReportedAt = now.AddDays(-13) },
            new() { PatientId = patients[2].Id, TestName = "Troponin I", Value = "0.03", Unit = "ng/mL", ReferenceRange = "<0.04", IsAbnormal = false, CollectedAt = now.AddDays(-14), ReportedAt = now.AddDays(-14) },

            // Patient Delta labs (normal wellness panel)
            new() { PatientId = patients[3].Id, TestName = "CBC — WBC", Value = "7.2", Unit = "K/uL", ReferenceRange = "4.5-11.0", IsAbnormal = false, CollectedAt = now.AddDays(-58), ReportedAt = now.AddDays(-57) },
            new() { PatientId = patients[3].Id, TestName = "CBC — Hemoglobin", Value = "13.8", Unit = "g/dL", ReferenceRange = "12.0-16.0", IsAbnormal = false, CollectedAt = now.AddDays(-58), ReportedAt = now.AddDays(-57) },
            new() { PatientId = patients[3].Id, TestName = "Glucose", Value = "92", Unit = "mg/dL", ReferenceRange = "70-100", IsAbnormal = false, CollectedAt = now.AddDays(-58), ReportedAt = now.AddDays(-57) },

            // Patient Epsilon labs
            new() { PatientId = patients[4].Id, TestName = "CBC — WBC", Value = "14.2", Unit = "K/uL", ReferenceRange = "4.5-11.0", IsAbnormal = true, CollectedAt = now.AddDays(-3), ReportedAt = now.AddDays(-3) },
            new() { PatientId = patients[4].Id, TestName = "CRP", Value = "48", Unit = "mg/L", ReferenceRange = "<10", IsAbnormal = true, CollectedAt = now.AddDays(-3), ReportedAt = now.AddDays(-3) },
            new() { PatientId = patients[4].Id, TestName = "ABG — pO2", Value = "62", Unit = "mmHg", ReferenceRange = "80-100", IsAbnormal = true, CollectedAt = now.AddDays(-3), ReportedAt = now.AddDays(-3) },
            new() { PatientId = patients[4].Id, TestName = "ABG — pCO2", Value = "52", Unit = "mmHg", ReferenceRange = "35-45", IsAbnormal = true, CollectedAt = now.AddDays(-3), ReportedAt = now.AddDays(-3) },
        };
    }
}
