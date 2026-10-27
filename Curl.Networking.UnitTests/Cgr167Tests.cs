using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// CGR-167 (BL-3696, ADR-0862): under <c>-k</c> curl's OpenSSL build loads no trust store, so
/// <c>--cert-status</c>'s <c>OCSP_basic_verify</c> trusts no responder and every stapled
/// response that reads as successful, good, revoked, for another certificate or with its issuer
/// missing, is exit 91 <c>OCSP response verification failed</c>; a missing, unreadable or
/// unsuccessful response keeps its own message (measured with curl 8.18.0 on OpenSSL 3.5.5).
/// </summary>
[TestClass]
public sealed class Cgr167Tests
{
    private static readonly object VerificationFailed = (CurlExitCode.SslInvalidCertStatus, "OCSP response verification failed");

    [TestMethod]
    [DataRow(OcspStapleStatus.Good, DisplayName = "a good staple")]
    [DataRow(OcspStapleStatus.Revoked, DisplayName = "a revoked staple")]
    [DataRow(OcspStapleStatus.Unknown, DisplayName = "an unknown staple")]
    public void Verify_UnderInsecureWithAReadableStaple_RefusesItAsUnverifiable(OcspStapleStatus status)
    {
        using var pki = new OcspTestPki();
        var staple = (pki.Response(DateTimeOffset.UtcNow) with { CertStatus = status, RevocationReason = 1 }).Build();

        var verdict = InsecureVerifier(TimeProvider.System).Verify(new ServerCertificateChain(pki.Chain, "localhost", staple));

        Assert.AreEqual(VerificationFailed, verdict.Rejection);
        Assert.AreEqual(TlsAlertDescription.BadCertificateStatusResponse, verdict.Alert);
    }

    [TestMethod]
    public void Verify_UnderInsecureWithAStapleForAnotherCertificate_RefusesItAsUnverifiable()
    {
        using var pki = new OcspTestPki();
        var staple = (pki.Response(DateTimeOffset.UtcNow) with { SerialNumber = [0x7f] }).Build();

        var verdict = InsecureVerifier(TimeProvider.System).Verify(new ServerCertificateChain(pki.Chain, "localhost", staple));

        Assert.AreEqual(VerificationFailed, verdict.Rejection);
    }

    [TestMethod]
    public void Verify_UnderInsecureWithTheIssuerMissingFromTheChain_RefusesItAsUnverifiable()
    {
        using var pki = new OcspTestPki();
        var staple = pki.Response(DateTimeOffset.UtcNow).Build();

        var verdict = InsecureVerifier(TimeProvider.System).Verify(new ServerCertificateChain([pki.Leaf.RawData], "localhost", staple));

        Assert.AreEqual(VerificationFailed, verdict.Rejection);
    }

    [TestMethod]
    public void Verify_UnderInsecureWithNoStaple_LeavesItToTheStatusCheck()
    {
        using var pki = new OcspTestPki();

        var verdict = InsecureVerifier(TimeProvider.System).Verify(new ServerCertificateChain(pki.Chain, "localhost", null));

        Assert.IsTrue(verdict.IsAccepted);
    }

    [TestMethod]
    public void Verify_UnderInsecureWithAnUnsuccessfulResponse_LeavesItToTheStatusCheck()
    {
        using var pki = new OcspTestPki();
        var staple = (pki.Response(DateTimeOffset.UtcNow) with { ResponseStatus = 3, OmitResponseBytes = true }).Build();

        var verdict = InsecureVerifier(TimeProvider.System).Verify(new ServerCertificateChain(pki.Chain, "localhost", staple));

        Assert.IsTrue(verdict.IsAccepted);
    }

    [TestMethod]
    public void Verify_UnderInsecureWhenTheServerSentNoCertificate_AcceptsAsInsecureDoes()
    {
        var verdict = InsecureVerifier(TimeProvider.System).Verify(new ServerCertificateChain([], "localhost", [0x30, 0x00]));

        Assert.IsTrue(verdict.IsAccepted);
    }

    [TestMethod]
    public void Verify_WithoutTheUntrustedStapleClock_AcceptsAGoodStaple()
    {
        using var pki = new OcspTestPki();
        var staple = pki.Response(DateTimeOffset.UtcNow).Build();

        var verdict = InsecureVerifier(null).Verify(new ServerCertificateChain(pki.Chain, "localhost", staple));

        Assert.IsTrue(verdict.IsAccepted);
    }

    private static HandBuiltCertificateVerifier InsecureVerifier(TimeProvider? untrustedStapleClock) =>
        new(new ServerCertificateVerification(new TlsClientOptions(Insecure: true, RequireCertificateStatus: true), false, TimeProvider.System), null, [], null, "localhost", untrustedStapleClock);
}
