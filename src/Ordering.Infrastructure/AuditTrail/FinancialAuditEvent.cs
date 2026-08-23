namespace eShop.Ordering.Infrastructure.AuditTrail;

/// <summary>
/// Immutable, append-only financial audit record persisted in the ordering
/// database. Each row carries the SHA-256 hash of its canonical payload chained
/// to the previous row's hash, making tampering or deletion of history
/// detectable (AML / PSD2 record-keeping).
/// </summary>
public sealed class FinancialAuditEvent
{
    public long Id { get; set; }

    public required string EventType { get; set; }

    public required DateTimeOffset OccurredAtUtc { get; set; }

    public required string CorrelationId { get; set; }

    public string ActorId { get; set; }

    public string Subject { get; set; }

    public decimal? Amount { get; set; }

    public string Currency { get; set; }

    public required string Outcome { get; set; }

    public string Iso20022MessageId { get; set; }

    /// <summary>JSON serialized audit details (masked values only).</summary>
    public required string DetailsJson { get; set; }

    /// <summary>Hash of the previous record in the chain, or "GENESIS" for the first.</summary>
    public required string PreviousHash { get; set; }

    /// <summary>SHA-256 hash chaining this record to <see cref="PreviousHash"/>.</summary>
    public required string Hash { get; set; }
}
