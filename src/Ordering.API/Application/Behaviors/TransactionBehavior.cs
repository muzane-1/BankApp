namespace eShop.Ordering.API.Application.Behaviors;

using System.Data;
using Microsoft.Extensions.Logging;

public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Commands that mutate monetary state. They run under
    /// <see cref="IsolationLevel.Serializable"/> so concurrent balance/payment
    /// mutations cannot interleave; the EF Core execution strategy transparently
    /// retries the whole unit of work on serialization failures (SQLSTATE 40001).
    /// </summary>
    private static readonly HashSet<string> MonetaryCommands = new(StringComparer.Ordinal)
    {
        nameof(CreateOrderCommand),
        nameof(SetPaidOrderStatusCommand),
        nameof(CancelOrderCommand)
    };

    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
    private readonly OrderingContext _dbContext;
    private readonly IOrderingIntegrationEventService _orderingIntegrationEventService;

    public TransactionBehavior(OrderingContext dbContext,
        IOrderingIntegrationEventService orderingIntegrationEventService,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentException(nameof(OrderingContext));
        _orderingIntegrationEventService = orderingIntegrationEventService ?? throw new ArgumentException(nameof(orderingIntegrationEventService));
        _logger = logger ?? throw new ArgumentException(nameof(ILogger));
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = default(TResponse);
        var typeName = request.GetGenericTypeName();

        try
        {
            if (_dbContext.HasActiveTransaction)
            {
                return await next();
            }

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            var isolationLevel = GetIsolationLevel(request);

            await strategy.ExecuteAsync(async () =>
            {
                Guid transactionId;

                await using var transaction = await _dbContext.BeginTransactionAsync(isolationLevel);
                using (_logger.BeginScope(new List<KeyValuePair<string, object>> { new("TransactionContext", transaction.TransactionId) }))
                {
                    _logger.LogInformation("Begin transaction {TransactionId} for {CommandName} ({@Command})", transaction.TransactionId, typeName, request);

                    response = await next();

                    _logger.LogInformation("Commit transaction {TransactionId} for {CommandName}", transaction.TransactionId, typeName);

                    await _dbContext.CommitTransactionAsync(transaction);

                    transactionId = transaction.TransactionId;
                }

                await _orderingIntegrationEventService.PublishEventsThroughEventBusAsync(transactionId);
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Handling transaction for {CommandName} ({@Command})", typeName, request);

            throw;
        }
    }

    private static IsolationLevel GetIsolationLevel(TRequest request)
    {
        var commandType = request is IIdentifiedCommand identified
            ? identified.Command.GetType()
            : request.GetType();

        return MonetaryCommands.Contains(commandType.Name)
            ? IsolationLevel.Serializable
            : IsolationLevel.ReadCommitted;
    }
}
