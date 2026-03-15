using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using MediMind.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Redis;

/// <summary>
/// In-memory fallback session store when Redis is unavailable.
/// Not suitable for multi-instance deployments but works for local dev.
/// </summary>
public class InMemorySessionStore : ISessionStore
{
    private static readonly ActivitySource _activitySource = new("MediMind.Data", "1.0.0");

    private readonly ConcurrentDictionary<string, (string Json, DateTime? Expiry)> _store = new();
    private readonly ILogger<InMemorySessionStore> _logger;

    public InMemorySessionStore(ILogger<InMemorySessionStore> logger)
    {
        _logger = logger;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.SessionStore.Set", ActivityKind.Internal);
        activity?.SetTag("session.store", "InMemory");
        activity?.SetTag("session.key", key);
        activity?.SetTag("session.has_expiry", expiry.HasValue);

        var json = JsonSerializer.Serialize(value);
        var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
        _store[key] = (json, expiryTime);

        activity?.SetTag("session.value_length", json.Length);
        activity?.SetStatus(ActivityStatusCode.Ok);

        _logger.LogDebug("InMemory SET: {Key}, Length: {Len}, TraceId: {TraceId}",
            key, json.Length, Activity.Current?.TraceId.ToString() ?? "none");
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.SessionStore.Get", ActivityKind.Internal);
        activity?.SetTag("session.store", "InMemory");
        activity?.SetTag("session.key", key);

        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.Expiry.HasValue && entry.Expiry.Value < DateTime.UtcNow)
            {
                _store.TryRemove(key, out _);
                activity?.SetTag("session.result", "expired");
                activity?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogDebug("InMemory EXPIRED: {Key}", key);
                return Task.FromResult<T?>(default);
            }

            activity?.SetTag("session.result", "hit");
            activity?.SetTag("session.value_length", entry.Json.Length);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogDebug("InMemory HIT: {Key}", key);
            return Task.FromResult(JsonSerializer.Deserialize<T>(entry.Json));
        }

        activity?.SetTag("session.result", "miss");
        activity?.SetStatus(ActivityStatusCode.Ok);
        _logger.LogDebug("InMemory MISS: {Key}", key);
        return Task.FromResult<T?>(default);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.SessionStore.Remove", ActivityKind.Internal);
        activity?.SetTag("session.store", "InMemory");
        activity?.SetTag("session.key", key);

        _store.TryRemove(key, out _);
        activity?.SetStatus(ActivityStatusCode.Ok);
        _logger.LogDebug("InMemory DEL: {Key}", key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.SessionStore.Exists", ActivityKind.Internal);
        activity?.SetTag("session.store", "InMemory");
        activity?.SetTag("session.key", key);

        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.Expiry.HasValue && entry.Expiry.Value < DateTime.UtcNow)
            {
                _store.TryRemove(key, out _);
                activity?.SetTag("session.result", "expired");
                return Task.FromResult(false);
            }
            activity?.SetTag("session.result", "exists");
            return Task.FromResult(true);
        }
        activity?.SetTag("session.result", "not_found");
        return Task.FromResult(false);
    }
}
