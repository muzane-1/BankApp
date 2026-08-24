namespace eShop.Payment.Shared.Idempotency;

public enum IdempotencyTokenStatus
{
    /// <summary>No token exists for the key; the operation has not been seen before.</summary>
    NotFound,

    /// <summary>A token exists but the operation is still executing; concurrent duplicate.</summary>
    InProgress,

    /// <summary>The operation completed successfully; replay the stored outcome instead of re-executing.</summary>
    Completed
}

/// <summary>
/// Stores idempotency tokens for financial transaction endpoints so a retried
/// request (network timeout, client retry, duplicate publish) can never cause
/// double-spending or duplicate execution. Backed by Redis in production or an
/// in-process memory cache for development.
/// </summary>
public interface IIdempotencyTokenStore
{
    /// <summary>
    /// Atomically acquires an "in progress" token for <paramref name="key"/>.
    /// Returns true when the caller owns the token and may execute the operation;
    /// false when another request already holds or has completed it.
    /// </summary>
    Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task<IdempotencyTokenStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Marks an acquired token as completed, retaining it for <paramref name="ttl"/> so replays are recognized.</summary>
    Task MarkCompletedAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Releases an "in progress" token after a failure so the request may be retried.</summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);
}
