using MediMind.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediMind.Infrastructure.Persistence;

/// <summary>
/// Primary EF Core DbContext for MediMind clinical data.
/// </summary>
public class MediMindDbContext : DbContext
{
    public MediMindDbContext(DbContextOptions<MediMindDbContext> options) : base(options) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<LabResult> LabResults => Set<LabResult>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<AgentTrace> AgentTraces => Set<AgentTrace>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediMindDbContext).Assembly);
    }
}
