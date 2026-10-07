using System.Net.Sockets;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives the <c>[SETUP]</c>, <c>[HTTP-PROXY]</c> and <c>[H1-PROXY]</c> lines curl 8.21.0 writes around a
/// CONNECT tunnel through an HTTPS proxy (measured with <c>Record-CurlExchange.ps1 -Tls -Script</c>
/// playing the proxy, BL-1255 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly ProxyEndpoint TunnelHttpsProxy = new(ProxyKind.Https, "192.0.2.10", 3128, null);

    [TestMethod]
    public async Task ConnectAsync_TracingTheProxyAndSetupFiltersThroughAnHttpsProxy_WritesTheTunnelLinesInCurlsOrder()
    {
        // curl -s -v --trace-config proxy,setup -p --proxy-insecure -x https://127.0.0.1:18955
        // http://example.test/x, without curl's [SSL-PROXY] lines (BL-1255 Notes).
        var (events, result) = await TraceThroughHttpsProxyAsync(TunnelEstablishedReply, tracesSetup: true);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        AssertTunnelLines(
            new[]
            {
                "* [SETUP] added",
                "* [SETUP] happy eyeballing to proxy 192.0.2.10:3128",
                "*   Trying 192.0.2.10:3128...",
                "* [SETUP] added SSL filter for HTTP proxy",
                "* [SETUP] added HTTP proxy tunnel filter",
                "* [HTTP-PROXY] CONNECT",
                "* [HTTP-PROXY] CONNECT",
                "* [HTTP-PROXY] CONNECT",
                "* CONNECT: no ALPN negotiated",
                "* [HTTP-PROXY] installing subfilter for HTTP/1.1",
                "* [H1-PROXY] connect",
                "* [H1-PROXY] CONNECT start",
                "* Establishing HTTP proxy tunnel to example.test:80",
                "* [H1-PROXY] new tunnel state 'connect'",
                "* [H1-PROXY] CONNECT send",
            },
            events.Transcript.Take(15).ToArray());
        AssertTunnelLines(
            new[]
            {
                "* [H1-PROXY] new tunnel state 'established'",
                "* CONNECT phase completed for HTTP proxy",
                "* CONNECT tunnel established, response 200",
                "* [H1-PROXY] new tunnel state 'failed'",
                "* [SETUP] removing connected setup filter",
                "* [SETUP] destroy",
                "* [HTTP-PROXY] removing connected setup filter",
                "* [HTTP-PROXY] destroy",
                "* [H1-PROXY] query ALPN",
            },
            events.Transcript.TakeLast(9).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheProxyFiltersThroughAnHttpsProxyThatAgreedOnHttp11_PollsBeforeSayingSo()
    {
        var (events, _) = await TraceThroughHttpsProxyAsync(TunnelEstablishedReply, agreed: "http/1.1");

        Diagnostics.Assert("first traced line", "[HTTP-PROXY] CONNECT", events.Info[1]);
        AssertTunnelLines(
            new[]
            {
                "[HTTP-PROXY] CONNECT",
                "[HTTP-PROXY] CONNECT",
                "[HTTP-PROXY] CONNECT",
                "CONNECT: 'http/1.1' negotiated",
                "[HTTP-PROXY] installing subfilter for HTTP/1.1",
            },
            events.Info.Skip(1).Take(5).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterThroughAnHttpsProxyToAnHttpsOrigin_AddsTheOriginsSslFilterAfterTheTunnel()
    {
        // curl -s -k --proxy-insecure -v --trace-config https-connect,setup -x https://127.0.0.1:18458
        // https://example.test/: no [SETUP] added, and added SSL filter for origin once the tunnel is open (BL-1283 Context).
        var events = new RecordingTransferEvents();
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(TunnelEstablishedReply));
        var connector = HttpsProxyTunnelConnector(new SequencedTlsProvider(ConnectResult.Connected(secured, null)), tracesSetup: true);
        var target = new ConnectTarget("example.test", 443, UseTls: true) { Proxy = TunnelHttpsProxy, PoolScheme = "https", Events = events };

        await ConnectLoggedAsync(connector, target);

        Diagnostics.Assert("setup added line written", false, events.Info.Contains(SetupFilterTraceEvents.AddedLine));
        Assert.DoesNotContain(SetupFilterTraceEvents.AddedLine, events.Info);
        Assert.AreEqual("[SETUP] happy eyeballing to proxy 192.0.2.10:3128", events.Info[0]);
        var failed = events.Info.IndexOf("[H1-PROXY] new tunnel state 'failed'");
        Assert.AreEqual(failed + 1, events.Info.IndexOf(TcpConnector.SslFilterAddedLine));
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpsProxyWithoutTracingIt_WritesNoTunnelOrSetupLine()
    {
        var (events, result) = await TraceThroughHttpsProxyAsync(TunnelEstablishedReply, tracesHttpProxy: false, tracesH1Proxy: false);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith('[')));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingOnlyTheSetupFilterThroughAnHttpsProxy_WritesNoProxyFilterLine()
    {
        var (events, _) = await TraceThroughHttpsProxyAsync(TunnelEstablishedReply, tracesHttpProxy: false, tracesH1Proxy: false, tracesSetup: true);

        Diagnostics.Assert("all bracketed lines are setup lines", true, events.Info.Where(line => line.StartsWith('[')).All(line => line.StartsWith("[SETUP] ", StringComparison.Ordinal)));
        Assert.IsTrue(events.Info.Where(line => line.StartsWith('[')).All(line => line.StartsWith("[SETUP] ", StringComparison.Ordinal)));
        Assert.Contains(TcpConnector.HttpsProxySslFilterAddedLine, events.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheProxyFiltersWhenTheHttpsProxysHandshakeFails_StopsAfterOnePollRound()
    {
        // A failed handshake waits one poll round, as a failed origin handshake measured (BL-1287 Notes).
        var events = new RecordingTransferEvents();
        var connector = HttpsProxyTunnelConnector(
            new SequencedTlsProvider(ConnectResult.Failed(CurlExitCode.SslConnectError, "handshake failed")),
            tracesSetup: true);

        var result = await ConnectLoggedAsync(connector, HttpsProxyTunnelTarget with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        AssertTunnelLines(
            new[] { TcpConnector.HttpsProxySslFilterAddedLine, TcpConnector.HttpProxyTunnelFilterAddedLine, "[HTTP-PROXY] CONNECT", "[HTTP-PROXY] CONNECT" },
            events.Info.Where(line => line.StartsWith('[')).TakeLast(4).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheTunnelWhenTheHttpsProxyRefusesTheDial_WritesOnlyTheSetupLines()
    {
        // curl -s -v --trace-config proxy,setup -p --proxy-insecure -x https://127.0.0.1:1
        // http://example.test/x: [SETUP] added, happy eyeballing to proxy, Trying, exit 7 (BL-1255 Notes).
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) },
            new FakeTlsProvider(),
            new ManualTimeProvider())
        {
            TracesSetupFilter = true,
            TracesHttpProxyFilter = true,
            TracesH1ProxyFilter = true,
        };

        var result = await ConnectLoggedAsync(connector, HttpsProxyTunnelTarget with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        AssertTunnelLines(
            new[] { SetupFilterTraceEvents.AddedLine, "[SETUP] happy eyeballing to proxy 192.0.2.10:3128" },
            events.Info.Where(line => line.StartsWith('[')).ToArray());
    }

    private static ConnectTarget HttpsProxyTunnelTarget => TunnelTarget with { Proxy = TunnelHttpsProxy };

    private static TcpConnector HttpsProxyTunnelConnector(ITlsProvider proxyTlsProvider, bool tracesHttpProxy = true, bool tracesH1Proxy = true, bool tracesSetup = false, bool tracesSslProxy = false) =>
        new(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            HttpProxyTunnelOptions.Default with { MatchesSchannelBuild = true },
            proxyTlsProvider: proxyTlsProvider)
        {
            TracesHttpProxyFilter = tracesHttpProxy,
            TracesH1ProxyFilter = tracesH1Proxy,
            TracesSetupFilter = tracesSetup,
            TracesSslProxyFilter = tracesSslProxy,
        };

    // Connects to example.test:80 through an HTTPS proxy whose handshake agrees on the ALPN protocol
    // given and whose CONNECT reply is proxyReply.
    private async Task<(RecordingTransferEvents Events, ConnectResult Result)> TraceThroughHttpsProxyAsync(
        string proxyReply,
        bool tracesHttpProxy = true,
        bool tracesH1Proxy = true,
        bool tracesSetup = false,
        string? agreed = null)
    {
        var events = new RecordingTransferEvents();
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(proxyReply));
        var proxyTls = new SequencedTlsProvider(ConnectResult.Connected(secured, null, applicationProtocol: agreed));
        var connector = HttpsProxyTunnelConnector(proxyTls, tracesHttpProxy, tracesH1Proxy, tracesSetup);
        var result = await ConnectLoggedAsync(connector, HttpsProxyTunnelTarget with { Events = events });
        Assert.AreEqual(TunnelConnectHead, Encoding.Latin1.GetString(secured.Written.ToArray()));
        return (events, result);
    }
}
