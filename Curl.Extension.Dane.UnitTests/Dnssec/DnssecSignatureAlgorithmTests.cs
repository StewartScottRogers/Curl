using System.Security.Cryptography;
using static Curl.Extension.Dane.Dnssec.DnssecTestRecords;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// Checks <see cref="DnssecSignatureAlgorithm" />'s key formats: RFC 3110 section 2's RSA exponent lengths, RFC 6605
/// section 4's ECDSA points and RFC 8080 section 3's Ed25519 and Ed448 sizes, a malformed key giving false.
/// </summary>
[TestClass]
public sealed class DnssecSignatureAlgorithmTests
{
    private static readonly byte[] Data = [1, 2, 3];

    [TestMethod]
    public void TryReadRsaPublicKey_Rfc3110Section2ThreeOctetExponentLength_ReadsExponentAndModulus()
    {
        bool read = DnssecSignatureAlgorithm.TryReadRsaPublicKey([0, 0, 1, 3, 0xAB, 0xCD], out RSAParameters parameters);

        Assert.IsTrue(read);
        CollectionAssert.AreEqual(new byte[] { 3 }, parameters.Exponent);
        CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD }, parameters.Modulus);
    }

    [TestMethod]
    public void Verify_Rfc3110Section2RsaKeyWithThreeOctetExponentLength_Verifies()
    {
        using RSA rsa = RSA.Create(2048);
        RSAParameters parameters = rsa.ExportParameters(false);
        byte[] key = [0, 0, (byte)parameters.Exponent!.Length, .. parameters.Exponent, .. parameters.Modulus!];

        bool verified = DnssecSignatureAlgorithm.Verify(8, key, Data, rsa.SignData(Data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Assert.IsTrue(verified);
    }

    [TestMethod]
    [DataRow(new byte[0], DisplayName = "empty")]
    [DataRow(new byte[] { 0, 0 }, DisplayName = "three-octet length cut short")]
    [DataRow(new byte[] { 0, 0, 0, 1 }, DisplayName = "zero exponent length")]
    [DataRow(new byte[] { 2, 1, 0 }, DisplayName = "no modulus")]
    public void TryReadRsaPublicKey_Rfc3110Section2MalformedKey_IsFalse(byte[] key) =>
        Assert.IsFalse(DnssecSignatureAlgorithm.TryReadRsaPublicKey(key, out _));

    [TestMethod]
    public void Verify_Rfc3110Section2MalformedRsaKey_IsFalse() =>
        Assert.IsFalse(DnssecSignatureAlgorithm.Verify(8, [], Data, [1]));

    [TestMethod]
    public void Verify_Rfc3110Section2RsaModulusOfOneOctet_IsFalse() =>
        Assert.IsFalse(DnssecSignatureAlgorithm.Verify(8, [1, 3, 0x0F], Data, [1]));

    [TestMethod]
    public void Verify_Rfc5702Section3RsaSignatureOfWrongLength_IsFalse()
    {
        GeneratedZoneKey zoneKey = RsaSha512();

        Assert.IsFalse(DnssecSignatureAlgorithm.Verify(10, zoneKey.Key.PublicKey, Data, [1, 2, 3]));
    }

    [TestMethod]
    public void Verify_Rfc6605Section4EcdsaKeyOfWrongLength_IsFalse() =>
        Assert.IsFalse(DnssecSignatureAlgorithm.Verify(13, new byte[63], Data, new byte[64]));

    [TestMethod]
    public void Verify_Rfc6605Section4EcdsaPointNotOnCurve_IsFalse() =>
        Assert.IsFalse(DnssecSignatureAlgorithm.Verify(14, Enumerable.Repeat((byte)1, 96).ToArray(), Data, new byte[96]));

    [TestMethod]
    [DataRow((byte)15, 31, 64, DisplayName = "Ed25519 short key")]
    [DataRow((byte)15, 32, 63, DisplayName = "Ed25519 short signature")]
    [DataRow((byte)16, 56, 114, DisplayName = "Ed448 short key")]
    [DataRow((byte)16, 57, 113, DisplayName = "Ed448 short signature")]
    public void Verify_Rfc8080Section3EdwardsKeyOrSignatureOfWrongLength_IsFalse(byte algorithm, int keyLength, int signatureLength) =>
        Assert.IsFalse(DnssecSignatureAlgorithm.Verify(algorithm, new byte[keyLength], Data, new byte[signatureLength]));

    [TestMethod]
    public void Verify_Rfc8624Section3_1UnsupportedAlgorithm5_IsFalse()
    {
        Assert.IsFalse(DnssecSignatureAlgorithm.IsSupported(5));
        Assert.IsFalse(DnssecSignatureAlgorithm.Verify(5, [1], Data, [1]));
    }
}
