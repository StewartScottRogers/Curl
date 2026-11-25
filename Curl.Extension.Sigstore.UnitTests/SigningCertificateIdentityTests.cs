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
    public void NamesInSubjectAlternativeName_ADerSequence_GivesItsNames()
    {
        var names = SigningCertificateIdentity.NamesInSubjectAlternativeName(Convert.FromHexString("300681046140622E"));

        CollectionAssert.AreEqual(new[] { "a@b." }, names.ToArray());
    }

    [TestMethod]
    [DataRow("6140622E", DisplayName = "raw bytes")]
    [DataRow("300681046140", DisplayName = "a truncated sequence")]
    [DataRow("300681046140622E00", DisplayName = "a sequence with a trailing byte")]
    [DataRow("300981046140622E8101FF", DisplayName = "a name that is not IA5 after one that is")]
    [DataRow("", DisplayName = "nothing")]
    public void NamesInSubjectAlternativeName_AValueThatIsNotThatDer_GivesNone(string value)
    {
        var names = SigningCertificateIdentity.NamesInSubjectAlternativeName(Convert.FromHexString(value));

        Assert.IsEmpty(names);
    }

    [TestMethod]
    public void IssuerOf_NoIssuerExtension_GivesNone()
    {
        using var certificate = fulcio.Issue(key, issuerV2: null);

        Assert.IsNull(SigningCertificateIdentity.IssuerOf(certificate));
    }

    [TestMethod]
    [DataRow("68747470733A2F2F6973737565722E6578616D706C65", DisplayName = "the issuer's raw bytes")]
    [DataRow("160161", DisplayName = "an IA5String")]
    [DataRow("0C0261", DisplayName = "a truncated UTF8String")]
    [DataRow("0C0161FF", DisplayName = "a UTF8String with a trailing byte")]
    [DataRow("0C01FF", DisplayName = "a UTF8String that is not UTF-8")]
    [DataRow("", DisplayName = "nothing")]
    public void IssuerOf_AV2ExtensionThatIsNotADerUtf8String_GivesNone_EvenBesideAV1Extension(string extension)
    {
        using var certificate = fulcio.Issue(key, issuerV2: null, issuerV1: SigstoreFixture.Issuer, undecodedIssuerV2: Convert.FromHexString(extension));

        Assert.IsNull(SigningCertificateIdentity.IssuerOf(certificate));
    }
}
