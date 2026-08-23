using Microsoft.Extensions.Caching.Memory;

namespace eShop.Payment.Shared.Idempotency;

/// <summary>
/// In-process <see cref="IMemoryCache"/> implementation of the idempotency
/// token store. Suitable for single-instance development and for worker
/// services deduplicating event consumption; use the Redis-backed store for
/// horizontally scaled deployments.
/// </summary>
public sealed class MemoryCacheIdempotencyTokenStore : IIdempotencyTokenStore
{
    private const string Prefix = "idempotency:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    private readonly IMemoryCache _cache;
    private readonly object _gate = new();

    public MemoryCacheIdempotencyTokenStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // IMemoryCache has no atomic add-if-absent; the gate serializes check+set in-process.
        lock (_gate)
        {
            if (_cache.TryGetValue(CacheKey(key), out _))
            {
                return Task.FromResult(false);
            }

            _cache.Set(CacheKey(key), IdempotencyTokenStatus.InProgress, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TtlOrDefault(ttl)
            });

            return Task.FromResult(true);
        }
    }

    public Task<IdempotencyTokenStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Task.FromResult(
            _cache.TryGetValue(CacheKey(key), out IdempotencyTokenStatus status)
                ? status
                : IdempotencyTokenStatus.NotFound);
    }

    public Task MarkCompletedAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_gate)
        {
            _cache.Set(CacheKey(key), IdempotencyTokenStatus.Completed, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TtlOrDefault(ttl)
            });
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_gate)
        {
            _cache.Remove(CacheKey(key));
        }

        return Task.CompletedTask;
    }

    private static string CacheKey(string key) => Prefix + key;

    private static TimeSpan TtlOrDefault(TimeSpan ttl) => ttl <= TimeSpan.Zero ? DefaultTtl : ttl;
}
