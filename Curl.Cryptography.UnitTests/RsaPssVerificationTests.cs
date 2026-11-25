using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="RsaPssVerification" /> against signatures OpenSSL 3.5.9 made
/// (<c>openssl dgst -sign -sigopt rsa_padding_mode:pss</c> over <c>Curl RSASSA-PSS vector</c>,
/// RSA 1024), against the base class library's, and against hand-made encodings under the public
/// exponent 1, whose "signature" is the encoded message itself.
/// </summary>
[TestClass]
public sealed class RsaPssVerificationTests
{
    // SHA-256, MGF1 over SHA-256, the longest salt the key admits (94 bytes).
    private const string LongestSaltSignature =
        "8b2d714c52fa56c327e6c251e2a796ccd7af5c609ccf1dd835f69697aa75ae014f19756e599cc07239cd8db7e59b525a" +
        "0647dc4612a0659f40b773d33381e7fe641b581e0dd23cb323012c07033768a083084c60ebb550c2e752503ef1e9b4f4" +
        "a76e1df12e69d11457ce8019f840a1db4fa604cd25ae19a786f05ebf5b96bcac";

    // SHA-256, MGF1 over SHA-1, a 32-byte salt.
    private const string Sha1MaskSignature =
        "a5d59f013b1c2bcf1bc7525d49d88689da0b4a896a53b5a39f73f67d88d7ad1d18367a66b1835233e6e5b4db42b1dcfc" +
        "e48da912cc7afc7f3ea75024c1a061be3515eb475f1f088f7cb4e1d0fd4627018e47da16ae8b46b5e23b8f1c858aa020" +
        "8eb5333f87a7c484e2f721e7ceb9d659a3d0b1aa931f55b429f2f6c1075763c4";

    // SHA-1, MGF1 over SHA-512, no salt.
    private const string NoSaltSignature =
        "8826f6c401381c78fd63d0091f91bea0d7d714503f240ca7d75969aa54c3ea581f34c60b59bf079591e7129e1b27f141" +
        "4e2d497fce0f568b21cfd4a2e7db1451108c23b047f99074a69b31491660b056bff5606c2d8ccfad12510510d98a3ccc" +
        "b5b457b943a725d653c264aac661aff73a1b0ff64ae44caff1b6d390c56212d4";

    private static readonly byte[] Message = "Curl RSASSA-PSS vector"u8.ToArray();

    private static readonly RSAParameters OpenSslKey = new()
    {
        Modulus = Convert.FromHexString(
            "CBB88F4EFE9267F6337CCE71B16A34F48A82B5102096F565903E4C60C27CDCD0D778AD7AED8DCC4236A8F1373EAA04A0" +
            "57035B05320D10284A36DDC7A23376EFFB099709E683D8F94048FF3EA2B8BEA610601F08612A9E8E5839B7FF2260A1EF" +
            "D96273337EEC3BA1146E2F21508947585E945347C08D2759530DDBDC7F3938F7"),
        Exponent = [1, 0, 1],
    };

    [TestMethod]
    [DataRow(LongestSaltSignature, "SHA256", "SHA256", 94, DisplayName = "the longest salt")]
    [DataRow(Sha1MaskSignature, "SHA256", "SHA1", 32, DisplayName = "MGF1 over another hash")]
    [DataRow(NoSaltSignature, "SHA1", "SHA512", 0, DisplayName = "no salt")]
    public void Verify_OpenSslSignature_IsTrue(string signature, string hash, string maskHash, int saltLength)
    {
        Assert.IsTrue(RsaPssVerification.Verify(
            OpenSslKey, Message, Convert.FromHexString(signature), new(hash), new(maskHash), saltLength));
    }

    [TestMethod]
    [DataRow(LongestSaltSignature, "SHA256", "SHA256", 93, DisplayName = "salt declared one short")]
    [DataRow(LongestSaltSignature, "SHA256", "SHA256", 95, DisplayName = "salt too long for the key")]
    [DataRow(LongestSaltSignature, "SHA256", "SHA256", int.MaxValue, DisplayName = "absurd salt length")]
    [DataRow(Sha1MaskSignature, "SHA256", "SHA256", 32, DisplayName = "another mask hash")]
    [DataRow(NoSaltSignature, "SHA256", "SHA512", 0, DisplayName = "another message hash")]
    public void Verify_OpenSslSignatureReadWithOtherParameters_IsFalse(string signature, string hash, string maskHash, int saltLength)
    {
        Assert.IsFalse(RsaPssVerification.Verify(
            OpenSslKey, Message, Convert.FromHexString(signature), new(hash), new(maskHash), saltLength));
    }

    [TestMethod]
    public void Verify_OpenSslSignatureOfAnotherMessage_IsFalse()
    {
        Assert.IsFalse(RsaPssVerification.Verify(
            OpenSslKey, "other"u8, Convert.FromHexString(NoSaltSignature), HashAlgorithmName.SHA1, HashAlgorithmName.SHA512, 0));
    }

