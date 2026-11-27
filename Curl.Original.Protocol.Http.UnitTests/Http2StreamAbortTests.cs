using System.Net;
using System.Text;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// The frames an HTTP/2 connection closes with: curl 8.18.0 resets the stream of a transfer
/// that <c>-m</c> aborts with STREAM_CLOSED before its GOAWAY (measured, BL-5906 Notes), and
/// reports the connection left intact (measured, BL-5974 Notes) and sends the next transfer on
/// it (measured, BL-5976 Notes). A transfer <c>-f</c> fails does the same, its stream reset
/// only when it has not ended (measured, BL-5977 Notes).
/// </summary>
[TestClass]
public sealed class Http2StreamAbortTests
{
    /// <summary>The closing GOAWAY: NO_ERROR, last stream 0, debug data <c>shutdown</c> and a NUL.</summary>
    private const string ShutdownGoAway = "000011070000000000" + "00000000" + "00000000" + "73687574646F776E00";

    /// <summary>The GOAWAY for a connection error: PROTOCOL_ERROR, last stream 0, no debug data.</summary>
    private const string ProtocolErrorGoAway = "000008070000000000" + "00000000" + "00000001";

    /// <summary>RST_STREAM on stream 1 with STREAM_CLOSED.</summary>
    private const string StreamOneClosedReset = "000004030000000001" + "00000005";

    /// <summary>The type, flags (END_STREAM and END_HEADERS) and stream 3 of a request's HEADERS.</summary>
    private const string StreamThreeHeadersHeader = "010500000003";

    /// <summary>The type, flags and stream 1 of any RST_STREAM on stream 1.</summary>
    private const string StreamOneResetHeader = "000004030000000001";

    private const string LeftIntact = "Connection #0 to host example.com:80 left intact";

