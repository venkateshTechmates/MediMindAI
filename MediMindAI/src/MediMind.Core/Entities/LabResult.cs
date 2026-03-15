namespace MediMind.Core.Entities;

/// <summary>
/// Represents a laboratory test result for a patient.
/// </summary>
public class LabResult : BaseEntity
{
    public Guid PatientId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? ReferenceRange { get; set; }
    public bool IsAbnormal { get; set; }
    public DateTime CollectedAt { get; set; }
    public DateTime? ReportedAt { get; set; }

    // Navigation properties
    public Patient Patient { get; set; } = null!;
}
