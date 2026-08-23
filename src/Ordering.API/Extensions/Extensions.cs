using FluentValidation;

internal static class Extensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        
        // Add the authentication services to DI
        builder.AddDefaultAuthentication();

        // Pooling is disabled because of the following error:
        // Unhandled exception. System.InvalidOperationException:
        // The DbContext of type 'OrderingContext' cannot be pooled because it does not have a public constructor accepting a single parameter of type DbContextOptions or has more than one constructor.
        services.AddDbContext<OrderingContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("orderingdb"));
        });
        builder.EnrichNpgsqlDbContext<OrderingContext>();

        services.AddMigration<OrderingContext, OrderingContextSeed>();

        // Add the integration services that consume the DbContext
        services.AddTransient<IIntegrationEventLogService, IntegrationEventLogService<OrderingContext>>();

        services.AddTransient<IOrderingIntegrationEventService, OrderingIntegrationEventService>();

        builder.AddRabbitMqEventBus("eventbus")
               .AddEventBusSubscriptions();

        services.AddHttpContextAccessor();
        services.AddTransient<IIdentityService, IdentityService>();

        // Configure mediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(Program));

            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidatorBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        // Register the command validators for the validator behavior (validators based on FluentValidation library)
        services.AddValidatorsFromAssemblyContaining<CancelOrderCommandValidator>();

        services.AddScoped<IOrderQueries, OrderQueries>();
        services.AddScoped<IBuyerRepository, BuyerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IRequestManager, RequestManager>();

        AddFinancialSecurityServices(builder);
    }

    /// <summary>
    /// Registers the banking-grade cross-cutting services:
    /// PCI-DSS field-level encryption, AML/PSD2 audit trail and the
    /// idempotency token store (Redis when provisioned, in-memory otherwise).
    /// </summary>
    private static void AddFinancialSecurityServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        // PCI-DSS 3.4/3.5: AES-256 field-level encryption of payment data at rest.
        // The key must come from a secure configuration source (env var or key vault),
        // never from source control. Without a key a loud pass-through protector is
        // used so local development keeps working.
        services.AddOptions<PaymentDataProtectionOptions>()
            .BindConfiguration(PaymentDataProtectionOptions.SectionName);
        services.AddSingleton<ISensitiveDataProtector>(sp =>
        {
            var key = builder.Configuration[$"{PaymentDataProtectionOptions.SectionName}:Key"];
            return string.IsNullOrWhiteSpace(key)
                ? new NullSensitiveDataProtector(sp.GetRequiredService<ILogger<NullSensitiveDataProtector>>())
                : new Aes256SensitiveDataProtector(key);
        });

        // AML/PSD2: immutable, hash-chained financial audit trail.
        services.AddScoped<IFinancialAuditStore, EfFinancialAuditStore>();

        // Strict idempotency tokens for transaction endpoints: Redis in deployed
        // environments, in-memory cache for local dev and tests.
        if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
        {
            builder.AddRedisDistributedCache("redis");
            services.AddSingleton<IIdempotencyTokenStore, DistributedCacheIdempotencyTokenStore>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<IIdempotencyTokenStore, MemoryCacheIdempotencyTokenStore>();
        }
    }

    private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
    {
        eventBus.AddSubscription<GracePeriodConfirmedIntegrationEvent, GracePeriodConfirmedIntegrationEventHandler>();
        eventBus.AddSubscription<OrderStockConfirmedIntegrationEvent, OrderStockConfirmedIntegrationEventHandler>();
        eventBus.AddSubscription<OrderStockRejectedIntegrationEvent, OrderStockRejectedIntegrationEventHandler>();
        eventBus.AddSubscription<OrderPaymentFailedIntegrationEvent, OrderPaymentFailedIntegrationEventHandler>();
        eventBus.AddSubscription<OrderPaymentSucceededIntegrationEvent, OrderPaymentSucceededIntegrationEventHandler>();
    }
}
