using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace eShop.Payment.Shared.Audit;

/// <summary>
/// Append-only store for financial audit records. Implementations must be
/// immutable: records are written once and never updated or deleted
/// (PSD2 art. 97 record-keeping, AML directive record retention).
/// </summary>
public interface IFinancialAuditStore
{
    Task RecordAsync(FinancialAuditEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Computes the tamper-evident SHA-256 hash for an audit record, chained to the
/// hash of the previous record so any modification or deletion of history is
/// detectable (blockchain-style integrity chain).
/// </summary>
public static class FinancialAuditHash
{
    /// <summary>Hash used for the very first record in the chain.</summary>
    public const string GenesisHash = "GENESIS";

    public static string Compute(string previousHash, FinancialAuditEntry entry)
    {
        var canonical = string.Join('|',
            previousHash,
            entry.EventType,
            entry.OccurredAtUtc.UtcDateTime.ToString("O"),
            entry.CorrelationId,
            entry.ActorId ?? string.Empty,
            entry.Subject ?? string.Empty,
            entry.Amount?.ToString("0.00") ?? string.Empty,
            entry.Currency ?? string.Empty,
            entry.Outcome,
            entry.Iso20022MessageId ?? string.Empty,
            JsonSerializer.Serialize(entry.Details));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash);
    }
}
