using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Hpke" />'s base-mode setups to RFC 9180's vectors
/// (<see cref="HpkeBaseModeVectors" />), round-trips a random ephemeral key through the
/// recipient, and checks unusable peer keys are ADR-0118's <c>false</c> while caller
/// mistakes throw <see cref="ArgumentException" />.
/// </summary>
[TestClass]
public sealed class HpkeTests
{
    /// <summary>The base-mode vectors, one data row each.</summary>
    public static IEnumerable<object[]> BaseModeVectors => HpkeBaseModeVectors.All;

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void TrySetupBaseSender_BaseModeVector_GivesThePublishedEncapsulatedKeyAndKeySchedule(HpkeBaseModeVector vector)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] encapsulatedKey = new byte[Hpke.GetEncapsulatedKeySize(vector.Kem)];
        diagnostics.Arrange("vector source", vector.Source);
        diagnostics.Arrange("suite", $"{vector.Kem}, HkdfSha256, {vector.Aead}");
        diagnostics.Bytes("pkRm", Convert.FromHexString(vector.RecipientPublicKey));
        diagnostics.Bytes("skEm", Convert.FromHexString(vector.EphemeralPrivateKey));
        diagnostics.Bytes("info", Convert.FromHexString(HpkeBaseModeVectors.Info));

        bool setUp = Hpke.TrySetupBaseSender(
            vector.Kem,
            HpkeKdf.HkdfSha256,
            vector.Aead,
            Convert.FromHexString(vector.RecipientPublicKey),
            Convert.FromHexString(vector.EphemeralPrivateKey),
            Convert.FromHexString(HpkeBaseModeVectors.Info),
            encapsulatedKey,
            out HpkeContext? sender);
        diagnostics.Act("set up", setUp);
        diagnostics.Bytes("enc", encapsulatedKey);

        diagnostics.Assert("set up", true, setUp);
        Assert.IsTrue(setUp);
        Assert.IsNotNull(sender);
        using (sender)
        {
            diagnostics.Diff("key", Convert.FromHexString(vector.Key), sender.Key);
            diagnostics.Diff("base_nonce", Convert.FromHexString(vector.BaseNonce), sender.BaseNonce);
            diagnostics.Diff("exporter_secret", Convert.FromHexString(vector.ExporterSecret), sender.ExporterSecret);
            Assert.AreEqual(vector.Key, Convert.ToHexStringLower(sender.Key));
            Assert.AreEqual(vector.BaseNonce, Convert.ToHexStringLower(sender.BaseNonce));
            Assert.AreEqual(vector.ExporterSecret, Convert.ToHexStringLower(sender.ExporterSecret));
        }

        diagnostics.Diff("enc", Convert.FromHexString(vector.EncapsulatedKey), encapsulatedKey);
        Assert.AreEqual(vector.EncapsulatedKey, Convert.ToHexStringLower(encapsulatedKey));
    }

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void TrySetupBaseRecipient_BaseModeVector_GivesThePublishedKeySchedule(HpkeBaseModeVector vector)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", vector.Source);
        diagnostics.Arrange("suite", $"{vector.Kem}, HkdfSha256, {vector.Aead}");
        diagnostics.Bytes("enc", Convert.FromHexString(vector.EncapsulatedKey));
        diagnostics.Bytes("skRm", Convert.FromHexString(vector.RecipientPrivateKey));
        diagnostics.Bytes("info", Convert.FromHexString(HpkeBaseModeVectors.Info));

        bool setUp = Hpke.TrySetupBaseRecipient(
            vector.Kem,
            HpkeKdf.HkdfSha256,
            vector.Aead,
            Convert.FromHexString(vector.EncapsulatedKey),
            Convert.FromHexString(vector.RecipientPrivateKey),
            Convert.FromHexString(HpkeBaseModeVectors.Info),
            out HpkeContext? recipient);
        diagnostics.Act("set up", setUp);

        diagnostics.Assert("set up", true, setUp);
        Assert.IsTrue(setUp);
        Assert.IsNotNull(recipient);
        using (recipient)
        {
            diagnostics.Diff("key", Convert.FromHexString(vector.Key), recipient.Key);
            diagnostics.Diff("base_nonce", Convert.FromHexString(vector.BaseNonce), recipient.BaseNonce);
            diagnostics.Diff("exporter_secret", Convert.FromHexString(vector.ExporterSecret), recipient.ExporterSecret);
            Assert.AreEqual(vector.Key, Convert.ToHexStringLower(recipient.Key));
            Assert.AreEqual(vector.BaseNonce, Convert.ToHexStringLower(recipient.BaseNonce));
            Assert.AreEqual(vector.ExporterSecret, Convert.ToHexStringLower(recipient.ExporterSecret));
        }
    }

    [TestMethod]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, HpkeAead.Aes128Gcm)]
    [DataRow(HpkeKem.DhkemP256HkdfSha256, HpkeAead.ChaCha20Poly1305)]
    public void TrySetupBaseSender_RandomEphemeralKey_RoundTripsThroughTheRecipient(HpkeKem kem, HpkeAead aead)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HpkeBaseModeVector vector = kem == HpkeKem.DhkemX25519HkdfSha256
            ? HpkeBaseModeVectors.X25519Aes128Gcm
            : HpkeBaseModeVectors.P256ChaCha20Poly1305;
        byte[] encapsulatedKey = new byte[Hpke.GetEncapsulatedKeySize(kem)];
        byte[] plaintext = Convert.FromHexString(HpkeBaseModeVectors.Plaintext);
        byte[] ciphertext = new byte[plaintext.Length + HpkeContext.TagSize];
        byte[] opened = new byte[plaintext.Length];
        diagnostics.Arrange("vector source", $"{vector.Source} recipient key pair, random ephemeral key");
        diagnostics.Arrange("suite", $"{kem}, HkdfSha256, {aead}");
        diagnostics.Bytes("plaintext", plaintext);

        Assert.IsTrue(Hpke.TrySetupBaseSender(kem, HpkeKdf.HkdfSha256, aead, Convert.FromHexString(vector.RecipientPublicKey), [], encapsulatedKey, out HpkeContext? sender));
        using (sender)
        {
            sender.Seal([], plaintext, ciphertext);
        }

        Assert.IsTrue(Hpke.TrySetupBaseRecipient(kem, HpkeKdf.HkdfSha256, aead, encapsulatedKey, Convert.FromHexString(vector.RecipientPrivateKey), [], out HpkeContext? recipient));
        using (recipient)
        {
            Assert.IsTrue(recipient.TryOpen([], ciphertext, opened));
        }

        diagnostics.Act("enc length", encapsulatedKey.Length);
        diagnostics.Act("ciphertext length", ciphertext.Length);

        diagnostics.Diff("opened", plaintext, opened);
        diagnostics.Assert("enc differs from the vector's", true, vector.EncapsulatedKey != Convert.ToHexStringLower(encapsulatedKey));
        CollectionAssert.AreEqual(plaintext, opened);
        Assert.AreNotEqual(vector.EncapsulatedKey, Convert.ToHexStringLower(encapsulatedKey));
    }

    [TestMethod]
    public void TrySetupBaseSender_LowOrderRecipientPublicKey_ReturnsFalseWithNoContext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", $"{HpkeBaseModeVectors.X25519Aes128Gcm.Source} skEm, all-zero (low-order) pkRm");
        diagnostics.Arrange("kem", HpkeKem.DhkemX25519HkdfSha256);

        bool setUp = Hpke.TrySetupBaseSender(
            HpkeKem.DhkemX25519HkdfSha256,
            HpkeKdf.HkdfSha256,
            HpkeAead.Aes128Gcm,
            new byte[X25519.KeySize],
            Convert.FromHexString(HpkeBaseModeVectors.X25519Aes128Gcm.EphemeralPrivateKey),
            [],
            new byte[X25519.KeySize],
            out HpkeContext? sender);
        diagnostics.Act("set up", setUp);

        diagnostics.Assert("set up", false, setUp);
        diagnostics.Assert("context is null", true, sender is null);
        Assert.IsFalse(setUp);
        Assert.IsNull(sender);
    }

    [TestMethod]
    public void TrySetupBaseRecipient_OffCurveEncapsulatedKey_ReturnsFalseWithNoContext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", $"{HpkeBaseModeVectors.P256Aes128Gcm.Source} skRm, all-zero (off-curve) enc");
        diagnostics.Arrange("kem", HpkeKem.DhkemP256HkdfSha256);

        bool setUp = Hpke.TrySetupBaseRecipient(
            HpkeKem.DhkemP256HkdfSha256,
            HpkeKdf.HkdfSha256,
            HpkeAead.Aes128Gcm,
            new byte[Hpke.GetEncapsulatedKeySize(HpkeKem.DhkemP256HkdfSha256)],
            Convert.FromHexString(HpkeBaseModeVectors.P256Aes128Gcm.RecipientPrivateKey),
            [],
            out HpkeContext? recipient);
        diagnostics.Act("set up", setUp);

        diagnostics.Assert("set up", false, setUp);
        diagnostics.Assert("context is null", true, recipient is null);
        Assert.IsFalse(setUp);
        Assert.IsNull(recipient);
    }

    [TestMethod]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, 32)]
    [DataRow(HpkeKem.DhkemP256HkdfSha256, 65)]
    public void GetEncapsulatedKeySize_SupportedKem_GivesNenc(HpkeKem kem, int expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "RFC 9180 section 7.1, table 2 (Nenc)");
        diagnostics.Arrange("kem", kem);

        int size = Hpke.GetEncapsulatedKeySize(kem);
        diagnostics.Act("size", size);

        diagnostics.Assert("size", expected, size);
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    public void GetEncapsulatedKeySize_UnsupportedKem_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("kem", "0x0021");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Hpke.GetEncapsulatedKeySize((HpkeKem)0x0021));
        diagnostics.Act("parameter", exception.ParamName);

        diagnostics.Assert("parameter", "kem", exception.ParamName);
        Assert.AreEqual("kem", exception.ParamName);
    }

    [TestMethod]
    [DataRow((HpkeKem)0x0011, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm, "kem")]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, (HpkeKdf)0x0002, HpkeAead.Aes128Gcm, "kdf")]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, (HpkeAead)0x0000, "aead")]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, (HpkeAead)0xFFFF, "aead")]
    public void TrySetupBaseRecipient_UnsupportedSuite_ThrowsArgumentExceptionNamingIt(HpkeKem kem, HpkeKdf kdf, HpkeAead aead, string parameterName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("suite", $"kem 0x{(int)kem:x4}, kdf 0x{(int)kdf:x4}, aead 0x{(int)aead:x4}");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Hpke.TrySetupBaseRecipient(kem, kdf, aead, new byte[32], new byte[32], [], out _));
        diagnostics.Act("parameter", exception.ParamName);

        diagnostics.Assert("parameter", parameterName, exception.ParamName);
        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    public void TrySetupBaseSender_RandomEphemeralKeyAndUnsupportedKem_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("kem", "0x0012");
        diagnostics.Arrange("ephemeral private key", "empty (random)");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Hpke.TrySetupBaseSender((HpkeKem)0x0012, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm, new byte[32], [], new byte[32], out _));
        diagnostics.Act("parameter", exception.ParamName);

        diagnostics.Assert("parameter", "kem", exception.ParamName);
        Assert.AreEqual("kem", exception.ParamName);
    }

    [TestMethod]
    [DataRow(31, 32, "ephemeralPrivateKey")]
    [DataRow(32, 33, "encapsulatedKey")]
    public void TrySetupBaseSender_WrongBufferLength_ThrowsArgumentExceptionNamingIt(int privateKeyLength, int encapsulatedKeyLength, string parameterName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ephemeral private key length", privateKeyLength);
        diagnostics.Arrange("enc length", encapsulatedKeyLength);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Hpke.TrySetupBaseSender(
                HpkeKem.DhkemX25519HkdfSha256,
                HpkeKdf.HkdfSha256,
                HpkeAead.Aes128Gcm,
                Convert.FromHexString(HpkeBaseModeVectors.X25519Aes128Gcm.RecipientPublicKey),
                new byte[privateKeyLength],
                [],
                new byte[encapsulatedKeyLength],
                out _));
        diagnostics.Act("parameter", exception.ParamName);

        diagnostics.Assert("parameter", parameterName, exception.ParamName);
        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    public void TrySetupBaseRecipient_PrivateKeyOfTheWrongLength_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("recipient private key length", 33);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Hpke.TrySetupBaseRecipient(
                HpkeKem.DhkemX25519HkdfSha256,
                HpkeKdf.HkdfSha256,
                HpkeAead.Aes128Gcm,
                Convert.FromHexString(HpkeBaseModeVectors.X25519Aes128Gcm.EncapsulatedKey),
                new byte[33],
                [],
                out _));
        diagnostics.Act("parameter", exception.ParamName);

        diagnostics.Assert("parameter", "recipientPrivateKey", exception.ParamName);
        Assert.AreEqual("recipientPrivateKey", exception.ParamName);
    }
}
