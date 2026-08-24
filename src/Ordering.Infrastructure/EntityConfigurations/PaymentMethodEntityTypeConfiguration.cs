using eShop.Payment.Shared.Security;

namespace eShop.Ordering.Infrastructure.EntityConfigurations;

class PaymentMethodEntityTypeConfiguration
    : IEntityTypeConfiguration<PaymentMethod>
{
    private readonly ISensitiveDataProtector _sensitiveDataProtector;

    public PaymentMethodEntityTypeConfiguration(ISensitiveDataProtector sensitiveDataProtector = null)
    {
        _sensitiveDataProtector = sensitiveDataProtector;
    }

    public void Configure(EntityTypeBuilder<PaymentMethod> paymentConfiguration)
    {
        paymentConfiguration.ToTable("paymentmethods");

        paymentConfiguration.Ignore(b => b.DomainEvents);

        paymentConfiguration.Property(b => b.Id)
            .UseHiLo("paymentseq");

        paymentConfiguration.Property<int>("BuyerId");

        paymentConfiguration
            .Property("_cardHolderName")
            .HasColumnName("CardHolderName")
            .HasMaxLength(200);

        paymentConfiguration
            .Property("_alias")
            .HasColumnName("Alias")
            .HasMaxLength(200);

        // PCI-DSS 3.4: the PAN and security code are encrypted at rest with
        // AES-256 when a data protection key is configured. Ciphertext is
        // Base64 and larger than the cleartext, so the columns are widened.
        var cardNumber = paymentConfiguration
            .Property("_cardNumber")
            .HasColumnName("CardNumber")
            .HasMaxLength(256)
            .IsRequired();

        var securityNumber = paymentConfiguration
            .Property("_securityNumber")
            .HasColumnName("SecurityNumber")
            .HasMaxLength(256);

        if (_sensitiveDataProtector?.IsEncryptionEnabled == true)
        {
            cardNumber.HasConversion(new EncryptedStringConverter(_sensitiveDataProtector));
            securityNumber.HasConversion(new EncryptedStringConverter(_sensitiveDataProtector));
        }

        paymentConfiguration
            .Property("_expiration")
            .HasColumnName("Expiration")
            .HasMaxLength(25);

        paymentConfiguration
            .Property("_cardTypeId")
            .HasColumnName("CardTypeId");

        paymentConfiguration.HasOne(p => p.CardType)
            .WithMany()
            .HasForeignKey("_cardTypeId");
    }
}
