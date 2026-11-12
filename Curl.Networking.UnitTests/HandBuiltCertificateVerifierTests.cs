using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;
using Curl.Testing;
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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Verify_WithNoCertificateUnderInsecure_AcceptsIt()
    {
        var verifier = Verifier(new TlsClientOptions(Insecure: true));
        Diagnostics.Arrange("options", "Insecure: true");
        Diagnostics.Arrange("chain", "no certificates, host localhost");

        var verdict = verifier.Verify(new ServerCertificateChain([], "localhost", null));

        Diagnostics.Act("accepted", verdict.IsAccepted);
        Diagnostics.Act("observed verified", verifier.Observed.Verified);
        Diagnostics.Act("peer certificates", verifier.PeerCertificates.Length);
        Diagnostics.Assert("accepted", true, verdict.IsAccepted);
        Diagnostics.Assert("observed verified", false, verifier.Observed.Verified);
        Diagnostics.Assert("peer certificates", 0, verifier.PeerCertificates.Length);
        Assert.IsTrue(verdict.IsAccepted);
        Assert.IsFalse(verifier.Observed.Verified);
        Assert.IsEmpty(verifier.PeerCertificates);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Verify_WithNoCertificate_RejectsItWithExit60(bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("chain", "no certificates, host localhost");

        var verdict = Verifier(new TlsClientOptions(), matchesSchannelBuild).Verify(new ServerCertificateChain([], "localhost", null));

        Diagnostics.Act("accepted", verdict.IsAccepted);
        Diagnostics.Act("rejection", verdict.Rejection);
        Diagnostics.Assert("accepted", false, verdict.IsAccepted);
        Assert.IsFalse(verdict.IsAccepted);
        var exitCode = (((CurlExitCode ExitCode, string Message))verdict.Rejection!).ExitCode;
        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, exitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, (((CurlExitCode ExitCode, string Message))verdict.Rejection!).ExitCode);
    }

    [TestMethod]
    public void Verify_WhenTheServersOwnCertificateDoesNotParse_JudgesItAsNoCertificate()
    {
        var verifier = Verifier(new TlsClientOptions(Insecure: true));
        Diagnostics.Arrange("options", "Insecure: true");
        Diagnostics.Bytes("server certificate", [1, 2, 3]);

        var verdict = verifier.Verify(new ServerCertificateChain([[1, 2, 3]], "localhost", null));

        Diagnostics.Act("accepted", verdict.IsAccepted);
        Diagnostics.Act("peer certificates", verifier.PeerCertificates.Length);
        Diagnostics.Act("observed chain", verifier.Observed.Chain.Length);
        Diagnostics.Assert("accepted", true, verdict.IsAccepted);
        Diagnostics.Assert("peer certificates", 1, verifier.PeerCertificates.Length);
        Diagnostics.Assert("observed chain", 1, verifier.Observed.Chain.Length);
        Assert.IsTrue(verdict.IsAccepted);
        Assert.AreEqual(1, verifier.PeerCertificates.Length);
        Assert.AreEqual(1, verifier.Observed.Chain.Length);
    }

    [TestMethod]
    public void Verify_WhenTheServersOwnCertificateDoesNotParseBeforeAValidOne_JudgesItAsNoCertificate()
    {
        using var certificate = SelfSignedLocalhostCertificate();
        var verifier = Verifier(new TlsClientOptions(Insecure: true));
        var nothingSent = Verifier(new TlsClientOptions(Insecure: true));
        Diagnostics.Arrange("options", "Insecure: true");
        Diagnostics.Bytes("server certificate", [1, 2, 3]);
        Diagnostics.Arrange("second certificate", "self-signed P-256 CN=localhost");

        var verdict = verifier.Verify(new ServerCertificateChain([[1, 2, 3], certificate.RawData], "localhost", null));
        nothingSent.Verify(new ServerCertificateChain([], "localhost", null));

        Diagnostics.Act("accepted", verdict.IsAccepted);
        Diagnostics.Act("observed verify result", verifier.Observed.VerifyResult);
        Diagnostics.Act("verify result with nothing sent", nothingSent.Observed.VerifyResult);
        Diagnostics.Assert("accepted", true, verdict.IsAccepted);
        Diagnostics.Assert("observed verify result", nothingSent.Observed.VerifyResult, verifier.Observed.VerifyResult);
        Assert.IsTrue(verdict.IsAccepted);
        Assert.AreNotEqual(18L, verifier.Observed.VerifyResult);
        Assert.AreEqual(nothingSent.Observed.VerifyResult, verifier.Observed.VerifyResult);
    }

    [TestMethod]
    public void Verify_WhenAnotherCertificateDoesNotParse_LeavesItOutOfTheChain()
    {
        using var certificate = SelfSignedLocalhostCertificate();
        var verifier = Verifier(new TlsClientOptions(Insecure: true));
        Diagnostics.Arrange("options", "Insecure: true");
        Diagnostics.Arrange("server certificate", "self-signed P-256 CN=localhost");
        Diagnostics.Bytes("second certificate", [1, 2, 3]);

        var verdict = verifier.Verify(new ServerCertificateChain([certificate.RawData, [1, 2, 3]], "localhost", null));

        Diagnostics.Act("accepted", verdict.IsAccepted);
        Diagnostics.Act("peer certificates", verifier.PeerCertificates.Length);
        Diagnostics.Act("observed verify result", verifier.Observed.VerifyResult);
        Diagnostics.Assert("accepted", true, verdict.IsAccepted);
        Diagnostics.Assert("peer certificates", 2, verifier.PeerCertificates.Length);
        Diagnostics.Assert("observed verify result", 18L, verifier.Observed.VerifyResult);
        Assert.IsTrue(verdict.IsAccepted);
        Assert.AreEqual(2, verifier.PeerCertificates.Length);
        Assert.AreEqual(18L, verifier.Observed.VerifyResult);
    }

    [TestMethod]
    public void Verify_WithNullChain_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("chain", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => Verifier(new TlsClientOptions()).Verify(null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private static X509Certificate2 SelfSignedLocalhostCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    [TestMethod]
    [DataRow(true, "schannel: CertGetNameString() failed to match connection hostname (a.test) against server certificate names")]
    [DataRow(false, "SSL: no alternative certificate subject name matches target hostname 'a.test'")]
    public void Verify_WithASingleLabelWildcardCertificate_RejectsItWithExit60AndTheBuildsMessage(bool matchesSchannelBuild, string message)
    {
        using var certificate = CertificateNamed("*.test");
        Diagnostics.Arrange("certificate", "SAN DNS:*.test, host a.test");

        var verdict = Verifier(new TlsClientOptions(Insecure: true, ChecksHostNameWhenInsecure: true, CaCertificateFile: "ca.pem"), matchesSchannelBuild, "a.test")
            .Verify(new ServerCertificateChain([certificate.RawData], "a.test", null));

        var rejection = ((CurlExitCode ExitCode, string Message))verdict.Rejection!;
        Diagnostics.Act("rejection", rejection);
        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, rejection.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, rejection.ExitCode);
        Assert.AreEqual(message, rejection.Message);
    }

    [TestMethod]
    [DataRow("*.a.test", "x.a.test", true)]
    [DataRow("*.test", "b.a.test", false)]
    [DataRow("f*.test", "f.test", false)]
    [DataRow("*", "a.test", false)]
    [DataRow("a.test", "a.test", true)]
    public void Verify_WithWildcardPatterns_AcceptsOnlyWhatCurlMatches(string pattern, string host, bool accepted)
    {
        using var certificate = CertificateNamed(pattern);
        Diagnostics.Arrange("certificate", $"SAN DNS:{pattern}, host {host}");

        var verdict = Verifier(new TlsClientOptions(Insecure: true, ChecksHostNameWhenInsecure: true), false, host)
            .Verify(new ServerCertificateChain([certificate.RawData], host, null));

        Diagnostics.Assert("accepted", accepted, verdict.IsAccepted);
        Assert.AreEqual(accepted, verdict.IsAccepted);
    }

    [TestMethod]
    public void Verify_WithAnIpAddressTarget_KeepsDotNetsAnswer()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=x", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(System.Net.IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var verdict = Verifier(new TlsClientOptions(Insecure: true, ChecksHostNameWhenInsecure: true), false, "127.0.0.1")
            .Verify(new ServerCertificateChain([certificate.RawData], "127.0.0.1", null));

        Assert.IsTrue(verdict.IsAccepted);
    }

    private static X509Certificate2 CertificateNamed(string dnsName)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=x", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(dnsName);
        request.CertificateExtensions.Add(names.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static HandBuiltCertificateVerifier Verifier(TlsClientOptions options, bool matchesSchannelBuild = false, string host = "localhost") =>
        new(new ServerCertificateVerification(options, matchesSchannelBuild, TimeProvider.System), null, [], null, host);
}
