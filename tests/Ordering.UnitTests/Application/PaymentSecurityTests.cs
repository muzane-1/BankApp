using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace eShop.Ordering.UnitTests.Application;

[TestClass]
public class PaymentSecurityTests
{
    private static readonly string TestKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    // ---------- PCI-DSS: AES-256 field-level encryption ----------

    [TestMethod]
    public void Aes256_encrypt_decrypt_roundtrips_plaintext()
    {
        var protector = new Aes256SensitiveDataProtector(TestKey);
        const string pan = "4111111111111111";

        var ciphertext = protector.Encrypt(pan);

        Assert.AreNotEqual(pan, ciphertext);
        Assert.IsFalse(ciphertext.Contains(pan));
        Assert.AreEqual(pan, protector.Decrypt(ciphertext));
    }

    [TestMethod]
    public void Aes256_produces_unique_ciphertext_per_encryption()
    {
        var protector = new Aes256SensitiveDataProtector(TestKey);
        const string pan = "4111111111111111";

        var first = protector.Encrypt(pan);
        var second = protector.Encrypt(pan);

        // Random nonce per encryption: identical plaintext must not produce
        // identical ciphertext (prevents pattern analysis of stored PANs).
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void Aes256_rejects_tampered_ciphertext()
    {
        var protector = new Aes256SensitiveDataProtector(TestKey);
        var payload = Convert.FromBase64String(protector.Encrypt("4111111111111111"));
        payload[^1] ^= 0xFF; // flip a bit in the ciphertext

        Assert.ThrowsExactly<CryptographicException>(() => protector.Decrypt(Convert.ToBase64String(payload)));
    }

    [TestMethod]
    public void Aes256_rejects_decryption_with_wrong_key()
    {
        var protector = new Aes256SensitiveDataProtector(TestKey);
        var otherProtector = new Aes256SensitiveDataProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var ciphertext = protector.Encrypt("4111111111111111");

        Assert.ThrowsExactly<CryptographicException>(() => otherProtector.Decrypt(ciphertext));
    }

    [TestMethod]
    public void Aes256_rejects_keys_that_are_not_256_bits()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        Assert.ThrowsExactly<ArgumentException>(() => new Aes256SensitiveDataProtector(shortKey));
    }

    [TestMethod]
    public void Null_protector_passthrough_is_marked_as_not_encrypting()
    {
        var protector = new NullSensitiveDataProtector();

        Assert.IsFalse(protector.IsEncryptionEnabled);
        Assert.AreEqual("4111111111111111", protector.Encrypt("4111111111111111"));
    }

    // ---------- PCI-DSS 3.3: PAN masking ----------

    [TestMethod]
    public void PanMasking_keeps_only_last_four_digits()
    {
        Assert.AreEqual("XXXXXXXXXXXX1111", PanMasking.Mask("4111111111111111"));
        Assert.AreEqual("XXXXXXXXXXXX1111", PanMasking.Mask("4111-1111-1111-1111"));
        Assert.AreEqual("XXXX", PanMasking.Mask("1234"));
        Assert.AreEqual(string.Empty, PanMasking.Mask(null));
    }

    [TestMethod]
    public void PanMasking_fully_redacts_security_codes()
    {
        Assert.AreEqual("***", PanMasking.MaskSecurityCode("123"));
        Assert.AreEqual(string.Empty, PanMasking.MaskSecurityCode(null));
    }

    // ---------- ISO 20022 message modeling ----------

    [TestMethod]
    public void Pain001_contains_iso20022_group_header_and_transaction()
    {
        var instruction = FakeInstruction();

        var message = Iso20022PaymentMessageFactory.CreatePain001(instruction, "eShop Ordering");

        Assert.IsTrue(message.GroupHeader.MessageId.StartsWith("PAIN-"));
        Assert.IsTrue(message.GroupHeader.MessageId.Length <= 35, "MsgId must satisfy ISO 20022 Max35Text");
        Assert.AreEqual(1, message.GroupHeader.NumberOfTransactions);
        Assert.AreEqual(125.50m, message.GroupHeader.ControlSum);

        var pmtInf = message.PaymentInformation.Single();
        Assert.AreEqual("TRA", pmtInf.PaymentMethod);
        Assert.AreEqual("Jane Buyer", pmtInf.DebtorName);
        Assert.AreEqual("ORDER-42", pmtInf.CreditTransferTransactionInformation.PaymentIdEndToEndId);
        Assert.AreEqual(125.50m, pmtInf.CreditTransferTransactionInformation.Amount);
        Assert.AreEqual("USD", pmtInf.CreditTransferTransactionInformation.Currency);
        Assert.AreEqual("eShop Inc.", pmtInf.CreditTransferTransactionInformation.CreditorName);
    }

    [TestMethod]
    public void Pain001_serializes_to_iso20022_xml_document()
    {
        var message = Iso20022PaymentMessageFactory.CreatePain001(FakeInstruction(), "eShop Ordering");

        var document = message.ToXml();

        Assert.AreEqual(Pain001CustomerCreditTransferInitiation.Namespace, document.Root!.Name.NamespaceName);
        Assert.AreEqual("CstmrCdtTrfInitn", document.Root.Elements().Single().Name.LocalName);
        var xml = document.ToString();
        StringAssert.Contains(xml, "<MsgId>");
        StringAssert.Contains(xml, "<NbOfTxs>1</NbOfTxs>");
        StringAssert.Contains(xml, "<EndToEndId>ORDER-42</EndToEndId>");
        StringAssert.Contains(xml, "Ccy=\"USD\"");
        Assert.IsFalse(xml.Contains("4111111111111111"), "Cleartext PAN must never appear in an ISO 20022 message");
    }

