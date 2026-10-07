using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesHttpProxyFilter" /> and <see cref="TcpConnector.TracesH1ProxyFilter" />,
/// curl 8.21.0's <c>[HTTP-PROXY]</c> and <c>[H1-PROXY]</c> lines around a CONNECT tunnel through a plain
/// HTTP proxy, and the setup filter's lines on that path (measured with <c>Record-CurlExchange.ps1</c>
/// playing the proxy, BL-1193 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string TunnelEstablishedReply = "HTTP/1.1 200 Connection established\r\n\r\n";

    private const string TunnelConnectHead =
        "CONNECT example.test:80 HTTP/1.1\r\nHost: example.test:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n";

    private static readonly ProxyEndpoint TunnelProxy = new(ProxyKind.Http, "192.0.2.10", 3128, null);

    [TestMethod]
    public async Task ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder()
    {
        // curl -s -v --trace-config proxy -p -x http://127.0.0.1:18932 http://example.test/x (BL-1193 Notes).
        var (events, result) = await TraceThroughTunnelAsync(TunnelEstablishedReply);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        AssertTunnelLines(
            new[]
            {
                "*   Trying 192.0.2.10:3128...",
                "* [HTTP-PROXY] CONNECT",
                "* CONNECT: no ALPN negotiated",
                "* [HTTP-PROXY] installing subfilter for HTTP/1.1",
                "* [H1-PROXY] connect",
                "* [H1-PROXY] CONNECT start",
                "* Establishing HTTP proxy tunnel to example.test:80",
                "* [H1-PROXY] new tunnel state 'connect'",
                "* [H1-PROXY] CONNECT send",
                "> CONNECT example.test:80 HTTP/1.1",
                "> Host: example.test:80",
                "> User-Agent: curl/8.21.0",
                "> Proxy-Connection: Keep-Alive",
                "> ",
                "* [H1-PROXY] new tunnel state 'receive'",
                "* [H1-PROXY] CONNECT receive",
                "* [HTTP-PROXY] CONNECT",
                "* [H1-PROXY] connect",
                "* [H1-PROXY] CONNECT receive",
                "< HTTP/1.1 200 Connection established",
                "< ",
                "* [H1-PROXY] new tunnel state 'response'",
                "* [H1-PROXY] CONNECT response",
                "* [H1-PROXY] new tunnel state 'established'",
                "* CONNECT phase completed for HTTP proxy",
                "* CONNECT tunnel established, response 200",
                "* [H1-PROXY] new tunnel state 'failed'",
                "* [HTTP-PROXY] removing connected setup filter",
                "* [HTTP-PROXY] destroy",
                "* [H1-PROXY] query ALPN",
            },
            events.Transcript);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheProxyFiltersThroughATunnelAsTheOpenSslBuild_AllocatesTheConnectBufferAfterConnectStart()
    {
        // curl's OpenSSL build writes allocate connect buffer before Establishing (ADR-0342).
        var (events, _) = await TraceThroughTunnelAsync(TunnelEstablishedReply, matchesSchannelBuild: false);

        Diagnostics.Assert("connect start line", "* [H1-PROXY] CONNECT start", events.Transcript[5]);
        AssertTunnelLines(
            new[]
            {
                "* [H1-PROXY] CONNECT start",
                "* allocate connect buffer",
                "* Establishing HTTP proxy tunnel to example.test:80",
            },
            events.Transcript.Skip(5).Take(3).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheProxyFiltersThroughATunnel_RemovesTheHttpProxyFilterAfterEstablishedConnection()
    {
        var openedBeforeRemoval = -1;
        RecordingTransferEvents? recording = null;
        recording = new RecordingTransferEvents
        {
            OnInfo = line =>
            {
                if (line == "[HTTP-PROXY] removing connected setup filter")
                {
                    openedBeforeRemoval = recording!.Opened.Count;
                }
            },
        };

        await TraceThroughTunnelAsync(TunnelEstablishedReply, events: recording);

        Diagnostics.Assert("events opened before removal", 1, openedBeforeRemoval);
        Assert.AreEqual(1, openedBeforeRemoval);
    }

    [TestMethod]
    [DataRow(true, false, "[HTTP-PROXY] ", 5)]
    [DataRow(false, true, "[H1-PROXY] ", 13)]
    public async Task ConnectAsync_TracingOneProxyFilter_WritesOnlyThatFiltersLines(bool tracesHttpProxy, bool tracesH1Proxy, string prefix, int count)
    {
        // curl -v --trace-config http-proxy, and --trace-config h1-proxy (BL-1193 Notes).
        var (events, _) = await TraceThroughTunnelAsync(TunnelEstablishedReply, tracesHttpProxy, tracesH1Proxy);

        var tunnelLines = events.Info.Where(line => line.StartsWith("[HTTP-PROXY] ", StringComparison.Ordinal) || line.StartsWith("[H1-PROXY] ", StringComparison.Ordinal)).ToArray();
        Diagnostics.Assert("tunnel line count", count, tunnelLines.Length);
        Assert.HasCount(count, tunnelLines);
        Assert.IsTrue(tunnelLines.All(line => line.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughATunnelWithoutTracingIt_WritesNoTunnelOrSetupLine()
    {
        var (events, result) = await TraceThroughTunnelAsync(TunnelEstablishedReply, tracesHttpProxy: false, tracesH1Proxy: false);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith('[')));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheProxyFiltersWhenTheProxyRefusesTheConnect_EndsTheTunnelStatesAtFailed()
    {
        // curl -s -v --trace-config proxy,setup -p -x http://127.0.0.1:18942 http://example.test/x, the proxy
        // answering 403: exit 7 after 'response', CONNECT response and 'failed' (BL-1193 Notes).
        var (events, result) = await TraceThroughTunnelAsync("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n");

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        AssertTunnelLines(
            new[]
            {
                "[H1-PROXY] new tunnel state 'response'",
                "[H1-PROXY] CONNECT response",
                "[H1-PROXY] new tunnel state 'failed'",
            },
            events.Info.Where(line => line.StartsWith('[')).TakeLast(3).ToArray());
        Assert.IsFalse(events.Info.Contains("[HTTP-PROXY] removing connected setup filter"));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterThroughATunnel_WritesItsLinesAroundTheTunnel()
    {
        // curl -s -vv -p -x http://127.0.0.1:18933 http://example.test/x (BL-1193 Notes).
        var (events, _) = await TraceThroughTunnelAsync(TunnelEstablishedReply, tracesHttpProxy: false, tracesH1Proxy: false, tracesSetup: true);

        Diagnostics.Assert("first line", "* [SETUP] added", events.Transcript[0]);
        AssertTunnelLines(
            new[]
            {
                "* [SETUP] added",
                "* [SETUP] happy eyeballing to proxy 192.0.2.10:3128",
                "*   Trying 192.0.2.10:3128...",
                "* [SETUP] added HTTP proxy tunnel filter",
                "* CONNECT: no ALPN negotiated",
                "* Establishing HTTP proxy tunnel to example.test:80",
            },
            events.Transcript.Take(6).ToArray());
        AssertTunnelLines(
            new[] { "* [SETUP] removing connected setup filter", "* [SETUP] destroy" },
            events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterThroughAnHttp10ProxyTunnel_EyeballsToTheProxy()
    {
        // --proxy1.0 tunnels through the same filters as -x http:// (BL-1193 Notes).
        var target = TunnelTarget with { Proxy = TunnelProxy with { Kind = ProxyKind.Http10 } };
        var (events, _) = await TraceThroughTunnelAsync("HTTP/1.0 200 Connection established\r\n\r\n", tracesSetup: true, target: target, connectHead: TunnelConnectHead.Replace("HTTP/1.1", "HTTP/1.0", StringComparison.Ordinal));

        Diagnostics.Assert("eyeballing line", "[SETUP] happy eyeballing to proxy 192.0.2.10:3128", events.Info[1]);
        Assert.AreEqual("[SETUP] happy eyeballing to proxy 192.0.2.10:3128", events.Info[1]);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupAndProxyFiltersThroughATunnel_RemovesTheSetupFilterFirst()
    {
        // curl -s -v --trace-config all -p -x http://127.0.0.1:18931 http://example.test/x (BL-1193 Notes).
        var (events, _) = await TraceThroughTunnelAsync(TunnelEstablishedReply, tracesSetup: true);

        Diagnostics.Assert("last line", "* [H1-PROXY] query ALPN", events.Transcript[^1]);
        AssertTunnelLines(
            new[]
            {
                "* [SETUP] removing connected setup filter",
                "* [SETUP] destroy",
                "* [HTTP-PROXY] removing connected setup filter",
                "* [HTTP-PROXY] destroy",
                "* [H1-PROXY] query ALPN",
            },
            events.Transcript.TakeLast(5).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterThroughATunnelToAnHttpsOrigin_AddsTheSslFilterAfterTheTunnel()
    {
        // curl -s -v --trace-config proxy,setup -x http://127.0.0.1:18937 https://example.test/x: no
        // [SETUP] added, and [SETUP] added SSL filter for origin after the tunnel's 'failed' state (BL-1193 Notes).
        var target = new ConnectTarget("example.test", 443, UseTls: true) { Proxy = TunnelProxy, PoolScheme = "https" };
        var (events, _) = await TraceThroughTunnelAsync(TunnelEstablishedReply, tracesSetup: true, target: target);

        Diagnostics.Assert("setup added line written", false, events.Info.Contains(SetupFilterTraceEvents.AddedLine));
        Assert.IsFalse(events.Info.Contains(SetupFilterTraceEvents.AddedLine));
        Assert.AreEqual("[SETUP] happy eyeballing to proxy 192.0.2.10:3128", events.Info[0]);
        var failed = events.Info.IndexOf("[H1-PROXY] new tunnel state 'failed'");
        Assert.AreEqual(failed + 1, events.Info.IndexOf(TcpConnector.SslFilterAddedLine));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheTunnelWhenTheProxyRefusesTheDial_WritesNoTunnelLine()
    {
        // curl -s -v --trace-config proxy,setup -p -x http://127.0.0.1:1 http://example.test/x: [SETUP]
        // added, happy eyeballing to proxy, Trying and the failure, exit 7 (BL-1193 Notes).
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

        var result = await ConnectLoggedAsync(connector, TunnelTarget with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        AssertTunnelLines(
            new[] { SetupFilterTraceEvents.AddedLine, "[SETUP] happy eyeballing to proxy 192.0.2.10:3128" },
            events.Info.Where(line => line.StartsWith('[')).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheTunnelWithAHeaderOutput_StillWritesTheReplyHeadToIt()
    {
        var events = new HeadRecordingTransferEvents();
        var connector = TunnelConnector(new ScriptedConnection(Encoding.Latin1.GetBytes(TunnelEstablishedReply)), tracesSetup: true);

        await ConnectLoggedAsync(connector, TunnelTarget with { Events = events });

        Diagnostics.Assert("reply head", TunnelEstablishedReply, string.Concat(events.Heads));
        AssertTunnelLines(new[] { TunnelEstablishedReply }, events.Heads);
    }

    private void AssertTunnelLines(string[] expected, IEnumerable<string> actual)
    {
        var expectedText = string.Join(Environment.NewLine, expected);
        var actualText = string.Join(Environment.NewLine, actual);
        Diagnostics.Diff("tunnel lines", string.Join('\n', expected), string.Join('\n', actual));
        Assert.AreEqual(expectedText, actualText);
    }

    private static ConnectTarget TunnelTarget => new("example.test", 80, UseTls: false) { Proxy = TunnelProxy, PoolScheme = "http" };

    // Pins curl's Schannel build, the one these lines were measured from (BL-1193 Notes), so the
    // tests pass on every platform; the OpenSSL build's extra line is pinned by its own test (BL-1256).
    private static TcpConnector TunnelConnector(ScriptedConnection proxyConnection, bool tracesHttpProxy = true, bool tracesH1Proxy = true, bool tracesSetup = false, bool matchesSchannelBuild = true) =>
        new(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => proxyConnection },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            HttpProxyTunnelOptions.Default with { MatchesSchannelBuild = matchesSchannelBuild })
        {
            TracesHttpProxyFilter = tracesHttpProxy,
            TracesH1ProxyFilter = tracesH1Proxy,
            TracesSetupFilter = tracesSetup,
        };

    // Connects to example.test:80 (or the target given) through an HTTP proxy at
    // proxy.example:3128 that answers the CONNECT with proxyReply.
    private async Task<(RecordingTransferEvents Events, ConnectResult Result)> TraceThroughTunnelAsync(
        string proxyReply,
        bool tracesHttpProxy = true,
        bool tracesH1Proxy = true,
        bool tracesSetup = false,
        ConnectTarget? target = null,
        RecordingTransferEvents? events = null,
        string connectHead = TunnelConnectHead,
        bool matchesSchannelBuild = true)
    {
        events ??= new RecordingTransferEvents();
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes(proxyReply));
        var connector = TunnelConnector(proxyConnection, tracesHttpProxy, tracesH1Proxy, tracesSetup, matchesSchannelBuild);
        var result = await ConnectLoggedAsync(connector, (target ?? TunnelTarget) with { Events = events });
        Assert.AreEqual(connectHead.Replace("example.test:80", $"example.test:{(target ?? TunnelTarget).Port}", StringComparison.Ordinal), Encoding.Latin1.GetString(proxyConnection.Written.ToArray()));
        return (events, result);
    }
}
