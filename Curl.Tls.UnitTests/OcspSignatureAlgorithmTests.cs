using System.Numerics;
using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Checks that <see cref="OcspSignatureAlgorithm" /> reads all of <c>RSASSA-PSS-params</c>, so a
/// signature is verified with the MGF1 hash and salt length it states (STD-217).
/// </summary>
[TestClass]
public sealed class OcspSignatureAlgorithmTests
{
    private const string Sha1Oid = "1.3.14.3.2.26";
    private const string Sha384Oid = "2.16.840.1.101.3.4.2.2";
    private const string UnknownOid = "2.16.840.1.101.3.4.2.99";

    private static readonly byte[] Content = [1, 2, 3, 4];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(64, OcspResponseBuilder.Sha256Oid, "SHA256")]
    [DataRow(32, OcspResponseBuilder.Sha256Oid, "SHA256")]
    [DataRow(0, OcspResponseBuilder.Sha256Oid, "SHA256")]
    [DataRow(32, Sha384Oid, "SHA384")]
    public void FindRule_PssStatingSaltAndMaskHash_VerifiesTheSignature(int saltLength, string maskHashOid, string maskHashName)
    {
        using RSA rsa = RSA.Create(2048);
        byte[] algorithm = OcspResponseBuilder.PssAlgorithmIdentifier(OcspResponseBuilder.Sha256Oid, maskHashOid, saltLength, 1);
        byte[] signature = TestRsaPssSignature.Sign(rsa, Content, HashAlgorithmName.SHA256, new HashAlgorithmName(maskHashName), saltLength);
        Diagnostics.Arrange("salt length and MGF1 hash", $"{saltLength}, {maskHashName}");

        TlsAlertDescription? alert = Verify(rsa, algorithm, signature);

        Diagnostics.Assert("alert", "null", alert?.ToString() ?? "null");
        Assert.IsNull(alert);
    }

    [TestMethod]
    public void FindRule_PssWithEmptyParameters_TakesSha1AndA20ByteSalt()
    {
        using RSA rsa = RSA.Create(2048);
        byte[] algorithm = OcspResponseBuilder.PssAlgorithmIdentifier(null);
        byte[] signature = TestRsaPssSignature.Sign(rsa, Content, HashAlgorithmName.SHA1, HashAlgorithmName.SHA1, 20);
        Diagnostics.Arrange("parameters", "empty");

        TlsAlertDescription? alert = Verify(rsa, algorithm, signature);

        Diagnostics.Assert("alert", "null", alert?.ToString() ?? "null");
        Assert.IsNull(alert);
    }

    [TestMethod]
    [DataRow(32, OcspResponseBuilder.Sha256Oid)]
    [DataRow(64, Sha1Oid)]
    public void FindRule_PssSignedOtherThanStated_FailsTheSignature(int statedSaltLength, string statedMaskHashOid)
    {
        using RSA rsa = RSA.Create(2048);
        byte[] algorithm = OcspResponseBuilder.PssAlgorithmIdentifier(OcspResponseBuilder.Sha256Oid, statedMaskHashOid, statedSaltLength);
        byte[] signature = TestRsaPssSignature.Sign(rsa, Content, HashAlgorithmName.SHA256, HashAlgorithmName.SHA256, 64);
        Diagnostics.Arrange("stated salt length and MGF1 OID", $"{statedSaltLength}, {statedMaskHashOid}");

        TlsAlertDescription? alert = Verify(rsa, algorithm, signature);

        Diagnostics.Assert("alert", TlsAlertDescription.DecryptError, alert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, alert);
    }

    [TestMethod]
    [DataRow(UnknownOid, OcspResponseBuilder.Mgf1Oid, "32", 1)]
    [DataRow(OcspResponseBuilder.Sha256Oid, UnknownOid, "32", 1)]
    [DataRow(OcspResponseBuilder.Sha256Oid, OcspResponseBuilder.Mgf1Oid, "-1", 1)]
    [DataRow(OcspResponseBuilder.Sha256Oid, OcspResponseBuilder.Mgf1Oid, "2147483648", 1)]
    [DataRow(OcspResponseBuilder.Sha256Oid, OcspResponseBuilder.Mgf1Oid, "32", 2)]
    public void FindRule_PssTheClientCannotCheck_IsNull(string maskHashOrUnknownOid, string maskOid, string saltLength, int trailerField)
    {
        using RSA rsa = RSA.Create(2048);
        byte[] algorithm = OcspResponseBuilder.PssAlgorithmIdentifier(
            OcspResponseBuilder.Sha256Oid, maskHashOrUnknownOid, BigInteger.Parse(saltLength, System.Globalization.CultureInfo.InvariantCulture), trailerField, maskOid);
        Diagnostics.Bytes("algorithm identifier", algorithm);

        TlsSignatureRule? rule = OcspSignatureAlgorithm.FindRule(algorithm, KeyOf(rsa));

        Diagnostics.Assert("rule", "null", rule?.ToString() ?? "null");
        Assert.IsNull(rule);
    }

    private static TlsCertificatePublicKey KeyOf(RSA rsa) => TlsCertificatePublicKey.ReadSubjectPublicKeyInfo(rsa.ExportSubjectPublicKeyInfo());

    private static TlsAlertDescription? Verify(RSA rsa, byte[] algorithm, byte[] signature)
    {
        TlsCertificatePublicKey key = KeyOf(rsa);
        return key.VerifySignature(OcspSignatureAlgorithm.FindRule(algorithm, key), Content, signature);
    }
}
