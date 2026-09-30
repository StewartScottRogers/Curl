using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="RsaSshPrivateKey" />'s signature blobs for the fixed test key: PKCS #1
/// v1.5 is deterministic, so each algorithm's blob over fixed data is one exact value.
/// </summary>
[TestClass]
public sealed class RsaSshPrivateKeyTests
{
    // OpenSSL 3.5, openssl dgst -sha256 -sign over the same data and key, wrapped in the blob.
    private const string RsaSha2256Blob =
        "0000000C7273612D736861322D32353600000080"
        + "4FDF15BC8837A55B2298D8E8D7ECE303A472BDDCAED2276470DAA0AA7B72A9614DCB949C64207B19589AF056BC20EC206F5DF2CBA29CBA6F2D42AB304E822E"
        + "4A2FF755FDF740676AA0FC4FA167D9A793376C3D3E22BA7411F7AAA5F8DB75089B8CFBDD0768A8464022525AB1206DAA7E073E8BFEDFF0F737EC21E85543A5C3F2";

    private static readonly byte[] Data = Encoding.ASCII.GetBytes("session identifier and request");

    [TestMethod]
    [DataRow("rsa-sha2-512", "SHA512")]
    [DataRow("rsa-sha2-256", "SHA256")]
    [DataRow("ssh-rsa", "SHA1")]
    public void Sign_EachAlgorithm_WritesItsNameAndAPkcs1Signature(string algorithm, string hashName)
    {
        SshPrivateKey key = SshPrivateKeyReader.Read(TestUserKeys.RsaPkcs1, [])!;

        byte[] blob = key.Sign(algorithm, Data);

        using RSA rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(PemBody(TestUserKeys.RsaPkcs1), out _);
        byte[] expected = rsa.SignData(Data, new HashAlgorithmName(hashName), RSASignaturePadding.Pkcs1);
        CollectionAssert.AreEqual(Join(Name(algorithm), String(expected)), blob);
    }

    [TestMethod]
    public void Sign_RsaSha2256_PinsTheSignatureBlob()
    {
        SshPrivateKey key = SshPrivateKeyReader.Read(TestUserKeys.RsaPkcs1, [])!;

        byte[] blob = key.Sign("rsa-sha2-256", Data);

        Assert.AreEqual(RsaSha2256Blob, Convert.ToHexString(blob));
    }

    [TestMethod]
    public void SignatureAlgorithms_LibSsh2sOrder()
    {
        CollectionAssert.AreEqual(new[] { "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa" }, RsaSshPrivateKey.SignatureAlgorithms.ToArray());
    }

    [TestMethod]
    public void FromComponents_ZeroCoefficient_Throws()
    {
        Assert.ThrowsExactly<CryptographicException>(() => RsaSshPrivateKey.FromComponents([15], [3], [3], [], [3], [5]));
    }

    [TestMethod]
    public void FromComponents_ZeroPrivateExponent_EncodesItAsZeroAndTheImportRefusesIt()
    {
        // The import refuses the key; OpenSSL's refusal is a subclass of CryptographicException.
        Assert.Throws<CryptographicException>(() => RsaSshPrivateKey.FromComponents([15], [3], [0, 0], [2], [3], [5]));
    }

    [TestMethod]
    [DataRow(new byte[] { 1 }, new byte[] { 5 }, DisplayName = "p is one")]
    [DataRow(new byte[] { 3 }, new byte[0], DisplayName = "q is zero")]
    public void FromComponents_PrimeOfOneOrLess_ThrowsRatherThanDividingByZero(byte[] prime1, byte[] prime2)
    {
        Assert.ThrowsExactly<CryptographicException>(() => RsaSshPrivateKey.FromComponents([15], [3], [3], [2], prime1, prime2));
    }

    private static byte[] PemBody(string pem) =>
        Convert.FromBase64String(string.Concat(pem.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('-'))));
}
