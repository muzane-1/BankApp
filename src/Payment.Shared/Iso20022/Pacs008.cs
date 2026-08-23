using System.Xml.Linq;

namespace eShop.Payment.Shared.Iso20022;

/// <summary>
/// ISO 20022 pacs.008.001.08 - FIToFICustomerCreditTransfer.
/// Moves funds between financial institutions to settle a customer credit transfer.
/// </summary>
public sealed record Pacs008FinancialInstitutionCreditTransfer
{
    public const string Namespace = "urn:iso:std:iso:20022:tech:xsd:pacs.008.001.08";

    public required Pacs008GroupHeader GroupHeader { get; init; }

    public required Pacs008CreditTransferTransactionInformation CreditTransferTransactionInformation { get; init; }

    public XDocument ToXml()
    {
        XNamespace ns = Namespace;
        return new XDocument(
            new XElement(ns + "Document",
                new XElement(ns + "FIToFICstmrCdtTrf",
                    GroupHeader.ToXml(ns),
                    CreditTransferTransactionInformation.ToXml(ns))));
    }

    public sealed record Pacs008GroupHeader
    {
        public required string MessageId { get; init; }
        public required DateTimeOffset CreationDateTime { get; init; }
        public required int NumberOfTransactions { get; init; }
        public required string SettlementMethod { get; init; } // "CLRG" = clearing system

        public XElement ToXml(XNamespace ns) =>
            new(ns + "GrpHdr",
                new XElement(ns + "MsgId", MessageId),
                new XElement(ns + "CreDtTm", CreationDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz")),
                new XElement(ns + "NbOfTxs", NumberOfTransactions.ToString()),
                new XElement(ns + "SttlmInf", new XElement(ns + "SttlmMtd", SettlementMethod)));
    }

    public sealed record Pacs008CreditTransferTransactionInformation
    {
        public required string PaymentIdentificationInstructionId { get; init; }
        public required string PaymentIdentificationEndToEndId { get; init; }
        public decimal? InterbankSettlementAmount { get; init; }
        public required string Currency { get; init; }
        public required string ChargeBearer { get; init; }
        public required string DebtorName { get; init; }
        public string? DebtorAccountId { get; init; }
        public string? DebtorAgentBic { get; init; }
        public string? CreditorAgentBic { get; init; }
        public string? CreditorAccountId { get; init; }
        public required string CreditorName { get; init; }
        public string? RemittanceInformation { get; init; }

        public XElement ToXml(XNamespace ns)
        {
            var element = new XElement(ns + "CdtTrfTxInf",
                new XElement(ns + "PmtId",
                    new XElement(ns + "InstrId", PaymentIdentificationInstructionId),
                    new XElement(ns + "EndToEndId", PaymentIdentificationEndToEndId)));

            if (InterbankSettlementAmount.HasValue)
            {
                element.Add(new XElement(ns + "IntrBkSttlmAmt",
                    new XAttribute("Ccy", Currency), InterbankSettlementAmount.Value.ToString("0.00")));
            }

            element.Add(
                new XElement(ns + "ChrgBr", ChargeBearer),
                new XElement(ns + "Dbtr", new XElement(ns + "Nm", DebtorName)));

            if (DebtorAccountId is not null)
            {
                element.Add(new XElement(ns + "DbtrAcct",
                    new XElement(ns + "Id", new XElement(ns + "Othr", new XElement(ns + "Id", DebtorAccountId)))));
            }

            if (DebtorAgentBic is not null)
            {
                element.Add(new XElement(ns + "DbtrAgt",
                    new XElement(ns + "FinInstnId", new XElement(ns + "BICFI", DebtorAgentBic))));
            }

            if (CreditorAgentBic is not null)
            {
                element.Add(new XElement(ns + "CdtrAgt",
                    new XElement(ns + "FinInstnId", new XElement(ns + "BICFI", CreditorAgentBic))));
            }

            if (CreditorAccountId is not null)
            {
                element.Add(new XElement(ns + "CdtrAcct",
                    new XElement(ns + "Id", new XElement(ns + "Othr", new XElement(ns + "Id", CreditorAccountId)))));
            }

            element.Add(new XElement(ns + "Cdtr", new XElement(ns + "Nm", CreditorName)));

            if (RemittanceInformation is not null)
            {
                element.Add(new XElement(ns + "RmtInf", new XElement(ns + "Ustrd", RemittanceInformation)));
            }

            return element;
        }
    }
}
