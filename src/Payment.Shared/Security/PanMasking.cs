namespace eShop.Payment.Shared.Security;

/// <summary>
/// PCI-DSS requirement 3.3: the primary account number (PAN) must be masked
/// when displayed or logged, showing at most the first six and last four digits.
/// </summary>
public static class PanMasking
{
    /// <summary>
    /// Masks a PAN for logs, audit records and UI display. Only the last four
    /// digits are retained; everything else is replaced by 'X'.
    /// </summary>
    public static string Mask(string? pan)
    {
        if (string.IsNullOrWhiteSpace(pan))
        {
            return string.Empty;
        }

        var digitsOnly = new string(pan.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length <= 4)
        {
            return new string('X', digitsOnly.Length);
        }

        return new string('X', digitsOnly.Length - 4) + digitsOnly[^4..];
    }

    /// <summary>Masks sensitive authentication data (CVV/CVC) entirely; it must never be logged.</summary>
    public static string MaskSecurityCode(string? securityCode) =>
        string.IsNullOrEmpty(securityCode) ? string.Empty : "***";
}
