using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;
using Curl.Tls;

using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins what <see cref="HandBuiltCertificateVerifier" /> makes of chains a handshake test
/// cannot send: none at all, and certificates that do not parse.
/// </summary>
[TestClass]
public sealed class HandBuiltCertificateVerifierTests
{
    [TestMethod]
    public void Verify_WithNoCertificateUnderInsecure_AcceptsIt()
    {
        var verifier = Verifier(new TlsClientOptions(Insecure: true));

        var verdict = verifier.Verify(new ServerCertificateChain([], "localhost", null));

        Assert.IsTrue(verdict.IsAccepted);
        Assert.IsFalse(verifier.Observed.Verified);
        Assert.IsEmpty(verifier.PeerCertificates);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Verify_WithNoCertificate_RejectsItWithExit60(bool matchesSchannelBuild)
    {
        var verdict = Verifier(new TlsClientOptions(), matchesSchannelBuild).Verify(new ServerCertificateChain([], "localhost", null));

        Assert.IsFalse(verdict.IsAccepted);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, (((CurlExitCode ExitCode, string Message))verdict.Rejection!).ExitCode);
    }

    [TestMethod]
    public void Verify_WhenTheServersOwnCertificateDoesNotParse_JudgesItAsNoCertificate()
    {
        var verifier = Verifier(new TlsClientOptions(Insecure: true));

        var verdict = verifier.Verify(new ServerCertificateChain([[1, 2, 3]], "localhost", null));

        Assert.IsTrue(verdict.IsAccepted);
        Assert.AreEqual(1, verifier.PeerCertificates.Length);
        Assert.AreEqual(1, verifier.Observed.Chain.Length);
    }

    [TestMethod]
    public void Verify_WhenAnotherCertificateDoesNotParse_LeavesItOutOfTheChain()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var verifier = Verifier(new TlsClientOptions(Insecure: true));

        var verdict = verifier.Verify(new ServerCertificateChain([certificate.RawData, [1, 2, 3]], "localhost", null));

        Assert.IsTrue(verdict.IsAccepted);
        Assert.AreEqual(2, verifier.PeerCertificates.Length);
        Assert.AreEqual(18L, verifier.Observed.VerifyResult);
    }

    [TestMethod]
    public void Verify_WithNullChain_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Verifier(new TlsClientOptions()).Verify(null!));

    private static HandBuiltCertificateVerifier Verifier(TlsClientOptions options, bool matchesSchannelBuild = false) =>
        new(new ServerCertificateVerification(options, matchesSchannelBuild, TimeProvider.System), null, [], "localhost");
}
