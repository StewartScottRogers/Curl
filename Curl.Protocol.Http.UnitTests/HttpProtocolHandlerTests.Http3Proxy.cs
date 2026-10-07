using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <c>--http3</c> and <c>--http3-only</c> through a proxy. A SOCKS proxy refuses HTTP/3
/// with <c>HTTP/3 is not supported over a SOCKS proxy</c> (BL-837, ADR-0223; unchanged in curl
/// 8.22.0): <c>--http3-only</c> then fails with exit 3 before connecting, and <c>--http3</c> runs
/// over TCP through the proxy and reports any failure with the refusal's text and the failure's
/// own exit code. An HTTP or HTTPS proxy is asked for QUIC through a CONNECT-UDP tunnel, as curl
/// 8.21.0 and later do (BL-942, measured on curl.se's 8.22.0 build): the connector gets the QUIC
/// connect with the proxy on its target, and <c>--http3</c> races it against a TCP
/// <c>CONNECT</c>.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string Http3SocksRefusal = "HTTP/3 is not supported over a SOCKS proxy";

    private static readonly ProxyKind[] Http3HttpProxyKinds = [ProxyKind.Http, ProxyKind.Http10, ProxyKind.Https];

    private static readonly ProxyKind[] Http3SocksProxyKinds = [ProxyKind.Socks4, ProxyKind.Socks4a, ProxyKind.Socks5, ProxyKind.Socks5Hostname];

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyThroughASocksProxy_FailsWithExit3AndTheRefusalBeforeConnecting()
    {
        // curl --http3-only -x socks5://127.0.0.1:<p> https://example.test/: exit 3, nothing
        // reaches the proxy, and -v prints the refusal then "closing connection #-1" (BL-837).
        foreach (ProxyKind kind in Http3SocksProxyKinds)
        {
            QueueConnector connector = new();
            RecordingTransferEvents events = new();

            Diagnostics.Arrange("url, proxy kind, version", $"https://example.test/, {kind}, http3-only");
            TransferResult result = await Handler(connector).ExecuteAsync(
                Http3Context("https://example.test/", new MemoryStream(), options: ThroughProxy(kind), events: events));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, result.ExitCode);
            Diagnostics.Assert("error message", Http3SocksRefusal, result.ErrorMessage);
            Diagnostics.Assert("info events", $"{Http3SocksRefusal}|closing connection #-1", string.Join("|", events.Info));
            Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode, kind.ToString());
            Assert.AreEqual(Http3SocksRefusal, result.ErrorMessage, kind.ToString());
            CollectionAssert.AreEqual(new[] { Http3SocksRefusal, "closing connection #-1" }, events.Info, kind.ToString());
            Assert.IsTrue(result.Report!.UsedProxy, kind.ToString());
            Assert.IsEmpty(connector.Targets, kind.ToString());
            Assert.IsEmpty(connector.MultiplexedTargets, kind.ToString());
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ThroughASocksProxy_ReportsTheRefusalAndConnectsOverTcpThroughTheProxy()
    {
        foreach (ProxyKind kind in Http3SocksProxyKinds)
        {
            QueueConnector connector = QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536));
            RecordingTransferEvents events = new();
            MemoryStream output = new();

            Diagnostics.Arrange("url, proxy kind, version", $"https://example.test/, {kind}, http3");
            Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length: 2, ok");
            TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
                "https://example.test/", output, options: ThroughProxy(kind), events: events, version: HttpVersionPreference.Http3));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Diff("output", "ok", Latin1(output.ToArray()));
            Diagnostics.Assert("first info event", Http3SocksRefusal, events.Info[0]);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, kind.ToString());
            Assert.IsNull(result.ErrorMessage, kind.ToString());
            Assert.AreEqual("ok", Latin1(output.ToArray()), kind.ToString());
            Assert.AreEqual(new Version(1, 1), result.Report!.HttpVersion, kind.ToString());
            Assert.AreEqual(Http3SocksRefusal, events.Info[0], "the refusal comes before the connect's lines");
            Assert.HasCount(1, connector.Targets, kind.ToString());
            Assert.AreEqual(kind, connector.Targets[0].Proxy!.Kind, "tunnelled through the proxy");
            Assert.IsEmpty(connector.MultiplexedTargets, kind.ToString());
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ThroughASocksProxyThatFails_KeepsTheFailuresExitAndReportsTheRefusal()
    {
        // curl --http3 -x socks5://… against a server that answers the greeting with junk:
        // curl: (97) HTTP/3 is not supported over a SOCKS proxy (BL-837). The refusal is the
        // first message curl writes into its error buffer, so the later failure's is not shown.
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.Proxy, "SOCKS5 nothing"));

        Diagnostics.Arrange("url, proxy kind, version", "https://example.test/, Socks5, http3");
        Diagnostics.Arrange("scripted connect", "failed, Proxy, SOCKS5 nothing");
        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "https://example.test/", new MemoryStream(), options: ThroughProxy(ProxyKind.Socks5), version: HttpVersionPreference.Http3));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Proxy, result.ExitCode);
        Diagnostics.Assert("error message", Http3SocksRefusal, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(Http3SocksRefusal, result.ErrorMessage);
        Assert.IsEmpty(connector.MultiplexedTargets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyThroughAnHttpProxy_RunsHttp3OverTheProxysUdpTunnel()
    {
        // curl --http3-only -x http://127.0.0.1:<p> https://example.com/ sends CONNECT-UDP to the
        // proxy and runs QUIC inside the tunnel (BL-942); the connector opens the tunnel, so the
        // handler asks it for QUIC with the proxy on the target and never connects over TCP.
        foreach (ProxyKind kind in Http3HttpProxyKinds)
        {
            FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200"), Http3Data("ok")), 65536);
            QueueConnector connector = QuicConnector(new FakeMultiplexedConnection(stream));
            MemoryStream output = new();

            Diagnostics.Arrange("url, proxy kind, version", $"https://example.test/, {kind}, http3-only");
            Diagnostics.Arrange("scripted response", "h3 200 ok");
            TransferResult result = await Handler(connector).ExecuteAsync(
                Http3Context("https://example.test/", output, options: ThroughProxy(kind)));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Diff("output", "ok", Latin1(output.ToArray()));
            Diagnostics.Assert("http version", new Version(3, 0), result.Report!.HttpVersion);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, kind.ToString());
            Assert.AreEqual("ok", Latin1(output.ToArray()), kind.ToString());
            Assert.AreEqual(new Version(3, 0), result.Report!.HttpVersion, kind.ToString());
            Assert.IsTrue(result.Report.UsedProxy, kind.ToString());
            ConnectTarget target = connector.MultiplexedTargets.Single();
            Assert.AreEqual("example.test", target.Host, kind.ToString());
            Assert.AreEqual(443, target.Port, kind.ToString());
            Assert.AreEqual(kind, target.Proxy!.Kind, "the QUIC connect goes through the proxy");
            Assert.IsEmpty(connector.Targets, kind.ToString());
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyThroughAnHttpProxyThatRefusesTheUdpTunnel_FailsWithExit7AndTheProxysStatus()
    {
        // curl 8.22.0 --http3-only -x http://127.0.0.1:<p> https://example.com/ answered 403:
        // curl: (7) CONNECT-UDP tunnel failed, response 403 (BL-942 Notes).
        QueueConnector connector = new();
        connector.MultiplexedResults.Enqueue(MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT-UDP tunnel failed, response 403"));

        Diagnostics.Arrange("url, proxy kind, version", "https://example.test/, Http, http3-only");
        Diagnostics.Arrange("scripted quic connect", "failed, CouldntConnect, CONNECT-UDP tunnel failed, response 403");
        TransferResult result = await Handler(connector).ExecuteAsync(
            Http3Context("https://example.test/", new MemoryStream(), options: ThroughProxy(ProxyKind.Http)));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", "CONNECT-UDP tunnel failed, response 403", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT-UDP tunnel failed, response 403", result.ErrorMessage);
        Assert.HasCount(1, connector.MultiplexedTargets);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ThroughAnHttpProxyThatRefusesBothTunnels_FailsWithTheTcpConnectsExitAndMessage()
    {
        // curl 8.22.0 --http3 -x http://127.0.0.1:<p> https://example.com/ answered 403 to both:
        // CONNECT-UDP first, then CONNECT, and curl: (7) CONNECT tunnel failed, response 403 -
        // the TCP attempt's message, not the QUIC one's (BL-942 Notes).
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 403"));
        connector.MultiplexedResults.Enqueue(MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT-UDP tunnel failed, response 403"));

        Diagnostics.Arrange("url, proxy kind, version", "https://example.test/, Http, http3");
        Diagnostics.Arrange("scripted connects", "tcp: CONNECT tunnel failed, response 403; quic: CONNECT-UDP tunnel failed, response 403");
        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "https://example.test/", new MemoryStream(), options: ThroughProxy(ProxyKind.Http), version: HttpVersionPreference.Http3));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", "CONNECT tunnel failed, response 403", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 403", result.ErrorMessage);
        Assert.AreEqual(ProxyKind.Http, connector.MultiplexedTargets.Single().Proxy!.Kind);
        Assert.AreEqual(ProxyKind.Http, connector.Targets.Single().Proxy!.Kind);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ThroughAnHttpProxyThatRefusesTheUdpTunnel_FallsBackToHttp11OverATcpConnect()
    {
        // The race's TCP CONNECT starts once the CONNECT-UDP fails (measured: at once after the
        // 403, or 100 ms in when the proxy is slow to answer, BL-942 Notes) and carries the transfer.
        QueueConnector connector = QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536));
        connector.MultiplexedResults.Enqueue(MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT-UDP tunnel failed, response 403"));
        MemoryStream output = new();

        Diagnostics.Arrange("url, proxy kind, version", "https://example.test/, Https, http3");
        Diagnostics.Arrange("scripted responses", "quic: failed 403; tcp: HTTP/1.1 200 ok");
        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "https://example.test/", output, options: ThroughProxy(ProxyKind.Https), version: HttpVersionPreference.Http3));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "ok", Latin1(output.ToArray()));
        Diagnostics.Assert("http version", new Version(1, 1), result.Report!.HttpVersion);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.AreEqual(new Version(1, 1), result.Report!.HttpVersion);
        Assert.HasCount(1, connector.MultiplexedTargets);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyThroughAProxyWithHttpUrl_FailsForTheUrlFirst()
    {
        // curl --http3-only -x http://127.0.0.1:<p> http://example.test/: exit 3,
        // HTTP/3 requested for non-HTTPS URL (BL-837).
        QueueConnector connector = new();
        RecordingTransferEvents events = new();

        Diagnostics.Arrange("url, proxy kind, version", "http://example.test/, Http, http3-only");
        TransferResult result = await Handler(connector).ExecuteAsync(
            Http3Context("http://example.test/", new MemoryStream(), options: ThroughProxy(ProxyKind.Http), events: events));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, result.ExitCode);
        Diagnostics.Assert("error message", "HTTP/3 requested for non-HTTPS URL", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("HTTP/3 requested for non-HTTPS URL", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "HTTP/3 requested for non-HTTPS URL", "closing connection #-1" }, events.Info);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ThroughAProxyWithHttpUrl_IsNotRefused()
    {
        // --http3 with an http:// URL is plain HTTP/1.1 (ADR-0144), so no refusal is reported.
        QueueConnector connector = QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536));
        RecordingTransferEvents events = new();

        Diagnostics.Arrange("url, proxy kind, version", "http://example.test/, Socks5, http3");
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length: 2, ok");
        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "http://example.test/", new MemoryStream(), options: ThroughProxy(ProxyKind.Socks5), events: events, version: HttpVersionPreference.Http3));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("info contains refusal", false, events.Info.Contains(Http3SocksRefusal));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.DoesNotContain(events.Info, Http3SocksRefusal);
    }

    private static HttpRequestOptions ThroughProxy(ProxyKind kind) =>
        new() { ForwardProxy = new ProxyEndpoint(kind, "127.0.0.1", 47837, null) };
}
