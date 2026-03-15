using MediMind.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediMind.Infrastructure.Persistence.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("PatientId");
        builder.Property(p => p.FullName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Gender).IsRequired().HasMaxLength(20);
        builder.Property(p => p.BloodGroup).HasMaxLength(10);
        builder.Property(p => p.Allergies).HasColumnType("nvarchar(max)"); // JSON
        builder.HasIndex(p => p.FullName);
        builder.HasIndex(p => p.IsActive);
    }
}

public class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.ToTable("Encounters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("EncounterId");
        builder.Property(e => e.ChiefComplaint).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Diagnosis).HasColumnType("nvarchar(max)"); // JSON
        builder.Property(e => e.Notes).HasColumnType("nvarchar(max)");
        builder.Property(e => e.DischargeInstructions).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ClinicianId).HasMaxLength(100);

        builder.HasOne(e => e.Patient)
            .WithMany(p => p.Encounters)
            .HasForeignKey(e => e.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PatientId);
        builder.HasIndex(e => e.EncounterDate);
    }
}

public class MedicationConfiguration : IEntityTypeConfiguration<Medication>
{
    public void Configure(EntityTypeBuilder<Medication> builder)
    {
        builder.ToTable("Medications");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("MedicationId");
        builder.Property(m => m.DrugName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Dosage).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Frequency).IsRequired().HasMaxLength(100);

        builder.HasOne(m => m.Patient)
            .WithMany(p => p.Medications)
            .HasForeignKey(m => m.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Encounter)
            .WithMany(e => e.Medications)
            .HasForeignKey(m => m.EncounterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.PatientId);
        builder.HasIndex(m => m.IsActive);
    }
}

public class LabResultConfiguration : IEntityTypeConfiguration<LabResult>
{
    public void Configure(EntityTypeBuilder<LabResult> builder)
    {
        builder.ToTable("LabResults");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("LabResultId");
        builder.Property(l => l.TestName).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Value).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Unit).IsRequired().HasMaxLength(50);
        builder.Property(l => l.ReferenceRange).HasMaxLength(100);

        builder.HasOne(l => l.Patient)
            .WithMany(p => p.LabResults)
            .HasForeignKey(l => l.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.PatientId);
        builder.HasIndex(l => l.IsAbnormal);
        builder.HasIndex(l => l.CollectedAt);
    }
}

public class IngestionJobConfiguration : IEntityTypeConfiguration<IngestionJob>
{
    public void Configure(EntityTypeBuilder<IngestionJob> builder)
    {
        builder.ToTable("IngestionJobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("JobId");
        builder.Property(j => j.DocumentName).IsRequired().HasMaxLength(500);
        builder.Property(j => j.DocumentType).IsRequired().HasMaxLength(50);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.ErrorMessage).HasColumnType("nvarchar(max)");

        builder.HasIndex(j => j.Status);
    }
}

public class AgentTraceConfiguration : IEntityTypeConfiguration<AgentTrace>
{
    public void Configure(EntityTypeBuilder<AgentTrace> builder)
    {
        builder.ToTable("AgentTraces");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("TraceId");
        builder.Property(t => t.AgentName).IsRequired().HasMaxLength(100);
        builder.Property(t => t.UserId).HasMaxLength(100);
        builder.Property(t => t.OrchestratorPlan).HasColumnType("nvarchar(max)"); // JSON
        builder.Property(t => t.AgentInput).HasColumnType("nvarchar(max)");
        builder.Property(t => t.AgentOutput).HasColumnType("nvarchar(max)");

        builder.HasIndex(t => t.SessionId);
        builder.HasIndex(t => t.CreatedAt);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("LogId");
        builder.Property(a => a.UserId).HasMaxLength(100);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.OldValue).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewValue).HasColumnType("nvarchar(max)");
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.Property(a => a.UserAgent).HasMaxLength(500);

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.EntityType);
    }
}
