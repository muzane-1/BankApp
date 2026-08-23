using eShop.Payment.Shared.Idempotency;

namespace eShop.Ordering.API.Infrastructure.Idempotency;

/// <summary>
/// Enforces strict idempotency on financial transaction endpoints using the
/// client-supplied "x-requestid" header as the idempotency token:
/// a replayed token returns the original outcome without re-executing, and a
/// token that is still in flight is rejected with 409 Conflict. This prevents
/// double-spending caused by client retries, network timeouts or duplicate
/// message delivery. Tokens are retained for 24 hours (Redis in production,
/// in-memory cache for development).
/// </summary>
public sealed class IdempotencyEndpointFilter : IEndpointFilter
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(24);

    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(IdempotencyEndpointFilter));

        var requestId = httpContext.Request.Headers["x-requestid"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId) || !Guid.TryParse(requestId, out var parsedId) || parsedId == Guid.Empty)
        {
            // The endpoint itself validates the header and returns the 400 response.
            return await next(context);
        }

        var store = httpContext.RequestServices.GetRequiredService<IIdempotencyTokenStore>();
        var key = $"{httpContext.Request.Method}:{httpContext.Request.Path.Value}:{parsedId}";
        var cancellationToken = httpContext.RequestAborted;

        var status = await store.GetStatusAsync(key, cancellationToken);
        switch (status)
        {
            case IdempotencyTokenStatus.Completed:
                logger.LogInformation("Idempotent replay of {RequestId} on {Path}; returning stored outcome without re-executing", parsedId, httpContext.Request.Path.Value);
                return TypedResults.Ok();

            case IdempotencyTokenStatus.InProgress:
                logger.LogWarning("Rejected concurrent duplicate transaction {RequestId} on {Path}", parsedId, httpContext.Request.Path.Value);
                return TypedResults.Conflict("A transaction with this request id is already being processed.");
        }

        if (!await store.TryAcquireAsync(key, TokenTtl, cancellationToken))
        {
            logger.LogWarning("Rejected concurrent duplicate transaction {RequestId} on {Path}", parsedId, httpContext.Request.Path.Value);
            return TypedResults.Conflict("A transaction with this request id is already being processed.");
        }

        try
        {
            var result = await next(context);

            if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 })
            {
                await store.MarkCompletedAsync(key, TokenTtl, cancellationToken);
            }
            else
            {
                // Failed executions must not be pinned as completed; the client may retry.
                await store.ReleaseAsync(key, cancellationToken);
            }

            return result;
        }
        catch
        {
            await store.ReleaseAsync(key, CancellationToken.None);
            throw;
        }
    }
}

public static class IdempotencyEndpointRouteBuilderExtensions
{
    /// <summary>Marks a transaction endpoint as requiring an idempotency token.</summary>
    public static RouteHandlerBuilder RequireIdempotencyToken(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<IdempotencyEndpointFilter>();
}
