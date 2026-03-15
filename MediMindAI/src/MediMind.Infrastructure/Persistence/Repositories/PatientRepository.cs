using MediMind.Core.Entities;
using MediMind.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediMind.Infrastructure.Persistence.Repositories;

public class PatientRepository : Repository<Patient>, IPatientRepository
{
    // EF Core Compiled Queries for hot-path lookups (FR-27)
    private static readonly Func<MediMindDbContext, Guid, Task<Patient?>> _getByIdCompiled =
        EF.CompileAsyncQuery((MediMindDbContext ctx, Guid id) =>
            ctx.Patients.FirstOrDefault(p => p.Id == id));

    public PatientRepository(MediMindDbContext context) : base(context) { }

    public override async Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _getByIdCompiled(_context, id);

    public async Task<Patient?> GetWithEncountersAsync(Guid patientId, CancellationToken ct = default)
        => await _dbSet
            .Include(p => p.Encounters)
            .FirstOrDefaultAsync(p => p.Id == patientId, ct);

    public async Task<Patient?> GetWithMedicationsAsync(Guid patientId, CancellationToken ct = default)
        => await _dbSet
            .Include(p => p.Medications.Where(m => m.IsActive))
            .FirstOrDefaultAsync(p => p.Id == patientId, ct);

    public async Task<Patient?> GetFullProfileAsync(Guid patientId, CancellationToken ct = default)
        => await _dbSet
            .Include(p => p.Encounters.OrderByDescending(e => e.EncounterDate).Take(10))
            .Include(p => p.Medications.Where(m => m.IsActive))
            .Include(p => p.LabResults.OrderByDescending(l => l.CollectedAt).Take(20))
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == patientId, ct);

    public async Task<IReadOnlyList<Patient>> SearchByNameAsync(string name, CancellationToken ct = default)
        => await _dbSet
            .Where(p => p.FullName.Contains(name) && p.IsActive)
            .AsNoTracking()
            .Take(20)
            .ToListAsync(ct);
}
