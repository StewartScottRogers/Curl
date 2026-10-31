using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-031 on <see cref="HttpProtocolHandler" /> over <see cref="Http2StreamConnection" />:
/// a stream the server refuses with RST_STREAM REFUSED_STREAM, or leaves unprocessed by a GOAWAY
/// whose last stream ID is below it, is sent again on a new connection, as curl 8.18.0 with
/// nghttp2 does, and after five such retries the transfer fails with exit 56 and
/// <c>Connection died, tried 5 times before giving up</c> (measured, BL-3560 Notes).
/// </summary>
[TestClass]
public sealed class Cgr031Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    public static IEnumerable<object[]> Refusals =>
    [
        ["RST_STREAM REFUSED_STREAM", Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.RefusedStream)],
        ["GOAWAY last 0 NO_ERROR", Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty)],
        ["GOAWAY last 0 INTERNAL_ERROR", Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.InternalError, ReadOnlyMemory<byte>.Empty)],
    ];

    [TestMethod]
    [DynamicData(nameof(Refusals))]
    public async Task ExecuteAsync_Http2StreamRefusedOnTheFirstConnection_SendsTheRequestAgainOnASecondOne(string refusal, Http2Frame refusingFrame)
    {
        HpackEncoder server = new();
        byte[] served = Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello h2\n"u8.ToArray(), isEndStream: true));
        QueueConnector connector = QueueConnector.For(new ScriptedConnection(Response(refusingFrame), 65536), new ScriptedConnection(served, 65536));
        MemoryStream output = new();
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("first connection, second connection", $"{refusal}, HEADERS 200 then DATA hello h2");

        TransferResult result = await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(Context(output, events));

        Diagnostics.Act("exit code, connects, body", string.Join(", ", result.ExitCode, connector.Targets.Count, Encoding.Latin1.GetString(output.ToArray())));
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, connector.Targets.Count);
        Assert.AreEqual("hello h2\n", Encoding.Latin1.GetString(output.ToArray()));
        Diagnostics.Assert("-v says it retried", true, events.Info.Contains("REFUSED_STREAM, retrying a fresh connect"));
        CollectionAssert.Contains(events.Info, "REFUSED_STREAM, retrying a fresh connect");
        Assert.IsFalse(events.Info.Contains("Failure when receiving data from the peer"), "curl prints no -v line of its own for the refusal");
    }

    [TestMethod]
    [DynamicData(nameof(Refusals))]
    public async Task ExecuteAsync_Http2StreamRefusedEveryTime_GivesUpAfterFiveRetriesWithExit56(string refusal, Http2Frame refusingFrame)
    {
        ScriptedConnection[] connections = [.. Enumerable.Range(0, 6).Select(_ => new ScriptedConnection(Response(refusingFrame), 65536))];
        QueueConnector connector = QueueConnector.For(connections);
        Diagnostics.Arrange("every connection", refusal);

        TransferResult result = await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(Context(new MemoryStream(), new RecordingTransferEvents()));

        Diagnostics.Act("exit code, connects, message", string.Join(", ", result.ExitCode, connector.Targets.Count, result.ErrorMessage));
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(6, connector.Targets.Count);
        Assert.AreEqual("Connection died, tried 5 times before giving up", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2GoAwayWithAnErrorThatCoversTheStream_FailsWithExit56WithoutARetry()
    {
        QueueConnector connector = QueueConnector.For(new ScriptedConnection(Response(Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.InternalError, ReadOnlyMemory<byte>.Empty)), 65536));
        Diagnostics.Arrange("first connection", "GOAWAY last 1 INTERNAL_ERROR");

        TransferResult result = await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(Context(new MemoryStream(), new RecordingTransferEvents()));

        Diagnostics.Act("exit code, connects, message", string.Join(", ", result.ExitCode, connector.Targets.Count, result.ErrorMessage));
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(1, connector.Targets.Count);
        Assert.AreEqual("Recv failure: Connection reset by peer", result.ErrorMessage);
    }

    private static TransferContext Context(Stream output, ITransferEvents events) => new()
    {
        Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
        Output = output,
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
