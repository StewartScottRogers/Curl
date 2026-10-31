using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-136 on <see cref="HttpProtocolHandler" /> over <see cref="Http2StreamConnection" />:
/// a connection-specific field in an HTTP/2 response head (<c>transfer-encoding</c>,
/// <c>connection</c>, <c>keep-alive</c>, <c>proxy-connection</c>, <c>upgrade</c>, or <c>te</c>
/// other than <c>trailers</c>) is an invalid header field to nghttp2 (RFC 9113 section 8.2.2):
/// curl 8.18.0 writes the head up to that field, sends GOAWAY PROTOCOL_ERROR and exits 16
/// (measured, BL-3665 Notes), rather than chunk-decoding the DATA.
/// </summary>
[TestClass]
public sealed class Cgr136Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("transfer-encoding", "chunked", "HTTP/2 200 \r\n", DisplayName = "r_resp_te")]
    [DataRow("transfer-encoding", "chunked", "HTTP/2 200 \r\nserver: x\r\n", "server", DisplayName = "after server: x")]
    [DataRow("connection", "close", "HTTP/2 200 \r\n", DisplayName = "connection")]
    [DataRow("keep-alive", "timeout=5", "HTTP/2 200 \r\n", DisplayName = "keep-alive")]
    [DataRow("proxy-connection", "close", "HTTP/2 200 \r\n", DisplayName = "proxy-connection")]
    [DataRow("upgrade", "h2c", "HTTP/2 200 \r\n", DisplayName = "upgrade")]
    [DataRow("te", "gzip", "HTTP/2 200 \r\n", DisplayName = "te other than trailers")]
    public async Task ExecuteAsync_Http2HeadWithAConnectionSpecificField_ShutsTheConnectionDownWithExit16(string name, string value, string expectedHead, string? fieldBefore = null)
    {
        HeaderField[] fields = fieldBefore is null
            ? [new(":status", "200"), new(name, value), new("content-type", "text/plain")]
            : [new(":status", "200"), new(fieldBefore, "x"), new(name, value), new("content-type", "text/plain")];
        ScriptedConnection connection = new(Response(fields), 65536);
        MemoryStream output = new();
        MemoryStream headers = new();
        Diagnostics.Arrange("field", $"{name}: {value}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()).ExecuteAsync(Context(output, headers));

        Diagnostics.Act("exit code, message, head", string.Join(", ", result.ExitCode, result.ErrorMessage, Encoding.Latin1.GetString(headers.ToArray())));
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual($"Invalid HTTP header field was received: frame type: 1, stream: 1, name: [{name}], value: [{value}]", result.ErrorMessage);
        Assert.AreEqual(expectedHead, Encoding.Latin1.GetString(headers.ToArray()));
        Assert.AreEqual(0, output.Length);
        byte[] goAway = Http2FrameCodec.Serialize(Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty));
        Assert.IsTrue(connection.Written.AsSpan().EndsWith(goAway), "the last frame sent is GOAWAY last 0 PROTOCOL_ERROR with no debug data");
    }

    [TestMethod]
    [DataRow("trailers")]
    [DataRow("Trailers")]
    public async Task ExecuteAsync_Http2HeadWithTeTrailers_Succeeds(string value)
    {
        ScriptedConnection connection = new(Response([new(":status", "200"), new("te", value), new("content-length", "9")]), 65536);
        MemoryStream output = new();
        Diagnostics.Arrange("field", $"te: {value}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()).ExecuteAsync(Context(output, new MemoryStream()));

        Diagnostics.Act("exit code, body", $"{result.ExitCode}, {Encoding.Latin1.GetString(output.ToArray())}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello h2\n", Encoding.Latin1.GetString(output.ToArray()));
    }

    private static TransferContext Context(Stream output, Stream headers) => new()
    {
        Url = CurlUrl.Parse("http://127.0.0.1:18665/x"),
        Output = output,
        HeaderOutput = headers,
        Events = new RecordingTransferEvents(),
        Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" },
    };

    private static byte[] Response(HeaderField[] fields) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(fields), isEndStream: false, isEndHeaders: true)),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "hello h2\n"u8.ToArray(), isEndStream: true)),
    ];
}
