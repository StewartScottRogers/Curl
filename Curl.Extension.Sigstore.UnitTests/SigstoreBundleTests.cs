using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.Sigstore.UnitTests;

[TestClass]
public sealed class SigstoreBundleTests
{
    private static readonly byte[] Artifact = Encoding.ASCII.GetBytes("artifact");

    [TestMethod]
    public void Parse_AV03Bundle_ReadsTheCertificateDigestSignatureAndIntegratedTime_BundleSpecV03()
    {
        using SigstoreFixture fulcio = new();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = fulcio.Issue(key);

        var bundle = SigstoreBundle.Parse(Encoding.UTF8.GetBytes(SigstoreFixture.Bundle(certificate, Artifact, [9, 8, 7])))!;

        CollectionAssert.AreEqual(certificate.RawData, bundle.Certificate);
        Assert.AreEqual("SHA2_256", bundle.DigestAlgorithm);
        CollectionAssert.AreEqual(SHA256.HashData(Artifact), bundle.Digest);
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, bundle.Signature);
        Assert.AreEqual(SigstoreFixture.IssuedAt.AddMinutes(5), bundle.IntegratedTime);
        Assert.IsTrue(bundle.DigestMatches(Artifact));
        Assert.IsFalse(bundle.DigestMatches([1]));
    }

    [TestMethod]
    public void Parse_AV02BundleWithACertificateChainAndANumericTime_ReadsTheFirstCertificate_BundleSpecV02()
    {
        using SigstoreFixture fulcio = new();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = fulcio.Issue(key);

        var bundle = SigstoreBundle.Parse(Encoding.UTF8.GetBytes(SigstoreFixture.Bundle(certificate, Artifact, [1], "v0.2", integratedTime: "1790856300")))!;

        CollectionAssert.AreEqual(certificate.RawData, bundle.Certificate);
        Assert.AreEqual(SigstoreFixture.IssuedAt.AddMinutes(5), bundle.IntegratedTime);
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("{\"mediaType\": \"application/json\"}")]
    [DataRow("{\"mediaType\": null}")]
    [DataRow("{\"mediaType\": \"application/vnd.dev.sigstore.bundle.v0.3+json\"}")]
    [DataRow("{\"mediaType\": \"application/vnd.dev.sigstore.bundle.v0.3+json\", \"verificationMaterial\": {\"certificate\": {\"rawBytes\": \"AA==\"}, \"tlogEntries\": []}, \"messageSignature\": {\"messageDigest\": {\"algorithm\": null, \"digest\": \"AA==\"}, \"signature\": \"AA==\"}}")]
    [DataRow("{\"mediaType\": \"application/vnd.dev.sigstore.bundle.v0.3+json\", \"verificationMaterial\": {\"certificate\": {\"rawBytes\": \"AA==\"}, \"tlogEntries\": [{\"integratedTime\": \"-1\"}]}, \"messageSignature\": {\"messageDigest\": {\"algorithm\": \"SHA2_256\", \"digest\": \"AA==\"}, \"signature\": \"AA==\"}}")]
    [DataRow("{\"mediaType\": \"application/vnd.dev.sigstore.bundle.v0.3+json\", \"verificationMaterial\": {\"certificate\": {\"rawBytes\": \"AA==\"}, \"tlogEntries\": [{\"integratedTime\": \"99999999999999999999\"}]}, \"messageSignature\": {\"messageDigest\": {\"algorithm\": \"SHA2_256\", \"digest\": \"AA==\"}, \"signature\": \"AA==\"}}")]
    [DataRow("{\"mediaType\": \"application/vnd.dev.sigstore.bundle.v0.3+json\", \"verificationMaterial\": {\"certificate\": {\"rawBytes\": \"AA==\"}, \"tlogEntries\": [{\"integratedTime\": 999999999999999}]}, \"messageSignature\": {\"messageDigest\": {\"algorithm\": \"SHA2_256\", \"digest\": \"AA==\"}, \"signature\": \"AA==\"}}")]
    [DataRow("{\"mediaType\": \"application/vnd.dev.sigstore.bundle.v0.3+json\", \"verificationMaterial\": {\"certificate\": {\"rawBytes\": \"!!\"}, \"tlogEntries\": []}, \"messageSignature\": {\"messageDigest\": {\"algorithm\": \"SHA2_256\", \"digest\": \"AA==\"}, \"signature\": \"AA==\"}}")]
    public void Parse_NotABundleWithAMessageSignature_GivesNone(string json)
    {
        Assert.IsNull(SigstoreBundle.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [TestMethod]
    public void Parse_ADsseEnvelopeBundle_GivesNone()
    {
        const string json = "{\"mediaType\": \"application/vnd.dev.sigstore.bundle.v0.3+json\", \"verificationMaterial\": {\"certificate\": {\"rawBytes\": \"AA==\"}, \"tlogEntries\": [{\"integratedTime\": \"1\"}]}, \"dsseEnvelope\": {}}";

        Assert.IsNull(SigstoreBundle.Parse(Encoding.UTF8.GetBytes(json)));
    }
}
