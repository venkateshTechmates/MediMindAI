using System.Collections.Concurrent;
using MediMind.Core.Entities;
using MediMind.Core.Interfaces;

namespace MediMind.Infrastructure.Persistence.Repositories;

/// <summary>
/// Unit of Work implementation wrapping all repositories with a shared DbContext.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly MediMindDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();
    private IPatientRepository? _patients;
    private IEncounterRepository? _encounters;
    private ILabResultRepository? _labResults;
    private IMedicationRepository? _medications;

    public UnitOfWork(MediMindDbContext context)
    {
        _context = context;
    }

    public IPatientRepository Patients =>
        _patients ??= new PatientRepository(_context);

    public IEncounterRepository Encounters =>
        _encounters ??= new EncounterRepository(_context);

    public ILabResultRepository LabResults =>
        _labResults ??= new LabResultRepository(_context);

    public IMedicationRepository Medications =>
        _medications ??= new MedicationRepository(_context);

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        return (IRepository<T>)_repositories.GetOrAdd(typeof(T), _ => new Repository<T>(_context));
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
