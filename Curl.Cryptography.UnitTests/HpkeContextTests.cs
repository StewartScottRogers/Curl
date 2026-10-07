using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="HpkeContext" />'s key schedule, <c>Seal</c>, <c>Open</c> and
/// <c>Export</c> to RFC 9180's base-mode vectors (<see cref="HpkeBaseModeVectors" />), and
/// checks a failed open is ADR-0118's <c>false</c>, lengths are checked and
/// <c>Dispose</c> zeroes every secret.
/// </summary>
[TestClass]
public sealed class HpkeContextTests
{
    /// <summary>The base-mode vectors, one data row each.</summary>
    public static IEnumerable<object[]> BaseModeVectors => HpkeBaseModeVectors.All;

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void Constructor_BaseModeVector_DerivesThePublishedKeyBaseNonceAndExporterSecret(HpkeBaseModeVector vector)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", vector.Source);
        diagnostics.Arrange("suite", $"{vector.Kem}, HkdfSha256, {vector.Aead}");
        diagnostics.Bytes("shared_secret", Convert.FromHexString(vector.SharedSecret));
        diagnostics.Bytes("info", Convert.FromHexString(HpkeBaseModeVectors.Info));

        using HpkeContext context = new(
            vector.Kem,
            HpkeKdf.HkdfSha256,
            vector.Aead,
            Convert.FromHexString(vector.SharedSecret),
            Convert.FromHexString(HpkeBaseModeVectors.Info));
        diagnostics.Act("sequence number", context.SequenceNumber);

