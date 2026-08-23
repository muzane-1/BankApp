namespace eShop.Payment.Shared.Iso20022;

/// <summary>
/// Builds ISO 20022 financial messages (pain.001 / pacs.008) from canonical
/// payment instructions. Message identifiers are constrained to the ISO 20022
/// Max35Text type and timestamps are emitted in UTC.
/// </summary>
public static class Iso20022PaymentMessageFactory
{
    /// <summary>
    /// Creates a pain.001.001.09 CustomerCreditTransferInitiation message,
    /// used when a buyer initiates payment to the merchant.
    /// </summary>
    public static Pain001CustomerCreditTransferInitiation CreatePain001(
        PaymentInstruction instruction,
        string initiatingPartyName,
        DateTimeOffset? creationTime = null)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(initiatingPartyName);

        var now = creationTime ?? DateTimeOffset.UtcNow;
        var amount = instruction.Amount ?? 0m;

        return new Pain001CustomerCreditTransferInitiation
        {
            GroupHeader = new Pain001CustomerCreditTransferInitiation.Pain001GroupHeader
            {
                MessageId = NewMessageId("PAIN"),
                CreationDateTime = now,
                NumberOfTransactions = 1,
                ControlSum = amount,
                InitiatingPartyName = initiatingPartyName
            },
            PaymentInformation =
            [
                new Pain001CustomerCreditTransferInitiation.Pain001PaymentInformation
                {
                    PaymentInformationId = NewMessageId("PMTINF"),
                    PaymentMethod = "TRA",
                    RequestedExecutionDate = DateOnly.FromDateTime(now.UtcDateTime),
                    DebtorName = instruction.DebtorName,
                    DebtorAccountId = instruction.DebtorAccountId,
                    DebtorAgentBic = instruction.DebtorAgentBic,
                    ChargeBearer = instruction.ChargeBearer,
                    CreditTransferTransactionInformation = new Pain001CustomerCreditTransferInitiation.Pain001CreditTransferTransactionInformation
                    {
                        PaymentIdEndToEndId = Truncate(instruction.EndToEndId),
                        Amount = amount,
                        Currency = instruction.Currency,
                        CreditorAgentBic = instruction.CreditorAgentBic,
                        CreditorName = instruction.CreditorName,
                        CreditorAccountId = instruction.CreditorAccountId,
                        RemittanceInformation = instruction.RemittanceInformation
                    }
                }
            ]
        };
    }

    /// <summary>
    /// Creates a pacs.008.001.08 FIToFICustomerCreditTransfer message,
    /// used when the payment gateway settles the transaction between institutions.
    /// </summary>
    public static Pacs008FinancialInstitutionCreditTransfer CreatePacs008(
        PaymentInstruction instruction,
        DateTimeOffset? creationTime = null)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        var now = creationTime ?? DateTimeOffset.UtcNow;

        return new Pacs008FinancialInstitutionCreditTransfer
        {
            GroupHeader = new Pacs008FinancialInstitutionCreditTransfer.Pacs008GroupHeader
            {
                MessageId = NewMessageId("PACS"),
                CreationDateTime = now,
                NumberOfTransactions = 1,
                SettlementMethod = "CLRG"
            },
            CreditTransferTransactionInformation = new Pacs008FinancialInstitutionCreditTransfer.Pacs008CreditTransferTransactionInformation
            {
                PaymentIdentificationInstructionId = NewMessageId("INSTR"),
                PaymentIdentificationEndToEndId = Truncate(instruction.EndToEndId),
                InterbankSettlementAmount = instruction.Amount,
                Currency = instruction.Currency,
                ChargeBearer = instruction.ChargeBearer,
                DebtorName = instruction.DebtorName,
                DebtorAccountId = instruction.DebtorAccountId,
                DebtorAgentBic = instruction.DebtorAgentBic,
                CreditorAgentBic = instruction.CreditorAgentBic,
                CreditorAccountId = instruction.CreditorAccountId,
                CreditorName = instruction.CreditorName,
                RemittanceInformation = instruction.RemittanceInformation
            }
        };
    }

    /// <summary>Generates an ISO 20022 Max35Text compliant message identifier.</summary>
    private static string NewMessageId(string prefix) =>
        Truncate($"{prefix}-{Guid.NewGuid():N}");

    private static string Truncate(string value) =>
        value.Length <= 35 ? value : value[..35];
}
