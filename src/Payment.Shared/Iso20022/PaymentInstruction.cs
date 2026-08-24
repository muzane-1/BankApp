namespace eShop.Payment.Shared.Iso20022;

/// <summary>
/// Canonical payment instruction consumed by the ISO 20022 message factory.
/// Sensitive account identifiers must already be masked before being placed
/// on an instruction (PCI-DSS requirement 3.3).
/// </summary>
public sealed record PaymentInstruction
{
    /// <summary>End-to-end identification that travels unchanged through the payment chain (max 35 chars).</summary>
    public required string EndToEndId { get; init; }

    /// <summary>Instructed amount; null when the amount is resolved downstream by the settlement system.</summary>
    public decimal? Amount { get; init; }

    /// <summary>ISO 4217 currency code, e.g. "USD".</summary>
    public required string Currency { get; init; }

    /// <summary>Debtor (ordering customer) display name.</summary>
    public required string DebtorName { get; init; }

    /// <summary>Debtor account identification (masked PAN or IBAN).</summary>
    public string? DebtorAccountId { get; init; }

    /// <summary>Debtor agent BIC.</summary>
    public string? DebtorAgentBic { get; init; }

    /// <summary>Creditor (merchant) display name.</summary>
    public required string CreditorName { get; init; }

    /// <summary>Creditor account identification (IBAN).</summary>
    public string? CreditorAccountId { get; init; }

    /// <summary>Creditor agent BIC.</summary>
    public string? CreditorAgentBic { get; init; }

    /// <summary>Charge bearer, ISO code such as "SLEV" or "SHAR".</summary>
    public string ChargeBearer { get; init; } = "SLEV";

    /// <summary>Unstructured remittance information.</summary>
    public string? RemittanceInformation { get; init; }
}
