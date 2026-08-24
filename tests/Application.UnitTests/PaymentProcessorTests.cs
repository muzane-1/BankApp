using eShop.EventBus.Abstractions;
using eShop.Payment.Shared.Audit;
using eShop.Payment.Shared.Idempotency;
using eShop.PaymentProcessor;
using eShop.PaymentProcessor.IntegrationEvents.EventHandling;
using eShop.PaymentProcessor.IntegrationEvents.Events;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace eShop.Application.UnitTests;

[TestClass]
public class PaymentProcessorTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PublishesConfiguredPaymentOutcome(bool paymentSucceeded)
    {
        var eventBus = Substitute.For<IEventBus>();
        var options = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        options.CurrentValue.Returns(new PaymentOptions { PaymentSucceeded = paymentSucceeded });
        var handler = new OrderStatusChangedToStockConfirmedIntegrationEventHandler(
            eventBus,
            options,
            new MemoryCacheIdempotencyTokenStore(new MemoryCache(new MemoryCacheOptions())),
            Substitute.For<IFinancialAuditStore>(),
            NullLogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler>.Instance);

        await handler.Handle(new OrderStatusChangedToStockConfirmedIntegrationEvent(42));

        if (paymentSucceeded)
        {
            await eventBus.Received(1).PublishAsync(
                Arg.Is<OrderPaymentSucceededIntegrationEvent>(e => e.OrderId == 42));
            await eventBus.DidNotReceive().PublishAsync(Arg.Any<OrderPaymentFailedIntegrationEvent>());
        }
        else
        {
            await eventBus.Received(1).PublishAsync(
                Arg.Is<OrderPaymentFailedIntegrationEvent>(e => e.OrderId == 42));
            await eventBus.DidNotReceive().PublishAsync(Arg.Any<OrderPaymentSucceededIntegrationEvent>());
        }
    }

    [TestMethod]
    public async Task SuppressesDuplicatePaymentExecution()
    {
        var eventBus = Substitute.For<IEventBus>();
        var options = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        options.CurrentValue.Returns(new PaymentOptions { PaymentSucceeded = true });
        var auditStore = Substitute.For<IFinancialAuditStore>();
        var handler = new OrderStatusChangedToStockConfirmedIntegrationEventHandler(
            eventBus,
            options,
            new MemoryCacheIdempotencyTokenStore(new MemoryCache(new MemoryCacheOptions())),
            auditStore,
            NullLogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler>.Instance);

        // The same integration event (same Id) redelivered by the broker.
        var stockConfirmed = new OrderStatusChangedToStockConfirmedIntegrationEvent(42);
        await handler.Handle(stockConfirmed);
        await handler.Handle(stockConfirmed);

        // The payment must be executed exactly once: no double-spending.
        await eventBus.Received(1).PublishAsync(Arg.Any<OrderPaymentSucceededIntegrationEvent>());
        await auditStore.Received(1).RecordAsync(
            Arg.Is<FinancialAuditEntry>(e => e.EventType == FinancialAuditEventType.PaymentAuthorized),
            Arg.Any<CancellationToken>());
    }
}
