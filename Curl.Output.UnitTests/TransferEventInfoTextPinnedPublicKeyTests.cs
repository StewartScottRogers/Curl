using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins where each build prints <c>--pinnedpubkey</c>'s <c> public key hash:</c> line (BL-877):
/// curl 8.21.0's Schannel build between the two ALPN lines, measured 2026-10-01 with
/// <c>Record-CurlExchange.ps1 -Tls -TlsPublicKeyFile</c> and <c>-v -k --pinnedpubkey sha256//...</c>,
/// and curl 8.18.0's OpenSSL build after <c> SSL certificate verification failed, continuing
/// anyway!</c>, measured in BL-608.
/// </summary>
[TestClass]
public sealed class TransferEventInfoTextPinnedPublicKeyTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Hash = "sha256//7VmZsXC/uSi8KudeBgyot4QIl/fLiETstYonpMQATl8=";

    [TestMethod]
    public void TlsHandshake_SchannelWithAHashPin_PrintsTheHashBetweenTheAlpnLines()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "Schannel, ALPN http/1.1, pinned hash");
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, ["http/1.1"]) with { PinnedPublicKeyHash = Hash }, TlsBackend.Schannel);
        diagnostics.Act("lines", string.Join("\n", lines));

        string[] expected1 = new[]
            {
                "ALPN: curl offers http/1.1",
                " public key hash: " + Hash,
                "ALPN: server did not agree on a protocol. Uses default.",
            };
        string[] actual1 = lines.ToArray();
        diagnostics.Diff("lines", string.Join("\n", expected1), string.Join("\n", actual1));
        CollectionAssert.AreEqual(expected1, actual1);
    }

    [TestMethod]
    public void TlsHandshake_SchannelWithAHashPinAndNoAlpn_PrintsOnlyTheHash()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "Schannel, no ALPN, pinned hash");
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, []) with { PinnedPublicKeyHash = Hash }, TlsBackend.Schannel);
        diagnostics.Act("lines", string.Join("\n", lines));

        string[] expected2 = new[] { " public key hash: " + Hash };
        string[] actual2 = lines.ToArray();
        diagnostics.Diff("lines", string.Join("\n", expected2), string.Join("\n", actual2));
        CollectionAssert.AreEqual(expected2, actual2);
    }

    [TestMethod]
    public void TlsHandshake_SchannelWithoutAHashPin_PrintsOnlyTheAlpnLines()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "Schannel, ALPN http/1.1, no pin");
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, ["http/1.1"]), TlsBackend.Schannel);
        diagnostics.Act("lines", string.Join("\n", lines));

        diagnostics.Assert("line count", 2, lines.Count);
        Assert.HasCount(2, lines);
    }

    [TestMethod]
    public void TlsHandshake_OpenSslWithAHashPin_PrintsTheHashAfterTheVerifyResult()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL, self-signed CN=localhost, ALPN http/1.1, pinned hash");
        using var certificate = SelfSigned();

        var lines = TransferEventInfoText.TlsHandshake(Handshake(certificate, ["http/1.1"]) with { PinnedPublicKeyHash = Hash }, TlsBackend.OpenSsl);
        diagnostics.Act("lines", string.Join("\n", lines));

        string[] expected3 = new[] { " SSL certificate verification failed, continuing anyway!", " public key hash: " + Hash };
        string[] actual3 = lines.TakeLast(2).ToArray();
        diagnostics.Diff("lines", string.Join("\n", expected3), string.Join("\n", actual3));
        CollectionAssert.AreEqual(expected3, actual3);
    }

    [TestMethod]
    public void TlsHandshake_OpenSslWithoutAHashPin_EndsWithTheVerifyResult()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL, self-signed CN=localhost, ALPN http/1.1, no pin");
        using var certificate = SelfSigned();

        var lines = TransferEventInfoText.TlsHandshake(Handshake(certificate, ["http/1.1"]), TlsBackend.OpenSsl);
        diagnostics.Act("lines", string.Join("\n", lines));

        diagnostics.Assert("line", " SSL certificate verification failed, continuing anyway!", lines[^1]);
        Assert.AreEqual(" SSL certificate verification failed, continuing anyway!", lines[^1]);
    }

    [TestMethod]
    public void TlsHandshake_SchannelFailedWithAHashPin_PrintsTheAlpnOfferThenTheHash()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "Schannel, failed, ALPN http/1.1, pinned hash");
        // curl 8.21.0 Schannel -v -k --pinnedpubkey sha256//<wrong>, measured 2026-10-02 (BL-1149).
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, ["http/1.1"]) with { PinnedPublicKeyHash = Hash, Failed = true }, TlsBackend.Schannel);
        diagnostics.Act("lines", string.Join("\n", lines));

        string[] expected4 = new[] { "ALPN: curl offers http/1.1", " public key hash: " + Hash };
        string[] actual4 = lines.ToArray();
        diagnostics.Diff("lines", string.Join("\n", expected4), string.Join("\n", actual4));
        CollectionAssert.AreEqual(expected4, actual4);
    }

    [TestMethod]
    public void TlsHandshake_SchannelFailedWithoutAPin_PrintsOnlyTheAlpnOffer()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "Schannel, failed, ALPN http/1.1, no pin");
        // curl 8.21.0 Schannel -v, an untrusted certificate, exit 60, measured 2026-10-02 (BL-1149).
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, ["http/1.1"]) with { Failed = true }, TlsBackend.Schannel);
        diagnostics.Act("lines", string.Join("\n", lines));

        string[] expected5 = new[] { "ALPN: curl offers http/1.1" };
        string[] actual5 = lines.ToArray();
        diagnostics.Diff("lines", string.Join("\n", expected5), string.Join("\n", actual5));
        CollectionAssert.AreEqual(expected5, actual5);
    }

    [TestMethod]
    public void TlsHandshake_OpenSslFailedWithAHashPin_PrintsTheCertificateDetailsBeforeTheHash()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL, failed, self-signed CN=localhost, ALPN http/1.1, pinned hash");
        // curl 8.18.0 OpenSSL -v -k --pinnedpubkey sha256//<wrong>, measured 2026-10-02 (BL-1149).
        using var certificate = SelfSigned();

        var lines = TransferEventInfoText.TlsHandshake(Handshake(certificate, ["http/1.1"]) with { PinnedPublicKeyHash = Hash, Failed = true }, TlsBackend.OpenSsl);
        diagnostics.Act("lines", string.Join("\n", lines));

        string[] expected6 = new[]
            {
                "ALPN: curl offers http/1.1",
                "SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF",
                "ALPN: server did not agree on a protocol. Uses default.",
                "Server certificate:",
                "  subject: CN=localhost",
            };
        string[] actual6 = lines.Take(5).ToArray();
        diagnostics.Diff("lines", string.Join("\n", expected6), string.Join("\n", actual6));
        CollectionAssert.AreEqual(expected6, actual6);
        string[] expected7 = new[] { " SSL certificate verification failed, continuing anyway!", " public key hash: " + Hash };
        string[] actual7 = lines.TakeLast(2).ToArray();
        diagnostics.Diff("lines", string.Join("\n", expected7), string.Join("\n", actual7));
        CollectionAssert.AreEqual(expected7, actual7);
    }

    [TestMethod]
    public void TlsHandshake_OpenSslFailedOnAnUntrustedCertificate_PrintsTheCertificateDetailsWithoutTheVerifyResult()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL, failed, self-signed CN=localhost, verified host name localhost");
        // curl 8.18.0 OpenSSL -v, a self-signed certificate naming the host, exit 60: the SAN line
        // (here the common name's) last, the verify result left to the error message (measured 2026-10-02, BL-1178).
        using var certificate = SelfSigned();

        var lines = TransferEventInfoText.TlsHandshake(
            Handshake(certificate, ["http/1.1"]) with { VerifiedHostName = "localhost", Failed = true }, TlsBackend.OpenSsl);
        diagnostics.Act("lines", string.Join("\n", lines));

        diagnostics.Assert("line", "Server certificate:", lines[3]);
        Assert.AreEqual("Server certificate:", lines[3]);
        diagnostics.Assert("line", " common name: localhost (matched)", lines[^1]);
        Assert.AreEqual(" common name: localhost (matched)", lines[^1]);
    }

    [TestMethod]
    public void TlsHandshake_OpenSslFailedOnAHostNameMismatch_PrintsTheCertificateDetailsThenTheMismatchLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL, failed, certificate for 127.0.0.1, verified host name 172.26.96.1");
        // curl 8.18.0 OpenSSL -v https://172.26.96.1:47811/, a certificate for 127.0.0.1, exit 60
        // (measured 2026-10-02, BL-1178).
        using var certificate = SelfSignedFor127001();

        var lines = TransferEventInfoText.TlsHandshake(
            Handshake(certificate, ["h2", "http/1.1"]) with { VerifiedHostName = "172.26.96.1", Failed = true }, TlsBackend.OpenSsl);
        diagnostics.Act("lines", string.Join("\n", lines));

        diagnostics.Assert("line", "SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF", lines[1]);
        Assert.AreEqual("SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF", lines[1]);
        diagnostics.Assert("line", " subjectAltName does not match ipv4 address 172.26.96.1", lines[^1]);
        Assert.AreEqual(" subjectAltName does not match ipv4 address 172.26.96.1", lines[^1]);
    }

    [TestMethod]
    public void TlsHandshake_OpenSslFailedWithAPinOnATrustedCertificate_PrintsTheVerifyResultAndTheHash()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL, failed, trusted self-signed CN=localhost, no ALPN, pinned hash");
        using var certificate = SelfSigned();

        var lines = TransferEventInfoText.TlsHandshake(
            Handshake(certificate, []) with { CertificateVerified = true, CertificateVerifyResult = 0, VerifiedHostName = "localhost", PinnedPublicKeyHash = Hash, Failed = true },
            TlsBackend.OpenSsl);
        diagnostics.Act("lines", string.Join("\n", lines));

        string[] expected8 = new[] { "SSL certificate verified via OpenSSL.", " public key hash: " + Hash };
        string[] actual8 = lines.TakeLast(2).ToArray();
        diagnostics.Diff("lines", string.Join("\n", expected8), string.Join("\n", actual8));
        CollectionAssert.AreEqual(expected8, actual8);
    }

    [TestMethod]
    public void TlsHandshake_OpenSslFailedBeforeNegotiatingAnything_PrintsOnlyTheAlpnOffer()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handshake", "OpenSSL, failed, no protocol version, ALPN h2 and http/1.1");
        // curl 8.18.0 OpenSSL -v -k --tlsv1.3 against a TLS 1.2 server, exit 35 (measured 2026-10-02, BL-1178).
        var lines = TransferEventInfoText.TlsHandshake(
            Handshake(null, ["h2", "http/1.1"]) with { ProtocolVersion = SslProtocols.None, CipherSuite = null, Failed = true }, TlsBackend.OpenSsl);
        diagnostics.Act("lines", string.Join("\n", lines));

        string[] expected9 = new[] { "ALPN: curl offers h2,http/1.1" };
        string[] actual9 = lines.ToArray();
        diagnostics.Diff("lines", string.Join("\n", expected9), string.Join("\n", actual9));
        CollectionAssert.AreEqual(expected9, actual9);
    }

    private static TlsHandshakeEvent Handshake(X509Certificate2? certificate, IReadOnlyList<string> offered)
    {
        return new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls13,
            CipherSuite = TlsCipherSuite.TLS_AES_256_GCM_SHA384,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = offered,
            ServerCertificate = certificate,
            CertificateVerified = false,
            CertificateVerifyResult = 18,
            PeerCertificateChain = certificate is null ? [] : [certificate],
        };
    }

    private static X509Certificate2 SelfSignedFor127001()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        return request.CreateSelfSigned(new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 9, 28, 0, 0, 0, TimeSpan.Zero));
    }

    private static X509Certificate2 SelfSigned()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 9, 28, 0, 0, 0, TimeSpan.Zero));
    }
}
