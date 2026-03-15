using FluentAssertions;
using MediMind.Core.Entities;
using MediMind.Infrastructure.Persistence;
using MediMind.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MediMind.UnitTests.Repositories;

public class PatientRepositoryTests : IDisposable
{
    private readonly MediMindDbContext _context;
    private readonly PatientRepository _repository;

    public PatientRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MediMindDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MediMindDbContext(options);
        _repository = new PatientRepository(_context);

        SeedData();
    }

    private void SeedData()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = "John Doe",
            DateOfBirth = new DateTime(1980, 1, 1),
            Gender = "Male",
            BloodGroup = "A+",
            Allergies = "[\"Penicillin\"]",
            IsActive = true
        };

        var encounter = new Encounter
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ClinicianId = "DR-SMITH-001",
            EncounterDate = DateTime.UtcNow.AddDays(-5),
            ChiefComplaint = "Chest pain",
            Diagnosis = "[\"Angina\"]"
        };

        var medication = new Medication
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            DrugName = "Aspirin",
            Dosage = "81mg",
            Frequency = "Once daily",
            StartDate = DateTime.UtcNow.AddDays(-30),
            IsActive = true
        };

        _context.Patients.Add(patient);
        _context.Encounters.Add(encounter);
        _context.Medications.Add(medication);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnPatient()
    {
        var patient = await _context.Patients.FirstAsync();
        var result = await _repository.GetByIdAsync(patient.Id);

        result.Should().NotBeNull();
        result!.FullName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ShouldReturnNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchByNameAsync_ShouldFindMatchingPatients()
    {
        var results = await _repository.SearchByNameAsync("John");

        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.FullName == "John Doe");
    }

    [Fact]
    public async Task SearchByNameAsync_WithNoMatch_ShouldReturnEmpty()
    {
        var results = await _repository.SearchByNameAsync("ZZZZZ");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFullProfileAsync_ShouldIncludeRelatedEntities()
    {
        var patient = await _context.Patients.FirstAsync();
        var result = await _repository.GetFullProfileAsync(patient.Id);

        result.Should().NotBeNull();
        result!.Encounters.Should().NotBeEmpty();
        result.Medications.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddAsync_ShouldAddPatient()
    {
        var newPatient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = "Jane Smith",
            DateOfBirth = new DateTime(1990, 6, 15),
            Gender = "Female",
            IsActive = true
        };

        var result = await _repository.AddAsync(newPatient);
        await _context.SaveChangesAsync();

        result.Should().NotBeNull();
        var count = await _context.Patients.CountAsync();
        count.Should().Be(2);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
