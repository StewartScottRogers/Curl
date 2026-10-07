using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="HpkeDhkem" />'s <c>Encap</c> and <c>Decap</c> to the enc and
/// shared_secret of RFC 9180's base-mode vectors (<see cref="HpkeBaseModeVectors" />), and
/// checks every unusable peer key is ADR-0118's <c>false</c> with the shared secret zeroed.
/// </summary>
[TestClass]
public sealed class HpkeDhkemTests
{
    /// <summary>The base-mode vectors, one data row each.</summary>
    public static IEnumerable<object[]> BaseModeVectors => HpkeBaseModeVectors.All;

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void TryEncapsulate_BaseModeVector_GivesThePublishedEncapsulatedKeyAndSharedSecret(HpkeBaseModeVector vector)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] encapsulatedKey = new byte[HpkeDhkem.GetPublicKeySize(vector.Kem)];
        byte[] sharedSecret = new byte[HpkeDhkem.SharedSecretSize];
        diagnostics.Arrange("vector source", vector.Source);
        diagnostics.Arrange("kem", vector.Kem);
        diagnostics.Bytes("skEm", Convert.FromHexString(vector.EphemeralPrivateKey));
        diagnostics.Bytes("pkRm", Convert.FromHexString(vector.RecipientPublicKey));

        bool encapsulated = HpkeDhkem.TryEncapsulate(
            vector.Kem,
            Convert.FromHexString(vector.EphemeralPrivateKey),
            Convert.FromHexString(vector.RecipientPublicKey),
            encapsulatedKey,
            sharedSecret);
        diagnostics.Act("encapsulated", encapsulated);
        diagnostics.Bytes("enc", encapsulatedKey);
        diagnostics.Bytes("shared_secret", sharedSecret);

        diagnostics.Assert("encapsulated", true, encapsulated);
        diagnostics.Diff("enc", Convert.FromHexString(vector.EncapsulatedKey), encapsulatedKey);
        diagnostics.Diff("shared_secret", Convert.FromHexString(vector.SharedSecret), sharedSecret);
        Assert.IsTrue(encapsulated);
        Assert.AreEqual(vector.EncapsulatedKey, Convert.ToHexStringLower(encapsulatedKey));
        Assert.AreEqual(vector.SharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    [TestMethod]
    [DynamicData(nameof(BaseModeVectors))]
    public void TryDecapsulate_BaseModeVector_GivesThePublishedSharedSecret(HpkeBaseModeVector vector)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] sharedSecret = new byte[HpkeDhkem.SharedSecretSize];
        diagnostics.Arrange("vector source", vector.Source);
        diagnostics.Arrange("kem", vector.Kem);
        diagnostics.Bytes("enc", Convert.FromHexString(vector.EncapsulatedKey));
        diagnostics.Bytes("skRm", Convert.FromHexString(vector.RecipientPrivateKey));

        bool decapsulated = HpkeDhkem.TryDecapsulate(
            vector.Kem,
            Convert.FromHexString(vector.EncapsulatedKey),
            Convert.FromHexString(vector.RecipientPrivateKey),
            sharedSecret);
        diagnostics.Act("decapsulated", decapsulated);
        diagnostics.Bytes("shared_secret", sharedSecret);

        diagnostics.Assert("decapsulated", true, decapsulated);
        diagnostics.Diff("shared_secret", Convert.FromHexString(vector.SharedSecret), sharedSecret);
        Assert.IsTrue(decapsulated);
        Assert.AreEqual(vector.SharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    // X25519: the all-zero point is low-order and gives the all-zero result RFC 9180 section 7.1.4 rejects; a 31-byte key.
    // P-256: a 32-byte key, the right length with the compressed-form prefix 05, pkRm of A.3.1 with Y changed off the curve,
    // then with X, then Y, replaced by 2^256 - 1, which is not below p.
    [TestMethod]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, "0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow(HpkeKem.DhkemX25519HkdfSha256, "3948cfe0ad1ddb695d780e59077195da6c56506b027329794ab02bca80815c")]
    [DataRow(HpkeKem.DhkemP256HkdfSha256, "3948cfe0ad1ddb695d780e59077195da6c56506b027329794ab02bca80815c4d")]
    [DataRow(
        HpkeKem.DhkemP256HkdfSha256,
        "05fe8c19ce0905191ebc298a9245792531f26f0cece2460639e8bc39cb7f706a826a779b4cf969b8a0e539c7f62fb3d30ad6aa8f80e30f1d128aafd68a2ce72ea0")]
    [DataRow(
        HpkeKem.DhkemP256HkdfSha256,
        "04fe8c19ce0905191ebc298a9245792531f26f0cece2460639e8bc39cb7f706a826a779b4cf969b8a0e539c7f62fb3d30ad6aa8f80e30f1d128aafd68a2ce72ea1")]
    [DataRow(
        HpkeKem.DhkemP256HkdfSha256,
        "04ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff6a779b4cf969b8a0e539c7f62fb3d30ad6aa8f80e30f1d128aafd68a2ce72ea0")]
    [DataRow(
        HpkeKem.DhkemP256HkdfSha256,
        "04fe8c19ce0905191ebc298a9245792531f26f0cece2460639e8bc39cb7f706a82ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    public void TryEncapsulate_UnusableRecipientPublicKey_ReturnsFalseWithTheSharedSecretZeroed(HpkeKem kem, string recipientPublicKey)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HpkeBaseModeVector vector = kem == HpkeKem.DhkemX25519HkdfSha256
            ? HpkeBaseModeVectors.X25519Aes128Gcm
            : HpkeBaseModeVectors.P256Aes128Gcm;
        byte[] sharedSecret = Enumerable.Repeat((byte)0xAA, HpkeDhkem.SharedSecretSize).ToArray();
        diagnostics.Arrange("vector source", $"{vector.Source} skEm, with an unusable pkRm (RFC 9180 section 7.1.4)");
        diagnostics.Arrange("kem", kem);
        diagnostics.Bytes("pkRm", Convert.FromHexString(recipientPublicKey));

        bool encapsulated = HpkeDhkem.TryEncapsulate(
            kem,
            Convert.FromHexString(vector.EphemeralPrivateKey),
            Convert.FromHexString(recipientPublicKey),
            new byte[HpkeDhkem.GetPublicKeySize(kem)],
            sharedSecret);
        diagnostics.Act("encapsulated", encapsulated);

        diagnostics.Assert("encapsulated", false, encapsulated);
        diagnostics.Diff("shared_secret", new byte[HpkeDhkem.SharedSecretSize], sharedSecret);
        Assert.IsFalse(encapsulated);
        Assert.IsTrue(sharedSecret.All(value => value == 0));
    }

    [TestMethod]
    public void TryDecapsulate_AllZeroP256EncapsulatedKey_ReturnsFalseWithTheSharedSecretZeroed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] sharedSecret = Enumerable.Repeat((byte)0xAA, HpkeDhkem.SharedSecretSize).ToArray();
        diagnostics.Arrange("vector source", $"{HpkeBaseModeVectors.P256Aes128Gcm.Source} skRm, with an all-zero enc");
        diagnostics.Arrange("enc length", HpkeDhkem.GetPublicKeySize(HpkeKem.DhkemP256HkdfSha256));

        bool decapsulated = HpkeDhkem.TryDecapsulate(
            HpkeKem.DhkemP256HkdfSha256,
            new byte[HpkeDhkem.GetPublicKeySize(HpkeKem.DhkemP256HkdfSha256)],
            Convert.FromHexString(HpkeBaseModeVectors.P256Aes128Gcm.RecipientPrivateKey),
            sharedSecret);
        diagnostics.Act("decapsulated", decapsulated);

        diagnostics.Assert("decapsulated", false, decapsulated);
        diagnostics.Diff("shared_secret", new byte[HpkeDhkem.SharedSecretSize], sharedSecret);
        Assert.IsFalse(decapsulated);
        Assert.IsTrue(sharedSecret.All(value => value == 0));
    }
}
