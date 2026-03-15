namespace MediMind.Core.Entities;

/// <summary>
/// Represents a patient in the clinical system.
/// </summary>
public class Patient : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? BloodGroup { get; set; }
    
    /// <summary>
    /// JSON-serialized list of known allergies.
    /// </summary>
    public string? Allergies { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();
    public ICollection<Medication> Medications { get; set; } = new List<Medication>();
    public ICollection<LabResult> LabResults { get; set; } = new List<LabResult>();
}
