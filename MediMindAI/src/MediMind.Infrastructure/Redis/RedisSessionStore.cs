using System.Diagnostics;
using System.Text.Json;
using MediMind.Core.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MediMind.Infrastructure.Redis;

/// <summary>
/// Redis-backed session store for conversation history caching (FR-31).
/// </summary>
public class RedisSessionStore : ISessionStore
{
    private static readonly ActivitySource _activitySource = new("MediMind.Data", "1.0.0");

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisSessionStore> _logger;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(8);

    public RedisSessionStore(IConnectionMultiplexer redis, ILogger<RedisSessionStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.SessionStore.Set", ActivityKind.Client);
        activity?.SetTag("session.store", "Redis");
        activity?.SetTag("session.key", key);
        activity?.SetTag("session.expiry_seconds", (expiry ?? DefaultExpiry).TotalSeconds);

        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(value);
        await db.StringSetAsync(key, json, expiry ?? DefaultExpiry);

        activity?.SetTag("session.value_length", json.Length);
        activity?.SetStatus(ActivityStatusCode.Ok);
        _logger.LogDebug("Redis SET: {Key}, Length: {Len}", key, json.Length);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.SessionStore.Get", ActivityKind.Client);
        activity?.SetTag("session.store", "Redis");
        activity?.SetTag("session.key", key);

        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
        {
            activity?.SetTag("session.result", "miss");
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogDebug("Redis MISS: {Key}", key);
            return default;
        }

        activity?.SetTag("session.result", "hit");
        activity?.SetTag("session.value_length", ((string)value!).Length);
        activity?.SetStatus(ActivityStatusCode.Ok);
        _logger.LogDebug("Redis HIT: {Key}", key);
        return JsonSerializer.Deserialize<T>((string)value!);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.SessionStore.Remove", ActivityKind.Client);
        activity?.SetTag("session.store", "Redis");
        activity?.SetTag("session.key", key);

        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);

        activity?.SetStatus(ActivityStatusCode.Ok);
        _logger.LogDebug("Redis DEL: {Key}", key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("Data.SessionStore.Exists", ActivityKind.Client);
        activity?.SetTag("session.store", "Redis");
        activity?.SetTag("session.key", key);

        var db = _redis.GetDatabase();
        var exists = await db.KeyExistsAsync(key);

        activity?.SetTag("session.result", exists ? "exists" : "not_found");
        activity?.SetStatus(ActivityStatusCode.Ok);
        return exists;
    }
}
