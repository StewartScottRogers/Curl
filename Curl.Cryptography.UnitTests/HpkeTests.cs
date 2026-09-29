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

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void TrySetupBaseSender_BaseModeVector_GivesThePublishedEncapsulatedKeyAndKeySchedule(HpkeBaseModeVector vector)
    {
        byte[] encapsulatedKey = new byte[Hpke.GetEncapsulatedKeySize(vector.Kem)];

        bool setUp = Hpke.TrySetupBaseSender(
            vector.Kem,
            HpkeKdf.HkdfSha256,
            vector.Aead,
            Convert.FromHexString(vector.RecipientPublicKey),
            Convert.FromHexString(vector.EphemeralPrivateKey),
            Convert.FromHexString(HpkeBaseModeVectors.Info),
            encapsulatedKey,
            out HpkeContext? sender);

        Assert.IsTrue(setUp);
        Assert.IsNotNull(sender);
        using (sender)
        {
            Assert.AreEqual(vector.Key, Convert.ToHexStringLower(sender.Key));
            Assert.AreEqual(vector.BaseNonce, Convert.ToHexStringLower(sender.BaseNonce));
            Assert.AreEqual(vector.ExporterSecret, Convert.ToHexStringLower(sender.ExporterSecret));
        }

        Assert.AreEqual(vector.EncapsulatedKey, Convert.ToHexStringLower(encapsulatedKey));
    }

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void TrySetupBaseRecipient_BaseModeVector_GivesThePublishedKeySchedule(HpkeBaseModeVector vector)
    {
        bool setUp = Hpke.TrySetupBaseRecipient(
            vector.Kem,
            HpkeKdf.HkdfSha256,
            vector.Aead,
            Convert.FromHexString(vector.EncapsulatedKey),
            Convert.FromHexString(vector.RecipientPrivateKey),
            Convert.FromHexString(HpkeBaseModeVectors.Info),
            out HpkeContext? recipient);

        Assert.IsTrue(setUp);
        Assert.IsNotNull(recipient);
        using (recipient)
        {
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
        HpkeBaseModeVector vector = kem == HpkeKem.DhkemX25519HkdfSha256
            ? HpkeBaseModeVectors.X25519Aes128Gcm
            : HpkeBaseModeVectors.P256ChaCha20Poly1305;
        byte[] encapsulatedKey = new byte[Hpke.GetEncapsulatedKeySize(kem)];
        byte[] plaintext = Convert.FromHexString(HpkeBaseModeVectors.Plaintext);
        byte[] ciphertext = new byte[plaintext.Length + HpkeContext.TagSize];
        byte[] opened = new byte[plaintext.Length];

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

        CollectionAssert.AreEqual(plaintext, opened);
        Assert.AreNotEqual(vector.EncapsulatedKey, Convert.ToHexStringLower(encapsulatedKey));
    }

    [TestMethod]
    public void TrySetupBaseSender_LowOrderRecipientPublicKey_ReturnsFalseWithNoContext()
    {
        bool setUp = Hpke.TrySetupBaseSender(
            HpkeKem.DhkemX25519HkdfSha256,
            HpkeKdf.HkdfSha256,
            HpkeAead.Aes128Gcm,
            new byte[X25519.KeySize],
            Convert.FromHexString(HpkeBaseModeVectors.X25519Aes128Gcm.EphemeralPrivateKey),
            [],
            new byte[X25519.KeySize],
            out HpkeContext? sender);

        Assert.IsFalse(setUp);
        Assert.IsNull(sender);
    }

    [TestMethod]
    public void TrySetupBaseRecipient_OffCurveEncapsulatedKey_ReturnsFalseWithNoContext()
    {
        bool setUp = Hpke.TrySetupBaseRecipient(
            HpkeKem.DhkemP256HkdfSha256,
            HpkeKdf.HkdfSha256,
            HpkeAead.Aes128Gcm,
            new byte[Hpke.GetEncapsulatedKeySize(HpkeKem.DhkemP256HkdfSha256)],
            Convert.FromHexString(HpkeBaseModeVectors.P256Aes128Gcm.RecipientPrivateKey),
            [],
            out HpkeContext? recipient);

        Assert.IsFalse(setUp);
        Assert.IsNull(recipient);
    }

    [TestMethod]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, 32)]
    [DataRow(HpkeKem.DhkemP256HkdfSha256, 65)]
    public void GetEncapsulatedKeySize_SupportedKem_GivesNenc(HpkeKem kem, int expected)
    {
        int size = Hpke.GetEncapsulatedKeySize(kem);

        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    public void GetEncapsulatedKeySize_UnsupportedKem_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Hpke.GetEncapsulatedKeySize((HpkeKem)0x0021));

        Assert.AreEqual("kem", exception.ParamName);
    }

    [TestMethod]
    [DataRow((HpkeKem)0x0011, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm, "kem")]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, (HpkeKdf)0x0002, HpkeAead.Aes128Gcm, "kdf")]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, (HpkeAead)0x0000, "aead")]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, (HpkeAead)0xFFFF, "aead")]
    public void TrySetupBaseRecipient_UnsupportedSuite_ThrowsArgumentExceptionNamingIt(HpkeKem kem, HpkeKdf kdf, HpkeAead aead, string parameterName)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Hpke.TrySetupBaseRecipient(kem, kdf, aead, new byte[32], new byte[32], [], out _));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    public void TrySetupBaseSender_RandomEphemeralKeyAndUnsupportedKem_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Hpke.TrySetupBaseSender((HpkeKem)0x0012, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm, new byte[32], [], new byte[32], out _));

        Assert.AreEqual("kem", exception.ParamName);
    }

    [TestMethod]
    [DataRow(31, 32, "ephemeralPrivateKey")]
    [DataRow(32, 33, "encapsulatedKey")]
    public void TrySetupBaseSender_WrongBufferLength_ThrowsArgumentExceptionNamingIt(int privateKeyLength, int encapsulatedKeyLength, string parameterName)
    {
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

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    public void TrySetupBaseRecipient_PrivateKeyOfTheWrongLength_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Hpke.TrySetupBaseRecipient(
                HpkeKem.DhkemX25519HkdfSha256,
                HpkeKdf.HkdfSha256,
                HpkeAead.Aes128Gcm,
                Convert.FromHexString(HpkeBaseModeVectors.X25519Aes128Gcm.EncapsulatedKey),
                new byte[33],
                [],
                out _));

        Assert.AreEqual("recipientPrivateKey", exception.ParamName);
    }
}
