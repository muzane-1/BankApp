using System.Text.Json;
using eShop.Payment.Shared.Audit;
using Microsoft.Extensions.Logging;

namespace eShop.Ordering.Infrastructure.AuditTrail;

/// <summary>
/// Append-only, hash-chained audit store backed by the ordering database.
/// Records participate in the ambient EF transaction so an audit entry is
/// committed atomically with the financial state change it describes (ACID),
/// and every record is mirrored to the structured log stream for SIEM export.
/// </summary>
public sealed class EfFinancialAuditStore : IFinancialAuditStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly OrderingContext _context;
    private readonly ILogger<EfFinancialAuditStore> _logger;

    public EfFinancialAuditStore(OrderingContext context, ILogger<EfFinancialAuditStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RecordAsync(FinancialAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var previousHash = await _context.FinancialAuditEvents
            .OrderByDescending(a => a.Id)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync(cancellationToken)
            ?? FinancialAuditHash.GenesisHash;

        var auditEvent = new FinancialAuditEvent
        {
            EventType = entry.EventType.ToString(),
            OccurredAtUtc = entry.OccurredAtUtc,
            CorrelationId = entry.CorrelationId,
            ActorId = entry.ActorId,
            Subject = entry.Subject,
            Amount = entry.Amount,
            Currency = entry.Currency,
            Outcome = entry.Outcome,
            Iso20022MessageId = entry.Iso20022MessageId,
            DetailsJson = JsonSerializer.Serialize(entry.Details, SerializerOptions),
            PreviousHash = previousHash,
            Hash = FinancialAuditHash.Compute(previousHash, entry)
        };

        _context.FinancialAuditEvents.Add(auditEvent);

        _logger.LogInformation(
            StructuredLogFinancialAuditStore.AuditEventId,
            "FinancialAudit {AuditEventType} CorrelationId={AuditCorrelationId} Subject={AuditSubject} Actor={AuditActor} Amount={AuditAmount} {AuditCurrency} Outcome={AuditOutcome} Iso20022MsgId={AuditIso20022MessageId} Hash={AuditHash}",
            auditEvent.EventType,
            auditEvent.CorrelationId,
            auditEvent.Subject,
            auditEvent.ActorId,
            auditEvent.Amount,
            auditEvent.Currency,
            auditEvent.Outcome,
            auditEvent.Iso20022MessageId,
            auditEvent.Hash);
    }
}
