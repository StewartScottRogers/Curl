using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesHttpsConnectFilter" /> through an HTTP proxy's tunnel, a SOCKS
/// proxy and a Unix socket, with the <c>[SETUP]</c> lines of the <c>https://</c> origin beside them
/// (measured with <c>Record-CurlExchange.ps1</c> playing the proxy or the socket, BL-1254 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly string[] HttpsConnectInit =
    [
        "[HTTPS-CONNECT] added",
        "[HTTPS-CONNECT] connect, init",
        "[HTTPS-CONNECT] 1st attempt uses h2 from wanted versions",
    ];

    private static readonly string[] HttpsConnectOpened =
    [
        "[HTTPS-CONNECT] connect -> 0, done=1",
        "opened",
        "[HTTPS-CONNECT] removing connected setup filter",
        "[HTTPS-CONNECT] destroy",
        "[SETUP] removing connected setup filter",
        "[SETUP] destroy",
    ];

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectAndSetupFiltersThroughAnHttpProxy_WritesTheirLinesAroundTheTunnel()
    {
        // curl -s -k -v --trace-config https-connect,setup -x http://127.0.0.1:18454 https://example.test/ (BL-1254 Notes).
        var events = new CountingTransferEvents();
        var connector = HttpsConnectTraceConnector(new ScriptedConnection(Encoding.Latin1.GetBytes(TunnelEstablishedReply)), ProxyAddress);

        var result = await ConnectLoggedAsync(connector, HttpsOriginThrough(TunnelProxy, events));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        AssertTunnelLines(
            [
                .. HttpsConnectInit,
                "[SETUP] happy eyeballing to proxy 192.0.2.10:3128",
                "  Trying 192.0.2.10:3128...",
                HttpsConnecting,
                HttpsPollset,
                TcpConnector.HttpProxyTunnelFilterAddedLine,
                "CONNECT: no ALPN negotiated",
                "Establishing HTTP proxy tunnel to example.test:443",
                "request",
                HttpsConnecting,
                HttpsPollset,
                "response",
                "response",
                "CONNECT phase completed for HTTP proxy",
                "CONNECT tunnel established, response 200",
                TcpConnector.SslFilterAddedLine,
                HttpsConnecting,
                HttpsPollset,
                HttpsConnecting,
                HttpsPollset,
                "handshake",
                .. HttpsConnectOpened,
            ],
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectFilterThroughAnHttpProxyWhenTheHandshakeFails_EndsWithExit35()
    {
        // The proxy closes the tunnel at the ClientHello: exit 35 (BL-1254 Notes).
        var events = new CountingTransferEvents();
        var connector = HttpsConnectTraceConnector(
            new ScriptedConnection(Encoding.Latin1.GetBytes(TunnelEstablishedReply)),
            ProxyAddress,
            new FakeTlsProvider { FailureToReturn = ConnectResult.Failed(CurlExitCode.SslConnectError, "x") });

        var result = await ConnectLoggedAsync(connector, HttpsOriginThrough(TunnelProxy, events));

        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "[HTTPS-CONNECT] connect, all attempts failed", "[HTTPS-CONNECT] connect -> 35, done=0" },
            events.Calls.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectAndSetupFiltersThroughSocks5h_WritesTwoPollRoundsForTheHandshake()
    {
        // curl -s -k -v --trace-config https-connect,setup -x socks5h://127.0.0.1:18455 https://example.test/ (BL-1254 Notes).
        var events = new CountingTransferEvents();
        var connector = HttpsConnectTraceConnector(new ScriptedConnection([.. Socks5NoAuthentication, .. Socks5Succeeded]), ProxyAddress);
        var socks = new ProxyEndpoint(ProxyKind.Socks5Hostname, "192.0.2.10", 1080, null);

        var result = await ConnectLoggedAsync(connector, HttpsOriginThrough(socks, events));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        AssertTunnelLines(
            [
                .. HttpsConnectInit,
                "[SETUP] happy eyeballing to origin 192.0.2.10:1080",
                "  Trying 192.0.2.10:1080...",
                HttpsConnecting,
                HttpsPollset,
                "[SETUP] added SOCKS filter to example.test:443",
                HttpsConnecting,
                HttpsPollset,
                HttpsConnecting,
                HttpsPollset,
                "Opened SOCKS connection from 127.0.0.1 port 50000 to example.test port 443 (via 192.0.2.10 port 1080)",
                TcpConnector.SslFilterAddedLine,
                HttpsConnecting,
                HttpsPollset,
                HttpsConnecting,
                HttpsPollset,
                "handshake",
                .. HttpsConnectOpened,
            ],
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterThroughSocksToAnHttpOrigin_EyeballsToTheSocksProxyAsOrigin()
    {
        // curl -s -v --trace-config setup -x socks5h://127.0.0.1:18457 http://example.test/ (BL-1254 Notes).
        Diagnostics.Arrange("proxy", "socks5h to example.test, setup filter traced");
        RecordingTransferEvents events;
        using (Diagnostics.Phase("socks handshake"))
        {
            events = await TraceThroughSocksAsync(ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, .. Socks5Succeeded], tracesSocks: false, tracesSetup: true);
        }

        Diagnostics.Act("info lines", events.Info.Count);

        Diagnostics.Assert("first setup line", SetupFilterTraceEvents.AddedLine, events.Info.First(line => line.StartsWith("[SETUP]", StringComparison.Ordinal)));
        AssertTunnelLines(
            [
                SetupFilterTraceEvents.AddedLine,
                "[SETUP] happy eyeballing to origin socks.example:1080",
                "[SETUP] added SOCKS filter to example.test:8080",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            ],
            events.Info.Where(line => line.StartsWith("[SETUP]", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectAndSetupFiltersOverAUnixSocket_EyeballsToThePathAtPort0()
    {
        // curl -s -k -v --trace-config https-connect,setup --unix-socket <path> https://example.test/ (BL-1254 Notes).
        var events = new CountingTransferEvents();
        var socket = new UnixSocketAddress("/run/app.sock", IsAbstract: false);
        var connector = new TcpConnector(
            new FakeDnsResolver(),
            new FakeTcpDialer { UnixSocketDialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider { HandshakeToReport = Handshake(verified: false) },
            new ManualTimeProvider(),
            unixSocket: socket)
        {
            TracesSetupFilter = true,
            TracesHttpsConnectFilter = true,
        };

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 443, UseTls: true) { Events = events, PoolScheme = "https" });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        AssertTunnelLines(
            [
                .. HttpsConnectInit,
                "[SETUP] happy eyeballing to origin /run/app.sock:0",
                "  Trying /run/app.sock:0...",
                HttpsConnecting,
                HttpsPollset,
                TcpConnector.SslFilterAddedLine,
                HttpsConnecting,
                HttpsPollset,
                HttpsConnecting,
                HttpsPollset,
                "handshake",
                .. HttpsConnectOpened,
            ],
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterOverAUnixSocketToAnHttpOrigin_WritesTheAddedLineFirst()
    {
        // curl -s -v --trace-config setup --unix-socket <path> http://example.test/ (BL-1254 Notes).
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(),
            new FakeTcpDialer { UnixSocketDialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            unixSocket: new UnixSocketAddress("/run/app.sock", IsAbstract: false))
        {
            TracesSetupFilter = true,
            TracesHttpsConnectFilter = true,
        };

        await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 80, UseTls: false) { Events = events, PoolScheme = "http" });

        Diagnostics.Assert("first call", SetupFilterTraceEvents.AddedLine, events.Calls[0]);
        AssertTunnelLines(
            [
                SetupFilterTraceEvents.AddedLine,
                "[SETUP] happy eyeballing to origin /run/app.sock:0",
                "  Trying /run/app.sock:0...",
                "opened",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            ],
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectFilterWhenTheUnixSocketRefuses_EndsWithAllAttemptsFailedAndExit7()
    {
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(),
            new FakeTcpDialer { UnixSocketDialOutcome = _ => throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused) },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            unixSocket: new UnixSocketAddress("/run/app.sock", IsAbstract: false))
        {
            TracesHttpsConnectFilter = true,
        };

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 443, UseTls: true) { Events = events, PoolScheme = "https" });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "[HTTPS-CONNECT] connect, all attempts failed", "[HTTPS-CONNECT] connect -> 7, done=0" },
            events.Calls.TakeLast(2).ToArray());
    }

    private static ConnectTarget HttpsOriginThrough(ProxyEndpoint proxy, ITransferEvents events) =>
        new("example.test", 443, UseTls: true) { Proxy = proxy, Events = events, PoolScheme = "https" };

    private static TcpConnector HttpsConnectTraceConnector(ScriptedConnection proxyConnection, System.Net.IPAddress proxyAddress, FakeTlsProvider? tlsProvider = null) =>
        new(
            new FakeDnsResolver(proxyAddress),
            new FakeTcpDialer { DialOutcome = _ => proxyConnection },
            tlsProvider ?? new FakeTlsProvider { HandshakeToReport = Handshake(verified: false) },
            new ManualTimeProvider(),
            HttpProxyTunnelOptions.Default with { MatchesSchannelBuild = true })
        {
            TracesSetupFilter = true,
            TracesHttpsConnectFilter = true,
        };
}
