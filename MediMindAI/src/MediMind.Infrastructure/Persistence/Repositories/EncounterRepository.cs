using MediMind.Core.Entities;
using MediMind.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediMind.Infrastructure.Persistence.Repositories;

public class EncounterRepository : Repository<Encounter>, IEncounterRepository
{
    public EncounterRepository(MediMindDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Encounter>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _dbSet
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.EncounterDate)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<Encounter?> GetLatestByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _dbSet
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.EncounterDate)
            .Include(e => e.Medications)
            .FirstOrDefaultAsync(ct);
}
