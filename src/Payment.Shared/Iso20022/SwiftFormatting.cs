using System.Text.RegularExpressions;

namespace eShop.Payment.Shared.Iso20022;

/// <summary>
/// SWIFT formatting and validation helpers for BIC (ISO 9362) and IBAN (ISO 13616)
/// identifiers carried by ISO 20022 payment messages.
/// </summary>
public static partial class SwiftFormatting
{
    // ISO 9362: 4-letter institution, 2-letter country, 2-alphanumeric location,
    // optional 3-alphanumeric branch. Total 8 or 11 characters.
    [GeneratedRegex("^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.Compiled)]
    private static partial Regex BicPattern();

    // ISO 13616: 2-letter country, 2 check digits, up to 30 alphanumeric BBAN chars.
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.Compiled)]
    private static partial Regex IbanPattern();

    /// <summary>Validates a SWIFT/BIC code per ISO 9362 (8 or 11 characters).</summary>
    public static bool IsValidBic(string? bic) =>
        !string.IsNullOrWhiteSpace(bic) && BicPattern().IsMatch(bic.Trim());

    /// <summary>Validates an IBAN structurally per ISO 13616 (format only, no mod-97 check).</summary>
    public static bool IsValidIban(string? iban) =>
        !string.IsNullOrWhiteSpace(iban) && IbanPattern().IsMatch(NormalizeIban(iban));

    /// <summary>Removes whitespace and upper-cases an IBAN for canonical storage/transmission.</summary>
    public static string NormalizeIban(string iban) =>
        string.Concat(iban.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    /// <summary>
    /// Formats an IBAN in the SWIFT-printable presentation form: groups of four
    /// characters separated by single spaces (e.g. "DE89 3704 0044 0532 0130 00").
    /// </summary>
    public static string FormatIbanForDisplay(string iban)
    {
        var normalized = NormalizeIban(iban);
        return string.Join(' ', Enumerable.Range(0, (normalized.Length + 3) / 4)
            .Select(i => normalized.Substring(i * 4, Math.Min(4, normalized.Length - i * 4))));
    }

    /// <summary>
    /// Masks an IBAN for logging/display per PCI-DSS-style data minimization:
    /// preserves the country code and check digits plus the last four characters.
    /// </summary>
    public static string MaskIban(string iban)
    {
        var normalized = NormalizeIban(iban);
        if (normalized.Length <= 8)
        {
            return normalized;
        }

        return $"{normalized[..4]}{new string('X', normalized.Length - 8)}{normalized[^4..]}";
    }
}
