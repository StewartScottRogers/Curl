using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// Pins which RSASSA-PSS parameters verify a revocation list's signature (STD-197), and when a
/// list counts as unexpired.
/// </summary>
[TestClass]
public sealed class RsaPssSignatureParametersTests
{
    private static readonly X509Certificate2 Authority = CreateAuthority();

    [TestMethod]
    [DataRow(TestRevocationList.Sha256, 32, "SHA256")]
    [DataRow(TestRevocationList.Sha384, 48, "SHA384")]
    [DataRow(TestRevocationList.Sha512, 64, "SHA512")]
    public void IsSignedBy_PssWithTheHashItsMgf1AndAHashLongSalt_IsTrue(string hashOid, int saltLength, string hashName)
    {
        var list = CertificateRevocationList.Decode(PssList(TestRevocationList.PssParameters(hashOid, hashOid, saltLength), new(hashName)));

        Assert.IsTrue(list.IsSignedBy(Authority));
    }

    [TestMethod]
    public void IsSignedBy_PssWithEveryFieldAtItsSha1Default_IsTrue()
    {
        var list = CertificateRevocationList.Decode(PssList(TestRevocationList.PssParameters(), HashAlgorithmName.SHA1));

        Assert.IsTrue(list.IsSignedBy(Authority));
    }

    [TestMethod]
    public void IsSignedBy_PssSignatureOverAnotherHashThanNamed_IsFalse()
    {
        var parameters = TestRevocationList.PssParameters(TestRevocationList.Sha256, TestRevocationList.Sha256, 32);

        var list = CertificateRevocationList.Decode(PssList(parameters, HashAlgorithmName.SHA384));

        Assert.IsFalse(list.IsSignedBy(Authority));
    }

    [TestMethod]
    public void IsSignedBy_PssWithoutParameters_IsFalse()
    {
        var list = CertificateRevocationList.Decode(PssList(null, HashAlgorithmName.SHA256));

        Assert.IsFalse(list.IsSignedBy(Authority));
    }

    [TestMethod]
    public void ReadVerifiableHash_ExplicitTrailerFieldOne_IsTheHash()
    {
        var parameters = TestRevocationList.PssParameters(TestRevocationList.Sha256, TestRevocationList.Sha256, 32, trailerField: 1);

        Assert.AreEqual(HashAlgorithmName.SHA256, RsaPssSignatureParameters.ReadVerifiableHash(parameters));
    }

    [TestMethod]
    [DataRow("1.2.840.113549.2.5", "1.2.840.113549.2.5", 16, 1, TestRevocationList.Mgf1, DisplayName = "MD5")]
    [DataRow(TestRevocationList.Sha256, TestRevocationList.Sha1, 32, 1, TestRevocationList.Mgf1, DisplayName = "MGF1 over another hash")]
    [DataRow(TestRevocationList.Sha256, TestRevocationList.Sha256, 20, 1, TestRevocationList.Mgf1, DisplayName = "salt not hash long")]
    [DataRow(TestRevocationList.Sha256, TestRevocationList.Sha256, 32, 2, TestRevocationList.Mgf1, DisplayName = "trailer field 2")]
    [DataRow(TestRevocationList.Sha256, TestRevocationList.Sha256, 32, 1, "1.2.3", DisplayName = "not MGF1")]
    public void ReadVerifiableHash_ParametersTheBaseClassLibraryCannotVerify_IsNull(
        string hash, string maskHash, int saltLength, int trailerField, string maskFunction)
    {
        var parameters = TestRevocationList.PssParameters(hash, maskHash, saltLength, trailerField, maskFunction);

        Assert.IsNull(RsaPssSignatureParameters.ReadVerifiableHash(parameters));
    }

    [TestMethod]
    [DataRow(new byte[0], DisplayName = "empty")]
    [DataRow(new byte[] { 0x05, 0x00 }, DisplayName = "NULL")]
    [DataRow(new byte[] { 0x30, 0x02, 0x05, 0x00 }, DisplayName = "an unknown field")]
    public void ReadVerifiableHash_MalformedParameters_IsNull(byte[] parameters)
    {
        Assert.IsNull(RsaPssSignatureParameters.ReadVerifiableHash(parameters));
    }

    [TestMethod]
    [DataRow(-1, false)]
    [DataRow(0, true)]
    [DataRow(1, true)]
    public void IsUnexpiredAt_ListNamingANextUpdate_IsTrueUntilItPasses(int secondsLeft, bool expected)
    {
        var now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

        var list = CertificateRevocationList.Decode(TestRevocationList.Write(Authority, nextUpdate: now.AddSeconds(secondsLeft)));

        Assert.AreEqual(expected, list.IsUnexpiredAt(now));
    }

    [TestMethod]
    public void IsUnexpiredAt_ListNamingNoNextUpdate_IsTrue()
    {
        var list = CertificateRevocationList.Decode(TestRevocationList.Write(Authority));

        Assert.IsTrue(list.IsUnexpiredAt(DateTimeOffset.MaxValue));
    }

    private static byte[] PssList(byte[]? parameters, HashAlgorithmName hash) =>
        TestRevocationList.Write(
            Authority, signatureAlgorithm: TestRevocationList.RsaSsaPss, signatureParameters: parameters, signatureHash: hash);

    private static X509Certificate2 CreateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=PSS List Authority", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
