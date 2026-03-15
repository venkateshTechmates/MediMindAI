using MediMind.Core.Entities;
using MediMind.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediMind.Infrastructure.Persistence.Repositories;

public class LabResultRepository : Repository<LabResult>, ILabResultRepository
{
    public LabResultRepository(MediMindDbContext context) : base(context) { }

    public async Task<IReadOnlyList<LabResult>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _dbSet
            .Where(l => l.PatientId == patientId)
            .OrderByDescending(l => l.CollectedAt)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LabResult>> GetAbnormalByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _dbSet
            .Where(l => l.PatientId == patientId && l.IsAbnormal)
            .OrderByDescending(l => l.CollectedAt)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LabResult>> GetRecentByPatientIdAsync(Guid patientId, int days = 30, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        return await _dbSet
            .Where(l => l.PatientId == patientId && l.CollectedAt >= cutoff)
            .OrderByDescending(l => l.CollectedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
