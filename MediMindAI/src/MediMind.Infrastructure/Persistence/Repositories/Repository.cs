using MediMind.Core.Entities;
using MediMind.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediMind.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic repository implementation backed by EF Core.
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly MediMindDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(MediMindDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.AsNoTracking().ToListAsync(ct);

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        return entity;
    }

    public virtual void Add(T entity)
    {
        _dbSet.Add(entity);
    }

    public virtual Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity is not null)
            _dbSet.Remove(entity);
    }
}
