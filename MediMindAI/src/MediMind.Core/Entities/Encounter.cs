namespace MediMind.Core.Entities;

/// <summary>
/// Represents a clinical encounter/visit for a patient.
/// </summary>
public class Encounter : BaseEntity
{
    public Guid PatientId { get; set; }
    public string? ClinicianId { get; set; }
    public DateTime EncounterDate { get; set; }
    public string ChiefComplaint { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON-serialized diagnosis data.
    /// </summary>
    public string? Diagnosis { get; set; }
    
    public string? Notes { get; set; }
    public string? DischargeInstructions { get; set; }

    // Navigation properties
    public Patient Patient { get; set; } = null!;
    public ICollection<Medication> Medications { get; set; } = new List<Medication>();
}
