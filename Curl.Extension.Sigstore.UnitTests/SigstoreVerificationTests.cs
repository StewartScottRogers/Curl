using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Extension.Sigstore.UnitTests;

[TestClass]
public sealed class SigstoreVerificationTests
{
    private static readonly byte[] Artifact = Encoding.ASCII.GetBytes("artifact");

    private readonly SigstoreFixture fulcio = new();

    private readonly ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    [TestCleanup]
    public void CleanUp()
    {
        fulcio.Dispose();
        signer.Dispose();
    }

    [TestMethod]
    public void FailureOf_ASignatureByTheIdentityUnderTheRoot_IsNone_SigstoreClientSpecVerification()
    {
        using var certificate = fulcio.Issue(signer);

        Assert.IsNull(Verify(Bundle(certificate), SigstoreFixture.Email, SigstoreFixture.Issuer));
        Assert.IsNull(Verify(Bundle(certificate), SigstoreFixture.Email, null));
    }

    [TestMethod]
    public void FailureOf_AnIssuedCertificateBesideTheRoot_IsTakenAsAnIntermediateAndStillVerifies()
    {
        using var certificate = fulcio.Issue(signer);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var intermediate = fulcio.Issue(other);

        Assert.IsNull(SigstoreVerification.FailureOf(Artifact, Bundle(certificate), [fulcio.Root, intermediate], SigstoreFixture.Email, null));
    }

    [TestMethod]
    public void FailureOf_AnotherDigestAlgorithm_Fails()
    {
        using var certificate = fulcio.Issue(signer);

        StringAssert.Contains(Verify(Bundle(certificate, algorithm: "SHA2_384"), SigstoreFixture.Email, null), "SHA-256");
    }

    [TestMethod]
    public void FailureOf_ACertificateFromAnotherRoot_Fails_FulcioChain()
    {
        using SigstoreFixture other = new();
        using var certificate = other.Issue(signer);

        StringAssert.Contains(Verify(Bundle(certificate), SigstoreFixture.Email, null), "chain");
    }

    [TestMethod]
    public void FailureOf_LoggedAfterTheCertificateExpired_Fails_FulcioShortLivedCertificates()
    {
        using var certificate = fulcio.Issue(signer);
        var late = SigstoreBundle.Parse(Encoding.UTF8.GetBytes(SigstoreFixture.Bundle(certificate, Artifact, SigstoreFixture.Sign(signer, Artifact), integratedTime: "1790857000")))!;

        StringAssert.Contains(Verify(late, SigstoreFixture.Email, null), "chain");
    }

    [TestMethod]
    public void FailureOf_AnUnreadableCertificate_Fails()
    {
        var bundle = new SigstoreBundle([1, 2, 3], "SHA2_256", SHA256.HashData(Artifact), [], SigstoreFixture.IssuedAt);

        StringAssert.Contains(Verify(bundle, SigstoreFixture.Email, null), "chain");
    }

    [TestMethod]
    public void FailureOf_ASignatureByAnotherKey_Fails()
    {
        using var certificate = fulcio.Issue(signer);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        StringAssert.Contains(Verify(Bundle(certificate, SigstoreFixture.Sign(other, Artifact)), SigstoreFixture.Email, null), "signature");
    }

    [TestMethod]
    public void FailureOf_ACertificateWithAnRsaKey_Fails()
    {
        using var rsa = RSA.Create(2048);
        using var certificate = fulcio.Issue(rsa);

        StringAssert.Contains(Verify(Bundle(certificate), SigstoreFixture.Email, null), "signature");
    }

    [TestMethod]
    public void FailureOf_AnotherIssuer_Fails_FulcioIssuerExtension()
    {
        using var certificate = fulcio.Issue(signer);

        StringAssert.Contains(Verify(Bundle(certificate), SigstoreFixture.Email, "https://other.example.com"), "issuer");
    }

    private SigstoreBundle Bundle(X509Certificate2 certificate, byte[]? signature = null, string algorithm = "SHA2_256") =>
        SigstoreBundle.Parse(Encoding.UTF8.GetBytes(SigstoreFixture.Bundle(certificate, Artifact, signature ?? SigstoreFixture.Sign(signer, Artifact), algorithm: algorithm)))!;

    private string? Verify(SigstoreBundle bundle, string identity, string? issuer) =>
        SigstoreVerification.FailureOf(Artifact, bundle, [fulcio.Root], identity, issuer);
}
