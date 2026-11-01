using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins the <c>-v</c> line <c>received GOAWAY, error=N, last_stream=N</c> that curl 8.18.0 with
/// nghttp2 writes when an HTTP/2 server sends GOAWAY (<c>on_frame_recv</c>, BL-3839).
/// </summary>
[TestClass]
public sealed class Http2GoAwayInfoLineTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_GoAwayLeavesStreamOneUnprocessed_WritesTheGoAwayLineBeforeTheRetryLine()
    {
        HpackEncoder server = new();
        byte[] served = Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "ok\n"u8.ToArray(), isEndStream: true));
        QueueConnector connector = QueueConnector.For(
            new ScriptedConnection(Response(Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty)), 65536),
            new ScriptedConnection(served, 65536));
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("first connection", "GOAWAY last 0 NO_ERROR");

        await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(Context(events));

        int goAway = events.Info.IndexOf("received GOAWAY, error=0, last_stream=0");
        int retry = events.Info.IndexOf("REFUSED_STREAM, retrying a fresh connect");
        Diagnostics.Act("index of the GOAWAY line, index of the retry line", $"{goAway}, {retry}");
        Assert.IsTrue(goAway >= 0, "the GOAWAY line is written");
        Assert.IsTrue(goAway < retry, "the GOAWAY line comes before the retry line");
    }

    [TestMethod]
    public async Task ExecuteAsync_GoAwayWithAnErrorCode_WritesTheCodeAsANumber()
    {
        QueueConnector connector = QueueConnector.For(
            new ScriptedConnection(Response(Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.InternalError, ReadOnlyMemory<byte>.Empty)), 65536));
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("connection", "GOAWAY last 1 INTERNAL_ERROR");

        await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(Context(events));

        Diagnostics.Assert("line written", true, events.Info.Contains("received GOAWAY, error=2, last_stream=1"));
        CollectionAssert.Contains(events.Info, "received GOAWAY, error=2, last_stream=1");
    }

    private static TransferContext Context(ITransferEvents events) => new()
    {
        Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
        Output = new MemoryStream(),
        Events = events,
        Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, UserAgent = "curl/8.18.0" },
    };

    private static byte[] Response(params Http2Frame[] frames) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
        .. frames.SelectMany(Http2FrameCodec.Serialize),
    ];
}
