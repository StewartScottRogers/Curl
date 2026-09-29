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

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void Constructor_BaseModeVector_DerivesThePublishedKeyBaseNonceAndExporterSecret(HpkeBaseModeVector vector)
    {
        using HpkeContext context = new(
            vector.Kem,
            HpkeKdf.HkdfSha256,
            vector.Aead,
            Convert.FromHexString(vector.SharedSecret),
            Convert.FromHexString(HpkeBaseModeVectors.Info));

        Assert.AreEqual(vector.Key, Convert.ToHexStringLower(context.Key));
        Assert.AreEqual(vector.BaseNonce, Convert.ToHexStringLower(context.BaseNonce));
        Assert.AreEqual(vector.ExporterSecret, Convert.ToHexStringLower(context.ExporterSecret));
        Assert.AreEqual(0UL, context.SequenceNumber);
    }

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void Seal_BaseModeVector_GivesThePublishedCiphertextsOfSequenceNumbers0And1(HpkeBaseModeVector vector)
    {
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(vector);
        byte[] plaintext = Convert.FromHexString(HpkeBaseModeVectors.Plaintext);
        byte[] ciphertext0 = new byte[plaintext.Length + HpkeContext.TagSize];
        byte[] ciphertext1 = new byte[ciphertext0.Length];

        sender.Seal(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData0), plaintext, ciphertext0);
        sender.Seal(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData1), plaintext, ciphertext1);

        Assert.AreEqual(vector.Ciphertext0, Convert.ToHexStringLower(ciphertext0));
        Assert.AreEqual(vector.Ciphertext1, Convert.ToHexStringLower(ciphertext1));
        Assert.AreEqual(2UL, sender.SequenceNumber);
    }

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void TryOpen_BaseModeVector_OpensThePublishedCiphertextsInOrder(HpkeBaseModeVector vector)
    {
        using HpkeContext recipient = HpkeBaseModeVectors.SetUpRecipient(vector);
        byte[] plaintext0 = new byte[HpkeBaseModeVectors.Plaintext.Length / 2];
        byte[] plaintext1 = new byte[plaintext0.Length];

        bool opened0 = recipient.TryOpen(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData0), Convert.FromHexString(vector.Ciphertext0), plaintext0);
        bool opened1 = recipient.TryOpen(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData1), Convert.FromHexString(vector.Ciphertext1), plaintext1);

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
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(vector);
        byte[] exportedValue = new byte[32];

        sender.Export([], exportedValue);

        Assert.AreEqual(vector.ExportedValue, Convert.ToHexStringLower(exportedValue));
    }

    // RFC 9180 Appendix A.1.1.2, the two exports with a non-empty exporter context.
    [TestMethod]
    [DataRow("00", "2e8f0b54673c7029649d4eb9d5e33bf1872cf76d623ff164ac185da9e88c21a5")]
    [DataRow("54657374436f6e74657874", "e9e43065102c3836401bed8c3c3c75ae46be1639869391d62c61f1ec7af54931")]
    public void Export_Rfc9180A112ExporterContext_GivesThePublishedValue(string exporterContext, string expected)
    {
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(HpkeBaseModeVectors.X25519Aes128Gcm);
        byte[] exportedValue = new byte[32];

        sender.Export(Convert.FromHexString(exporterContext), exportedValue);

        Assert.AreEqual(expected, Convert.ToHexStringLower(exportedValue));
    }

    // RFC 9180 Appendix A.1.1.1, sequence numbers 255 and 256: the nonce's XOR reaches its second-lowest byte.
    [TestMethod]
    public void Seal_SequenceNumbers255And256_GiveThePublishedCiphertexts()
    {
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(HpkeBaseModeVectors.X25519Aes128Gcm);
        byte[] plaintext = Convert.FromHexString(HpkeBaseModeVectors.Plaintext);
        byte[] ciphertext255 = new byte[plaintext.Length + HpkeContext.TagSize];
        byte[] ciphertext256 = new byte[ciphertext255.Length];
        for (int sequenceNumber = 0; sequenceNumber < 255; sequenceNumber++)
        {
            sender.Seal([], plaintext, ciphertext255);
        }

        sender.Seal(Convert.FromHexString("436f756e742d323535"), plaintext, ciphertext255);
        sender.Seal(Convert.FromHexString("436f756e742d323536"), plaintext, ciphertext256);

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
        HpkeBaseModeVector vector = aead == HpkeAead.Aes128Gcm
            ? HpkeBaseModeVectors.X25519Aes128Gcm
            : HpkeBaseModeVectors.X25519ChaCha20Poly1305;
        using HpkeContext recipient = HpkeBaseModeVectors.SetUpRecipient(vector);
        byte[] ciphertext = Convert.FromHexString(vector.Ciphertext0);
        ciphertext[0] ^= 0x01;
        byte[] plaintext = Enumerable.Repeat((byte)0xAA, ciphertext.Length - HpkeContext.TagSize).ToArray();

        bool opened = recipient.TryOpen(Convert.FromHexString(HpkeBaseModeVectors.AssociatedData0), ciphertext, plaintext);

        Assert.IsFalse(opened);
        Assert.IsTrue(plaintext.All(value => value == 0));
        Assert.AreEqual(0UL, recipient.SequenceNumber);
    }

    [TestMethod]
    public void TryOpen_CiphertextShorterThanATag_ReturnsFalseWithThePlaintextZeroed()
    {
        using HpkeContext recipient = HpkeBaseModeVectors.SetUpRecipient(HpkeBaseModeVectors.X25519Aes128Gcm);
        byte[] plaintext = [0xAA];

        bool opened = recipient.TryOpen([], new byte[HpkeContext.TagSize - 1], plaintext);

        Assert.IsFalse(opened);
        Assert.AreEqual(0, plaintext[0]);
    }

    [TestMethod]
    public void TryOpen_PlaintextOfTheWrongLength_ThrowsArgumentException()
    {
        using HpkeContext recipient = HpkeBaseModeVectors.SetUpRecipient(HpkeBaseModeVectors.X25519Aes128Gcm);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => recipient.TryOpen([], new byte[HpkeContext.TagSize + 2], new byte[1]));

        Assert.AreEqual("plaintext", exception.ParamName);
    }

    [TestMethod]
    public void Seal_CiphertextOfTheWrongLength_ThrowsArgumentException()
    {
        using HpkeContext sender = HpkeBaseModeVectors.SetUpSender(HpkeBaseModeVectors.X25519Aes128Gcm);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => sender.Seal([], new byte[4], new byte[4]));

        Assert.AreEqual("ciphertext", exception.ParamName);
    }

    [TestMethod]
    public void Dispose_ThenAnyCall_ThrowsObjectDisposedExceptionWithEverySecretZeroed()
    {
        HpkeContext context = HpkeBaseModeVectors.SetUpSender(HpkeBaseModeVectors.X25519Aes128Gcm);

        context.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => context.Seal([], [], new byte[HpkeContext.TagSize]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => context.TryOpen([], new byte[HpkeContext.TagSize], []));
        Assert.ThrowsExactly<ObjectDisposedException>(() => context.Export([], new byte[32]));
        Assert.IsTrue(context.Key.ToArray().All(value => value == 0));
        Assert.IsTrue(context.BaseNonce.ToArray().All(value => value == 0));
        Assert.IsTrue(context.ExporterSecret.ToArray().All(value => value == 0));
    }
}
