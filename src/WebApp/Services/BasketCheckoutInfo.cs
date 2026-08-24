using System.ComponentModel.DataAnnotations;

namespace eShop.WebApp.Services;

public class BasketCheckoutInfo
{
    [Required]
    public string? Street { get; set; }

    [Required]
    public string? City { get; set; }

    [Required]
    public string? State { get; set; }

    [Required]
    public string? Country { get; set; }

    [Required]
    public string? ZipCode { get; set; }

    public string? CardNumber { get; set; }

    public string? CardHolderName { get; set; }

    public string? CardSecurityNumber { get; set; }

    public DateTime? CardExpiration { get; set; }

    public int CardTypeId { get; set; }

    public string? Buyer { get; set; }
    public Guid RequestId { get; set; }

    // ISO 20022 / SWIFT payment-transfer gateway fields.
    // BeneficiaryName maps to the ISO 20022 Creditor party name,
    // BeneficiaryIban to the creditor account identification, and
    // SwiftCode to the creditor agent BIC (BICFI element).

    /// <summary>Beneficiary (creditor) display name — ISO 20022 Cdtr/Nm.</summary>
    [Required]
    [StringLength(140, ErrorMessage = "Beneficiary name must be 140 characters or fewer")]
    public string? BeneficiaryName { get; set; }

    /// <summary>Beneficiary account — ISO 20022 IBAN identification.</summary>
    [Required]
    [RegularExpression("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
        ErrorMessage = "Enter a valid IBAN (e.g. DE89370400440532013000)")]
    public string? BeneficiaryIban { get; set; }

    /// <summary>Beneficiary bank SWIFT/BIC code — ISO 20022 BICFI identifier.</summary>
    [Required]
    [RegularExpression("^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$",
        ErrorMessage = "Enter a valid SWIFT/BIC code (8 or 11 characters)")]
    public string? SwiftCode { get; set; }

    /// <summary>Transaction amount — ISO 20022 instructed amount (InstdAmt).</summary>
    [Range(0.01, 1000000, ErrorMessage = "Amount must be between 0.01 and 1,000,000")]
    public decimal TransferAmount { get; set; }

    /// <summary>Unstructured remittance information — ISO 20022 RmtInf/Ustrd.</summary>
    [StringLength(140, ErrorMessage = "Reference must be 140 characters or fewer")]
    public string? RemittanceReference { get; set; }
}
