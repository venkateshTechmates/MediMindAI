namespace MediMind.Core.Interfaces;

/// <summary>
/// Session/cache store for conversation history and transient data (Redis-backed).
/// </summary>
public interface ISessionStore
{
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
