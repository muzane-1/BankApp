using eShop.Payment.Shared.Audit;
using eShop.Payment.Shared.Idempotency;
using eShop.Payment.Shared.Iso20022;

namespace eShop.PaymentProcessor.IntegrationEvents.EventHandling;

public class OrderStatusChangedToStockConfirmedIntegrationEventHandler(
    IEventBus eventBus,
    IOptionsMonitor<PaymentOptions> options,
    IIdempotencyTokenStore idempotencyTokenStore,
    IFinancialAuditStore financialAuditStore,
    ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderStatusChangedToStockConfirmedIntegrationEvent>
{
    private static readonly TimeSpan IdempotencyTokenTtl = TimeSpan.FromHours(24);

    public async Task Handle(OrderStatusChangedToStockConfirmedIntegrationEvent @event)
    {
        logger.LogInformation("Handling integration event: {IntegrationEventId} - ({@IntegrationEvent})", @event.Id, @event);

        // Strict idempotency: a redelivered event must never execute the payment
        // twice (double-spending protection on the payment execution boundary).
        var idempotencyKey = $"payment-execution:{@event.Id}";
        if (!await idempotencyTokenStore.TryAcquireAsync(idempotencyKey, IdempotencyTokenTtl))
        {
            logger.LogWarning(
                "Duplicate payment execution suppressed for event {IntegrationEventId} (order {OrderId})",
                @event.Id, @event.OrderId);
            return;
        }

        // ISO 20022: model the gateway payment as a pacs.008 interbank credit transfer.
        var pacs008 = Iso20022PaymentMessageFactory.CreatePacs008(new PaymentInstruction
        {
            EndToEndId = $"ORDER-{@event.OrderId}",
            Currency = "USD",
            DebtorName = "eShop Buyer",
            CreditorName = "eShop Inc.",
            RemittanceInformation = $"eShop order {@event.OrderId} payment"
        });

        IntegrationEvent orderPaymentIntegrationEvent;

        // Business feature comment:
        // When OrderStatusChangedToStockConfirmed Integration Event is handled.
        // Here we're simulating that we'd be performing the payment against any payment gateway
        // Instead of a real payment we just take the env. var to simulate the payment 
        // The payment can be successful or it can fail

        if (options.CurrentValue.PaymentSucceeded)
        {
            orderPaymentIntegrationEvent = new OrderPaymentSucceededIntegrationEvent(@event.OrderId);
        }
        else
        {
            orderPaymentIntegrationEvent = new OrderPaymentFailedIntegrationEvent(@event.OrderId);
        }

        // AML/PSD2: immutable audit record of the payment authorization decision.
        await financialAuditStore.RecordAsync(new FinancialAuditEntry
        {
            EventType = FinancialAuditEventType.PaymentAuthorized,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = @event.Id.ToString(),
            Subject = $"Order/{@event.OrderId}",
            Currency = "USD",
            Outcome = options.CurrentValue.PaymentSucceeded ? "Succeeded" : "Failed",
            Iso20022MessageId = pacs008.GroupHeader.MessageId,
            Details = new Dictionary<string, string>
            {
                ["PaymentMessageType"] = "pacs.008.001.08"
            }
        });

        logger.LogInformation("Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", orderPaymentIntegrationEvent.Id, orderPaymentIntegrationEvent);

        await eventBus.PublishAsync(orderPaymentIntegrationEvent);

        await idempotencyTokenStore.MarkCompletedAsync(idempotencyKey, IdempotencyTokenTtl);
    }
}