    [TestMethod]
    public void Pacs008_contains_settlement_and_transaction_details()
    {
        var message = Iso20022PaymentMessageFactory.CreatePacs008(FakeInstruction());

        Assert.IsTrue(message.GroupHeader.MessageId.StartsWith("PACS-"));
        Assert.AreEqual("CLRG", message.GroupHeader.SettlementMethod);

        var tx = message.CreditTransferTransactionInformation;
        Assert.AreEqual("ORDER-42", tx.PaymentIdentificationEndToEndId);
        Assert.AreEqual(125.50m, tx.InterbankSettlementAmount);
        Assert.AreEqual("SLEV", tx.ChargeBearer);
        Assert.AreEqual("Jane Buyer", tx.DebtorName);
        Assert.AreEqual("eShop Inc.", tx.CreditorName);

        var xml = message.ToXml().ToString();
        StringAssert.Contains(xml, "FIToFICstmrCdtTrf");
        StringAssert.Contains(xml, "<IntrBkSttlmAmt Ccy=\"USD\">125.50</IntrBkSttlmAmt>");
    }

    [TestMethod]
    public void Iso20022_factory_truncates_ids_to_max35text()
    {
        var instruction = FakeInstruction() with { EndToEndId = new string('E', 100) };

        var message = Iso20022PaymentMessageFactory.CreatePacs008(instruction);

        Assert.AreEqual(35, message.CreditTransferTransactionInformation.PaymentIdentificationEndToEndId.Length);
    }

    // ---------- AML/PSD2: immutable hash-chained audit records ----------

    [TestMethod]
    public void Audit_hash_chain_links_records_and_detects_tampering()
    {
        var first = FakeAuditEntry("corr-1");
        var firstHash = FinancialAuditHash.Compute(FinancialAuditHash.GenesisHash, first);

        var second = FakeAuditEntry("corr-2");
        var secondHash = FinancialAuditHash.Compute(firstHash, second);

        Assert.AreNotEqual(firstHash, secondHash);

        // Tampering with the historical record breaks the chain verification.
        var tamperedFirst = first with { Amount = first.Amount + 1m };
        var tamperedFirstHash = FinancialAuditHash.Compute(FinancialAuditHash.GenesisHash, tamperedFirst);
        var recomputedSecondHash = FinancialAuditHash.Compute(tamperedFirstHash, second);

        Assert.AreNotEqual(secondHash, recomputedSecondHash, "Modified history must invalidate the hash chain");
    }

    [TestMethod]
    public void Audit_hash_is_deterministic()
    {
        var entry = FakeAuditEntry("corr-1");

        Assert.AreEqual(
            FinancialAuditHash.Compute(FinancialAuditHash.GenesisHash, entry),
            FinancialAuditHash.Compute(FinancialAuditHash.GenesisHash, entry));
    }

    // ---------- Strict idempotency tokens ----------

    [TestMethod]
    public async Task Idempotency_store_allows_single_acquire_per_key()
    {
        var store = new MemoryCacheIdempotencyTokenStore(new MemoryCache(new MemoryCacheOptions()));

        Assert.IsTrue(await store.TryAcquireAsync("tx-1", TimeSpan.FromMinutes(5)));
        Assert.IsFalse(await store.TryAcquireAsync("tx-1", TimeSpan.FromMinutes(5)), "A second acquire for the same token must be rejected");
        Assert.AreEqual(IdempotencyTokenStatus.InProgress, await store.GetStatusAsync("tx-1"));
        Assert.AreEqual(IdempotencyTokenStatus.NotFound, await store.GetStatusAsync("tx-other"));
    }

    [TestMethod]
    public async Task Idempotency_store_marks_completed_and_releases()
    {
        var store = new MemoryCacheIdempotencyTokenStore(new MemoryCache(new MemoryCacheOptions()));

        await store.TryAcquireAsync("tx-1", TimeSpan.FromMinutes(5));
        await store.MarkCompletedAsync("tx-1", TimeSpan.FromMinutes(5));
        Assert.AreEqual(IdempotencyTokenStatus.Completed, await store.GetStatusAsync("tx-1"));

        await store.ReleaseAsync("tx-1");
        Assert.AreEqual(IdempotencyTokenStatus.NotFound, await store.GetStatusAsync("tx-1"));
        Assert.IsTrue(await store.TryAcquireAsync("tx-1", TimeSpan.FromMinutes(5)), "A released token must be acquirable again");
    }

    // ---------- Helpers ----------

    private static PaymentInstruction FakeInstruction() => new()
    {
        EndToEndId = "ORDER-42",
        Amount = 125.50m,
        Currency = "USD",
        DebtorName = "Jane Buyer",
        DebtorAccountId = PanMasking.Mask("4111111111111111"),
        CreditorName = "eShop Inc.",
        CreditorAccountId = "US64ESHO00000012345",
        RemittanceInformation = "eShop order 42 payment"
    };

    private static FinancialAuditEntry FakeAuditEntry(string correlationId) => new()
    {
        EventType = FinancialAuditEventType.PaymentInitiated,
        OccurredAtUtc = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        CorrelationId = correlationId,
        ActorId = "buyer-1",
        Subject = "Order/42",
        Amount = 125.50m,
        Currency = "USD",
        Outcome = "Succeeded"
    };
}
