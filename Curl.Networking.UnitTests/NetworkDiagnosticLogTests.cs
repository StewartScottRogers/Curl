using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The lines <see cref="NetworkDiagnosticLog" /> writes for the branches the connector tests
/// do not reach, and that it builds nothing at a level that is off (ADR-0222, BL-920).
/// </summary>
[TestClass]
public sealed class NetworkDiagnosticLogTests
{
    [TestMethod]
    public void Connected_WithoutALocalEndPoint_SaysItWasNotReported()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        new NetworkDiagnosticLog(recording).Connected(new IPEndPoint(IPAddress.Loopback, 80), null);

        CollectionAssert.AreEqual(
            new[] { "connected to 127.0.0.1:80 from an unreported local end point" },
            recording.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Connect));
    }

    [TestMethod]
    public void HandshakeCompleted_WithAnAgreedProtocolAndNoCipherSuite_NamesTheProtocol()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var handshake = new TlsHandshakeEvent
        {
            ProtocolVersion = System.Security.Authentication.SslProtocols.Tls13,
            CipherSuite = null,
            NegotiatedApplicationProtocol = "h2",
            OfferedApplicationProtocols = ["h2"],
            ServerCertificate = null,
            CertificateVerified = true,
        };

        new NetworkDiagnosticLog(recording).HandshakeCompleted("example.com", TlsClientRoute.SslStream, null, handshake, "h2");

        CollectionAssert.AreEqual(
            new[] { "handshake with example.com complete: Tls13, an unreported cipher suite, ALPN h2, route SslStream" },
            recording.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Tls));
    }

    [TestMethod]
    public void HandshakeCompleted_AtVerboseWithAVerifiedChainAndNoCertificates_SaysTheChainWasVerified()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var handshake = new TlsHandshakeEvent
        {
            ProtocolVersion = System.Security.Authentication.SslProtocols.Tls13,
            CipherSuite = null,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = true,
        };

        new NetworkDiagnosticLog(recording).HandshakeCompleted("example.com", null, null, handshake, null);

        CollectionAssert.AreEqual(new[] { "certificate chain verified" }, recording.At(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Tls));
    }

    [TestMethod]
    public void QuicDialled_WhenTheDialFailed_LogsTheExitCodeAtError()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        new NetworkDiagnosticLog(recording).QuicDialled("quic.test", 443, MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to quic.test port 443"));

        CollectionAssert.AreEqual(
            new[] { "failed with CouldntConnect (7): Failed to connect to quic.test port 443" },
            recording.At(DiagnosticLogLevel.Error, DiagnosticLogComponents.Quic));
    }

    [TestMethod]
    public void EveryStep_WithTheLogOff_WritesNothing()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.None);
        var log = new NetworkDiagnosticLog(recording);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);

        log.Resolved("example.com", 80, [IPAddress.Loopback], fromCache: false, TimeSpan.Zero);
        log.NotResolved("example.com", 80);
        log.Dialling(endPoint);
        log.DialFailed(endPoint, new System.Net.Sockets.SocketException());
        log.Connected(endPoint, endPoint);
        log.TunnelRequested(proxy, "example.com", 80);
        log.SocksHandshakeStarting(proxy, "example.com", 80);
        log.TunnelEstablished(proxy, "example.com", 80, 200);
        log.HandshakeCompleted("example.com", TlsClientRoute.SslStream, null, null, null);
        log.RevocationCheckIncomplete("example.com");
        log.UnixSocketConnected("/run/curl.sock");
        log.DatagramChannelOpened(endPoint);
        log.Failed(DiagnosticLogComponents.Connect, CurlExitCode.CouldntConnect, "x");
        log.Threw(new IOException("x"));
        log.PoolDecision(new ConnectTarget("example.com", 80, UseTls: false), null);
        log.QuicDialling("example.com", 443, [IPAddress.Loopback]);
        log.QuicDialled("example.com", 443, MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "x"));

        Assert.IsEmpty(recording.Lines);
        Assert.IsFalse(log.IsEnabled(DiagnosticLogLevel.Error));
    }
}
