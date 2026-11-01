using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-138 on <see cref="HttpProtocolHandler" /> over <see cref="Http2StreamConnection" />:
/// a response whose DATA does not add up to its <c>content-length</c>, a body on a response to
/// HEAD or on a 204, a 1xx head that ends the stream, and a <c>content-length</c> that is not a
/// number or comes twice are malformed (RFC 9113 section 8.1.1), and curl 8.18.0 with nghttp2
/// shuts the connection down for them with GOAWAY PROTOCOL_ERROR and exit 16, even with
/// <c>--ignore-content-length</c> (measured, BL-3667 Notes).
/// </summary>
[TestClass]
public sealed class Cgr138Tests
{
    private const string ShutsDown = "nghttp2 shuts down connection with error 1: PROTOCOL_ERROR";

    private static readonly byte[] Body = "hello h2\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    public static IEnumerable<object?[]> MalformedResponses =>
    [
        ["cllong", null, false, Fields("200", "content-length", "3"), true, "HTTP/2 200 \r\ncontent-length: 3\r\n\r\n", "", ShutsDown],
        ["clshort", null, false, Fields("200", "content-length", "100"), true, "HTTP/2 200 \r\ncontent-length: 100\r\n\r\n", "hello h2\n", ShutsDown],
        ["clshort, no DATA", null, false, Fields("200", "content-length", "100"), false, "HTTP/2 200 \r\ncontent-length: 100\r\n\r\n", "", ShutsDown],
        ["204body", null, false, Fields("204"), true, "HTTP/2 204 \r\n\r\n", "", ShutsDown],
        ["304 with DATA", null, false, Fields("304"), true, "HTTP/2 304 \r\n\r\n", "", ShutsDown],
        ["1xxes", null, false, Fields("103", "link", "</s.css>"), false, "HTTP/2 103 \r\nlink: </s.css>\r\n", "", ShutsDown],
        ["clbad", null, false, Fields("200", "content-length", "abc"), true, "HTTP/2 200 \r\n", "", "Invalid HTTP header field was received: frame type: 1, stream: 1, name: [content-length], value: [abc]"],
        ["cltwodiff", null, false, Fields("200", "content-length", "9", "content-length", "10"), true, "HTTP/2 200 \r\ncontent-length: 9\r\n", "", "Invalid HTTP header field was received: frame type: 1, stream: 1, name: [content-length], value: [10]"],
        ["--ignore-content-length, cllong", null, true, Fields("200", "content-length", "3"), true, "HTTP/2 200 \r\ncontent-length: 3\r\n\r\n", "", ShutsDown],
        ["-X HEAD, ok", "HEAD", false, Fields("200", "content-length", "9"), true, "HTTP/2 200 \r\ncontent-length: 9\r\n\r\n", "", ShutsDown],
    ];

    [TestMethod]
    [DynamicData(nameof(MalformedResponses))]
    public async Task ExecuteAsync_Http2ResponseMalformedByItsLength_ShutsTheConnectionDownWithExit16(
        string scenario, string? method, bool isContentLengthIgnored, HeaderField[] fields, bool hasData, string expectedHead, string expectedBody, string expectedMessage)
    {
        ScriptedConnection connection = new(Response(fields, hasData), 65536);
        MemoryStream output = new();
        MemoryStream headers = new();
        Diagnostics.Arrange("scenario", scenario);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(Context(output, headers, method, isContentLengthIgnored));

        Diagnostics.Act("exit code, message, head, body", string.Join(", ", result.ExitCode, result.ErrorMessage, Encoding.Latin1.GetString(headers.ToArray()), Encoding.Latin1.GetString(output.ToArray())));
        Diagnostics.Assert("exit code", CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual(expectedMessage, result.ErrorMessage);
        Assert.AreEqual(expectedHead, Encoding.Latin1.GetString(headers.ToArray()));
        Assert.AreEqual(expectedBody, Encoding.Latin1.GetString(output.ToArray()));
        byte[] goAway = Http2FrameCodec.Serialize(Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty));
        Assert.IsTrue(connection.Written.AsSpan().EndsWith(goAway), "the last frame sent is GOAWAY last 0 PROTOCOL_ERROR with no debug data");
    }

    [TestMethod]
    [DataRow(null, "200", "content-length", "9", DisplayName = "content-length matching the DATA")]
    [DataRow(null, "200", "x-other", "1", DisplayName = "no content-length")]
    public async Task ExecuteAsync_Http2ResponseWhoseLengthAddsUp_Succeeds(string? method, string status, string name, string value)
    {
        ScriptedConnection connection = new(Response(Fields(status, name, value), hasData: true), 65536);
        MemoryStream output = new();
        Diagnostics.Arrange("method, head", $"{method ?? "GET"}, {status} {name}: {value}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(Context(output, new MemoryStream(), method, isContentLengthIgnored: false));

        Diagnostics.Act("exit code, body", string.Join(", ", result.ExitCode, Encoding.Latin1.GetString(output.ToArray())));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello h2\n", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow(true, CurlExitCode.Ok, DisplayName = "-I, upstream test1702")]
    [DataRow(false, CurlExitCode.Http2, DisplayName = "GET")]
    public async Task ExecuteAsync_H2cUpgradedResponseWithAContentLengthAndNoData_IsWholeOnlyForAHeadRequest(bool isHead, CurlExitCode expectedExitCode)
    {
        byte[] reply = [.. "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n"u8, .. Response(Fields("200", "content-length", "6"), hasData: false)];
        ScriptedConnection connection = new(reply, 65536);
        Diagnostics.Arrange("request, reply", $"{(isHead ? "HEAD" : "GET")} with Upgrade: h2c, 101 then HEADERS content-length 6 ending the stream");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:21612/1702"),
                Output = new MemoryStream(),
                Events = new RecordingTransferEvents(),
                NoBody = isHead,
                Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2 },
            });

        Diagnostics.Act("exit code, message", $"{result.ExitCode}, {result.ErrorMessage}");
        Diagnostics.Assert("exit code", expectedExitCode, result.ExitCode);
        Assert.AreEqual(expectedExitCode, result.ExitCode);
    }

    private static HeaderField[] Fields(string status, params string[] namesAndValues) =>
        [new(":status", status), .. namesAndValues.Chunk(2).Select(pair => new HeaderField(pair[0], pair[1]))];

    private static TransferContext Context(Stream output, Stream headers, string? method, bool isContentLengthIgnored) => new()
    {
        Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
        Output = output,
        HeaderOutput = headers,
        Events = new RecordingTransferEvents(),
        Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0", CustomMethod = method, IgnoreContentLength = isContentLengthIgnored },
    };

    private static byte[] Response(HeaderField[] fields, bool hasData)
    {
        Http2Frame[] frames = hasData
            ? [Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(fields), isEndStream: false, isEndHeaders: true), Http2FrameFactory.CreateData(1, Body, isEndStream: true)]
            : [Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(fields), isEndStream: true, isEndHeaders: true)];
        return
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
            .. frames.SelectMany(Http2FrameCodec.Serialize),
        ];
    }
}
