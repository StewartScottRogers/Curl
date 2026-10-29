using System.Net.Security;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins CGR-226: the OpenSSL build's <c>-v</c> connection line names the negotiated group and
/// peer signature type the handshake reported, and says <c>[blank] / UNDEF</c> only when it
/// reported none.
/// </summary>
[TestClass]
public sealed class Cgr226Tests
{
    [TestMethod]
    public void Lines_HandshakeReportsGroupAndSignature_ConnectionLineNamesThem()
    {
        var handshake = new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls13,
            CipherSuite = TlsCipherSuite.TLS_AES_256_GCM_SHA384,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = true,
            NegotiatedGroupName = "X25519MLKEM768",
            PeerSignatureTypeName = "RSASSA-PSS",
        };

        var lines = OpenSslHandshakeText.Lines(handshake, []);

        CollectionAssert.Contains(lines.ToList(), "SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS");
    }

    [TestMethod]
    public void Lines_HandshakeReportsNeither_ConnectionLineSaysBlankAndUndef()
    {
        var handshake = new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls13,
            CipherSuite = TlsCipherSuite.TLS_AES_256_GCM_SHA384,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = true,
        };

        var lines = OpenSslHandshakeText.Lines(handshake, []);

        CollectionAssert.Contains(lines.ToList(), "SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF");
    }
}