        diagnostics.Diff("key", Convert.FromHexString(vector.Key), context.Key);
        diagnostics.Diff("base_nonce", Convert.FromHexString(vector.BaseNonce), context.BaseNonce);
        diagnostics.Diff("exporter_secret", Convert.FromHexString(vector.ExporterSecret), context.ExporterSecret);
        diagnostics.Assert("sequence number", 0UL, context.SequenceNumber);
        Assert.AreEqual(vector.Key, Convert.ToHexStringLower(context.Key));
        Assert.AreEqual(vector.BaseNonce, Convert.ToHexStringLower(context.BaseNonce));
        Assert.AreEqual(vector.ExporterSecret, Convert.ToHexStringLower(context.ExporterSecret));
        Assert.AreEqual(0UL, context.SequenceNumber);
    }

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void Seal_BaseModeVector_GivesThePublishedCiphertextsOfSequenceNumbers0And1(HpkeBaseModeVector vector)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(vector);
        byte[] plaintext = Convert.FromHexString(HpkeBaseModeVectors.Plaintext);
        byte[] ciphertext0 = new byte[plaintext.Length + HpkeContext.TagSize];
        byte[] ciphertext1 = new byte[ciphertext0.Length];
        diagnostics.Arrange("vector source", vector.Source);
        diagnostics.Bytes("plaintext", plaintext);
        diagnostics.Bytes("aad 0", Convert.FromHexString(HpkeBaseModeVectors.AssociatedData0));
        diagnostics.Bytes("aad 1", Convert.FromHexString(HpkeBaseModeVectors.AssociatedData1));

        sender.Seal(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData0), plaintext, ciphertext0);
        sender.Seal(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData1), plaintext, ciphertext1);
        diagnostics.Act("sequence number", sender.SequenceNumber);

        diagnostics.Diff("ciphertext 0", Convert.FromHexString(vector.Ciphertext0), ciphertext0);
        diagnostics.Diff("ciphertext 1", Convert.FromHexString(vector.Ciphertext1), ciphertext1);
        diagnostics.Assert("sequence number", 2UL, sender.SequenceNumber);
        Assert.AreEqual(vector.Ciphertext0, Convert.ToHexStringLower(ciphertext0));
        Assert.AreEqual(vector.Ciphertext1, Convert.ToHexStringLower(ciphertext1));
        Assert.AreEqual(2UL, sender.SequenceNumber);
    }

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void TryOpen_BaseModeVector_OpensThePublishedCiphertextsInOrder(HpkeBaseModeVector vector)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using HpkeContext recipient = HpkeBaseModeVectors.SetUpRecipient(vector);
        byte[] plaintext0 = new byte[HpkeBaseModeVectors.Plaintext.Length / 2];
        byte[] plaintext1 = new byte[plaintext0.Length];
        diagnostics.Arrange("vector source", vector.Source);
        diagnostics.Bytes("ciphertext 0", Convert.FromHexString(vector.Ciphertext0));
        diagnostics.Bytes("ciphertext 1", Convert.FromHexString(vector.Ciphertext1));

        bool opened0 = recipient.TryOpen(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData0), Convert.FromHexString(vector.Ciphertext0), plaintext0);
        bool opened1 = recipient.TryOpen(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData1), Convert.FromHexString(vector.Ciphertext1), plaintext1);
        diagnostics.Act("opened 0", opened0);
        diagnostics.Act("opened 1", opened1);

        diagnostics.Assert("opened 0", true, opened0);
        diagnostics.Assert("opened 1", true, opened1);
        diagnostics.Diff("plaintext 0", Convert.FromHexString(HpkeBaseModeVectors.Plaintext), plaintext0);
        diagnostics.Diff("plaintext 1", Convert.FromHexString(HpkeBaseModeVectors.Plaintext), plaintext1);
        diagnostics.Assert("sequence number", 2UL, recipient.SequenceNumber);
        Assert.IsTrue(opened0);
        Assert.IsTrue(opened1);
        Assert.AreEqual(HpkeBaseModeVectors.Plaintext, Convert.ToHexStringLower(plaintext0));
        Assert.AreEqual(HpkeBaseModeVectors.Plaintext, Convert.ToHexStringLower(plaintext1));
        Assert.AreEqual(2UL, recipient.SequenceNumber);
    }

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void Export_BaseModeVectorAndEmptyExporterContext_GivesThePublishedValue(HpkeBaseModeVector vector)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(vector);
        byte[] exportedValue = new byte[32];
        diagnostics.Arrange("vector source", vector.Source);
        diagnostics.Arrange("exporter context", "empty");

        sender.Export([], exportedValue);
        diagnostics.Act("exported value", Convert.ToHexStringLower(exportedValue));

        diagnostics.Diff("exported value", Convert.FromHexString(vector.ExportedValue), exportedValue);
        Assert.AreEqual(vector.ExportedValue, Convert.ToHexStringLower(exportedValue));
    }

    // RFC 9180 Appendix A.1.1.2, the two exports with a non-empty exporter context.
    [TestMethod]
    [DataRow("00", "2e8f0b54673c7029649d4eb9d5e33bf1872cf76d623ff164ac185da9e88c21a5")]
    [DataRow("54657374436f6e74657874", "e9e43065102c3836401bed8c3c3c75ae46be1639869391d62c61f1ec7af54931")]
    public void Export_Rfc9180A112ExporterContext_GivesThePublishedValue(string exporterContext, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(HpkeBaseModeVectors.X25519Aes128Gcm);
        byte[] exportedValue = new byte[32];
        diagnostics.Arrange("vector source", "RFC 9180 appendix A.1.1.2");
        diagnostics.Bytes("exporter context", Convert.FromHexString(exporterContext));

        sender.Export(Convert.FromHexString(exporterContext), exportedValue);
        diagnostics.Act("exported value", Convert.ToHexStringLower(exportedValue));

        diagnostics.Diff("exported value", Convert.FromHexString(expected), exportedValue);
        Assert.AreEqual(expected, Convert.ToHexStringLower(exportedValue));
    }

    // RFC 9180 Appendix A.1.1.1, sequence numbers 255 and 256: the nonce's XOR reaches its second-lowest byte.
    [TestMethod]
    public void Seal_SequenceNumbers255And256_GiveThePublishedCiphertexts()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(HpkeBaseModeVectors.X25519Aes128Gcm);
        byte[] plaintext = Convert.FromHexString(HpkeBaseModeVectors.Plaintext);
        byte[] ciphertext255 = new byte[plaintext.Length + HpkeContext.TagSize];
        byte[] ciphertext256 = new byte[ciphertext255.Length];
        diagnostics.Arrange("vector source", "RFC 9180 appendix A.1.1.1, sequence numbers 255 and 256");
        diagnostics.Bytes("plaintext", plaintext);
        using (diagnostics.Phase("seal sequence numbers 0 to 254"))
        {
            for (int sequenceNumber = 0; sequenceNumber < 255; sequenceNumber++)
            {
                sender.Seal([], plaintext, ciphertext255);
            }
        }

        sender.Seal(Convert.FromHexString("436f756e742d323535"), plaintext, ciphertext255);
        sender.Seal(Convert.FromHexString("436f756e742d323536"), plaintext, ciphertext256);
        diagnostics.Act("sequence number", sender.SequenceNumber);

        diagnostics.Diff("ciphertext 255", Convert.FromHexString("7175db9717964058640a3a11fb9007941a5d1757fda1a6935c805c21af32505bf106deefec4a49ac38d71c9e0a"), ciphertext255);
        diagnostics.Diff("ciphertext 256", Convert.FromHexString("957f9800542b0b8891badb026d79cc54597cb2d225b54c00c5238c25d05c30e3fbeda97d2e0e1aba483a2df9f2"), ciphertext256);
        Assert.AreEqual(
            "7175db9717964058640a3a11fb9007941a5d1757fda1a6935c805c21af32505bf106deefec4a49ac38d71c9e0a",
            Convert.ToHexStringLower(ciphertext255));
        Assert.AreEqual(
            "957f9800542b0b8891badb026d79cc54597cb2d225b54c00c5238c25d05c30e3fbeda97d2e0e1aba483a2df9f2",
            Convert.ToHexStringLower(ciphertext256));
    }

    [TestMethod]
    [DataRow(HpkeAead.Aes128Gcm)]
    [DataRow(HpkeAead.ChaCha20Poly1305)]
    public void TryOpen_FlippedCiphertextBit_ReturnsFalseWithThePlaintextZeroedAndTheSequenceNumberKept(HpkeAead aead)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HpkeBaseModeVector vector = aead == HpkeAead.Aes128Gcm
            ? HpkeBaseModeVectors.X25519Aes128Gcm
            : HpkeBaseModeVectors.X25519ChaCha20Poly1305;
        using HpkeContext recipient = HpkeBaseModeVectors.SetUpRecipient(vector);
        byte[] ciphertext = Convert.FromHexString(vector.Ciphertext0);
        ciphertext[0] ^= 0x01;
        byte[] plaintext = Enumerable.Repeat((byte)0xAA, ciphertext.Length - HpkeContext.TagSize).ToArray();
        diagnostics.Arrange("vector source", $"{vector.Source}, ciphertext 0 with its first bit flipped");
        diagnostics.Bytes("ciphertext", ciphertext);

        bool opened = recipient.TryOpen(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData0), ciphertext, plaintext);
        diagnostics.Act("opened", opened);

        diagnostics.Assert("opened", false, opened);
        diagnostics.Diff("plaintext", new byte[plaintext.Length], plaintext);
        diagnostics.Assert("sequence number", 0UL, recipient.SequenceNumber);
        Assert.IsFalse(opened);
        Assert.IsTrue(plaintext.All(value => value == 0));
        Assert.AreEqual(0UL, recipient.SequenceNumber);
    }

    [TestMethod]
    public void TryOpen_CiphertextShorterThanATag_ReturnsFalseWithThePlaintextZeroed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using HpkeContext recipient = HpkeBaseModeVectors.SetUpRecipient(HpkeBaseModeVectors.X25519Aes128Gcm);
        byte[] plaintext = [0xAA];
        diagnostics.Arrange("ciphertext length", HpkeContext.TagSize - 1);

        bool opened = recipient.TryOpen([], new byte[HpkeContext.TagSize - 1], plaintext);
        diagnostics.Act("opened", opened);

        diagnostics.Assert("opened", false, opened);
        diagnostics.Assert("plaintext[0]", (byte)0, plaintext[0]);
        Assert.IsFalse(opened);
        Assert.AreEqual(0, plaintext[0]);
    }

    [TestMethod]
    public void TryOpen_PlaintextOfTheWrongLength_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using HpkeContext recipient = HpkeBaseModeVectors.SetUpRecipient(HpkeBaseModeVectors.X25519Aes128Gcm);
        diagnostics.Arrange("ciphertext length", HpkeContext.TagSize + 2);
        diagnostics.Arrange("plaintext length", 1);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => recipient.TryOpen([], new byte[HpkeContext.TagSize + 2], new byte[1]));
        diagnostics.Act("parameter", exception.ParamName);

        diagnostics.Assert("parameter", "plaintext", exception.ParamName);
        Assert.AreEqual("plaintext", exception.ParamName);
    }

    [TestMethod]
    public void Seal_CiphertextOfTheWrongLength_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(HpkeBaseModeVectors.X25519Aes128Gcm);
        diagnostics.Arrange("plaintext length", 4);
        diagnostics.Arrange("ciphertext length", 4);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => sender.Seal([], new byte[4], new byte[4]));
        diagnostics.Act("parameter", exception.ParamName);

        diagnostics.Assert("parameter", "ciphertext", exception.ParamName);
        Assert.AreEqual("ciphertext", exception.ParamName);
    }

    [TestMethod]
    public void Dispose_ThenAnyCall_ThrowsObjectDisposedExceptionWithEverySecretZeroed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HpkeContext context = HpkeBaseModeVectors.SetUpSender(HpkeBaseModeVectors.X25519Aes128Gcm);
        diagnostics.Arrange("vector source", HpkeBaseModeVectors.X25519Aes128Gcm.Source);

        context.Dispose();
        diagnostics.Act("state", "disposed");

        Assert.ThrowsExactly<ObjectDisposedException>(() => context.Seal([], [], new byte[HpkeContext.TagSize]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => context.TryOpen([], new byte[HpkeContext.TagSize], []));
        Assert.ThrowsExactly<ObjectDisposedException>(() => context.Export([], new byte[32]));
        diagnostics.Assert("Seal, TryOpen and Export after Dispose", nameof(ObjectDisposedException), nameof(ObjectDisposedException));
        diagnostics.Diff("key", new byte[context.Key.Length], context.Key);
        diagnostics.Diff("base_nonce", new byte[context.BaseNonce.Length], context.BaseNonce);
        diagnostics.Diff("exporter_secret", new byte[context.ExporterSecret.Length], context.ExporterSecret);
        Assert.IsTrue(context.Key.ToArray().All(value => value == 0));
        Assert.IsTrue(context.BaseNonce.ToArray().All(value => value == 0));
        Assert.IsTrue(context.ExporterSecret.ToArray().All(value => value == 0));
    }
}
