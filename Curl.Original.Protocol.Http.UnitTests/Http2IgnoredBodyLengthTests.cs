using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5947: an HTTP/2 final head that ends the stream although its <c>content-length</c>
/// promises a body fails with exit 16 for <c>-I -X GET</c>, as without <c>-I</c>, and succeeds
/// for <c>-I</c> alone, whose HEAD request is promised no body.
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

    private static async Task<(TransferResult Result, string Output, RecordingTransferEvents Events)> TransferAsync(bool noBody, string? customMethod, string status, string contentLength)
    {
        HpackEncoder server = new();
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", status), new("content-length", contentLength)]), isEndStream: true, isEndHeaders: true)),
        ];
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

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(scripted, 65536)), new SilentAuthenticator()).ExecuteAsync(context);

        return (result, System.Text.Encoding.Latin1.GetString(output.ToArray()), events);
    }
}
