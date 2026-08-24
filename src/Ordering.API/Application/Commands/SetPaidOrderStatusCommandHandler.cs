namespace eShop.Ordering.API.Application.Commands;

// Regular CommandHandler
public class SetPaidOrderStatusCommandHandler : IRequestHandler<SetPaidOrderStatusCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IFinancialAuditStore _financialAuditStore;

    public SetPaidOrderStatusCommandHandler(IOrderRepository orderRepository, IFinancialAuditStore financialAuditStore)
    {
        _orderRepository = orderRepository;
        _financialAuditStore = financialAuditStore;
    }

    /// <summary>
    /// Handler which processes the command when
    /// Shipment service confirms the payment
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<bool> Handle(SetPaidOrderStatusCommand command, CancellationToken cancellationToken)
    {
        // Simulate a work time for validating the payment
        await Task.Delay(10000, cancellationToken);

        var orderToUpdate = await _orderRepository.GetAsync(command.OrderNumber);
        if (orderToUpdate == null)
        {
            return false;
        }

        orderToUpdate.SetPaidStatus();

        // ISO 20022: model the capture/settlement leg as a pacs.008
        // interbank credit transfer and record it in the immutable audit trail.
        var pacs008 = Iso20022PaymentMessageFactory.CreatePacs008(new PaymentInstruction
        {
            EndToEndId = $"ORDER-{command.OrderNumber}",
            Amount = orderToUpdate.GetTotal(),
            Currency = "USD",
            DebtorName = orderToUpdate.Buyer?.Name ?? "eShop Buyer",
            CreditorName = "eShop Inc.",
            RemittanceInformation = $"eShop order {command.OrderNumber} settlement"
        });

        await _financialAuditStore.RecordAsync(new FinancialAuditEntry
        {
            EventType = FinancialAuditEventType.PaymentCaptured,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = pacs008.GroupHeader.MessageId,
            Subject = $"Order/{command.OrderNumber}",
            Amount = orderToUpdate.GetTotal(),
            Currency = "USD",
            Outcome = "Succeeded",
            Iso20022MessageId = pacs008.GroupHeader.MessageId,
            Details = new Dictionary<string, string>
            {
                ["PaymentMessageType"] = "pacs.008.001.08"
            }
        }, cancellationToken);

        return await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
    }
}


// Use for Idempotency in Command process
public class SetPaidIdentifiedOrderStatusCommandHandler : IdentifiedCommandHandler<SetPaidOrderStatusCommand, bool>
{
    public SetPaidIdentifiedOrderStatusCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<SetPaidOrderStatusCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for processing order.
    }
}
