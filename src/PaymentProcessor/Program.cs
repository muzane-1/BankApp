using eShop.Payment.Shared.Audit;
using eShop.Payment.Shared.Idempotency;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions));

// Strict idempotency for payment executions and the immutable AML/PSD2 audit stream.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IIdempotencyTokenStore, MemoryCacheIdempotencyTokenStore>();
builder.Services.AddSingleton<IFinancialAuditStore, StructuredLogFinancialAuditStore>();

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
