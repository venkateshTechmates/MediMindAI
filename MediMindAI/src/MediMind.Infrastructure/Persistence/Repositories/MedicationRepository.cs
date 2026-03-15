using MediMind.Core.Entities;
using MediMind.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediMind.Infrastructure.Persistence.Repositories;

public class MedicationRepository : Repository<Medication>, IMedicationRepository
{
    public MedicationRepository(MediMindDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Medication>> GetActiveByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _dbSet
            .Where(m => m.PatientId == patientId && m.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Medication>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _dbSet
            .Where(m => m.PatientId == patientId)
            .OrderByDescending(m => m.StartDate)
            .AsNoTracking()
            .ToListAsync(ct);
}
