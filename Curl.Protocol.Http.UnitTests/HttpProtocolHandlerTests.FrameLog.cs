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

        Diagnostics.Arrange("url, version, log level", $"{LogUrl}, Http2PriorKnowledge, Verbose");
        Diagnostics.Arrange("response", "HTTP/2 HEADERS :status 200 content-length 5, DATA hello");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536)))
            .ExecuteAsync(Http2LogContext(log));

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("all lines are http2", true, log.Lines.All(line => line.Component == DiagnosticLogComponents.Http2));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Http2));
        string[] verbose = log.MessagesAt(DiagnosticLogLevel.Verbose);
        Diagnostics.Assert("HEADERS sent line", true, verbose.Any(line => line.StartsWith("HEADERS sent on stream 1, ", StringComparison.Ordinal)));
        Assert.IsTrue(verbose.Any(line => line.StartsWith("HEADERS sent on stream 1, ", StringComparison.Ordinal)), string.Join('\n', verbose));
        Diagnostics.Assert("HEADERS received line", true, verbose.Any(line => line.StartsWith("HEADERS received on stream 1, ", StringComparison.Ordinal)));
        Assert.IsTrue(verbose.Any(line => line.StartsWith("HEADERS received on stream 1, ", StringComparison.Ordinal)), string.Join('\n', verbose));
        Diagnostics.Assert("verbose contains DATA line", "DATA received on stream 1, 5 bytes", string.Join(" | ", verbose));
        CollectionAssert.Contains(verbose, "DATA received on stream 1, 5 bytes");
        Diagnostics.Assert(
            "info contains SETTINGS line",
            "SETTINGS received: max concurrent streams unlimited, initial window 65535, max frame 16384, header table 4096",
            string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(
            log.MessagesAt(DiagnosticLogLevel.Info),
            "SETTINGS received: max concurrent streams unlimited, initial window 65535, max frame 16384, header table 4096");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2GetAtVerbose_LogsNoHeaderValueInAFrameLine()
    {
        RecordingDiagnosticLog log = new();
        HttpRequestOptions options = new() { Version = HttpVersionPreference.Http2PriorKnowledge, Headers = ["X-Secret: s3cret-frame"] };

        Diagnostics.Arrange("url, version, header", $"{LogUrl}, Http2PriorKnowledge, X-Secret: s3cret-frame");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536)))
            .ExecuteAsync(Http2LogContext(log, options));

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert(
            "frame line holding the secret",
            false,
            log.Lines.Any(line => line.Message.Contains("on stream", StringComparison.Ordinal) && line.Message.Contains("s3cret", StringComparison.Ordinal)));
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

        Diagnostics.Arrange("url, version, upload", "http://127.0.0.1:18922/up, Http2PriorKnowledge, abc");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536))).ExecuteAsync(context);

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert("verbose contains DATA line", "DATA sent on stream 1, 3 bytes", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
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

        Diagnostics.Arrange("url, response", $"{LogUrl}, HEADERS :status 200 then GOAWAY last stream 0 INTERNAL_ERROR");
        Diagnostics.Bytes("response frames", response);

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536))).ExecuteAsync(Http2LogContext(log));

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Assert("warning contains GOAWAY line", "GOAWAY received: last stream 0, error INTERNAL_ERROR (2)", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Warning)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Warning), "GOAWAY received: last stream 0, error INTERNAL_ERROR (2)");
        Diagnostics.Assert("all lines are http2", true, log.Lines.All(line => line.Component == DiagnosticLogComponents.Http2));
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

        Diagnostics.Arrange("url, response", $"{LogUrl}, HEADERS :status 200 then RST_STREAM on stream 1 CANCEL");
        Diagnostics.Bytes("response frames", response);

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536))).ExecuteAsync(Http2LogContext(log));

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert("exit code", CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Diagnostics.Assert("warning contains RST_STREAM line", "RST_STREAM received on stream 1: error CANCEL (8)", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Warning)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Warning), "RST_STREAM received on stream 1: error CANCEL (8)");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2GetAtInfo_RecordsNoFrameLine()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);

        Diagnostics.Arrange("url, version, log level", $"{LogUrl}, Http2PriorKnowledge, Info");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536))).ExecuteAsync(Http2LogContext(log));

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert("any frame line", false, log.Lines.Any(line => line.Message.Contains(" on stream ", StringComparison.Ordinal)));
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(" on stream ", StringComparison.Ordinal)), string.Join('\n', log.Lines.Select(line => line.Message)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3GetAtVerbose_LogsItsFramesUnderHttp3()
    {
        RecordingDiagnosticLog log = new();
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "5")), Http3Data("hello")), 65536);

        Diagnostics.Arrange("url, version", "https://example.com/, Http3Only");
        Diagnostics.Arrange("response", "HTTP/3 HEADERS :status 200 content-length 5, DATA hello");

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream))).ExecuteAsync(Http3LogContext(log));

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string[] frames = [.. log.Lines.Where(line => line.Component == DiagnosticLogComponents.Http3 && line.Level == DiagnosticLogLevel.Verbose).Select(line => line.Message)];
        Diagnostics.Assert("HEADERS sent line", true, frames.Any(line => line.StartsWith("HEADERS sent on stream 0, ", StringComparison.Ordinal)));
        Assert.IsTrue(frames.Any(line => line.StartsWith("HEADERS sent on stream 0, ", StringComparison.Ordinal)), string.Join('\n', frames));
        Diagnostics.Assert("HEADERS received line", true, frames.Any(line => line.StartsWith("HEADERS received on stream 0, ", StringComparison.Ordinal)));
        Assert.IsTrue(frames.Any(line => line.StartsWith("HEADERS received on stream 0, ", StringComparison.Ordinal)), string.Join('\n', frames));
        Diagnostics.Assert("frames contain DATA line", "DATA received on stream 0, 5 bytes", string.Join(" | ", frames));
        CollectionAssert.Contains(frames, "DATA received on stream 0, 5 bytes");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3UploadAtVerbose_LogsTheDataFrameSent()
    {
        RecordingDiagnosticLog log = new();
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "0"))), 65536);
        TransferContext context = Http3LogContext(log, new MemoryStream("abc"u8.ToArray()));

        Diagnostics.Arrange("url, version, upload", "https://example.com/, Http3Only, abc");

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream))).ExecuteAsync(context);

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert("verbose contains DATA line", "DATA sent on stream 0, 3 bytes", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
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

        Diagnostics.Arrange("url, version, stream end", "https://example.com/, Http3Only, reset with error 0x10c");

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream))).ExecuteAsync(Http3LogContext(log));

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert("warning contains RESET_STREAM line", "RESET_STREAM received on stream 0: error 0x10c", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Warning)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Warning), "RESET_STREAM received on stream 0: error 0x10c");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3ServerSettingsAndGoaway_LogsTheSettingsAtInfoAndTheGoawayStreamAtWarningUnderHttp3()
    {
        RecordingDiagnosticLog log = new();
        FakeMultiplexedStream control = new(3, [0x00, .. new Http3SettingsFrame([new Http3Setting(0x06, 100)]).ToBytes(), .. new Http3GoawayFrame(4).ToBytes()]) { StaysOpen = true };
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "5")), Http3Data("hello")), 65536);

        Diagnostics.Arrange("url, version", "https://example.com/, Http3Only");
        Diagnostics.Arrange("control stream", "SETTINGS MAX_FIELD_SECTION_SIZE 100, GOAWAY 4");

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream) { ServerStreams = [control] })).ExecuteAsync(Http3LogContext(log));

        WriteResult(result);
        WriteLogLines(log);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("info SETTINGS line", "SETTINGS received: MAX_FIELD_SECTION_SIZE 100", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.Lines, (DiagnosticLogLevel.Info, DiagnosticLogComponents.Http3, "SETTINGS received: MAX_FIELD_SECTION_SIZE 100"));
        Diagnostics.Assert("warning GOAWAY line", "GOAWAY received: stream 4", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Warning)));
        CollectionAssert.Contains(log.Lines, (DiagnosticLogLevel.Warning, DiagnosticLogComponents.Http3, "GOAWAY received: stream 4"));
    }

    /// <summary>Writes an ACT line with every recorded log line, level and component first.</summary>
    private void WriteLogLines(RecordingDiagnosticLog log) =>
        Diagnostics.Act("log lines", string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Component} {line.Message}")));

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
