namespace eShop.Ordering.UnitTests.Application;

[TestClass]
public class SwiftFormattingTests
{
    // ---------- SWIFT/BIC (ISO 9362) validation ----------

    [TestMethod]
    [DataRow("COBADEFFXXX")]   // 11-char with branch
    [DataRow("DEUTDEFF")]      // 8-char primary office
    [DataRow("BNPAFRPP")]      // 8-char
    public void IsValidBic_accepts_valid_codes(string bic)
    {
        Assert.IsTrue(SwiftFormatting.IsValidBic(bic));
    }

    [TestMethod]
    [DataRow("cobadeffxxx")]   // lowercase not allowed in canonical form
    [DataRow("COBADEFFXXX1")]  // 12 chars
    [DataRow("COBADE")]        // too short
    [DataRow("C1BADEFF")]      // digit in institution code
    [DataRow("")]
    [DataRow(null)]
    public void IsValidBic_rejects_invalid_codes(string? bic)
    {
        Assert.IsFalse(SwiftFormatting.IsValidBic(bic));
    }

    // ---------- IBAN (ISO 13616) validation & formatting ----------

    [TestMethod]
    [DataRow("DE89370400440532013000")]
    [DataRow("GB29NWBK60161331926819")]
    [DataRow("FR1420041010050500013M02606")]
    public void IsValidIban_accepts_valid_ibans(string iban)
    {
        Assert.IsTrue(SwiftFormatting.IsValidIban(iban));
    }

    [TestMethod]
    public void IsValidIban_accepts_spaced_presentation_form()
    {
        Assert.IsTrue(SwiftFormatting.IsValidIban("DE89 3704 0044 0532 0130 00"));
    }

    [TestMethod]
    [DataRow("DE89370")]            // too short
    [DataRow("DEXX370400440532013000")] // check digits must be numeric
    [DataRow("89DE370400440532013000")] // wrong structure
    [DataRow("")]
    [DataRow(null)]
    public void IsValidIban_rejects_invalid_ibans(string? iban)
    {
        Assert.IsFalse(SwiftFormatting.IsValidIban(iban));
    }

    [TestMethod]
    public void FormatIbanForDisplay_groups_by_four()
    {
        Assert.AreEqual("DE89 3704 0044 0532 0130 00",
            SwiftFormatting.FormatIbanForDisplay("DE89370400440532013000"));
    }

    [TestMethod]
    public void MaskIban_preserves_country_check_and_last_four()
    {
        var masked = SwiftFormatting.MaskIban("DE89370400440532013000");
        Assert.AreEqual("DE89XXXXXXXXXXXXXX3000", masked);
        Assert.AreEqual(22, masked.Length);
        Assert.IsFalse(masked.Contains("3704"), "IBAN body must not leak into masked output");
    }

    // ---------- Factory gate: invalid BIC must be rejected ----------

    [TestMethod]
    public void CreatePacs008_rejects_invalid_creditor_bic()
    {
        var instruction = new PaymentInstruction
        {
            EndToEndId = "TRF-1",
            Currency = "USD",
            DebtorName = "Alice",
            CreditorName = "Bob",
            CreditorAgentBic = "NOT-A-BIC",
            Amount = 100m
        };

        Assert.ThrowsExactly<ArgumentException>(
            () => Iso20022PaymentMessageFactory.CreatePacs008(instruction));
    }

    [TestMethod]
    public void CreatePain001_rejects_invalid_debtor_bic()
    {
        var instruction = new PaymentInstruction
        {
            EndToEndId = "TRF-2",
            Currency = "EUR",
            DebtorName = "Alice",
            DebtorAgentBic = "X",
            CreditorName = "Bob",
            Amount = 50m
        };

        Assert.ThrowsExactly<ArgumentException>(
            () => Iso20022PaymentMessageFactory.CreatePain001(instruction, "Bank"));
    }

    [TestMethod]
    public void CreatePacs008_accepts_valid_swift_parties()
    {
        var instruction = new PaymentInstruction
        {
            EndToEndId = "TRF-3",
            Currency = "USD",
            DebtorName = "Alice",
            DebtorAgentBic = "DEUTDEFF",
            CreditorName = "Contoso Trading Ltd",
            CreditorAccountId = "DE89370400440532013000",
            CreditorAgentBic = "COBADEFFXXX",
            Amount = 2500m
        };

        var message = Iso20022PaymentMessageFactory.CreatePacs008(instruction);
        var xml = message.ToXml().ToString();

        Assert.Contains("COBADEFFXXX", xml);
        Assert.Contains("DEUTDEFF", xml);
        Assert.Contains("DE89370400440532013000", xml);
        Assert.Contains("2500.00", xml);
    }
}
