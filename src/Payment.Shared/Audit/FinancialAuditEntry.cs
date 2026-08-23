namespace eShop.Payment.Shared.Audit;

/// <summary>Type of a financial audit event (AML / PSD2 record-keeping taxonomy).</summary>
public enum FinancialAuditEventType
{
    PaymentInitiated,
    PaymentAuthorized,
    PaymentCaptured,
    PaymentFailed,
    PaymentRefunded,
    OrderCancelled
}

/// <summary>
/// A single immutable financial audit record. Sensitive attributes (PAN, CVV)
/// must be masked before being attached; audit payloads must never contain
/// cleartext cardholder data (PCI-DSS requirement 3.2 / 10.2).
/// </summary>
public sealed record FinancialAuditEntry
{
    public required FinancialAuditEventType EventType { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>Correlation / request id tying the audit record to the originating transaction.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Authenticated identity (subject) that triggered the financial operation.</summary>
    public string? ActorId { get; init; }

    /// <summary>Business subject of the record, e.g. "Order/42".</summary>
    public string? Subject { get; init; }

    public decimal? Amount { get; init; }

    public string? Currency { get; init; }

    /// <summary>Outcome of the operation, e.g. "Succeeded" / "Failed" / "Duplicate".</summary>
    public string Outcome { get; init; } = "Succeeded";

    /// <summary>ISO 20022 message id (MsgId) when the operation produced a financial message.</summary>
    public string? Iso20022MessageId { get; init; }

    /// <summary>Additional structured attributes. Values must be masked; never include PAN/CVV in cleartext.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } =
        new Dictionary<string, string>();
}
