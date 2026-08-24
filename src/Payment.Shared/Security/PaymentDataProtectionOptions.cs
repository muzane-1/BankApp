namespace eShop.Payment.Shared.Security;

/// <summary>
/// Configuration for PCI-DSS field-level encryption of payment data.
/// The key must be supplied through a secure configuration source
/// (environment variable, user-secrets, or a managed key vault) and
/// must never be committed to source control.
/// </summary>
public sealed class PaymentDataProtectionOptions
{
    public const string SectionName = "PaymentDataProtection";

    /// <summary>Base64-encoded 256-bit (32 byte) AES key.</summary>
    public string? Key { get; set; }
}
