using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="VerboseTransferEventWriter"/>'s TLS lines for a QUIC connect (BL-1050): on
/// Windows those curl.se's build (curl 8.18.0, LibreSSL 4.2.1, ngtcp2 1.21.0) wrote for
/// <c>-s -v --http3-only https://cloudflare-quic.com/</c> on 2026-10-01, with and without
/// <c>--cacert</c>, <c>--ca-native</c> and <c>-k</c>; on Linux and macOS the OpenSSL lines
/// the TCP path writes (ADR-0144).
/// </summary>
[TestClass]
public sealed class VerboseTransferEventWriterQuicTlsTests
{
    private const string Host = "cloudflare-quic.com";

    private readonly MemoryStream output = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TlsHandshake_QuicOnWindows_WritesCurlSesLibreSslLines()
    {
        using var leaf = EcCertificate();
        using var intermediate = RsaCertificate();

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("backend", TlsBackend.Schannel);
        diagnostics.Arrange("handshake", "QUIC, TLS 1.3, EC leaf + RSA intermediate, VerifiedHostName=" + Host);

        Writer(TlsBackend.Schannel).ReportTlsHandshake(QuicHandshake(leaf, intermediate) with { VerifiedHostName = Host });

        const string expected =
            "* SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF\n" +
            "* Server certificate:\n" +
            "*   subject: CN=cloudflare-quic.com\n" +
            "*   start date: Sep 20 17:51:11 2026 GMT\n" +
            "*   expire date: Dec 19 18:51:00 2026 GMT\n" +
            "*   issuer: CN=cloudflare-quic.com\n" +
            "*   Certificate level 0: Public key type ? (256/128 Bits/secBits), signed using ecdsa-with-SHA256\n" +
            "*   Certificate level 1: Public key type ? (2048/112 Bits/secBits), signed using sha256WithRSAEncryption\n" +
            "*   subjectAltName: \"cloudflare-quic.com\" matches cert's \"cloudflare-quic.com\"\n" +
            "* SSL certificate verified via OpenSSL.\n";
        string written = Written();
        diagnostics.Act("written", written);
        diagnostics.Diff("written", expected, written);
        Assert.AreEqual(expected, written);
    }

    // -k: no host-name line, and the failure line without "OpenSSL verify result".
    [TestMethod]
    public void TlsHandshake_QuicOnWindowsWithoutVerification_WritesTheFailureLineOnly()
    {
        using var leaf = EcCertificate();

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("backend", TlsBackend.Schannel);
        diagnostics.Arrange("handshake", "QUIC, CertificateVerified=false, CertificateVerifyResult=null");

        Writer(TlsBackend.Schannel).ReportTlsHandshake(QuicHandshake(leaf) with { CertificateVerified = false, CertificateVerifyResult = null });

        string written = Written();
        const string expectedEnd = "signed using ecdsa-with-SHA256\n*  SSL certificate verification failed, continuing anyway!\n";
        diagnostics.Act("written", written);
        diagnostics.Assert("ends with failure line", true, written.EndsWith(expectedEnd, StringComparison.Ordinal));
        StringAssert.EndsWith(written, expectedEnd);
    }

    [TestMethod]
    public void TlsHandshake_QuicOnLinux_WritesTheOpenSslLinesUnchanged()
    {
        using var leaf = EcCertificate();
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var handshake = QuicHandshake(leaf) with { VerifiedHostName = Host };
        diagnostics.Arrange("backend", TlsBackend.OpenSsl);
        diagnostics.Arrange("handshake", "QUIC then TCP, same leaf, VerifiedHostName=" + Host);

        Writer(TlsBackend.OpenSsl).ReportTlsHandshake(handshake);
        var quic = Written();
        output.SetLength(0);
        Writer(TlsBackend.OpenSsl).ReportTlsHandshake(handshake with { IsQuic = false });

        string tcp = Written();
        diagnostics.Act("quic written", quic);
        diagnostics.Act("tcp written", tcp);
        diagnostics.Diff("tcp vs quic", tcp, quic);
        Assert.AreEqual(tcp, quic);
        StringAssert.StartsWith(quic, "* SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519 / UNDEF\n");
        StringAssert.Contains(quic, "*   Certificate level 0: Public key type EC/prime256v1 (256/128 Bits/secBits)");
        StringAssert.EndsWith(quic, "* OpenSSL verify result: 0\n* SSL certificate verified via OpenSSL.\n");
    }

