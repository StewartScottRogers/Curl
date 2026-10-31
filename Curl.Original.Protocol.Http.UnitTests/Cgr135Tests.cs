using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-135 on <see cref="HttpProtocolHandler" /> over <see cref="Http2StreamConnection" />
/// and on <see cref="Http2ResponseHead.IndexOfInvalidField" />: an upper-case field name, a
/// connection-specific field, an unknown, repeated or misplaced pseudo-header, <c>:status 101</c>,
/// a pseudo-header or upper-case name in trailers, and trailers that do not end the stream are
/// malformed (RFC 9113 sections 8.1 to 8.3), and curl 8.18.0 with nghttp2 1.68 shuts the
/// connection down for them with GOAWAY PROTOCOL_ERROR, having written only the head lines
/// before the bad field (measured, BL-3664 Notes).
/// </summary>
[TestClass]
public sealed class Cgr135Tests
{
    private const string ShutsDown = "nghttp2 shuts down connection with error 1: PROTOCOL_ERROR";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    public static IEnumerable<object[]> MalformedResponses =>
    [
        ["upper", new[] { "H! :status: 200|X-Upper: v" }, CurlExitCode.Http2, "HTTP/2 200 \r\n", "", Invalid("X-Upper", "v")],
        ["connhdr", new[] { "H! :status: 200|connection: close" }, CurlExitCode.Http2, "HTTP/2 200 \r\n", "", Invalid("connection", "close")],
        ["keepalivehdr", new[] { "H! :status: 200|keep-alive: timeout=5" }, CurlExitCode.Http2, "HTTP/2 200 \r\n", "", Invalid("keep-alive", "timeout=5")],
        ["upgradehdr", new[] { "H! :status: 200|upgrade: h2c" }, CurlExitCode.Http2, "HTTP/2 200 \r\n", "", Invalid("upgrade", "h2c")],
        ["proxyconn", new[] { "H! :status: 200|proxy-connection: keep-alive" }, CurlExitCode.Http2, "HTTP/2 200 \r\n", "", Invalid("proxy-connection", "keep-alive")],
        ["transfer-encoding", new[] { "H! :status: 200|x-a: 1|transfer-encoding: chunked" }, CurlExitCode.Http2, "HTTP/2 200 \r\nx-a: 1\r\n", "", Invalid("transfer-encoding", "chunked")],
        ["resptegzip", new[] { "H! :status: 200|te: gzip" }, CurlExitCode.Http2, "HTTP/2 200 \r\n", "", Invalid("te", "gzip")],
        ["unkpseudo", new[] { "H! :status: 200|:foo: bar" }, CurlExitCode.Http2, "HTTP/2 200 \r\n", "", Invalid(":foo", "bar")],
        ["unkpseudofirst", new[] { "H! :foo: bar|:status: 200" }, CurlExitCode.Http2, "", "", Invalid(":foo", "bar")],
        ["dupstatus", new[] { "H! :status: 200|:status: 200" }, CurlExitCode.Http2, "HTTP/2 200 \r\n", "", Invalid(":status", "200")],
        ["pseudoafter", new[] { "H! x-a: 1|:status: 200" }, CurlExitCode.WeirdServerReply, "", "", Invalid(":status", "200")],
        ["status101", new[] { "H :status: 101", "H! :status: 200" }, CurlExitCode.Http2, "", "", Invalid(":status", "101")],
        ["trpseudo", new[] { "H :status: 200", "D hello h2\n", "H! :status: 200" }, CurlExitCode.Http2, "HTTP/2 200 \r\n\r\n", "hello h2\n", Invalid(":status", "200")],
        ["uppertrailer", new[] { "H :status: 200", "D hello h2\n", "H! X-T: 1" }, CurlExitCode.Http2, "HTTP/2 200 \r\n\r\n", "hello h2\n", Invalid("X-T", "1")],
        ["twoheaders", new[] { "H :status: 200", "H :status: 200", "D! hello h2\n" }, CurlExitCode.Http2, "HTTP/2 200 \r\n\r\n", "", Invalid(":status", "200")],
        ["trnoes", new[] { "H :status: 200", "D hello h2", "H x-t: 1", "D! more" }, CurlExitCode.Http2, "HTTP/2 200 \r\n\r\n", "hello h2", ShutsDown],
    ];

