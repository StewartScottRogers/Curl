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
/// that <c>-m</c> aborts with STREAM_CLOSED before its GOAWAY (measured, BL-5906 Notes).
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

    /// <summary>The type, flags and stream 1 of any RST_STREAM on stream 1.</summary>
    private const string StreamOneResetHeader = "000004030000000001";

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

    private static HttpProtocolHandler Handler(IConnection connection) => new(QueueConnector.For(connection), new SilentAuthenticator());

    private static TransferContext Context(HttpVersionPreference version, TimeProvider? time = null, TimeSpan? maxTime = null, bool noBody = false) =>
        new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Version = version },
            TimeProvider = time ?? TimeProvider.System,
            MaxTime = maxTime,
            NoBody = noBody,
        };

    private static Http2Frame Head(bool isEndStream) =>
        Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode([new(":status", "200")]), isEndStream, isEndHeaders: true);

    /// <summary>Gives a server's empty SETTINGS followed by <paramref name="frame" />.</summary>
    private static byte[] ServerFrames(Http2Frame frame) =>
        [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])), .. Http2FrameCodec.Serialize(frame)];

    /// <summary>Records every byte written, and reads from <paramref name="inner" />; it holds no session.</summary>
    private sealed class RecordingConnection(IConnection inner) : IConnection
    {
        private readonly MemoryStream written = new();

        public string WrittenHex => Convert.ToHexString(written.ToArray());

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            written.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
