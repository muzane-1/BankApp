using System.Security.Cryptography;
using System.Text;

namespace eShop.Payment.Shared.Security;

/// <summary>
/// AES-256-GCM authenticated field-level encryption for sensitive payment
/// attributes (PCI-DSS requirement 3.5). Each value is encrypted with a
/// unique random 96-bit nonce; the 128-bit authentication tag provides
/// tamper evidence so manipulated ciphertext is rejected on read.
/// Payload layout: Base64( nonce[12] | tag[16] | ciphertext ).
/// </summary>
public sealed class Aes256SensitiveDataProtector : ISensitiveDataProtector, IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public Aes256SensitiveDataProtector(string base64Key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Key);

        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("The payment data protection key must be a Base64-encoded 256-bit key.", nameof(base64Key), ex);
        }

        if (key.Length != 32)
        {
            throw new ArgumentException("The payment data protection key must be exactly 256 bits (32 bytes, Base64-encoded).", nameof(base64Key));
        }

        _key = key;
    }

    public bool IsEncryptionEnabled => true;

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        var payload = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, payload, NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        var payload = Convert.FromBase64String(ciphertext);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("The ciphertext payload is not a valid protected value.");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipherBytes = payload.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipherBytes.Length];

        try
        {
            using (var aes = new AesGcm(_key, TagSize))
            {
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new CryptographicException(
                "The protected value failed authentication: it was tampered with or encrypted with a different key.", ex);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}
