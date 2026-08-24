namespace eShop.Ordering.API.Application.Commands;

// Regular CommandHandler
public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IFinancialAuditStore _financialAuditStore;

    public CancelOrderCommandHandler(IOrderRepository orderRepository, IFinancialAuditStore financialAuditStore)
    {
        _orderRepository = orderRepository;
        _financialAuditStore = financialAuditStore;
    }

    /// <summary>
    /// Handler which processes the command when
    /// customer executes cancel order from app
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<bool> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var orderToUpdate = await _orderRepository.GetAsync(command.OrderNumber);
        if (orderToUpdate == null)
        {
            return false;
        }

        orderToUpdate.SetCancelledStatus();

        // AML: record the cancellation (potential refund trigger) in the
        // immutable audit trail within the same ACID transaction.
        await _financialAuditStore.RecordAsync(new FinancialAuditEntry
        {
            EventType = FinancialAuditEventType.OrderCancelled,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = $"CANCEL-{command.OrderNumber}-{Guid.NewGuid():N}",
            Subject = $"Order/{command.OrderNumber}",
            Amount = orderToUpdate.GetTotal(),
            Currency = "USD",
            Outcome = "Succeeded"
        }, cancellationToken);

        return await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
    }
}


// Use for Idempotency in Command process
public class CancelOrderIdentifiedCommandHandler : IdentifiedCommandHandler<CancelOrderCommand, bool>
{
    public CancelOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<CancelOrderCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for processing order.
    }
}