    private const string SwitchingProtocolsHead = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(HttpVersionPreference.Http2PriorKnowledge, "")]
    [DataRow(HttpVersionPreference.Http2, SwitchingProtocolsHead)]
    public async Task ExecuteAsync_MaxTimePassesWhileTheStreamIsUnfinished_ResetsItWithStreamClosedBeforeTheGoAway(HttpVersionPreference version, string upgrade)
    {
        byte[] response = [.. Encoding.Latin1.GetBytes(upgrade), .. ServerFrames(Head(isEndStream: false))];
        StalledConnection stalled = new(response, 65536);
        RecordingConnection connection = new(stalled);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        TransferContext context = Context(version, time, TimeSpan.FromSeconds(1));
        Diagnostics.Arrange("version, max time", $"{version}, 1000 ms; the server stalls after the head of stream 1");

        Task<TransferResult> transfer = Handler(connection).ExecuteAsync(context).AsTask();
        await stalled.Stalled;
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult result = await transfer;

        string written = connection.WrittenHex;
        Diagnostics.Act("exit code, bytes written (hex)", $"{result.ExitCode}, {written}");
        Diagnostics.Assert("ends with RST_STREAM then GOAWAY", true, written.EndsWith(StreamOneClosedReset + ShutdownGoAway, StringComparison.Ordinal));
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        StringAssert.EndsWith(written, StreamOneClosedReset + ShutdownGoAway, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(HttpVersionPreference.Http2PriorKnowledge, "", "200")]
    [DataRow(HttpVersionPreference.Http2PriorKnowledge, "", "100")]
    [DataRow(HttpVersionPreference.Http2, SwitchingProtocolsHead, "200")]
    public async Task ExecuteAsync_MaxTimePassesOnAStream_ReportsTheConnectionLeftIntact(HttpVersionPreference version, string upgrade, string status)
    {
        byte[] response = [.. Encoding.Latin1.GetBytes(upgrade), .. ServerFrames(Head(isEndStream: false, status))];
        StalledConnection stalled = new(response, 65536);
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        TransferContext context = Context(version, time, TimeSpan.FromSeconds(1), events: events);
        Diagnostics.Arrange("version, status, max time", $"{version}, {status}, 1000 ms; the server stalls after the head of stream 1");

        Task<TransferResult> transfer = Handler(stalled).ExecuteAsync(context).AsTask();
        await stalled.Stalled;
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult result = await transfer;

        string info = string.Join('|', events.Info);
        Diagnostics.Act("exit code, -v info lines", $"{result.ExitCode}, {info}");
        Diagnostics.Assert("last info line", LeftIntact, events.Info[^1]);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(LeftIntact, events.Info[^1], info);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("closing", StringComparison.Ordinal)), info);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTimePassesOnASharedConnection_LeavesTheLineToItsLastTransfer()
    {
        StalledConnection stalled = new(ServerFrames(Head(isEndStream: false)), 65536);
        RecordingConnection connection = new(stalled) { IsSharedWithAnotherTransfer = true };
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        TransferContext context = Context(HttpVersionPreference.Http2PriorKnowledge, time, TimeSpan.FromSeconds(1), events: events);
        Diagnostics.Arrange("connection", "shared with another transfer; the server stalls after the head of stream 1");

        Task<TransferResult> transfer = Handler(connection).ExecuteAsync(context).AsTask();
        await stalled.Stalled;
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult result = await transfer;

        string info = string.Join('|', events.Info);
        Diagnostics.Act("exit code, -v info lines", $"{result.ExitCode}, {info}");
        Diagnostics.Assert("says nothing of the connection's end", false, events.Info.Contains(LeftIntact));
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        CollectionAssert.DoesNotContain(events.Info, LeftIntact);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("closing", StringComparison.Ordinal)), info);
    }

    [TestMethod]
    public async Task ExecuteAsync_StreamEnded_WritesNoReset()
    {
        RecordingConnection connection = new(new ScriptedConnection(ServerFrames(Head(isEndStream: true)), 65536));
        Diagnostics.Arrange("server", "a head that ends stream 1");

        TransferResult result = await Handler(connection).ExecuteAsync(Context(HttpVersionPreference.Http2PriorKnowledge));

        string written = connection.WrittenHex;
        Diagnostics.Act("exit code, bytes written (hex)", $"{result.ExitCode}, {written}");
        Diagnostics.Assert("holds a RST_STREAM on stream 1", false, written.Contains(StreamOneResetHeader, StringComparison.Ordinal));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.DoesNotContain(StreamOneResetHeader, written);
        StringAssert.EndsWith(written, ShutdownGoAway, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerResetTheStream_WritesNoResetOfItsOwn()
    {
        Http2Frame reset = Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.Cancel);
        RecordingConnection connection = new(new ScriptedConnection(ServerFrames(reset), 65536));
        Diagnostics.Arrange("server", "RST_STREAM CANCEL on stream 1");

        TransferResult result = await Handler(connection).ExecuteAsync(Context(HttpVersionPreference.Http2PriorKnowledge));

        string written = connection.WrittenHex;
        Diagnostics.Act("exit code, bytes written (hex)", $"{result.ExitCode}, {written}");
        Diagnostics.Assert("holds a RST_STREAM on stream 1", false, written.Contains(StreamOneResetHeader, StringComparison.Ordinal));
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.DoesNotContain(StreamOneResetHeader, written);
        StringAssert.EndsWith(written, ShutdownGoAway, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadRequestEndedAtADataFrame_WritesNoReset()
    {
        // curl -I sends no RST_STREAM when DATA that does not end the stream follows the head (BL-5922 Notes),
        // and closes with GOAWAY PROTOCOL_ERROR, not shutdown (BL-5938 Notes).
        Http2Frame data = new(Http2FrameType.Data, Http2FrameFlags.None, 1, "no\n"u8.ToArray());
        byte[] response = [.. ServerFrames(Head(isEndStream: false)), .. Http2FrameCodec.Serialize(data)];
        RecordingConnection connection = new(new ScriptedConnection(response, 65536));
        TransferContext context = Context(HttpVersionPreference.Http2PriorKnowledge, noBody: true);
        Diagnostics.Arrange("server", "a head and a DATA frame, neither ending stream 1; the request is -I");

        TransferResult result = await Handler(connection).ExecuteAsync(context);

        string written = connection.WrittenHex;
        Diagnostics.Act("exit code, bytes written (hex)", $"{result.ExitCode}, {written}");
        Diagnostics.Assert("holds a RST_STREAM on stream 1", false, written.Contains(StreamOneResetHeader, StringComparison.Ordinal));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.DoesNotContain(StreamOneResetHeader, written);
        StringAssert.EndsWith(written, ProtocolErrorGoAway, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ShutDownAsync_TwoStreamsUnfinished_ResetsEachLowestFirst()
    {
        RecordingConnection connection = new(new ScriptedConnection([], 65536));
        Http2Session session = new(connection);
        for (int request = 0; request < 2; request++)
        {
            Http2StreamConnection stream = (Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false);
            await stream.WriteAsync("GET / HTTP/1.1\r\nHost: h\r\n\r\n"u8.ToArray(), CancellationToken.None);
        }

        Diagnostics.Arrange("session", "streams 1 and 3 opened, neither answered");

        await session.ShutDownAsync(CancellationToken.None);

        const string StreamThreeClosedReset = "000004030000000003" + "00000005";
        string written = connection.WrittenHex;
        Diagnostics.Act("bytes written (hex)", written);
        Diagnostics.Assert("ends with both resets then GOAWAY", true, written.EndsWith(StreamOneClosedReset + StreamThreeClosedReset + ShutdownGoAway, StringComparison.Ordinal));
        StringAssert.EndsWith(written, StreamOneClosedReset + StreamThreeClosedReset + ShutdownGoAway, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "between two frames")]
    [DataRow(4, DisplayName = "part way through a frame header")]
    [DataRow(12, DisplayName = "part way through a frame payload")]
    public async Task ExecuteAsync_NextTransferAfterATimeout_GoesOutOnTheSameConnectionAfterTheReset(int cut)
    {
        byte[] lost = Http2FrameCodec.Serialize(Data(1, "hello\n", isEndStream: false));
        PushedBytesConnection pushed = new();
        SessionHoldingConnection connection = new(pushed);
        QueueConnector connector = QueueConnector.For(connection, connection);
        HttpProtocolHandler handler = new(connector, new SilentAuthenticator());
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        MemoryStream second = new();
        pushed.Push([.. ServerFrames(Head(isEndStream: false)), .. lost[..cut]]);
        Diagnostics.Arrange("server", $"stalls {cut} bytes into stream 1's DATA, sends the rest after the timeout, then answers stream 3");

        Task<TransferResult> timingOut = handler.ExecuteAsync(Context(HttpVersionPreference.Http2PriorKnowledge, time, TimeSpan.FromSeconds(1))).AsTask();
        await pushed.ReadsAskedAsync(2);
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult timedOut = await timingOut;
        pushed.Push([.. lost[cut..], .. Http2FrameCodec.Serialize(Head(isEndStream: false, streamId: 3)), .. Http2FrameCodec.Serialize(Data(3, "again\n", isEndStream: true))]);
        TransferResult next = await handler.ExecuteAsync(Context(HttpVersionPreference.Http2PriorKnowledge, output: second));

        string written = Convert.ToHexString(pushed.Written);
        int reset = written.IndexOf(StreamOneClosedReset, StringComparison.Ordinal);
        int nextHeaders = written.IndexOf(StreamThreeHeadersHeader, StringComparison.Ordinal);
        Diagnostics.Act("exit codes, bytes written (hex)", $"{timedOut.ExitCode} then {next.ExitCode}, {written}");
        Diagnostics.Assert("second body", "again\n", Encoding.Latin1.GetString(second.ToArray()));
        Diagnostics.Assert("RST_STREAM comes before stream 3's HEADERS", true, reset >= 0 && reset < nextHeaders);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, timedOut.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, next.ExitCode);
        Assert.AreEqual("again\n", Encoding.Latin1.GetString(second.ToArray()));
        Assert.IsGreaterThanOrEqualTo(0, reset, written);
        Assert.IsGreaterThan(reset, nextHeaders, written);
        Assert.DoesNotContain(ShutdownGoAway, written);
        Assert.AreEqual(2, connection.ReturnedReusableCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_TheResetOfATimedOutStreamFails_DoesNotKeepTheConnection()
    {
        StalledConnection stalled = new(ServerFrames(Head(isEndStream: false)), 65536);
        RecordingConnection connection = new(stalled);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Diagnostics.Arrange("connection", "writes fail with IOException once the server has stalled");

        Task<TransferResult> transfer = Handler(connection).ExecuteAsync(Context(HttpVersionPreference.Http2PriorKnowledge, time, TimeSpan.FromSeconds(1))).AsTask();
        await stalled.Stalled;
        connection.WriteFailure = new IOException("broken pipe");
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult result = await transfer;

        Diagnostics.Act("exit code, marked reusable", $"{result.ExitCode}, {connection.IsMarkedReusable}");
        Diagnostics.Assert("marked reusable", false, connection.IsMarkedReusable);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.IsFalse(connection.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ResetUnlessEndedBothWaysAsync_AWriteWasCancelled_TakesNoNewStreamAndSendsNothing()
    {
        RecordingConnection connection = new(new ScriptedConnection([], 65536)) { WriteFailure = new OperationCanceledException() };
        Http2Session session = new(connection);
        Http2StreamConnection stream = (Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false);
        await Assert.ThrowsAsync<OperationCanceledException>(() => stream.WriteAsync(Request, CancellationToken.None).AsTask());
        connection.WriteFailure = null;
        Diagnostics.Arrange("session", "the write opening it was cancelled");

        await stream.ResetUnlessEndedBothWaysAsync(CancellationToken.None);

        Diagnostics.Act("accepts new streams, bytes written (hex)", $"{session.AcceptsNewStreams}, {connection.WrittenHex}");
        Diagnostics.Assert("accepts new streams", false, session.AcceptsNewStreams);
        Assert.IsFalse(session.AcceptsNewStreams);
        Assert.AreEqual(string.Empty, connection.WrittenHex);
    }

    [TestMethod]
    [DataRow(5L, DisplayName = "the request body is unsent")]
    [DataRow(0L, DisplayName = "the response is unfinished")]
    public async Task ResetUnlessEndedBothWaysAsync_TheStreamHasNotEndedBothWays_ResetsItOnce(long bodyLength)
    {
        RecordingConnection connection = new(new ScriptedConnection([], 65536));
        Http2Session session = new(connection);
        Http2StreamConnection stream = (Http2StreamConnection)session.CreateStream("http", bodyLength, ignoresBody: false);
        await stream.WriteAsync(Request, CancellationToken.None);
        Diagnostics.Arrange("request body length", bodyLength);

        await stream.ResetUnlessEndedBothWaysAsync(CancellationToken.None);
        await stream.ResetUnlessEndedBothWaysAsync(CancellationToken.None);

        string written = connection.WrittenHex;
        Diagnostics.Act("bytes written (hex)", written);
        Diagnostics.Assert("ends with RST_STREAM", true, written.EndsWith(StreamOneClosedReset, StringComparison.Ordinal));
        StringAssert.EndsWith(written, StreamOneClosedReset, StringComparison.Ordinal);
        Assert.AreEqual(written.IndexOf(StreamOneResetHeader, StringComparison.Ordinal), written.LastIndexOf(StreamOneResetHeader, StringComparison.Ordinal));
        Assert.IsTrue(session.AcceptsNewStreams);
    }

    [TestMethod]
    public async Task ResetUnlessEndedBothWaysAsync_TheStreamNeverOpened_SendsNothing()
    {
        RecordingConnection connection = new(new ScriptedConnection([], 65536));
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);
        Diagnostics.Arrange("stream", "no head written");

        await stream.ResetUnlessEndedBothWaysAsync(CancellationToken.None);

        Diagnostics.Act("bytes written (hex)", connection.WrittenHex);
        Diagnostics.Assert("bytes written (hex)", string.Empty, connection.WrittenHex);
        Assert.AreEqual(string.Empty, connection.WrittenHex);
    }

    [TestMethod]
    [DataRow(true, false, false, DisplayName = "the head ends the stream")]
    [DataRow(false, false, true, DisplayName = "the response is unfinished")]
    [DataRow(false, true, true, DisplayName = "unread DATA ends the stream")]
    public async Task ExecuteAsync_NextTransferAfterFFailsOne_GoesOutOnTheSameConnectionWhichIsLeftIntact(bool headEndsStream, bool dataFollows, bool resets)
    {
        byte[] data = dataFollows ? Http2FrameCodec.Serialize(Data(1, "err\n", isEndStream: true)) : [];
        PushedBytesConnection pushed = new();
        SessionHoldingConnection connection = new(pushed);
        HttpProtocolHandler handler = new(QueueConnector.For(connection, connection), new SilentAuthenticator());
        RecordingTransferEvents events = new();
        MemoryStream second = new();
        pushed.Push([.. ServerFrames(Head(headEndsStream, "500")), .. data]);
        Diagnostics.Arrange("server", $"answers stream 1 with 500 (head ends the stream: {headEndsStream}, DATA follows: {dataFollows}), then stream 3 with 200");

        TransferResult failed = await handler.ExecuteAsync(Context(HttpVersionPreference.Http2PriorKnowledge, events: events, fail: HttpFailMode.Fail));
        pushed.Push([.. Http2FrameCodec.Serialize(Head(isEndStream: false, streamId: 3)), .. Http2FrameCodec.Serialize(Data(3, "again\n", isEndStream: true))]);
        TransferResult next = await handler.ExecuteAsync(Context(HttpVersionPreference.Http2PriorKnowledge, output: second));

        string written = Convert.ToHexString(pushed.Written);
        int reset = written.IndexOf(StreamOneResetHeader, StringComparison.Ordinal);
        int nextHeaders = written.IndexOf(StreamThreeHeadersHeader, StringComparison.Ordinal);
        Diagnostics.Act("exit codes, -v lines, bytes written (hex)", $"{failed.ExitCode} then {next.ExitCode}, {string.Join('|', events.Info)}, {written}");
        Diagnostics.Assert("second body", "again\n", Encoding.Latin1.GetString(second.ToArray()));
        Diagnostics.Assert("stream 1 is reset", resets, reset >= 0);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, failed.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, next.ExitCode, next.ErrorMessage);
        Assert.AreEqual("again\n", Encoding.Latin1.GetString(second.ToArray()));
        CollectionAssert.Contains(events.Info, "Connection #0 to host example.com:80 left intact");
        CollectionAssert.DoesNotContain(events.Info, "closing connection #0");
        Assert.AreEqual(resets, reset >= 0, written);
        Assert.AreEqual(resets, written.Contains(StreamOneClosedReset, StringComparison.Ordinal), written);
        Assert.IsGreaterThan(reset, nextHeaders, written);
        Assert.AreEqual(reset, written.LastIndexOf(StreamOneResetHeader, StringComparison.Ordinal), written);
        Assert.DoesNotContain(ShutdownGoAway, written);
        Assert.AreEqual(2, connection.ReturnedReusableCount);
    }

    private static byte[] Request => "GET / HTTP/1.1\r\nHost: h\r\n\r\n"u8.ToArray();

    private static HttpProtocolHandler Handler(IConnection connection) => new(QueueConnector.For(connection), new SilentAuthenticator());

    // A clock no test advances, unless given one: on the real clock a slow machine can pass the
    // handler's stream cleanup limit and drop the reset the test expects (BL-6052).
    private static TransferContext Context(HttpVersionPreference version, TimeProvider? time = null, TimeSpan? maxTime = null, bool noBody = false, RecordingTransferEvents? events = null, Stream? output = null, HttpFailMode fail = HttpFailMode.None) =>
        new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = output ?? new MemoryStream(),
            Http = new HttpRequestOptions { Version = version, Fail = fail },
            TimeProvider = time ?? new FakeTimeProvider(DateTimeOffset.UnixEpoch),
            MaxTime = maxTime,
            NoBody = noBody,
            Events = events ?? new RecordingTransferEvents(),
        };

    private static Http2Frame Head(bool isEndStream, string status = "200", int streamId = 1) =>
        Http2FrameFactory.CreateHeaders(streamId, new HpackEncoder().Encode([new(":status", status)]), isEndStream, isEndHeaders: true);

    private static Http2Frame Data(int streamId, string text, bool isEndStream) =>
        Http2FrameFactory.CreateData(streamId, Encoding.Latin1.GetBytes(text), isEndStream);

    /// <summary>Gives a server's empty SETTINGS followed by <paramref name="frame" />.</summary>
    private static byte[] ServerFrames(Http2Frame frame) =>
        [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])), .. Http2FrameCodec.Serialize(frame)];

    /// <summary>Records every byte written, and reads from <paramref name="inner" />; it holds no session.</summary>
    private sealed class RecordingConnection(IConnection inner) : IConnection
    {
        private readonly MemoryStream written = new();

        public string WrittenHex => Convert.ToHexString(written.ToArray());

        public bool IsSharedWithAnotherTransfer { get; init; }

        /// <summary>Gets or sets what every write throws, or <see langword="null" /> while writes succeed.</summary>
        public Exception? WriteFailure { get; set; }

        public bool IsMarkedReusable { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public void MarkReusable() => IsMarkedReusable = true;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (WriteFailure is not null)
            {
                return ValueTask.FromException(WriteFailure);
            }

            written.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
