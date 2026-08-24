using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using eShop.Payment.Shared.Security;

namespace eShop.Ordering.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core value converter that transparently encrypts sensitive string columns
/// with AES-256 on write and decrypts on read (PCI-DSS requirement 3.4).
/// The in-memory domain model always holds plaintext; ciphertext never leaves
/// the persistence layer.
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(ISensitiveDataProtector protector)
        : base(
            plaintext => protector.Encrypt(plaintext),
            ciphertext => protector.Decrypt(ciphertext))
    {
    }
}
