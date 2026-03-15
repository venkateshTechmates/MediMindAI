namespace MediMind.Core.Entities;

/// <summary>
/// Represents a medication prescribed to a patient.
/// </summary>
public class Medication : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? EncounterId { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Patient Patient { get; set; } = null!;
    public Encounter? Encounter { get; set; }
}
