using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-032 on <see cref="HttpProtocolHandler" /> over <see cref="Http2StreamConnection" />:
/// a stream the server resets with RST_STREAM HTTP_1_1_REQUIRED (0xd) is sent again over
/// HTTP/1.1 on a new connection, its body too, as curl 8.18.0 with nghttp2 does
/// (<c>Downgrades to HTTP/1.1</c>, exit 0), and a TLS connection for it offers only
/// <c>http/1.1</c> through ALPN (ADR-0906, BL-3561 Notes).
/// </summary>
[TestClass]
public sealed class Cgr032Tests
{
    private static readonly byte[] Http11Response = "HTTP/1.1 200 OK\r\nContent-Length: 9\r\n\r\nh1 hello\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Http2PriorKnowledgeStreamResetWithHttp11Required_SendsTheRequestAgainOverHttp11()
    {
        ScriptedConnection second = new(Http11Response, 65536);
        QueueConnector connector = QueueConnector.For(new ScriptedConnection(ResetResponse(), 65536), second);
        MemoryStream output = new();
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("first connection, second connection", "RST_STREAM HTTP_1_1_REQUIRED, HTTP/1.1 200 h1 hello");

        TransferResult result = await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(
            Context("http://127.0.0.1:21612/x", output, events, new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" }));

        string sent = Encoding.Latin1.GetString(second.Written);
        Diagnostics.Act("exit code, connects, body, second request", string.Join(", ", result.ExitCode, connector.Targets.Count, Encoding.Latin1.GetString(output.ToArray()), sent));
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, connector.Targets.Count);
        Assert.AreEqual("h1 hello\n", Encoding.Latin1.GetString(output.ToArray()));
        Assert.StartsWith("GET /x HTTP/1.1\r\n", sent);
        CollectionAssert.Contains(events.Info, "Downgrades to HTTP/1.1");
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpsStreamResetWithHttp11Required_ResendsThePostOverAConnectionOfferingOnlyHttp11()
    {
        ScriptedConnection first = new(ResetResponse(), 65536) { IsSecure = true };
        ScriptedConnection second = new(Http11Response, 65536) { IsSecure = true };
        QueueConnector connector = new(ConnectResult.Connected(first, null, applicationProtocol: "h2"), ConnectResult.Connected(second, null, applicationProtocol: "http/1.1"));
        MemoryStream output = new();
        Diagnostics.Arrange("first connection (h2), second connection (http/1.1)", "RST_STREAM HTTP_1_1_REQUIRED, HTTP/1.1 200 h1 hello");

        TransferResult result = await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(
            Context("https://127.0.0.1:21613/x", output, new RecordingTransferEvents(), new HttpRequestOptions { UserAgent = "curl/8.18.0", Body = new BytesBody("hello"u8.ToArray(), "application/x-www-form-urlencoded") }));

        string sent = Encoding.Latin1.GetString(second.Written);
        Diagnostics.Act("exit code, second ALPN, second request", string.Join(", ", result.ExitCode, string.Join('/', connector.Targets[1].ApplicationProtocols ?? []), sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("h1 hello\n", Encoding.Latin1.GetString(output.ToArray()));
        Assert.IsNull(connector.Targets[0].ApplicationProtocols);
        CollectionAssert.AreEqual(new[] { "http/1.1" }, connector.Targets[1].ApplicationProtocols?.ToArray());
        Assert.StartsWith("POST /x HTTP/1.1\r\n", sent);
        Assert.EndsWith("\r\n\r\nhello", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2StreamResetWithAnotherError_FailsWithExit92WithoutARetry()
    {
        QueueConnector connector = QueueConnector.For(new ScriptedConnection(Response(Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.InternalError)), 65536));

        TransferResult result = await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(
            Context("http://127.0.0.1:21612/x", new MemoryStream(), new RecordingTransferEvents(), new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge }));

        Diagnostics.Act("exit code, connects", string.Join(", ", result.ExitCode, connector.Targets.Count));
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(1, connector.Targets.Count);
    }

    private static TransferContext Context(string url, Stream output, ITransferEvents events, HttpRequestOptions http) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        Events = events,
        Http = http,
    };

    private static byte[] ResetResponse() => Response(Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.Http11Required));

    private static byte[] Response(params Http2Frame[] frames) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
        .. frames.SelectMany(Http2FrameCodec.Serialize),
    ];
}
