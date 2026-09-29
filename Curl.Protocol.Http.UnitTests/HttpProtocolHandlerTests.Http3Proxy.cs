using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <c>--http3</c> and <c>--http3-only</c> through a proxy (BL-837, ADR-0223), as measured on
/// curl.se's 8.18.0 ngtcp2 build with <c>Record-CurlExchange.ps1</c> as the proxy: a SOCKS
/// proxy refuses HTTP/3 with <c>HTTP/3 is not supported over a SOCKS proxy</c>, an HTTP or HTTPS
/// proxy with <c>HTTP/3 is not supported over an HTTP proxy</c>; <c>--http3-only</c> then fails
/// with exit 3 before connecting, and <c>--http3</c> runs over TCP through the proxy and reports
/// any failure with the refusal's text and the failure's own exit code.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private static readonly (ProxyKind Kind, string Message)[] Http3ProxyRefusals =
    [
        (ProxyKind.Http, "HTTP/3 is not supported over an HTTP proxy"),
        (ProxyKind.Http10, "HTTP/3 is not supported over an HTTP proxy"),
        (ProxyKind.Https, "HTTP/3 is not supported over an HTTP proxy"),
        (ProxyKind.Socks4, "HTTP/3 is not supported over a SOCKS proxy"),
        (ProxyKind.Socks4a, "HTTP/3 is not supported over a SOCKS proxy"),
        (ProxyKind.Socks5, "HTTP/3 is not supported over a SOCKS proxy"),
        (ProxyKind.Socks5Hostname, "HTTP/3 is not supported over a SOCKS proxy"),
    ];

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyThroughAProxy_FailsWithExit3AndTheRefusalBeforeConnecting()
    {
        // curl --http3-only -x <kind>://127.0.0.1:<p> https://example.test/: exit 3, nothing
        // reaches the proxy, and -v prints the refusal then "closing connection #-1" (BL-837).
        foreach ((ProxyKind kind, string message) in Http3ProxyRefusals)
        {
            QueueConnector connector = new();
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(connector).ExecuteAsync(
                Http3Context("https://example.test/", new MemoryStream(), options: ThroughProxy(kind), events: events));

            Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode, kind.ToString());
            Assert.AreEqual(message, result.ErrorMessage, kind.ToString());
            CollectionAssert.AreEqual(new[] { message, "closing connection #-1" }, events.Info, kind.ToString());
            Assert.IsTrue(result.Report!.UsedProxy, kind.ToString());
            Assert.IsEmpty(connector.Targets, kind.ToString());
            Assert.IsEmpty(connector.MultiplexedTargets, kind.ToString());
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ThroughAProxy_ReportsTheRefusalAndConnectsOverTcpThroughTheProxy()
    {
        foreach ((ProxyKind kind, string message) in Http3ProxyRefusals)
        {
            QueueConnector connector = QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536));
            RecordingTransferEvents events = new();
            MemoryStream output = new();

            TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
                "https://example.test/", output, options: ThroughProxy(kind), events: events, version: HttpVersionPreference.Http3));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, kind.ToString());
            Assert.IsNull(result.ErrorMessage, kind.ToString());
            Assert.AreEqual("ok", Latin1(output.ToArray()), kind.ToString());
            Assert.AreEqual(new Version(1, 1), result.Report!.HttpVersion, kind.ToString());
            Assert.AreEqual(message, events.Info[0], "the refusal comes before the connect's lines");
            Assert.HasCount(1, connector.Targets, kind.ToString());
            Assert.AreEqual(kind, connector.Targets[0].Proxy!.Kind, "tunnelled through the proxy");
            Assert.IsEmpty(connector.MultiplexedTargets, kind.ToString());
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ThroughAProxyThatFails_KeepsTheFailuresExitAndReportsTheRefusal()
    {
        // curl --http3 -x socks5://… against a server that answers the greeting with junk:
        // curl: (97) HTTP/3 is not supported over a SOCKS proxy; -x http://… answered 403:
        // curl: (56) HTTP/3 is not supported over an HTTP proxy (BL-837). The refusal is the
        // first message curl writes into its error buffer, so the later failure's is not shown.
        (ProxyKind Kind, CurlExitCode Exit, string Failure, string Shown)[] cases =
        [
            (ProxyKind.Socks5, CurlExitCode.Proxy, "SOCKS5 nothing", "HTTP/3 is not supported over a SOCKS proxy"),
            (ProxyKind.Http, CurlExitCode.RecvError, "CONNECT tunnel failed, response 403", "HTTP/3 is not supported over an HTTP proxy"),
        ];
        foreach ((ProxyKind kind, CurlExitCode exit, string failure, string shown) in cases)
        {
            QueueConnector connector = new(ConnectResult.Failed(exit, failure));

            TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
                "https://example.test/", new MemoryStream(), options: ThroughProxy(kind), version: HttpVersionPreference.Http3));

            Assert.AreEqual(exit, result.ExitCode, kind.ToString());
            Assert.AreEqual(shown, result.ErrorMessage, kind.ToString());
            Assert.IsEmpty(connector.MultiplexedTargets, kind.ToString());
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyThroughAProxyWithHttpUrl_FailsForTheUrlFirst()
    {
        // curl --http3-only -x http://127.0.0.1:<p> http://example.test/: exit 3,
        // HTTP/3 requested for non-HTTPS URL (BL-837).
        QueueConnector connector = new();
        RecordingTransferEvents events = new();

        TransferResult result = await Handler(connector).ExecuteAsync(
            Http3Context("http://example.test/", new MemoryStream(), options: ThroughProxy(ProxyKind.Http), events: events));

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

        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "http://example.test/", new MemoryStream(), options: ThroughProxy(ProxyKind.Socks5), events: events, version: HttpVersionPreference.Http3));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.DoesNotContain(events.Info, "HTTP/3 is not supported over a SOCKS proxy");
    }

    private static HttpRequestOptions ThroughProxy(ProxyKind kind) =>
        new() { ForwardProxy = new ProxyEndpoint(kind, "127.0.0.1", 47837, null) };
}
