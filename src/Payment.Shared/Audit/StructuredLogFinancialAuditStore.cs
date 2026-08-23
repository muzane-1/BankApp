using Microsoft.Extensions.Logging;

namespace eShop.Payment.Shared.Audit;

/// <summary>
/// Writes financial audit records as immutable, structured log events on the
/// dedicated "FinancialAudit" event id so they can be shipped to a WORM
/// (write-once-read-many) SIEM sink. Used by services without a database
/// (e.g. the payment processor worker) and as a defence-in-depth mirror of
/// the database-backed audit trail.
/// </summary>
public sealed class StructuredLogFinancialAuditStore : IFinancialAuditStore
{
    public static readonly EventId AuditEventId = new(7700, "FinancialAudit");

    private readonly ILogger<StructuredLogFinancialAuditStore> _logger;

    public StructuredLogFinancialAuditStore(ILogger<StructuredLogFinancialAuditStore> logger)
    {
        _logger = logger;
    }

    public Task RecordAsync(FinancialAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _logger.LogInformation(
            AuditEventId,
            "FinancialAudit {AuditEventType} CorrelationId={AuditCorrelationId} Subject={AuditSubject} Actor={AuditActor} Amount={AuditAmount} {AuditCurrency} Outcome={AuditOutcome} Iso20022MsgId={AuditIso20022MessageId} Details={@AuditDetails}",
            entry.EventType.ToString(),
            entry.CorrelationId,
            entry.Subject,
            entry.ActorId,
            entry.Amount,
            entry.Currency,
            entry.Outcome,
            entry.Iso20022MessageId,
            entry.Details);

        return Task.CompletedTask;
    }
}
