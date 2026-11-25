using System.Text;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5391: an HTTP/2 HEAD response that carries DATA ends at its final head with -I, and still fails without it;
/// and BL-5938: with -I, non-empty DATA also closes the connection with GOAWAY PROTOCOL_ERROR.
/// </summary>
[TestClass]
public sealed class Http2HeadResponseWithDataTests
{
    private const string HeadRequest = "HEAD /headbody HTTP/1.1\r\nHost: h\r\n\r\n";

    [TestMethod]
    public async Task ReadToEndAsync_HeadResponseWithDataAndIgnoresBody_EndsAfterTheHeadWithoutFailing()
    {
        (string head, string tail) = await ExchangeAsync(ignoresBody: true, endsOnHeaders: false);

        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 5\r\n\r\n", head);
        Assert.AreEqual(string.Empty, tail);
    }

    [TestMethod]
    public async Task ReadToEndAsync_HeadResponseWithDataWithoutIgnoresBody_FailsWithExit16()
    {
        HttpTransferException failure = await Assert.ThrowsAsync<HttpTransferException>(async () => await ExchangeAsync(ignoresBody: false, endsOnHeaders: false));

        Assert.AreEqual(CurlExitCode.Http2, failure.ExitCode);
    }

    [TestMethod]
    public async Task ReadToEndAsync_HeadResponseEndedOnItsHeadAndIgnoresBody_IsUnchanged()
    {
        (string head, string tail) = await ExchangeAsync(ignoresBody: true, endsOnHeaders: true);

        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 5\r\n\r\n", head);
        Assert.AreEqual(string.Empty, tail);
    }

    [TestMethod]
    public async Task ReadToEndAsync_GetStreamEndingAtFinalHeadWithContentLength_EndsWithoutGoAway()
    {
        HpackEncoder server = new();
        List<byte> scripted = [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([]))];
        scripted.AddRange(Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "5")]), isEndStream: false, isEndHeaders: true)));
        ScriptedConnection connection = new([.. scripted], 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, true);
        await stream.WriteAsync(Encoding.Latin1.GetBytes("GET /g HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);
        byte[] buffer = new byte[256];

        int read = await stream.ReadAsync(buffer, CancellationToken.None);
        await stream.ReadToEndAsync(CancellationToken.None);
        int tail = await stream.ReadAsync(buffer, CancellationToken.None);

        Assert.AreEqual(0, tail);
        Assert.IsTrue(read > 0);
    }

    [TestMethod]
    public async Task ReadToEndAsync_TrailerHeadersRightAfterTheHeadAndIgnoresBody_KeepsTheTrailer()
    {
        HpackEncoder server = new();
        List<byte> scripted = [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([]))];
        scripted.AddRange(Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "5")]), isEndStream: false, isEndHeaders: true)));
        scripted.AddRange(Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new("x-t", "trailerval")]), isEndStream: true, isEndHeaders: true)));
        ScriptedConnection connection = new([.. scripted], 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, true);
        await stream.WriteAsync(Encoding.Latin1.GetBytes(HeadRequest), CancellationToken.None);
        byte[] buffer = new byte[256];

        _ = await stream.ReadAsync(buffer, CancellationToken.None);
        await stream.ReadToEndAsync(CancellationToken.None);

        Assert.AreEqual("x-t: trailerval\r\n", Encoding.Latin1.GetString(stream.TrailerBytes.Span));
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadRequestWithDataAfterTheHead_ExitsZeroClosingWithGoAwayProtocolError()
    {
        (TransferResult result, List<Http2Frame> sent, RecordingTransferEvents events) = await TransferHeadAsync("no\n"u8.ToArray());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Http2Frame goAway = sent.Single(frame => frame.Type == Http2FrameType.GoAway);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, goAway.Payload.ToArray(), "Last stream 0, PROTOCOL_ERROR and no debug data.");
        Assert.IsFalse(sent.Any(frame => frame.Type == Http2FrameType.RstStream));
        CollectionAssert.Contains(events.Info, "nghttp2 shuts down connection with error 1: PROTOCOL_ERROR");
        CollectionAssert.Contains(events.Info, "shutting down connection #0");
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadRequestWithEmptyDataAfterTheHead_ClosesWithTheShutDownGoAway()
    {
        (TransferResult result, List<Http2Frame> sent, RecordingTransferEvents events) = await TransferHeadAsync([]);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Http2Frame goAway = sent.Single(frame => frame.Type == Http2FrameType.GoAway);
        byte[] shutdown = [0, 0, 0, 0, 0, 0, 0, 0, .. "shutdown\0"u8.ToArray()];
        CollectionAssert.AreEqual(shutdown, goAway.Payload.ToArray(), "Last stream 0, NO_ERROR and debug data shutdown.");
        CollectionAssert.DoesNotContain(events.Info, "nghttp2 shuts down connection with error 1: PROTOCOL_ERROR");
        CollectionAssert.DoesNotContain(events.Info, "shutting down connection #0");
    }

    private static async Task<(TransferResult Result, List<Http2Frame> Sent, RecordingTransferEvents Events)> TransferHeadAsync(byte[] data)
    {
        HpackEncoder server = new();
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "3")]), isEndStream: false, isEndHeaders: true)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, data, isEndStream: true)),
        ];
        ScriptedConnection connection = new(scripted, 65536);
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://h/headbody"),
            Output = new MemoryStream(),
            Events = events,
            NoBody = true,
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge },
        };

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()).ExecuteAsync(context);

        return (result, await FramesAfterThePrefaceAsync(connection.Written), events);
    }

    private static async Task<List<Http2Frame>> FramesAfterThePrefaceAsync(byte[] written)
    {
        const int prefaceLength = 24;
        using MemoryStream stream = new(written, prefaceLength, written.Length - prefaceLength);
        List<Http2Frame> frames = [];
        while (await Http2FrameCodec.ReadAsync(stream, 16384, CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    private static async Task<(string Head, string Tail)> ExchangeAsync(bool ignoresBody, bool endsOnHeaders)
    {
        HpackEncoder server = new();
        List<byte> scripted = [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([]))];
        scripted.AddRange(Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "5")]), isEndStream: endsOnHeaders, isEndHeaders: true)));
        if (!endsOnHeaders)
        {
            scripted.AddRange(Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true)));
        }

        ScriptedConnection connection = new([.. scripted], 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody);
        await stream.WriteAsync(Encoding.Latin1.GetBytes(HeadRequest), CancellationToken.None);
        byte[] buffer = new byte[256];
        int read = await stream.ReadAsync(buffer, CancellationToken.None);
        await stream.ReadToEndAsync(CancellationToken.None);
        int tail = await stream.ReadAsync(buffer, CancellationToken.None);
        return (Encoding.Latin1.GetString(buffer, 0, read), Encoding.Latin1.GetString(buffer, 0, tail));
    }
}
