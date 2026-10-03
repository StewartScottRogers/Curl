using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The <c>[SETUP]</c> and <c>[HTTPS-CONNECT]</c> lines of a QUIC connect through a CONNECT-UDP
/// proxy, and of the CONNECT tunnel <c>--http3</c> tries after it on the same target, as curl.se's
/// curl 8.22.0 ngtcp2 build writes them under <c>-v --trace-config https-connect,setup</c>
/// (measured, BL-1320 Notes).
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    private const string ForbiddenReply = "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughARefusingHttpProxyWithNoSecondAttempt_WritesCurlsLinesAroundTheUdpTunnel()
    {
        // curl 8.22.0 --http3-only -x http://127.0.0.1:18321 https://example.com/, the proxy answering 403.
        var events = new RecordingTransferEvents();
        var connector = TracingTunnelConnector(_ => new CapsuleQuicProxyConnection(ForbiddenReply, null), null);

        var result = await connector.ConnectMultiplexedAsync(TunnelTarget(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])
            [
                "* [HTTPS-CONNECT] added",
                "* [HTTPS-CONNECT] connect, init",
                "* [HTTPS-CONNECT] 1st attempt uses h3 from wanted versions",
                .. UdpTunnelLines,
                "* [HTTPS-CONNECT] connect, all attempts failed",
                "* [HTTPS-CONNECT] connect -> 7, done=0",
            ],
            TunnelTranscript(events));
    }

    [TestMethod]
    public async Task ConnectAsync_AfterTheUdpTunnelWasRefusedForTheSameTarget_StartsH2OnTheSameFilterThroughTheProxy()
    {
        // curl 8.22.0 --http3 -x http://127.0.0.1:18320 https://example.com/, the proxy answering 403
        // twice. curl's "CONNECT tunnel failed, response 403" before "all attempts failed" is the TCP
        // CONNECT path's failure message, which the connector does not write as a -v line.
        var events = new RecordingTransferEvents();
        var connector = TracingTunnelConnector(_ => new CapsuleQuicProxyConnection(ForbiddenReply, null), "h2");
        var target = TunnelTarget(events);

        var quic = await connector.ConnectMultiplexedAsync(target, CancellationToken.None);
        var tcp = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, quic.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, tcp.ExitCode);
        CollectionAssert.AreEqual(
            (string[])
            [
                "* [HTTPS-CONNECT] added",
                "* [HTTPS-CONNECT] connect, init",
                "* [HTTPS-CONNECT] 1st attempt uses h3 from wanted versions",
                "* [HTTPS-CONNECT] 2nd attempt uses h2 from wanted versions",
                .. UdpTunnelLines,
                "* [HTTPS-CONNECT] h3 baller failed, starting h2",
                "* [SETUP] happy eyeballing to proxy proxy.example:3128",
                "*   Trying 192.0.2.10:3128...",
                OneSocketConnecting,
                OneSocketPolled,
                "* [SETUP] added HTTP proxy tunnel filter",
                "* CONNECT: no ALPN negotiated",
                "* Establishing HTTP proxy tunnel to quic.test:443",
                "> CONNECT quic.test:443 HTTP/1.1",
                OneSocketConnecting,
                OneSocketPolled,
                "< HTTP/1.1 403 Forbidden",
                "* [HTTPS-CONNECT] connect, all attempts failed",
                "* [HTTPS-CONNECT] connect -> 7, done=0",
            ],
            TunnelTranscript(events));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAnHttpProxyThatOpensTheTunnel_WritesTheFiltersRemovalOnceQuicConnected()
    {
        var events = new RecordingTransferEvents();
        var connector = TracingTunnelConnector(_ => new CapsuleQuicProxyConnection(UpgradeReply, Server()), "h2");

        var result = await connector.ConnectMultiplexedAsync(TunnelTarget(events), CancellationToken.None);

        await using var connection = result.Connection!;
        string[] transcript = TunnelTranscript(events);
        CollectionAssert.AreEqual(
            new[]
            {
                "< HTTP/1.1 101 Switching Protocols",
                "* CONNECT-UDP phase completed for HTTP proxy",
                "* CONNECT-UDP tunnel established, response 101",
                OneSocketConnecting,
                OneSocketPolled,
                "* [HTTPS-CONNECT] connect -> 0, done=1",
                "* [HTTPS-CONNECT] removing connected setup filter",
                "* [HTTPS-CONNECT] destroy",
                "* [SETUP] removing connected setup filter",
                "* [SETUP] destroy",
            },
            transcript[Array.IndexOf(transcript, "< HTTP/1.1 101 Switching Protocols")..]);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAnHttpsProxyUnderTheSetupFilter_AddsItsSslAndTunnelFiltersBeforeItsHandshake()
    {
        var secured = new CapsuleQuicProxyConnection(ForbiddenReply, null);
        var tls = new SequencedTlsProvider(ConnectResult.Connected(secured, null));
        var events = new RecordingTransferEvents();
        var connector = TracingTunnelConnector(_ => new FakeConnection(), null, tls);

        await connector.ConnectMultiplexedAsync(TunnelTarget(events) with { Proxy = TunnelHttpProxy with { Kind = ProxyKind.Https } }, CancellationToken.None);

        string[] transcript = TunnelTranscript(events);
        CollectionAssert.AreEqual(
            new[] { "* [SETUP] added SSL filter for HTTP proxy", "* [SETUP] added HTTP proxy tunnel filter", "* CONNECT-UDP: no ALPN negotiated" },
            transcript[Array.IndexOf(transcript, "* [SETUP] added SSL filter for HTTP proxy")..][..3]);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAnHttpProxyWithNoFilterTraced_WritesTheTunnelsVerboseLinesAlone()
    {
        var events = new RecordingTransferEvents();

        await TunnelConnector(_ => new CapsuleQuicProxyConnection(ForbiddenReply, null)).ConnectMultiplexedAsync(TunnelTarget(events), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "*   Trying 192.0.2.10:3128...",
                "* CONNECT-UDP: no ALPN negotiated",
                "* Establishing HTTP proxy UDP tunnel to quic.test:443",
                "> GET http://proxy.example:3128/.well-known/masque/udp/quic.test/443/ HTTP/1.1",
                "< HTTP/1.1 403 Forbidden",
                "* CONNECT-UDP tunnel failed, response 403",
            },
            TunnelTranscript(events));
    }

    private const string OneSocketConnecting = "* [HTTPS-CONNECT] connect -> 0, done=0";
    private const string OneSocketPolled = "* [HTTPS-CONNECT] adjust_pollset -> 0, 1 socks";

    // The lines from the setup filter's eyeballing to the refused CONNECT-UDP reply (BL-1320 Notes).
    private static readonly string[] UdpTunnelLines =
    [
        "* [SETUP] happy eyeballing to proxy proxy.example:3128",
        "*   Trying 192.0.2.10:3128...",
        OneSocketConnecting,
        OneSocketPolled,
        "* [SETUP] added HTTP proxy tunnel filter",
        "* CONNECT-UDP: no ALPN negotiated",
        "* Establishing HTTP proxy UDP tunnel to quic.test:443",
        "> GET http://proxy.example:3128/.well-known/masque/udp/quic.test/443/ HTTP/1.1",
        OneSocketConnecting,
        OneSocketPolled,
        "< HTTP/1.1 403 Forbidden",
        "* CONNECT-UDP tunnel failed, response 403",
    ];

    // The transcript's filter, Trying, tunnel and first request and reply lines, as the measured stderr shows them.
    private static string[] TunnelTranscript(RecordingTransferEvents events) =>
        [.. events.Transcript.Where(line =>
            line.StartsWith("* [", StringComparison.Ordinal)
            || line.StartsWith("*   Trying ", StringComparison.Ordinal)
            || line.StartsWith("* CONNECT", StringComparison.Ordinal)
            || line.StartsWith("* Establishing ", StringComparison.Ordinal)
            || line.StartsWith("> GET ", StringComparison.Ordinal)
            || line.StartsWith("> CONNECT ", StringComparison.Ordinal)
            || line.StartsWith("< HTTP/", StringComparison.Ordinal))];

    private static TcpConnector TracingTunnelConnector(Func<IPEndPoint, IConnection> dialOutcome, string? secondAttemptVersion, ITlsProvider? tlsProvider = null)
    {
        var clock = new ManualTimeProvider();
        return new TcpConnector(
            new FakeDnsResolver(TunnelProxyAddress),
            new FakeTcpDialer { DialOutcome = dialOutcome },
            tlsProvider ?? new FakeTlsProvider(),
            clock,
            proxyTunnelOptions: HttpProxyTunnelOptions.Default,
            quicDialer: new QuicDialer(new QuicServerChannelOpener(), new TlsClientOptions(Insecure: true), true, clock, SystemTlsRandomSource.Instance))
        {
            TracesHttpsConnectFilter = true,
            TracesSetupFilter = true,
            HttpsConnectFirstAttemptVersion = "h3",
            HttpsConnectSecondAttemptVersion = secondAttemptVersion,
        };
    }
}
