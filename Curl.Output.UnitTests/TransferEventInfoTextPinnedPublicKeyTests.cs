using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

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
    private const string Hash = "sha256//7VmZsXC/uSi8KudeBgyot4QIl/fLiETstYonpMQATl8=";

    [TestMethod]
    public void TlsHandshake_SchannelWithAHashPin_PrintsTheHashBetweenTheAlpnLines()
    {
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, ["http/1.1"]) with { PinnedPublicKeyHash = Hash }, TlsBackend.Schannel);

        CollectionAssert.AreEqual(
            new[]
            {
                "ALPN: curl offers http/1.1",
                " public key hash: " + Hash,
                "ALPN: server did not agree on a protocol. Uses default.",
            },
            lines.ToArray());
    }

    [TestMethod]
    public void TlsHandshake_SchannelWithAHashPinAndNoAlpn_PrintsOnlyTheHash()
    {
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, []) with { PinnedPublicKeyHash = Hash }, TlsBackend.Schannel);

        CollectionAssert.AreEqual(new[] { " public key hash: " + Hash }, lines.ToArray());
    }

    [TestMethod]
    public void TlsHandshake_SchannelWithoutAHashPin_PrintsOnlyTheAlpnLines()
    {
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, ["http/1.1"]), TlsBackend.Schannel);

        Assert.HasCount(2, lines);
    }

    [TestMethod]
    public void TlsHandshake_OpenSslWithAHashPin_PrintsTheHashAfterTheVerifyResult()
    {
        using var certificate = SelfSigned();

        var lines = TransferEventInfoText.TlsHandshake(Handshake(certificate, ["http/1.1"]) with { PinnedPublicKeyHash = Hash }, TlsBackend.OpenSsl);

        CollectionAssert.AreEqual(
            new[] { " SSL certificate verification failed, continuing anyway!", " public key hash: " + Hash },
            lines.TakeLast(2).ToArray());
    }

    [TestMethod]
    public void TlsHandshake_OpenSslWithoutAHashPin_EndsWithTheVerifyResult()
    {
        using var certificate = SelfSigned();

        var lines = TransferEventInfoText.TlsHandshake(Handshake(certificate, ["http/1.1"]), TlsBackend.OpenSsl);

        Assert.AreEqual(" SSL certificate verification failed, continuing anyway!", lines[^1]);
    }

    [TestMethod]
    public void TlsHandshake_SchannelFailedWithAHashPin_PrintsTheAlpnOfferThenTheHash()
    {
        // curl 8.21.0 Schannel -v -k --pinnedpubkey sha256//<wrong>, measured 2026-10-02 (BL-1149).
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, ["http/1.1"]) with { PinnedPublicKeyHash = Hash, Failed = true }, TlsBackend.Schannel);

        CollectionAssert.AreEqual(new[] { "ALPN: curl offers http/1.1", " public key hash: " + Hash }, lines.ToArray());
    }

    [TestMethod]
    public void TlsHandshake_SchannelFailedWithoutAPin_PrintsOnlyTheAlpnOffer()
    {
        // curl 8.21.0 Schannel -v, an untrusted certificate, exit 60, measured 2026-10-02 (BL-1149).
        var lines = TransferEventInfoText.TlsHandshake(Handshake(null, ["http/1.1"]) with { Failed = true }, TlsBackend.Schannel);

        CollectionAssert.AreEqual(new[] { "ALPN: curl offers http/1.1" }, lines.ToArray());
    }

    [TestMethod]
    public void TlsHandshake_OpenSslFailedWithAHashPin_PrintsTheCertificateDetailsBeforeTheHash()
    {
        // curl 8.18.0 OpenSSL -v -k --pinnedpubkey sha256//<wrong>, measured 2026-10-02 (BL-1149).
        using var certificate = SelfSigned();

        var lines = TransferEventInfoText.TlsHandshake(Handshake(certificate, ["http/1.1"]) with { PinnedPublicKeyHash = Hash, Failed = true }, TlsBackend.OpenSsl);

        CollectionAssert.AreEqual(
            new[]
            {
                "ALPN: curl offers http/1.1",
                "SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF",
                "ALPN: server did not agree on a protocol. Uses default.",
                "Server certificate:",
                "  subject: CN=localhost",
            },
            lines.Take(5).ToArray());
        CollectionAssert.AreEqual(
            new[] { " SSL certificate verification failed, continuing anyway!", " public key hash: " + Hash },
            lines.TakeLast(2).ToArray());
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

    private static X509Certificate2 SelfSigned()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 9, 28, 0, 0, 0, TimeSpan.Zero));
    }
}
