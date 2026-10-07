using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <c>--http3</c> and <c>--http3-only</c> over a Unix domain socket (BL-867), as read from
/// <c>Curl_conn_may_http3</c> in <c>lib/vquic/vquic.c</c> at <c>curl-8_21_0</c> (ADR-0187): the
/// Unix socket is checked first, so <c>--http3-only</c> fails before connecting with exit 96 and
/// <c>HTTP/3 cannot be used over UNIX domain sockets</c> for an <c>https://</c> and an
/// <c>http://</c> URL alike; <c>--http3</c> with an <c>https://</c> URL drops <c>h3</c> and goes
/// on over the Unix socket, reporting any later failure with the refusal's text and the
/// failure's own exit code.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string Http3NotOverUnixSocket = "HTTP/3 cannot be used over UNIX domain sockets";

    private static readonly HttpRequestOptions OverUnixSocket = new() { OverUnixSocket = true };

    [TestMethod]
    [DataRow("https://example.test/")]
    [DataRow("http://example.test/")]
    public async Task ExecuteAsync_Http3OnlyOverAUnixSocket_FailsWithExit96BeforeConnecting(string url)
    {
        // The Unix socket check comes before the non-HTTPS one, so an http:// URL gives exit 96
        // too, not exit 3 (curl-8_21_0, BL-867 Notes).
        QueueConnector connector = new();
        RecordingTransferEvents events = new();

        Diagnostics.Arrange("url, version, unix socket", $"{url}, http3-only, true");
        TransferResult result = await Handler(connector).ExecuteAsync(
            Http3Context(url, new MemoryStream(), options: OverUnixSocket, events: events));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.QuicConnectError, result.ExitCode);
        Diagnostics.Assert("error message", Http3NotOverUnixSocket, result.ErrorMessage);
        Diagnostics.Assert("info events", $"{Http3NotOverUnixSocket}|closing connection #-1", string.Join("|", events.Info));
        Assert.AreEqual(CurlExitCode.QuicConnectError, result.ExitCode);
        Assert.AreEqual(Http3NotOverUnixSocket, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { Http3NotOverUnixSocket, "closing connection #-1" }, events.Info);
        Assert.IsEmpty(connector.Targets);
        Assert.IsEmpty(connector.MultiplexedTargets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OnlyOverAUnixSocketThroughAProxy_FailsForTheUnixSocketFirst()
    {
        QueueConnector connector = new();

        Diagnostics.Arrange("url, version, proxy kind, unix socket", "https://example.test/, http3-only, Socks5, true");
        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "https://example.test/", new MemoryStream(), options: ThroughProxy(ProxyKind.Socks5) with { OverUnixSocket = true }));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.QuicConnectError, result.ExitCode);
        Diagnostics.Assert("error message", Http3NotOverUnixSocket, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.QuicConnectError, result.ExitCode);
        Assert.AreEqual(Http3NotOverUnixSocket, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OverAUnixSocket_ReportsTheRefusalAndConnectsWithoutQuic()
    {
        QueueConnector connector = QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536));
        RecordingTransferEvents events = new();
        MemoryStream output = new();

        Diagnostics.Arrange("url, version, unix socket", "https://example.test/, http3, true");
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length: 2, ok");
        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "https://example.test/", output, options: OverUnixSocket, events: events, version: HttpVersionPreference.Http3));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "ok", Latin1(output.ToArray()));
        Diagnostics.Assert("first info event", Http3NotOverUnixSocket, events.Info[0]);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.AreEqual(Http3NotOverUnixSocket, events.Info[0], "the refusal comes before the connect's lines");
        Assert.HasCount(1, connector.Targets);
        Assert.IsEmpty(connector.MultiplexedTargets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OverAUnixSocketThatFails_KeepsTheFailuresExitAndReportsTheRefusal()
    {
        // failf filled curl's error buffer with the refusal, so a later failure shows it.
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to the socket"));

        Diagnostics.Arrange("url, version, unix socket", "https://example.test/, http3, true");
        Diagnostics.Arrange("scripted connect", "failed, CouldntConnect, Failed to connect to the socket");
        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "https://example.test/", new MemoryStream(), options: OverUnixSocket, version: HttpVersionPreference.Http3));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", Http3NotOverUnixSocket, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(Http3NotOverUnixSocket, result.ErrorMessage);
        Assert.IsEmpty(connector.MultiplexedTargets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3OverAUnixSocketWithHttpUrl_IsNotRefused()
    {
        // --http3 with an http:// URL is plain HTTP/1.1 (ADR-0144), so no refusal is reported.
        QueueConnector connector = QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536));
        RecordingTransferEvents events = new();

        Diagnostics.Arrange("url, version, unix socket", "http://example.test/, http3, true");
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length: 2, ok");
        TransferResult result = await Handler(connector).ExecuteAsync(Http3Context(
            "http://example.test/", new MemoryStream(), options: OverUnixSocket, events: events, version: HttpVersionPreference.Http3));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("info contains refusal", false, events.Info.Contains(Http3NotOverUnixSocket));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.DoesNotContain(events.Info, Http3NotOverUnixSocket);
    }
}
