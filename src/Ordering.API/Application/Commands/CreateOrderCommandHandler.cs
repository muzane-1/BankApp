namespace eShop.Ordering.API.Application.Commands;

using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;

// Regular CommandHandler
public class CreateOrderCommandHandler
    : IRequestHandler<CreateOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IIdentityService _identityService;
    private readonly IMediator _mediator;
    private readonly IOrderingIntegrationEventService _orderingIntegrationEventService;
    private readonly IFinancialAuditStore _financialAuditStore;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    // Using DI to inject infrastructure persistence Repositories
    public CreateOrderCommandHandler(IMediator mediator,
        IOrderingIntegrationEventService orderingIntegrationEventService,
        IOrderRepository orderRepository,
        IIdentityService identityService,
        IFinancialAuditStore financialAuditStore,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _orderingIntegrationEventService = orderingIntegrationEventService ?? throw new ArgumentNullException(nameof(orderingIntegrationEventService));
        _financialAuditStore = financialAuditStore ?? throw new ArgumentNullException(nameof(financialAuditStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(CreateOrderCommand message, CancellationToken cancellationToken)
    {
        // Add Integration event to clean the basket
        var orderStartedIntegrationEvent = new OrderStartedIntegrationEvent(message.UserId);
        await _orderingIntegrationEventService.AddAndSaveEventAsync(orderStartedIntegrationEvent);

        // Add/Update the Buyer AggregateRoot
        // DDD patterns comment: Add child entities and value-objects through the Order Aggregate-Root
        // methods and constructor so validations, invariants and business logic 
        // make sure that consistency is preserved across the whole aggregate
        var address = new Address(message.Street, message.City, message.State, message.Country, message.ZipCode);
        var order = new Order(message.UserId, message.UserName, address, message.CardTypeId, message.CardNumber, message.CardSecurityNumber, message.CardHolderName, message.CardExpiration);

        foreach (var item in message.OrderItems)
        {
            order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice, item.Discount, item.PictureUrl, item.Units);
        }

        _logger.LogInformation("Creating Order - Order: {@Order}", order);

        _orderRepository.Add(order);

        // ISO 20022: model the order payment as a pain.001 credit transfer
        // initiation message. The PAN is masked (PCI-DSS 3.3) before it can
        // reach any message, log or audit payload.
        var paymentInstruction = new PaymentInstruction
        {
            EndToEndId = $"ORDER-{order.Id}",
            Amount = order.GetTotal(),
            Currency = "USD",
            DebtorName = message.UserName,
            DebtorAccountId = PanMasking.Mask(message.CardNumber),
            CreditorName = "eShop Inc.",
            RemittanceInformation = $"eShop order payment by buyer {message.UserId}"
        };
        var pain001 = Iso20022PaymentMessageFactory.CreatePain001(paymentInstruction, "eShop Ordering");

        // AML/PSD2: append an immutable audit record inside the same ACID
        // transaction as the order itself.
        await _financialAuditStore.RecordAsync(new FinancialAuditEntry
        {
            EventType = FinancialAuditEventType.PaymentInitiated,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = pain001.GroupHeader.MessageId,
            ActorId = message.UserId,
            Subject = $"Order/{order.Id}",
            Amount = order.GetTotal(),
            Currency = "USD",
            Outcome = "Succeeded",
            Iso20022MessageId = pain001.GroupHeader.MessageId,
            Details = new Dictionary<string, string>
            {
                ["PaymentMessageType"] = "pain.001.001.09",
                ["MaskedCardNumber"] = PanMasking.Mask(message.CardNumber),
                ["CardTypeId"] = message.CardTypeId.ToString()
            }
        }, cancellationToken);

        _logger.LogInformation(
            "ISO 20022 pain.001 payment initiation {MessageId} created for order {OrderId} (masked PAN {MaskedPan})",
            pain001.GroupHeader.MessageId, order.Id, PanMasking.Mask(message.CardNumber));

        return await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
    }
}


// Use for Idempotency in Command process
public class CreateOrderIdentifiedCommandHandler : IdentifiedCommandHandler<CreateOrderCommand, bool>
{
    public CreateOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<CreateOrderCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for creating order.
    }
}