    [TestMethod]
    public void TlsHandshake_TcpOnWindows_WritesOnlyTheAlpnLines()
    {
        using var leaf = EcCertificate();

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("backend", TlsBackend.Schannel);
        diagnostics.Arrange("handshake", "TCP, offered h2, negotiated h3");

        Writer(TlsBackend.Schannel).ReportTlsHandshake(QuicHandshake(leaf) with { IsQuic = false, OfferedApplicationProtocols = ["h2"] });

        const string expected = "* ALPN: curl offers h2\n* ALPN: server accepted h3\n";
        string written = Written();
        diagnostics.Act("written", written);
        diagnostics.Diff("written", expected, written);
        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    [DataRow(null, true, "* SSL Trust Anchors:\n*   Native: Windows System Stores ROOT+CA\n")]
    [DataRow(@"C:\roots.pem", false, "* SSL Trust Anchors:\n*   CAfile: C:\\roots.pem\n")]
    public void TlsTrust_QuicOnWindows_WritesTheTrustAnchorsCurlUsed(string? caFile, bool usesWindowsStores, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("caFile", caFile);
        diagnostics.Arrange("usesWindowsStores", usesWindowsStores);

        Writer(TlsBackend.Schannel).ReportTlsTrust(new TlsTrustEvent
        {
            VerifiesPeer = true,
            CaCertificateFile = caFile,
            UsesWindowsSystemStores = usesWindowsStores,
            IsQuic = true,
        });

        string written = Written();
        diagnostics.Act("written", written);
        diagnostics.Diff("written", expected, written);
        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public void TlsTrust_QuicOnWindowsWithoutVerification_SaysVerificationIsDisabled()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("trust event", "VerifiesPeer=false, IsQuic=true");

        Writer(TlsBackend.Schannel).ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false, IsQuic = true });

        const string expected = "* SSL Trust: peer verification disabled\n";
        string written = Written();
        diagnostics.Act("written", written);
        diagnostics.Diff("written", expected, written);
        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public void TlsTrust_TcpOnWindows_WritesTheSchannelLinesNotTheTrustAnchors()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("trust event", "VerifiesPeer=true, UsesWindowsSystemStores=true, TCP");

        Writer(TlsBackend.Schannel).ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true, UsesWindowsSystemStores = true });

        const string expected = "* schannel: disabled automatic use of client certificate\n";
        string written = Written();
        diagnostics.Act("written", written);
        diagnostics.Diff("written", expected, written);
        Assert.AreEqual(expected, written);
    }

    private static TlsHandshakeEvent QuicHandshake(params X509Certificate2[] chain)
    {
        return new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls13,
            CipherSuite = TlsCipherSuite.TLS_AES_256_GCM_SHA384,
            NegotiatedApplicationProtocol = "h3",
            OfferedApplicationProtocols = [],
            ServerCertificate = chain[0],
            CertificateVerified = true,
            CertificateVerifyResult = 0,

            // LibreSSL cannot name the group, so curl.se's build writes [blank] whatever it was.
            NegotiatedGroupName = "X25519",
            PeerCertificateChain = chain,
            IsQuic = true,
        };
    }

    private static X509Certificate2 EcCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new("CN=" + Host, key, HashAlgorithmName.SHA256);
        SubjectAlternativeNameBuilder names = new();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        return request.CreateSelfSigned(
            new DateTimeOffset(2026, 9, 20, 17, 51, 11, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 19, 18, 51, 0, TimeSpan.Zero));
    }

    private static X509Certificate2 RsaCertificate()
    {
        using var key = RSA.Create(2048);
        CertificateRequest request = new("CN=root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(100));
    }

    private VerboseTransferEventWriter Writer(TlsBackend tlsBackend)
    {
        return new VerboseTransferEventWriter(output, writesDataLines: true, tlsBackend);
    }

    private string Written()
    {
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
