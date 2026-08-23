using Npgsql;
using Polly;
using Polly.Retry;

namespace eShop.Ordering.API.Application.Resilience;

/// <summary>
/// Polly resilience pipelines for database operations handling monetary
/// balances. Retries transient PostgreSQL failures (connection drops,
/// serialization conflicts, deadlocks) with exponential backoff and jitter.
/// </summary>
public static class DatabaseResiliencePipelines
{
    /// <summary>Pipeline for read queries over monetary balances and order totals.</summary>
    public static readonly ResiliencePipeline MonetaryReads = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(200),
            ShouldHandle = new PredicateBuilder()
                .Handle<NpgsqlException>()
                .Handle<TimeoutException>()
        })
        .Build();
}
