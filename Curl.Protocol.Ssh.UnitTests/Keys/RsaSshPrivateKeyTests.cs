using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("rsa-sha2-512", "SHA512")]
    [DataRow("rsa-sha2-256", "SHA256")]
    [DataRow("ssh-rsa", "SHA1")]
    public void Sign_EachAlgorithm_WritesItsNameAndAPkcs1Signature(string algorithm, string hashName)
    {
        SshPrivateKey key = SshPrivateKeyReader.Read(TestUserKeys.RsaPkcs1, [])!;
        Diagnostics.Arrange("algorithm", algorithm);
        Diagnostics.Arrange("hash", hashName);
        Diagnostics.Bytes("data", Data);

        byte[] blob = key.Sign(algorithm, Data);

        Diagnostics.ActBytes("signature blob", blob);
        using RSA rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(PemBody(TestUserKeys.RsaPkcs1), out _);
        byte[] expected = rsa.SignData(Data, new HashAlgorithmName(hashName), RSASignaturePadding.Pkcs1);
        Diagnostics.AssertBytes("signature blob", Join(Name(algorithm), String(expected)), blob);
        CollectionAssert.AreEqual(Join(Name(algorithm), String(expected)), blob);
    }

    [TestMethod]
    public void Sign_RsaSha2256_PinsTheSignatureBlob()
    {
        SshPrivateKey key = SshPrivateKeyReader.Read(TestUserKeys.RsaPkcs1, [])!;
        Diagnostics.Arrange("algorithm", "rsa-sha2-256");
        Diagnostics.Bytes("data", Data);

        byte[] blob = key.Sign("rsa-sha2-256", Data);

        Diagnostics.ActBytes("signature blob", blob);
        Diagnostics.AssertBytes("signature blob", Convert.FromHexString(RsaSha2256Blob), blob);
        Assert.AreEqual(RsaSha2256Blob, Convert.ToHexString(blob));
    }

    [TestMethod]
    public void SignatureAlgorithms_LibSsh2sOrder()
    {
        string[] expected = ["rsa-sha2-512", "rsa-sha2-256", "ssh-rsa"];
        Diagnostics.Arrange("expected order", string.Join(", ", expected));

        string[] actual = RsaSshPrivateKey.SignatureAlgorithms.ToArray();

        Diagnostics.Act("signature algorithms", string.Join(", ", actual));
        Diagnostics.Assert("signature algorithms", string.Join(", ", expected), string.Join(", ", actual));
        CollectionAssert.AreEqual(new[] { "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa" }, actual);
    }

    [TestMethod]
    public void FromComponents_ZeroCoefficient_Throws()
    {
        Diagnostics.Arrange("components", "n 15, e 3, d 3, coefficient empty, p 3, q 5");

        var failure = Assert.ThrowsExactly<CryptographicException>(() => RsaSshPrivateKey.FromComponents([15], [3], [3], [], [3], [5]));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    public void FromComponents_ZeroPrivateExponent_EncodesItAsZeroAndTheImportRefusesIt()
    {
        Diagnostics.Arrange("components", "n 15, e 3, d 00 00, coefficient 2, p 3, q 5");

        // The import refuses the key; OpenSSL's refusal is a subclass of CryptographicException.
        var failure = Assert.Throws<CryptographicException>(() => RsaSshPrivateKey.FromComponents([15], [3], [0, 0], [2], [3], [5]));

        Diagnostics.ActAndAssertThrown($"{nameof(CryptographicException)} or a subclass", failure);
    }

    [TestMethod]
    [DataRow(new byte[] { 1 }, new byte[] { 5 }, DisplayName = "p is one")]
    [DataRow(new byte[] { 3 }, new byte[0], DisplayName = "q is zero")]
    public void FromComponents_PrimeOfOneOrLess_ThrowsRatherThanDividingByZero(byte[] prime1, byte[] prime2)
    {
        Diagnostics.Arrange("p", Convert.ToHexString(prime1));
        Diagnostics.Arrange("q", Convert.ToHexString(prime2));

        var failure = Assert.ThrowsExactly<CryptographicException>(() => RsaSshPrivateKey.FromComponents([15], [3], [3], [2], prime1, prime2));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    public void FromComponents_TheSixOpenSshIntegers_SignsAsTheFullPkcs1Key()
    {
        using RSA full = RSA.Create();
        full.ImportRSAPrivateKey(PemBody(TestUserKeys.RsaPkcs1), out _);
        RSAParameters expected = full.ExportParameters(includePrivateParameters: true);
        Diagnostics.Arrange("key", "the test RSA key without its CRT exponents dP and dQ");
        Diagnostics.Bytes("data", Data);

        // A wrong derived dP or dQ gives a wrong CRT signature wherever RSA trusts the CRT values.
        RsaSshPrivateKey key = RsaSshPrivateKey.FromComponents(
            expected.Modulus, expected.Exponent, expected.D, expected.InverseQ, expected.P, expected.Q);
        byte[] blob = key.Sign("rsa-sha2-256", Data);

        Diagnostics.ActBytes("signature blob", blob);
        Diagnostics.AssertBytes("signature blob", Convert.FromHexString(RsaSha2256Blob), blob);
        Assert.AreEqual(RsaSha2256Blob, Convert.ToHexString(blob));
    }

    [TestMethod]
    public void FromComponents_TheSixOpenSshIntegers_DerivesDpAsDModPMinusOneAndDqAsDModQMinusOne()
    {
        using RSA full = RSA.Create();
        full.ImportRSAPrivateKey(PemBody(TestUserKeys.RsaPkcs1), out _);
        RSAParameters expected = full.ExportParameters(includePrivateParameters: true);
        byte[] expectedEncoding = full.ExportRSAPrivateKey();
        Diagnostics.Bytes("expected dP", expected.DP!);
        Diagnostics.Bytes("expected dQ", expected.DQ!);

        // The encoding before import: Windows' RSA recomputes dP and dQ when it imports a key.
        byte[] actual = RsaSshPrivateKey.FromComponents(
            expected.Modulus, expected.Exponent, expected.D, expected.InverseQ, expected.P, expected.Q).Pkcs1Encoding;

        Diagnostics.ActBytes("PKCS #1 encoding", actual);
        Diagnostics.AssertBytes("PKCS #1 encoding", expectedEncoding, actual);
        CollectionAssert.AreEqual(expectedEncoding, actual);
    }

    private static byte[] PemBody(string pem) =>
        Convert.FromBase64String(string.Concat(pem.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('-'))));
}
