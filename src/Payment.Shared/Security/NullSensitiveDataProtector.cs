using Microsoft.Extensions.Logging;

namespace eShop.Payment.Shared.Security;

/// <summary>
/// Pass-through protector used only for local development when no
/// <see cref="PaymentDataProtectionOptions.Key"/> is configured.
/// It never encrypts and must never be active in production: every
/// encryption operation logs a loud warning so misconfiguration is visible.
/// </summary>
public sealed class NullSensitiveDataProtector : ISensitiveDataProtector
{
    private readonly ILogger<NullSensitiveDataProtector>? _logger;
    private int _warningLogged;

    public NullSensitiveDataProtector(ILogger<NullSensitiveDataProtector>? logger = null)
    {
        _logger = logger;
    }

    public bool IsEncryptionEnabled => false;

    public string Encrypt(string plaintext)
    {
        WarnOnce();
        return plaintext;
    }

    public string Decrypt(string ciphertext) => ciphertext;

    private void WarnOnce()
    {
        if (Interlocked.Exchange(ref _warningLogged, 1) == 0)
        {
            _logger?.LogWarning(
                "PCI-DSS VIOLATION RISK: no PaymentDataProtection key is configured, sensitive payment fields are stored unencrypted. " +
                "Configure '{SectionName}__Key' with a Base64-encoded 256-bit key (e.g. 'openssl rand -base64 32').",
                PaymentDataProtectionOptions.SectionName);
        }
    }
}