    [TestMethod]
    public void Verify_BaseClassLibrarySignature_IsTrue()
    {
        using var rsa = RSA.Create(2048);
        var signature = rsa.SignData(Message, HashAlgorithmName.SHA384, RSASignaturePadding.Pss);

        var verified = RsaPssVerification.Verify(
            rsa.ExportParameters(false), Message, signature, HashAlgorithmName.SHA384, HashAlgorithmName.SHA384, 48);

        Assert.IsTrue(verified);
    }

    [TestMethod]
    public void Verify_SignatureShorterThanTheModulus_IsFalse()
    {
        Assert.IsFalse(VerifyUnderExponentOne(Modulus(1024), new byte[127]));
    }

    [TestMethod]
    public void Verify_SignatureNotBelowTheModulus_IsFalse()
    {
        Assert.IsFalse(VerifyUnderExponentOne(Modulus(1024), Modulus(1024)));
    }

    [TestMethod]
    public void Verify_MessageLongerThanTheEncoding_IsFalse()
    {
        // A 1025-bit modulus has a 128-byte encoding, one byte shorter than its signature.
        var signature = new byte[129];
        signature[0] = 1;

        Assert.IsFalse(VerifyUnderExponentOne(Modulus(1025), signature));
    }

    [TestMethod]
    public void Verify_ModulusTooShortForTheHash_IsFalse()
    {
        Assert.IsFalse(VerifyUnderExponentOne(Modulus(256), new byte[32]));
    }

    [TestMethod]
    [DataRow(1024)]
    [DataRow(1025)]
    public void Verify_HandMadeEncoding_IsTrue(int modulusBits)
    {
        var signature = HandMadeSignature(modulusBits, _ => { });

        Assert.IsTrue(VerifyUnderExponentOne(Modulus(modulusBits), signature));
    }

    [TestMethod]
    [DataRow(127, 0x01, DisplayName = "the trailer byte")]
    [DataRow(0, 0x80, DisplayName = "an unused leading bit")]
    [DataRow(1, 0x01, DisplayName = "the zero padding")]
    [DataRow(127 - 32 - 4 - 1, 0x02, DisplayName = "the 0x01 before the salt")]
    [DataRow(127 - 32 - 1, 0x01, DisplayName = "the salt")]
    public void Verify_HandMadeEncodingWithOneByteChanged_IsFalse(int index, int flipped)
    {
        var signature = HandMadeSignature(1024, encoded => encoded[index] ^= (byte)flipped);

        Assert.IsFalse(VerifyUnderExponentOne(Modulus(1024), signature));
    }

    [TestMethod]
    public void Verify_NegativeSaltLength_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RsaPssVerification.Verify(
            OpenSslKey, Message, [], HashAlgorithmName.SHA256, HashAlgorithmName.SHA256, -1));
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Verify_KeyWithoutModulusOrExponent_Throws(bool hasModulus, bool hasExponent)
    {
        var key = new RSAParameters { Modulus = hasModulus ? [3] : null, Exponent = hasExponent ? [1] : null };

        Assert.ThrowsExactly<ArgumentException>(() => RsaPssVerification.Verify(
            key, Message, [], HashAlgorithmName.SHA256, HashAlgorithmName.SHA256, 0));
    }

    private static bool VerifyUnderExponentOne(byte[] modulus, byte[] signature) =>
        RsaPssVerification.Verify(
            new RSAParameters { Modulus = modulus, Exponent = [1] },
            Message,
            signature,
            HashAlgorithmName.SHA256,
            HashAlgorithmName.SHA256,
            4);

    // 2^bits - 1: every encoding of bits - 1 bits is below it.
    private static byte[] Modulus(int bits) =>
        ((BigInteger.One << bits) - 1).ToByteArray(isUnsigned: true, isBigEndian: true);

    // EMSA-PSS-ENCODE (RFC 8017, 9.1.1) of Message with SHA-256 and a four-byte salt, then change applied.
    private static byte[] HandMadeSignature(int modulusBits, Action<byte[]> change)
    {
        byte[] salt = [1, 2, 3, 4];
        byte[] salted = [.. new byte[8], .. SHA256.HashData(Message), .. salt];
        var embeddedHash = SHA256.HashData(salted);
        var encodedBits = modulusBits - 1;
        var encoded = new byte[(encodedBits + 7) / 8];
        var block = new byte[encoded.Length - embeddedHash.Length - 1];
        block[^(salt.Length + 1)] = 1;
        salt.CopyTo(block.AsSpan(block.Length - salt.Length));
        for (var offset = 0; offset < block.Length; offset++)
        {
            byte[] input = [.. embeddedHash, 0, 0, 0, (byte)(offset / 32)];
            block[offset] ^= SHA256.HashData(input)[offset % 32];
        }

        block[0] &= (byte)(0xFF >> (8 * encoded.Length - encodedBits));
        block.CopyTo(encoded.AsSpan());
        embeddedHash.CopyTo(encoded.AsSpan(block.Length));
        encoded[^1] = 0xBC;
        change(encoded);
        var signature = new byte[(modulusBits + 7) / 8];
        encoded.CopyTo(signature.AsSpan(signature.Length - encoded.Length));
        return signature;
    }
}
