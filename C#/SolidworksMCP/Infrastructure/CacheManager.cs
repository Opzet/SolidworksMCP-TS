namespace SolidworksMCP;

using System.Collections.Concurrent;

public sealed class CacheManager
{
    private sealed record CacheEntry(object? Value, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly int capacity;
    private readonly TimeSpan ttl;

    public CacheManager(int capacity, TimeSpan ttl)
    {
        this.capacity = capacity;
        this.ttl = ttl;
    }

    public void Set(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        PurgeExpired();

        if (cache.Count >= capacity)
        {
            var oldest = cache.OrderBy(item => item.Value.ExpiresAt).FirstOrDefault();
            if (!string.IsNullOrEmpty(oldest.Key))
            {
                cache.TryRemove(oldest.Key, out _);
            }
        }

        cache[key] = new CacheEntry(value, DateTimeOffset.UtcNow.Add(ttl));
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        PurgeExpired();

        if (cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            if (entry.Value is T typed)
            {
                value = typed;
                return true;
            }

            if (entry.Value is null && default(T) is null)
            {
                value = default;
                return true;
            }
        }

        value = default;
        return false;
    }

    public bool Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return cache.TryRemove(key, out _);
    }

    public void Clear() => cache.Clear();

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in cache)
        {
            if (item.Value.ExpiresAt <= now)
            {
                cache.TryRemove(item.Key, out _);
            }
        }
    }
}