    [TestMethod]
    [DynamicData(nameof(MalformedResponses))]
    public async Task ExecuteAsync_Http2ResponseWithAnInvalidField_ShutsTheConnectionDownAfterTheLinesBeforeIt(
        string scenario, string[] frames, CurlExitCode expectedExitCode, string expectedHead, string expectedBody, string expectedMessage)
    {
        ScriptedConnection connection = new(Response(frames), 65536);
        MemoryStream output = new();
        MemoryStream headers = new();
        Diagnostics.Arrange("scenario, frames", $"{scenario}, {string.Join(" / ", frames)}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(Context(output, headers));

        Diagnostics.Act("exit code, message, head, body", string.Join(", ", result.ExitCode, result.ErrorMessage, Encoding.Latin1.GetString(headers.ToArray()), Encoding.Latin1.GetString(output.ToArray())));
        Diagnostics.Assert("exit code", expectedExitCode, result.ExitCode);
        Assert.AreEqual(expectedExitCode, result.ExitCode);
        Assert.AreEqual(expectedMessage, result.ErrorMessage);
        Assert.AreEqual(expectedHead, Encoding.Latin1.GetString(headers.ToArray()));
        Assert.AreEqual(expectedBody, Encoding.Latin1.GetString(output.ToArray()));
        byte[] goAway = Http2FrameCodec.Serialize(Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty));
        Assert.IsTrue(connection.Written.AsSpan().EndsWith(goAway), "the last frame sent is GOAWAY last 0 PROTOCOL_ERROR with no debug data");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2HeadWithContentLengthTwice_ReportsTheHeaderLineWrittenBeforeTheFailure()
    {
        ScriptedConnection connection = new(Response(["H! :status: 200|content-length: 9|content-length: 9"]), 65536);
        Diagnostics.Arrange("frames", "H! :status: 200|content-length: 9|content-length: 9 (cltwo)");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(Context(new MemoryStream(), new MemoryStream()));

        Diagnostics.Act("exit code, response headers", $"{result.ExitCode}, {string.Join("; ", result.Report!.ResponseHeaders)}");
        Diagnostics.Assert("response headers, the source of num_headers", "[content-length, 9]", string.Join("; ", result.Report.ResponseHeaders));
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        CollectionAssert.AreEqual(new[] { KeyValuePair.Create("content-length", "9") }, result.Report.ResponseHeaders.ToArray());
    }

    [TestMethod]
    [DataRow("H :status: 200|te: trailers", "D! hello h2\n", "", "HTTP/2 200 \r\nte: trailers\r\n\r\n", DisplayName = "te: trailers")]
    [DataRow("H :status: 200|x-a: 1", "D hello h2\n", "H! x-t: 1", "HTTP/2 200 \r\nx-a: 1\r\n\r\nx-t: 1\r\n", DisplayName = "trailers ending the stream")]
    public async Task ExecuteAsync_Http2ResponseWithValidFields_Succeeds(string head, string data, string trailer, string expectedHeaders)
    {
        string[] frames = trailer.Length > 0 ? [head, data, trailer] : [head, data];
        ScriptedConnection connection = new(Response(frames), 65536);
        MemoryStream output = new();
        MemoryStream headers = new();
        Diagnostics.Arrange("frames", string.Join(" / ", frames));

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(Context(output, headers));

        Diagnostics.Act("exit code, message, body", $"{result.ExitCode}, {result.ErrorMessage}, {Encoding.Latin1.GetString(output.ToArray())}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello h2\n", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(expectedHeaders, Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    [DataRow(":status: 200|x-a: 1|content-length: 9", false, -1, DisplayName = "valid head")]
    [DataRow(":status: 200|x a: 1|X\rb: 1", false, -1, DisplayName = "ignored names are skipped")]
    [DataRow(":status: 200|x-a: v\r|X-B: 1", false, 2, DisplayName = "an ignored value is skipped")]
    [DataRow(":status: 200|content-length: 9|content-length: 9", false, 2, DisplayName = "second content-length")]
    [DataRow(":status: 200|content-length: 9x", false, 1, DisplayName = "content-length not a number")]
    [DataRow(":status: 200|TE: trailers", false, 1, DisplayName = "upper-case te")]
    [DataRow(":status: 200|te: TRAILERS", false, -1, DisplayName = "te: trailers in any case")]
    [DataRow(":status: 2x0", false, -1, DisplayName = "a bad :status value is left to StatusOf")]
    [DataRow("x-t: 1", true, -1, DisplayName = "valid trailer")]
    [DataRow("x-t: 1|:status: 200", true, 1, DisplayName = "pseudo-header in trailers")]
    public void IndexOfInvalidField_ResponseHeaderBlock_GivesTheFirstFieldNghttp2Rejects(string fields, bool isTrailer, int expectedIndex)
    {
        HeaderField[] block = Fields(fields);
        Diagnostics.Arrange("fields, trailer", $"{fields}, {isTrailer}");

        int index = Http2ResponseHead.IndexOfInvalidField(block, isTrailer);

        Diagnostics.Act("index", index);
        Assert.AreEqual(expectedIndex, index);
    }

    private static string Invalid(string name, string value) =>
        $"Invalid HTTP header field was received: frame type: 1, stream: 1, name: [{name}], value: [{value}]";

    /// <summary>Splits <c>name: value</c> fields separated by <c>|</c>, as Record-CurlExchange.ps1's -Http2Frames does.</summary>
    private static HeaderField[] Fields(string fields) =>
        [.. fields.Split('|').Select(field => field.IndexOf(": ", 1, StringComparison.Ordinal) is var colon ? new HeaderField(field[..colon], field[(colon + 2)..]) : default)];

    private static TransferContext Context(Stream output, Stream headers) => new()
    {
        Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
        Output = output,
        HeaderOutput = headers,
        Events = new RecordingTransferEvents(),
        Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" },
    };

    /// <summary>
    /// Gives the server's bytes for frames written as -Http2Frames takes them: <c>H</c> for HEADERS
    /// or <c>D</c> for DATA, <c>!</c> when it ends the stream, a blank, then the fields or the text.
    /// </summary>
    private static byte[] Response(string[] frames)
    {
        HpackEncoder encoder = new();
        return
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
            .. frames.SelectMany(frame => Http2FrameCodec.Serialize(FrameOf(frame, encoder))),
        ];
    }

    private static Http2Frame FrameOf(string frame, HpackEncoder encoder)
    {
        int blank = frame.IndexOf(' ', StringComparison.Ordinal);
        bool isEndStream = frame[..blank].EndsWith('!');
        string content = frame[(blank + 1)..];
        return frame[0] == 'H'
            ? Http2FrameFactory.CreateHeaders(1, encoder.Encode(Fields(content)), isEndStream, isEndHeaders: true)
            : Http2FrameFactory.CreateData(1, Encoding.Latin1.GetBytes(content), isEndStream);
    }
}
