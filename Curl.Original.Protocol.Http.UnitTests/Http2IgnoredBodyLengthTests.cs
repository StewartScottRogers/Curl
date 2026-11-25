using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5947: an HTTP/2 final head that ends the stream although its <c>content-length</c>
/// promises a body fails with exit 16 for <c>-I -X GET</c>, as without <c>-I</c>, and succeeds
/// for <c>-I</c> alone, whose HEAD request is promised no body; and BL-5959: with <c>-I -X GET</c>,
/// a trailer or DATA that ends the stream short of <c>content-length</c> shuts the connection down
/// with GOAWAY PROTOCOL_ERROR and succeeds.
/// </summary>
[TestClass]
public sealed class Http2IgnoredBodyLengthTests
{
    private const string ShutsDown = "nghttp2 shuts down connection with error 1: PROTOCOL_ERROR";

    [TestMethod]
    [DataRow(true, "GET", DisplayName = "-I -X GET")]
    [DataRow(false, null, DisplayName = "GET")]
    public async Task ExecuteAsync_GetWhoseHeadEndsTheStreamShortOfContentLength_ExitsSixteenWithoutTheEmptyHeadLine(bool noBody, string? customMethod)
    {
        (TransferResult result, string output, RecordingTransferEvents events) = await TransferAsync(noBody, customMethod, "200", "3");

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual(ShutsDown, result.ErrorMessage);
        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 3\r\n", output);
        CollectionAssert.AreEqual(
            new[] { "< HTTP/2 200 \r\n", "< content-length: 3\r\n", "* " + ShutsDown, "* closing connection #0" },
            events.Events.TakeLast(4).ToArray());
    }

    [TestMethod]
    [DataRow(null, "200", "3", DisplayName = "-I")]
    [DataRow("GET", "200", "0", DisplayName = "-I -X GET, content-length 0")]
    [DataRow("GET", "204", "3", DisplayName = "-I -X GET, 204")]
    [DataRow("GET", "304", "3", DisplayName = "-I -X GET, 304")]
    public async Task ExecuteAsync_HeadEndingTheStreamWithNoBodyPromised_ExitsZero(string? customMethod, string status, string contentLength)
    {
        (TransferResult result, string output, RecordingTransferEvents events) = await TransferAsync(noBody: true, customMethod, status, contentLength);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual($"HTTP/2 {status} \r\ncontent-length: {contentLength}\r\n\r\n", output);
        CollectionAssert.DoesNotContain(events.Info, ShutsDown);
    }

    [TestMethod]
    public async Task ExecuteAsync_IgnoredBodyWhoseTrailerEndsTheStreamShortOfContentLength_ExitsZeroDroppingTheTrailer()
    {
        HpackEncoder server = new();
        byte[] head = Headers(server, isEndStream: false, new(":status", "200"), new("content-length", "3"));
        byte[] trailer = Headers(server, isEndStream: true, new HeaderField("x-t", "v"));

        await AssertShutsDownAsync([.. head, .. trailer], "* " + ShutsDown, "{ ", "* shutting down connection #0");
    }

    [TestMethod]
    [DataRow("a", new[] { "{ a", "* " + ShutsDown, "{ ", "* shutting down connection #0" }, DisplayName = "one byte")]
    [DataRow("", new[] { "* " + ShutsDown, "{ ", "* shutting down connection #0" }, DisplayName = "empty")]
    public async Task ExecuteAsync_IgnoredBodyWhoseDataEndsTheStreamShortOfContentLength_ExitsZeroShuttingDown(string data, string[] lines)
    {
        byte[] head = Headers(new HpackEncoder(), isEndStream: false, new(":status", "200"), new("content-length", "3"));
        byte[] body = Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, System.Text.Encoding.Latin1.GetBytes(data), isEndStream: true));

        await AssertShutsDownAsync([.. head, .. body], lines);
    }

    private static async Task AssertShutsDownAsync(byte[] response, params string[] linesAfterTheHead)
    {
        ScriptedConnection connection = new([.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])), .. response], 65536);

        (TransferResult result, string output, RecordingTransferEvents events) = await TransferAsync(connection, noBody: true, "GET");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 3\r\n\r\n", output);
        CollectionAssert.AreEqual(linesAfterTheHead, events.Events.SkipWhile(line => line != "< \r\n").Skip(1).ToArray());
        // GOAWAY: length 8, type 7, no flags, stream 0; last stream 0, error 1 (PROTOCOL_ERROR).
        byte[] goAway = [0, 0, 8, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1];
        CollectionAssert.AreEqual(goAway, connection.Written[^goAway.Length..]);
    }

    private static byte[] Headers(HpackEncoder server, bool isEndStream, params HeaderField[] fields) =>
        Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode(fields), isEndStream, isEndHeaders: true));

    private static Task<(TransferResult Result, string Output, RecordingTransferEvents Events)> TransferAsync(bool noBody, string? customMethod, string status, string contentLength)
    {
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Headers(new HpackEncoder(), isEndStream: true, new(":status", status), new("content-length", contentLength)),
        ];
        return TransferAsync(new ScriptedConnection(scripted, 65536), noBody, customMethod);
    }

    private static async Task<(TransferResult Result, string Output, RecordingTransferEvents Events)> TransferAsync(IConnection connection, bool noBody, string? customMethod)
    {
        RecordingTransferEvents events = new();
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://h/x"),
            Output = output,
            HeaderOutput = output,
            Events = events,
            NoBody = noBody,
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, CustomMethod = customMethod },
        };

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()).ExecuteAsync(context);

        return (result, System.Text.Encoding.Latin1.GetString(output.ToArray()), events);
    }
}
