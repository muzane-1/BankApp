namespace eShop.Payment.Shared.Security;

/// <summary>
/// Provides field-level encryption for sensitive payment attributes
/// (PAN, security codes) as required by PCI-DSS requirement 3.4
/// ("Render PAN unreadable anywhere it is stored").
/// </summary>
public interface ISensitiveDataProtector
{
    /// <summary>Encrypts a plaintext value for storage at rest.</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypts a value previously produced by <see cref="Encrypt"/>.</summary>
    string Decrypt(string ciphertext);

    /// <summary>
    /// True when the protector performs real encryption. False for the
    /// pass-through development protector, which must never be used in production.
    /// </summary>
    bool IsEncryptionEnabled { get; }
}
