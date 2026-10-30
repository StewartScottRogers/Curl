using System.Security.Cryptography;

namespace Curl.Extension.Sigstore.UnitTests;

[TestClass]
public sealed class SigningCertificateIdentityTests
{
    private readonly SigstoreFixture fulcio = new();

    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    [TestCleanup]
    public void CleanUp()
    {
        fulcio.Dispose();
        key.Dispose();
    }

    [TestMethod]
    public void NamesOf_EmailUriAndDnsNames_GivesTheEmailAndUriOnly_FulcioSubjectAlternativeName()
    {
        using var certificate = fulcio.Issue(key, names =>
        {
            names.AddDnsName("example.com");
            names.AddEmailAddress(SigstoreFixture.Email);
            names.AddUri(new Uri(SigstoreFixture.Workflow));
        });

        CollectionAssert.AreEqual(new[] { SigstoreFixture.Email, SigstoreFixture.Workflow }, SigningCertificateIdentity.NamesOf(certificate).ToArray());
    }

    [TestMethod]
    public void NamesOf_NoSubjectAlternativeName_GivesNone()
    {
        using var certificate = fulcio.Issue(key, withSubjectAlternativeName: false);

        Assert.IsEmpty(SigningCertificateIdentity.NamesOf(certificate));
    }

    [TestMethod]
    public void IssuerOf_TheV2Extension_GivesItsUtf8String_FulcioOid1_8()
    {
        using var certificate = fulcio.Issue(key, issuerV1: "https://legacy.example.com");

        Assert.AreEqual(SigstoreFixture.Issuer, SigningCertificateIdentity.IssuerOf(certificate));
    }

    [TestMethod]
    public void IssuerOf_OnlyTheDeprecatedV1Extension_GivesItsRawBytes_FulcioOid1_1()
    {
        using var certificate = fulcio.Issue(key, issuerV2: null, issuerV1: SigstoreFixture.Issuer);

        Assert.AreEqual(SigstoreFixture.Issuer, SigningCertificateIdentity.IssuerOf(certificate));
    }

    [TestMethod]
    public void IssuerOf_NoIssuerExtension_GivesNone()
    {
        using var certificate = fulcio.Issue(key, issuerV2: null);

        Assert.IsNull(SigningCertificateIdentity.IssuerOf(certificate));
    }
}
