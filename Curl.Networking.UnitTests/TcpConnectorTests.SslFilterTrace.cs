using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives the <c>[SSL]</c> and <c>[SSL-PROXY]</c> lines curl 8.21.0's SSL filter writes around an
/// origin's and an HTTPS proxy's TLS handshake (<see cref="SslFilterTrace" />, measured with
/// <c>Record-CurlExchange.ps1 -Tls</c>, BL-1287 Notes; the poll rounds and the descriptor fixed,
/// ADR-0357's BL-1287 amendment).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly string[] OriginSslLines =
    [
        "[SSL] cf_connect()",
        "[SSL] cf_connect() -> 0, done=0",
        "[SSL] adjust_pollset, POLLIN fd=3",
        "[SSL] cf_connect()",
        "[SSL] cf_connect() -> 0, done=0",
        "[SSL] adjust_pollset, POLLIN fd=3",
        "[SSL] cf_connect()",
        "[SSL] cf_connect() -> 0, done=1",
        "[SSL] query ALPN",
        "[SSL] query ALPN: returning '(nil)'",
    ];

    [TestMethod]
    public async Task ConnectAsync_TracingTheSslFilterForHttps_WritesTheHandshakesLinesThenTheAlpnQueryAfterEstablishedConnection()
    {
        // curl -s -v -k --trace-config ssl https://127.0.0.1:P/x (BL-1287 Notes): done=1 before
        // Established connection, the ALPN query after it.
        var openedAtLine = new List<int>();
        RecordingTransferEvents events = null!;
        events = new RecordingTransferEvents { OnInfo = line => openedAtLine.AddRange(line.StartsWith("[SSL", StringComparison.Ordinal) ? [events.Opened.Count] : []) };
        var connector = SslTracingConnector(ConnectResult.Connected(new FakeConnection(), null), tracesSsl: true);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18961, UseTls: true) { Events = events, PoolScheme = "https" });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(OriginSslLines, SslLines(events));
        CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 }, openedAtLine);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSslFilterForHttpsThatAgreedOnH2_ReturnsTheAgreedProtocolToTheAlpnQuery()
    {
        var events = new RecordingTransferEvents();
        var connector = SslTracingConnector(ConnectResult.Connected(new FakeConnection(), null, applicationProtocol: "h2"), tracesSsl: true);

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18961, UseTls: true) { Events = events, PoolScheme = "https" });

        Diagnostics.Assert("last info line", "[SSL] query ALPN: returning 'h2'", events.Info[^1]);
        Assert.AreEqual("[SSL] query ALPN: returning 'h2'", events.Info[^1]);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSslFilterForAFailedHandshake_WritesOnePollRoundAndTheExitCode()
    {
        // curl -s -v --trace-config ssl https://127.0.0.1:P/x against an untrusted certificate: one
        // poll round, then cf_connect() -> 60, done=0 (BL-1287 Notes).
        var events = new RecordingTransferEvents();
        var connector = SslTracingConnector(ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "untrusted"), tracesSsl: true);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18963, UseTls: true) { Events = events, PoolScheme = "https" });

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SSL] cf_connect()",
                "[SSL] cf_connect() -> 0, done=0",
                "[SSL] adjust_pollset, POLLIN fd=3",
                "[SSL] cf_connect()",
                "[SSL] cf_connect() -> 60, done=0",
            },
            SslLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingOnlyTheSslProxyFilterForHttps_WritesNoSslLine()
    {
        var events = new RecordingTransferEvents();
        var connector = SslTracingConnector(ConnectResult.Connected(new FakeConnection(), null), tracesSslProxy: true);

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18961, UseTls: true) { Events = events, PoolScheme = "https" });

        Diagnostics.Assert("SSL lines", 0, SslLines(events).Length);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("[SSL", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSslFilterForAnImapsHandshake_WritesNoAlpnQuery()
    {
        var events = new RecordingTransferEvents();
        var connector = SslTracingConnector(ConnectResult.Connected(new FakeConnection(), null), tracesSsl: true);

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 993, UseTls: true) { Events = events, PoolScheme = "imaps" });

        Diagnostics.Diff("SSL lines", string.Join("\n", OriginSslLines.Take(8)), string.Join("\n", SslLines(events)));
        CollectionAssert.AreEqual(OriginSslLines.Take(8).ToArray(), SslLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSslProxyFilterForAForwardHttpsProxy_WritesSslProxyLinesWithoutAnAlpnQuery()
    {
        var events = new RecordingTransferEvents();
        var connector = SslTracingConnector(ConnectResult.Connected(new FakeConnection(), null), tracesSsl: true, tracesSslProxy: true);

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 3128, UseTls: true) { Events = events, PoolScheme = "https", IsForwardProxy = true });

        Diagnostics.Assert("SSL-PROXY lines", 8, SslLines(events).Length);
        CollectionAssert.AreEqual(OriginSslLines.Take(8).Select(line => line.Replace("[SSL]", "[SSL-PROXY]", StringComparison.Ordinal)).ToArray(), SslLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheProxyFiltersThroughAnHttpsProxy_WritesTheSslProxyLinesAmongTheTunnelsInCurlsOrder()
    {
        // curl -s -v --trace-config proxy -p --proxy-insecure -x https://127.0.0.1:P http://example.test/x
        // (BL-1255 and BL-1287 Notes).
        var events = new RecordingTransferEvents();
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(TunnelEstablishedReply));
        var connector = HttpsProxyTunnelConnector(new SequencedTlsProvider(ConnectResult.Connected(secured, null)), tracesSslProxy: true);

        await ConnectLoggedAsync(connector, HttpsProxyTunnelTarget with { Events = events });

        AssertTunnelLines(
            new[]
            {
                "[HTTP-PROXY] CONNECT",
                "[SSL-PROXY] cf_connect()",
                "[SSL-PROXY] cf_connect() -> 0, done=0",
                "[SSL-PROXY] adjust_pollset, POLLIN fd=3",
                "[HTTP-PROXY] CONNECT",
                "[SSL-PROXY] cf_connect()",
                "[SSL-PROXY] cf_connect() -> 0, done=0",
                "[SSL-PROXY] adjust_pollset, POLLIN fd=3",
                "[HTTP-PROXY] CONNECT",
                "[SSL-PROXY] cf_connect()",
                "[SSL-PROXY] cf_connect() -> 0, done=1",
                "[SSL-PROXY] query ALPN",
                "[SSL-PROXY] query ALPN: returning '(nil)'",
                "CONNECT: no ALPN negotiated",
                "[HTTP-PROXY] installing subfilter for HTTP/1.1",
            },
            events.Info.Skip(1).Take(15).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingOnlyTheSslFilterThroughAnHttpsProxyToAnHttpsOrigin_WritesTheOriginsLinesAndNoSslProxyLine()
    {
        // curl -s -v --trace-config ssl -p --proxy-insecure -x https://127.0.0.1:P ...: no [SSL-PROXY]
        // line (BL-1287 Notes); the origin's handshake runs over the tunnel.
        var events = new RecordingTransferEvents();
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(TunnelEstablishedReply));
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new SequencedTlsProvider(ConnectResult.Connected(new FakeConnection(), null)),
            new ManualTimeProvider(),
            HttpProxyTunnelOptions.Default with { MatchesSchannelBuild = true },
            proxyTlsProvider: new SequencedTlsProvider(ConnectResult.Connected(secured, null)))
        {
            TracesSslFilter = true,
        };

        await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 443, UseTls: true) { Proxy = TunnelHttpsProxy, PoolScheme = "https", Events = events });

        Diagnostics.Diff("SSL lines", string.Join("\n", OriginSslLines), string.Join("\n", SslLines(events)));
        CollectionAssert.AreEqual(OriginSslLines, events.Info.Where(line => line.StartsWith("[SSL", StringComparison.Ordinal)).ToArray());
    }

    private static TcpConnector SslTracingConnector(ConnectResult handshake, bool tracesSsl = false, bool tracesSslProxy = false) =>
        new(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new SequencedTlsProvider(handshake), new ManualTimeProvider(), proxyTlsProvider: new SequencedTlsProvider(handshake))
        {
            TracesSslFilter = tracesSsl,
            TracesSslProxyFilter = tracesSslProxy,
        };

    private static string[] SslLines(RecordingTransferEvents events) =>
        events.Info.Where(line => line.StartsWith("[SSL", StringComparison.Ordinal)).ToArray();
}
