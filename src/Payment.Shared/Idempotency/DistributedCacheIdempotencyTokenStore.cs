using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace eShop.Payment.Shared.Idempotency;

/// <summary>
/// Redis-backed <see cref="IDistributedCache"/> implementation of the
/// idempotency token store for horizontally scaled API deployments.
/// </summary>
public sealed class DistributedCacheIdempotencyTokenStore : IIdempotencyTokenStore
{
    private const string Prefix = "idempotency:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    private readonly IDistributedCache _cache;

    // IDistributedCache exposes no atomic set-if-absent primitive; the per-key
    // semaphore serializes check+set for this instance. In production, deploy
    // behind a Redis SET NX PX command for cross-instance atomicity.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    public DistributedCacheIdempotencyTokenStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            if (await _cache.GetAsync(CacheKey(key), cancellationToken) is not null)
            {
                return false;
            }

            await _cache.SetAsync(CacheKey(key), Serialize(IdempotencyTokenStatus.InProgress),
                Options(ttl), cancellationToken);

            return true;
        }
        finally
        {
            keyLock.Release();
        }
    }

    public async Task<IdempotencyTokenStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var bytes = await _cache.GetAsync(CacheKey(key), cancellationToken);
        return bytes is null ? IdempotencyTokenStatus.NotFound : Deserialize(bytes);
    }

    public async Task MarkCompletedAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            await _cache.SetAsync(CacheKey(key), Serialize(IdempotencyTokenStatus.Completed),
                Options(ttl), cancellationToken);
        }
        finally
        {
            keyLock.Release();
        }
    }

    public async Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await _cache.RemoveAsync(CacheKey(key), cancellationToken);
    }

    private static string CacheKey(string key) => Prefix + key;

    private static byte[] Serialize(IdempotencyTokenStatus status) => Encoding.UTF8.GetBytes(status.ToString());

    private static IdempotencyTokenStatus Deserialize(byte[] bytes) =>
        Enum.TryParse<IdempotencyTokenStatus>(Encoding.UTF8.GetString(bytes), out var status)
            ? status
            : IdempotencyTokenStatus.NotFound;

    private static DistributedCacheEntryOptions Options(TimeSpan ttl) => new()
    {
        AbsoluteExpirationRelativeToNow = ttl <= TimeSpan.Zero ? DefaultTtl : ttl
    };
}
