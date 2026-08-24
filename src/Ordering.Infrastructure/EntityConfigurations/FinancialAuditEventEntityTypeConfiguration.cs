using eShop.Ordering.Infrastructure.AuditTrail;

namespace eShop.Ordering.Infrastructure.EntityConfigurations;

class FinancialAuditEventEntityTypeConfiguration : IEntityTypeConfiguration<FinancialAuditEvent>
{
    public void Configure(EntityTypeBuilder<FinancialAuditEvent> auditConfiguration)
    {
        auditConfiguration.ToTable("financial_audit_events");

        // Identity column: the monotonically increasing id defines the hash-chain order.
        auditConfiguration.Property(a => a.Id)
            .UseIdentityAlwaysColumn();

        auditConfiguration.Property(a => a.EventType)
            .HasMaxLength(64)
            .IsRequired();

        auditConfiguration.Property(a => a.OccurredAtUtc)
            .IsRequired();

        auditConfiguration.Property(a => a.CorrelationId)
            .HasMaxLength(64)
            .IsRequired();

        auditConfiguration.Property(a => a.ActorId)
            .HasMaxLength(200);

        auditConfiguration.Property(a => a.Subject)
            .HasMaxLength(200);

        auditConfiguration.Property(a => a.Amount)
            .HasPrecision(18, 2);

        auditConfiguration.Property(a => a.Currency)
            .HasMaxLength(3);

        auditConfiguration.Property(a => a.Outcome)
            .HasMaxLength(32)
            .IsRequired();

        auditConfiguration.Property(a => a.Iso20022MessageId)
            .HasMaxLength(35);

        auditConfiguration.Property(a => a.DetailsJson)
            .HasColumnType("jsonb")
            .IsRequired();

        auditConfiguration.Property(a => a.PreviousHash)
            .HasMaxLength(64)
            .IsRequired();

        auditConfiguration.Property(a => a.Hash)
            .HasMaxLength(64)
            .IsRequired();

        // Query performance: audit lookups are by correlation id, subject, and time window.
        auditConfiguration.HasIndex(a => a.CorrelationId);
        auditConfiguration.HasIndex(a => a.OccurredAtUtc);
        auditConfiguration.HasIndex(a => a.Subject);
    }
}
