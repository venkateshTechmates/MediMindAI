using FluentAssertions;
using MediMind.Core.Entities;
using MediMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace MediMind.IntegrationTests;

/// <summary>
/// Integration tests using Testcontainers SQL Server.
/// These tests verify full EF Core + SQL Server behavior.
/// </summary>
public class SqlServerIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private MediMindDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<MediMindDbContext>()
            .UseSqlServer(_container.GetConnectionString(), sql =>
            {
                sql.MigrationsAssembly(typeof(MediMindDbContext).Assembly.FullName);
            })
            .Options;

        _context = new MediMindDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Database_ShouldBeCreatedSuccessfully()
    {
        var canConnect = await _context.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }

    [Fact]
    public async Task Patient_CRUD_ShouldWork()
    {
        // Create
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = "Integration Test",
            DateOfBirth = new DateTime(1985, 3, 15),
            Gender = "Male",
            BloodGroup = "A+",
            Allergies = "[\"Sulfa\"]",
            IsActive = true
        };

        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        // Read
        var found = await _context.Patients.FindAsync(patient.Id);
        found.Should().NotBeNull();
        found!.FullName.Should().Be("Integration Test");

        // Update
        found.FullName = "Integration Updated";
        await _context.SaveChangesAsync();
        var updated = await _context.Patients.FindAsync(patient.Id);
        updated!.FullName.Should().Be("Integration Updated");

        // Delete
        _context.Patients.Remove(updated);
        await _context.SaveChangesAsync();
        var deleted = await _context.Patients.FindAsync(patient.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task Patient_WithEncounters_ShouldLoadRelationships()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = "Relationship Test",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "Female",
            IsActive = true
        };

        var encounter = new Encounter
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            EncounterDate = DateTime.UtcNow,
            ChiefComplaint = "Follow-up",
            Diagnosis = "[\"Routine\"]"
        };

        _context.Patients.Add(patient);
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync();

        var loadedPatient = await _context.Patients
            .Include(p => p.Encounters)
            .FirstAsync(p => p.Id == patient.Id);

        loadedPatient.Encounters.Should().HaveCount(1);
        loadedPatient.Encounters.First().ChiefComplaint.Should().Be("Follow-up");
    }

    [Fact]
    public async Task AuditLog_ShouldBeImmutable()
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = "test-user",
            Action = "TestAction",
            EntityType = "Patient",
            EntityId = Guid.NewGuid().ToString(),
            OldValue = "Integration test audit log",
            IpAddress = "127.0.0.1",
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(audit);
        await _context.SaveChangesAsync();

        var loaded = await _context.AuditLogs.FindAsync(audit.Id);
        loaded.Should().NotBeNull();
        loaded!.Action.Should().Be("TestAction");
    }

    [Fact]
    public async Task DataSeeder_ShouldSeedSyntheticData()
    {
        var seeder = new DataSeeder(_context, NullLogger<DataSeeder>.Instance);
        await seeder.SeedAsync();

        var patientCount = await _context.Patients.CountAsync();
        patientCount.Should().BeGreaterThanOrEqualTo(5);

        var encounterCount = await _context.Encounters.CountAsync();
        encounterCount.Should().BeGreaterThan(0);

        var medicationCount = await _context.Medications.CountAsync();
        medicationCount.Should().BeGreaterThan(0);

        var labCount = await _context.LabResults.CountAsync();
        labCount.Should().BeGreaterThan(0);
    }
}
