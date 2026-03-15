using MediMind.Core.Entities;

namespace MediMind.Core.Interfaces;

/// <summary>
/// Generic repository interface for domain entities.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    void Add(T entity);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Patient-specific repository with clinical lookup methods.
/// </summary>
public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetWithEncountersAsync(Guid patientId, CancellationToken ct = default);
    Task<Patient?> GetWithMedicationsAsync(Guid patientId, CancellationToken ct = default);
    Task<Patient?> GetFullProfileAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> SearchByNameAsync(string name, CancellationToken ct = default);
}

/// <summary>
/// Encounter repository with clinical query methods.
/// </summary>
public interface IEncounterRepository : IRepository<Encounter>
{
    Task<IReadOnlyList<Encounter>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<Encounter?> GetLatestByPatientIdAsync(Guid patientId, CancellationToken ct = default);
}

/// <summary>
/// Lab result repository with abnormality filtering.
/// </summary>
public interface ILabResultRepository : IRepository<LabResult>
{
    Task<IReadOnlyList<LabResult>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<LabResult>> GetAbnormalByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<LabResult>> GetRecentByPatientIdAsync(Guid patientId, int days = 30, CancellationToken ct = default);
}

/// <summary>
/// Medication repository.
/// </summary>
public interface IMedicationRepository : IRepository<Medication>
{
    Task<IReadOnlyList<Medication>> GetActiveByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Medication>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
}

/// <summary>
/// Unit of Work pattern for transactional consistency across repositories.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IPatientRepository Patients { get; }
    IEncounterRepository Encounters { get; }
    ILabResultRepository LabResults { get; }
    IMedicationRepository Medications { get; }
    IRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
