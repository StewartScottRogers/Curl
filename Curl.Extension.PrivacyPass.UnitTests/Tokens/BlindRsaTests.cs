using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// Pins RSABSSA-SHA384-PSS-Deterministic (RFC 9474 §4.2 Blind, §4.3 Finalize) against an issuer that signs the blinded
/// message with its private key, and the key encoding of RFC 9578 §6.5.
/// </summary>
[TestClass]
public sealed class BlindRsaTests
{
    private static readonly BlindRsaPublicKey Key = BlindRsaPublicKey.Decode(TestIssuer.Spki)!;

    [TestMethod]
    public void Finalize_IssuersBlindSignature_GivesAnRsassaPssSignatureOfTheMessage()
    {
        byte[] message = [1, 2, 3];
        var blindRsa = new BlindRsa(Key, RandomNumberGenerator.GetBytes);
        var blinded = blindRsa.Blind(message);

        var signature = blindRsa.Finalize(message, TestIssuer.Sign(blinded));

        Assert.HasCount(BlindRsaPublicKey.ModulusLength, blinded);
        Assert.IsNotNull(signature);
        Assert.IsTrue(TestIssuer.Verifies(message, signature));
    }

    [TestMethod]
    public void Finalize_SignatureOfAnotherMessage_IsNull()
    {
        var blindRsa = new BlindRsa(Key, RandomNumberGenerator.GetBytes);
        var blinded = blindRsa.Blind([1]);

        Assert.IsNull(blindRsa.Finalize([2], TestIssuer.Sign(blinded)));
    }

    [TestMethod]
    public void Blind_FactorSharingAFactorWithTheModulus_Throws()
    {
        // An even modulus and random octets that make r = 2: r has no inverse.
        var key = new BlindRsaPublicKey(BigInteger.One << 2047, 3);
        var blindRsa = new BlindRsa(key, count => count == BlindRsa.SaltLength ? new byte[count] : [.. new byte[count - 1], 1]);

        Assert.ThrowsExactly<CryptographicException>(() => blindRsa.Blind([1]));
    }

    [TestMethod]
    public void ModularInverse_CoprimeAndNot_GivesTheInverseOrNull()
    {
        Assert.AreEqual(new BigInteger(4), BlindRsa.ModularInverse(3, 11));
        Assert.AreEqual(new BigInteger(3), BlindRsa.ModularInverse(7, 10));
        Assert.IsNull(BlindRsa.ModularInverse(4, 10));
    }

    [TestMethod]
    public void Decode_PssKey_ReadsModulusAndExponent()
    {
        Assert.AreEqual(TestIssuer.Modulus, Key.Modulus);
        Assert.AreEqual(TestIssuer.Exponent, Key.Exponent);
    }

    [TestMethod]
    public void Decode_RsaEncryptionKey_IsNull()
    {
        var spki = TestIssuer.SubjectPublicKeyInfo("1.2.840.113549.1.1.1", TestIssuer.Modulus.ToByteArray(true, true), [1, 0, 1]);

        Assert.IsNull(BlindRsaPublicKey.Decode(spki));
    }

    [TestMethod]
    public void Decode_1024BitKey_IsNull()
    {
        var spki = TestIssuer.SubjectPublicKeyInfo(BlindRsaPublicKey.RsassaPssOid, [0x80, .. new byte[127]], [1, 0, 1]);

        Assert.IsNull(BlindRsaPublicKey.Decode(spki));
    }

    [TestMethod]
    public void Decode_NotDer_IsNull() => Assert.IsNull(BlindRsaPublicKey.Decode([0x30, 0x05, 0x01]));
}
