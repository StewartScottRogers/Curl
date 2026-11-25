using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// Pins which RSASSA-PSS parameters verify a revocation list's signature (STD-197), and when a
/// list counts as unexpired. Measured with lists <c>openssl ca -gencrl</c> signed, 2026-11-24
/// (BL-5929, ADR-1376): curl 8.22.0 Schannel and curl 8.18.0 on OpenSSL 3.5.5 verify any salt
/// length the parameters name and fail a signature whose salt is another length; MGF1 over
/// another hash than the message's verifies in the OpenSSL build alone.
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
    [DataRow(0, DisplayName = "no salt")]
    [DataRow(20, DisplayName = "shorter than the hash")]
    [DataRow(222, DisplayName = "the longest an RSA 2048 key admits")]
    public void IsSignedBy_PssWithASaltNotAsLongAsTheHash_IsTrueInBothBuilds(int saltLength)
    {
        var parameters = TestRevocationList.PssParameters(TestRevocationList.Sha256, TestRevocationList.Sha256, saltLength);

        var list = CertificateRevocationList.Decode(PssList(parameters, HashAlgorithmName.SHA256, saltLength));

        Assert.IsTrue(list.IsSignedBy(Authority));
        Assert.IsTrue(list.IsSignedBy(Authority, anotherMaskHashVerifies: true));
    }

    [TestMethod]
    public void IsSignedBy_PssWithASaltOtherThanTheOneNamed_IsFalseInBothBuilds()
    {
        var parameters = TestRevocationList.PssParameters(TestRevocationList.Sha256, TestRevocationList.Sha256, 221);

        var list = CertificateRevocationList.Decode(PssList(parameters, HashAlgorithmName.SHA256, 222));

        Assert.IsFalse(list.IsSignedBy(Authority));
        Assert.IsFalse(list.IsSignedBy(Authority, anotherMaskHashVerifies: true));
    }

    [TestMethod]
    [DataRow(TestRevocationList.Sha1, "SHA1", 32)]
    [DataRow(TestRevocationList.Sha384, "SHA384", 20)]
    public void IsSignedBy_PssWithAnotherMaskHash_IsTrueInTheOpenSslBuildAlone(string maskOid, string maskName, int saltLength)
    {
        var parameters = TestRevocationList.PssParameters(TestRevocationList.Sha256, maskOid, saltLength);

        var list = CertificateRevocationList.Decode(PssList(parameters, HashAlgorithmName.SHA256, saltLength, new(maskName)));

        Assert.IsFalse(list.IsSignedBy(Authority));
        Assert.IsTrue(list.IsSignedBy(Authority, anotherMaskHashVerifies: true));
    }

    [TestMethod]
    public void IsSignedBy_PssListUnderAKeyThatIsNotRsa_IsFalse()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = new CertificateRequest("CN=PSS List Authority", key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var list = CertificateRevocationList.Decode(PssList(TestRevocationList.PssParameters(), HashAlgorithmName.SHA1));

        Assert.IsFalse(list.IsSignedBy(other));
    }

    [TestMethod]
    public void Read_ExplicitTrailerFieldOne_IsTheParameters()
    {
        var parameters = TestRevocationList.PssParameters(TestRevocationList.Sha256, TestRevocationList.Sha1, 222, trailerField: 1);

        Assert.AreEqual(
            new RsaPssSignatureParameters(HashAlgorithmName.SHA256, HashAlgorithmName.SHA1, 222),
            RsaPssSignatureParameters.Read(parameters));
    }

    [TestMethod]
    [DataRow("1.2.840.113549.2.5", TestRevocationList.Sha256, 16L, 1, TestRevocationList.Mgf1, DisplayName = "MD5")]
    [DataRow(TestRevocationList.Sha256, "1.2.840.113549.2.5", 32L, 1, TestRevocationList.Mgf1, DisplayName = "MGF1 over MD5")]
    [DataRow(TestRevocationList.Sha256, TestRevocationList.Sha256, -1L, 1, TestRevocationList.Mgf1, DisplayName = "negative salt length")]
    [DataRow(TestRevocationList.Sha256, TestRevocationList.Sha256, 2147483648L, 1, TestRevocationList.Mgf1, DisplayName = "salt length past int")]
    [DataRow(TestRevocationList.Sha256, TestRevocationList.Sha256, 32L, 2, TestRevocationList.Mgf1, DisplayName = "trailer field 2")]
    [DataRow(TestRevocationList.Sha256, TestRevocationList.Sha256, 32L, 1, "1.2.3", DisplayName = "not MGF1")]
    public void Read_ParametersNoBuildVerifies_IsNull(
        string hash, string maskHash, long saltLength, int trailerField, string maskFunction)
    {
        var parameters = TestRevocationList.PssParameters(hash, maskHash, saltLength, trailerField, maskFunction);

        Assert.IsNull(RsaPssSignatureParameters.Read(parameters));
    }

    [TestMethod]
    [DataRow(new byte[0], DisplayName = "empty")]
    [DataRow(new byte[] { 0x05, 0x00 }, DisplayName = "NULL")]
    [DataRow(new byte[] { 0x30, 0x02, 0x05, 0x00 }, DisplayName = "an unknown field")]
    public void Read_MalformedParameters_IsNull(byte[] parameters)
    {
        Assert.IsNull(RsaPssSignatureParameters.Read(parameters));
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

    private static byte[] PssList(byte[]? parameters, HashAlgorithmName hash, int? saltLength = null, HashAlgorithmName? maskHash = null) =>
        TestRevocationList.Write(
            Authority,
            signatureAlgorithm: TestRevocationList.RsaSsaPss,
            signatureParameters: parameters,
            signatureHash: hash,
            pssSaltLength: saltLength,
            pssMaskHash: maskHash);

    private static X509Certificate2 CreateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=PSS List Authority", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
