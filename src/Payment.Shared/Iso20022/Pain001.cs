using System.Xml.Linq;

namespace eShop.Payment.Shared.Iso20022;

/// <summary>
/// ISO 20022 pain.001.001.09 - CustomerCreditTransferInitiation.
/// Initiates a credit transfer from the debtor (buyer) to the creditor (merchant).
/// </summary>
public sealed record Pain001CustomerCreditTransferInitiation
{
    public const string Namespace = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.09";

    public required Pain001GroupHeader GroupHeader { get; init; }

    public required IReadOnlyList<Pain001PaymentInformation> PaymentInformation { get; init; }

    public XDocument ToXml()
    {
        XNamespace ns = Namespace;
        return new XDocument(
            new XElement(ns + "Document",
                new XElement(ns + "CstmrCdtTrfInitn",
                    GroupHeader.ToXml(ns),
                    PaymentInformation.Select(p => p.ToXml(ns)))));
    }

    public sealed record Pain001GroupHeader
    {
        public required string MessageId { get; init; }
        public required DateTimeOffset CreationDateTime { get; init; }
        public required int NumberOfTransactions { get; init; }
        public required decimal ControlSum { get; init; }
        public required string InitiatingPartyName { get; init; }

        public XElement ToXml(XNamespace ns) =>
            new(ns + "GrpHdr",
                new XElement(ns + "MsgId", MessageId),
                new XElement(ns + "CreDtTm", CreationDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz")),
                new XElement(ns + "NbOfTxs", NumberOfTransactions.ToString()),
                new XElement(ns + "CtrlSum", ControlSum.ToString("0.00")),
                new XElement(ns + "InitgPty",
                    new XElement(ns + "Nm", InitiatingPartyName)));
    }

    public sealed record Pain001PaymentInformation
    {
        public required string PaymentInformationId { get; init; }
        public required string PaymentMethod { get; init; } // "TRA" = credit transfer
        public required DateOnly RequestedExecutionDate { get; init; }
        public required string DebtorName { get; init; }
        public string? DebtorAccountId { get; init; }
        public string? DebtorAgentBic { get; init; }
        public required string ChargeBearer { get; init; }
        public required Pain001CreditTransferTransactionInformation CreditTransferTransactionInformation { get; init; }

        public XElement ToXml(XNamespace ns)
        {
            var element = new XElement(ns + "PmtInf",
                new XElement(ns + "PmtInfId", PaymentInformationId),
                new XElement(ns + "PmtMtd", PaymentMethod),
                new XElement(ns + "ReqdExctnDt", RequestedExecutionDate.ToString("yyyy-MM-dd")),
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

            element.Add(
                new XElement(ns + "ChrgBr", ChargeBearer),
                CreditTransferTransactionInformation.ToXml(ns));

            return element;
        }
    }

    public sealed record Pain001CreditTransferTransactionInformation
    {
        public required string PaymentIdEndToEndId { get; init; }
        public required decimal Amount { get; init; }
        public required string Currency { get; init; }
        public string? CreditorAgentBic { get; init; }
        public required string CreditorName { get; init; }
        public string? CreditorAccountId { get; init; }
        public string? RemittanceInformation { get; init; }

        public XElement ToXml(XNamespace ns)
        {
            var element = new XElement(ns + "CdtTrfTxInf",
                new XElement(ns + "PmtId", new XElement(ns + "EndToEndId", PaymentIdEndToEndId)),
                new XElement(ns + "Amt",
                    new XElement(ns + "InstdAmt", new XAttribute("Ccy", Currency), Amount.ToString("0.00"))));

            if (CreditorAgentBic is not null)
            {
                element.Add(new XElement(ns + "CdtrAgt",
                    new XElement(ns + "FinInstnId", new XElement(ns + "BICFI", CreditorAgentBic))));
            }

            element.Add(new XElement(ns + "Cdtr", new XElement(ns + "Nm", CreditorName)));

            if (CreditorAccountId is not null)
            {
                element.Add(new XElement(ns + "CdtrAcct",
                    new XElement(ns + "Id", new XElement(ns + "Othr", new XElement(ns + "Id", CreditorAccountId)))));
            }

            if (RemittanceInformation is not null)
            {
                element.Add(new XElement(ns + "RmtInf", new XElement(ns + "Ustrd", RemittanceInformation)));
            }

            return element;
        }
    }
}
