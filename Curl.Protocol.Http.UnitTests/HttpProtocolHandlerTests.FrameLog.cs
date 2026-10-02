using System.Net;
using System.Text;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the HTTP/2 and HTTP/3 frame lines an exchange writes to Curl's own diagnostic log
/// (BL-1073, ADR-0345): each frame at <c>verbose</c>, the server's SETTINGS at <c>info</c>,
/// its GOAWAY and stream resets at <c>warning</c>, under <c>http2</c> or <c>http3</c>.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_Http2GetAtVerbose_LogsItsHeadersAndDataFramesOnStream1UnderHttp2()
    {
        RecordingDiagnosticLog log = new();

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536)))
            .ExecuteAsync(Http2LogContext(log));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Http2));
        string[] verbose = log.MessagesAt(DiagnosticLogLevel.Verbose);
        Assert.IsTrue(verbose.Any(line => line.StartsWith("HEADERS sent on stream 1, ", StringComparison.Ordinal)), string.Join('\n', verbose));
        Assert.IsTrue(verbose.Any(line => line.StartsWith("HEADERS received on stream 1, ", StringComparison.Ordinal)), string.Join('\n', verbose));
        CollectionAssert.Contains(verbose, "DATA received on stream 1, 5 bytes");
        CollectionAssert.Contains(
            log.MessagesAt(DiagnosticLogLevel.Info),
            "SETTINGS received: max concurrent streams unlimited, initial window 65535, max frame 16384, header table 4096");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2GetAtVerbose_LogsNoHeaderValueInAFrameLine()
    {
        RecordingDiagnosticLog log = new();
        HttpRequestOptions options = new() { Version = HttpVersionPreference.Http2PriorKnowledge, Headers = ["X-Secret: s3cret-frame"] };

        await Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536)))
            .ExecuteAsync(Http2LogContext(log, options));

        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("on stream", StringComparison.Ordinal) && line.Message.Contains("s3cret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2UploadAtVerbose_LogsTheDataFramesSent()
    {
        RecordingDiagnosticLog log = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18922/up"),
            Output = new MemoryStream(),
            Upload = new MemoryStream("abc"u8.ToArray()),
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge },
            DiagnosticLog = log,
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };

        await Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536))).ExecuteAsync(context);

        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "DATA sent on stream 1, 3 bytes");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2ServerGoAway_LogsAWarningWithItsLastStreamAndErrorCode()
    {
        RecordingDiagnosticLog log = new();
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.InternalError, ReadOnlyMemory<byte>.Empty));

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536))).ExecuteAsync(Http2LogContext(log));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Warning), "GOAWAY received: last stream 0, error INTERNAL_ERROR (2)");
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Http2));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2ServerRstStream_LogsAWarningWithItsStreamAndErrorCode()
    {
        RecordingDiagnosticLog log = new();
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.Cancel));

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536))).ExecuteAsync(Http2LogContext(log));

        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Warning), "RST_STREAM received on stream 1: error CANCEL (8)");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2GetAtInfo_RecordsNoFrameLine()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);

        await Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536))).ExecuteAsync(Http2LogContext(log));

        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(" on stream ", StringComparison.Ordinal)), string.Join('\n', log.Lines.Select(line => line.Message)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3GetAtVerbose_LogsItsFramesUnderHttp3()
    {
        RecordingDiagnosticLog log = new();
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "5")), Http3Data("hello")), 65536);

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream))).ExecuteAsync(Http3LogContext(log));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string[] frames = [.. log.Lines.Where(line => line.Component == DiagnosticLogComponents.Http3 && line.Level == DiagnosticLogLevel.Verbose).Select(line => line.Message)];
        Assert.IsTrue(frames.Any(line => line.StartsWith("HEADERS sent on stream 0, ", StringComparison.Ordinal)), string.Join('\n', frames));
        Assert.IsTrue(frames.Any(line => line.StartsWith("HEADERS received on stream 0, ", StringComparison.Ordinal)), string.Join('\n', frames));
        CollectionAssert.Contains(frames, "DATA received on stream 0, 5 bytes");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3UploadAtVerbose_LogsTheDataFrameSent()
    {
        RecordingDiagnosticLog log = new();
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "0"))), 65536);
        TransferContext context = Http3LogContext(log, new MemoryStream("abc"u8.ToArray()));

        await Handler(QuicConnector(new FakeMultiplexedConnection(stream))).ExecuteAsync(context);

        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "DATA sent on stream 0, 3 bytes");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3StreamResetAfterTheHead_LogsAWarningWithItsStreamAndErrorCode()
    {
        RecordingDiagnosticLog log = new();
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "5"))), 65536)
        {
            EndException = new MultiplexedStreamResetException(0x10c, "cancelled"),
        };

        await Handler(QuicConnector(new FakeMultiplexedConnection(stream))).ExecuteAsync(Http3LogContext(log));

        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Warning), "RESET_STREAM received on stream 0: error 0x10c");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ServerSettingsAndGoaway_LogsTheSettingsAtInfoAndTheGoawayStreamAtWarningUnderHttp3()
    {
        RecordingDiagnosticLog log = new();
        FakeMultiplexedStream control = new(3, [0x00, .. new Http3SettingsFrame([new Http3Setting(0x06, 100)]).ToBytes(), .. new Http3GoawayFrame(4).ToBytes()]) { StaysOpen = true };
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "5")), Http3Data("hello")), 65536);

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream) { ServerStreams = [control] })).ExecuteAsync(Http3LogContext(log));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.Contains(log.Lines, (DiagnosticLogLevel.Info, DiagnosticLogComponents.Http3, "SETTINGS received: MAX_FIELD_SECTION_SIZE 100"));
        CollectionAssert.Contains(log.Lines, (DiagnosticLogLevel.Warning, DiagnosticLogComponents.Http3, "GOAWAY received: stream 4"));
    }

    private static byte[] Http2HelloResponse()
    {
        HpackEncoder server = new();
        return Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "5")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true));
    }

    private static TransferContext Http2LogContext(IDiagnosticLog log, HttpRequestOptions? options = null) => new()
    {
        Url = CurlUrl.Parse(LogUrl),
        Output = new MemoryStream(),
        Http = options ?? new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge },
        DiagnosticLog = log,
        TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
    };

    private static TransferContext Http3LogContext(IDiagnosticLog log, Stream? upload = null) => new()
    {
        Url = CurlUrl.Parse("https://example.com/"),
        Output = new MemoryStream(),
        Upload = upload,
        TimeProvider = TimeProvider.System,
        ConnectTimeout = TimeSpan.FromMilliseconds(1000),
        Http = new HttpRequestOptions { Version = HttpVersionPreference.Http3Only, UserAgent = MeasuredUserAgent },
        DiagnosticLog = log,
    };
}
