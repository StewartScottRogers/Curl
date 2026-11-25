using Curl.Http2;
using Curl.Http3;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// What <c>-m</c> does to an HTTP/3 transfer's stream and connection, as curl 8.18.0's ngtcp2
/// build was measured to do (BL-5978): STOP_SENDING with <c>H3_REQUEST_CANCELLED</c>, a QPACK
/// Stream Cancellation, <c>left intact</c>, and the next URL on a new stream of the connection.
/// </summary>
[TestClass]
public sealed class Http3StreamAbortTests
{
    private const long RequestCancelled = 0x10c;

    private const string LeftIntact = "Connection #0 to host example.com:443 left intact";

    private static readonly byte[] DecoderStreamType = [0x03];

    private static readonly byte[] DecoderStreamCancellingStreamZero = [0x03, 0x40];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_MaxTimePassesOnAStream_CancelsItAndReportsTheConnectionLeftIntact()
    {
        FakeMultiplexedStream stalled = new(0, Response(0)) { StaysOpen = true };
        FakeMultiplexedConnection quic = new(stalled);
        PoolingConnector connector = new(quic);
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Diagnostics.Arrange("server", "HEADERS :status 200 and DATA on stream 0, never its end; max time 1000 ms");

        Task<TransferResult> transfer = Handler(connector).ExecuteAsync(Context(time, TimeSpan.FromSeconds(1), events)).AsTask();
        await stalled.Drained;
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult result = await transfer;

        string info = string.Join('|', events.Info);
        byte[] decoderStream = quic.UnidirectionalStreams[2].Written.ToArray();
        Diagnostics.Act("exit code, abort code, -v info lines", $"{result.ExitCode}, {stalled.AbortCode}, {info}");
        Diagnostics.Bytes("decoder stream", decoderStream);
        Diagnostics.Assert("last info line", LeftIntact, events.Info[^1]);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(RequestCancelled, stalled.AbortCode);
        CollectionAssert.AreEqual(DecoderStreamCancellingStreamZero, decoderStream);
        Assert.AreEqual(LeftIntact, events.Info[^1], info);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("closing", StringComparison.Ordinal)), info);
    }

    [TestMethod]
    public async Task ExecuteAsync_NextTransferAfterATimeout_GoesOutOnANewStreamOfTheSameConnection()
    {
        FakeMultiplexedStream stalled = new(0, Response(0)) { StaysOpen = true };
        FakeMultiplexedStream answered = new(4, Response(4));
        FakeMultiplexedConnection quic = new(stalled, answered);
        PoolingConnector connector = new(quic);
        HttpProtocolHandler handler = Handler(connector);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        MemoryStream second = new();
        Diagnostics.Arrange("server", "stalls on stream 0, answers stream 4 whole");

        Task<TransferResult> timingOut = handler.ExecuteAsync(Context(time, TimeSpan.FromSeconds(1))).AsTask();
        await stalled.Drained;
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult timedOut = await timingOut;
        TransferResult next = await handler.ExecuteAsync(Context(output: second));

        Diagnostics.Act("exit codes, returned reusable", $"{timedOut.ExitCode} then {next.ExitCode}, {connector.Pooled!.ReturnedReusableCount}");
        Diagnostics.Assert("returned reusable", 2, connector.Pooled.ReturnedReusableCount);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, timedOut.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, next.ExitCode);
        CollectionAssert.AreEqual("hello\n"u8.ToArray(), second.ToArray());
        Assert.IsNull(answered.AbortCode);
        Assert.IsNull(quic.CloseCode);
        Assert.AreEqual(2, connector.Pooled.ReturnedReusableCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_TheCancellationOfATimedOutStreamFails_DoesNotKeepTheConnection()
    {
        FakeMultiplexedStream stalled = new(0, Response(0)) { StaysOpen = true };
        FakeMultiplexedConnection quic = new(stalled);
        PoolingConnector connector = new(quic);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Diagnostics.Arrange("server", "stalls on stream 0; the connection is lost before the cancellation is written");

        Task<TransferResult> transfer = Handler(connector).ExecuteAsync(Context(time, TimeSpan.FromSeconds(1))).AsTask();
        await stalled.Drained;
        quic.UnidirectionalStreams[2].WriteException = new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost");
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult result = await transfer;

        Diagnostics.Act("exit code, returned reusable", $"{result.ExitCode}, {connector.Pooled!.ReturnedReusableCount}");
        Diagnostics.Assert("returned reusable", 0, connector.Pooled.ReturnedReusableCount);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(0, connector.Pooled.ReturnedReusableCount);
    }

    [TestMethod]
    public async Task CancelUnlessEndedBothWaysAsync_TheStreamNeverOpened_SendsNothingAndReportsNothing()
    {
        FakeMultiplexedConnection quic = new();
        Http3StreamConnection stream = Stream(new Http3Session(quic), bodyLength: 0);
        Diagnostics.Arrange("stream", "no request head written");

        await stream.CancelUnlessEndedBothWaysAsync(CancellationToken.None);
        stream.ReportTransferDone(0);

        Diagnostics.Act("unidirectional streams opened", quic.UnidirectionalStreams.Count);
        Diagnostics.Assert("unidirectional streams opened", 0, quic.UnidirectionalStreams.Count);
        Assert.IsEmpty(quic.UnidirectionalStreams);
    }

    [TestMethod]
    public async Task CancelUnlessEndedBothWaysAsync_TheStreamEndedBothWays_SendsNothing()
    {
        FakeMultiplexedStream request = new(0, Response(0));
        FakeMultiplexedConnection quic = new(request);
        Http3StreamConnection stream = Stream(new Http3Session(quic), bodyLength: 0);
        await stream.WriteAsync("GET / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);
        await stream.ReadToEndAsync(CancellationToken.None);
        Diagnostics.Arrange("stream", "request without a body, response read to its end");

        await stream.CancelUnlessEndedBothWaysAsync(CancellationToken.None);

        Diagnostics.Act("abort code", request.AbortCode);
        Diagnostics.Assert("abort code", null, request.AbortCode);
        Assert.IsNull(request.AbortCode);
        CollectionAssert.AreEqual(DecoderStreamType, quic.UnidirectionalStreams[2].Written.ToArray());
    }

    [TestMethod]
    public async Task CancelUnlessEndedBothWaysAsync_TheUploadIsUnfinished_CancelsTheStream()
    {
        FakeMultiplexedStream request = new(0, Response(0));
        FakeMultiplexedConnection quic = new(request);
        Http3StreamConnection stream = Stream(new Http3Session(quic), bodyLength: 10);
        await stream.WriteAsync("PUT / HTTP/1.1\r\nHost: a\r\n\r\n"u8.ToArray(), CancellationToken.None);
        await stream.ReadToEndAsync(CancellationToken.None);
        Diagnostics.Arrange("stream", "response read to its end, none of a 10-byte body sent");

        await stream.CancelUnlessEndedBothWaysAsync(CancellationToken.None);

        Diagnostics.Act("abort code", request.AbortCode);
        Diagnostics.Assert("abort code", RequestCancelled, request.AbortCode);
        Assert.AreEqual(RequestCancelled, request.AbortCode);
        CollectionAssert.AreEqual(DecoderStreamCancellingStreamZero, quic.UnidirectionalStreams[2].Written.ToArray());
    }

    private static byte[] Response(long streamId) =>
    [
        .. new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(streamId, [new HeaderField(":status", "200")])).ToBytes(),
        .. new Http3DataFrame("hello\n"u8.ToArray()).ToBytes(),
    ];

    private static Http3StreamConnection Stream(Http3Session session, long bodyLength) =>
        (Http3StreamConnection)session.CreateStream("https", bodyLength, ignoresBody: false);

    private static HttpProtocolHandler Handler(IConnector connector) => new(connector, new SilentAuthenticator());

    private static TransferContext Context(TimeProvider? time = null, TimeSpan? maxTime = null, RecordingTransferEvents? events = null, Stream? output = null) => new()
    {
        Url = CurlUrl.Parse("https://example.com/"),
        Output = output ?? new MemoryStream(),
        TimeProvider = time ?? TimeProvider.System,
        ConnectTimeout = TimeSpan.FromSeconds(1),
        MaxTime = maxTime,
        Events = events ?? new RecordingTransferEvents(),
        Http = new HttpRequestOptions { Version = HttpVersionPreference.Http3Only },
    };

    /// <summary>Opens one HTTP/3 session and gives every connect the same pooled connection holding it, as a pool does.</summary>
    private sealed class PoolingConnector(FakeMultiplexedConnection quic) : IConnector
    {
        public SessionHoldingConnection? Pooled { get; private set; }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken)
        {
            if (Pooled is null)
            {
                IConnection session = openSession(quic);
                Pooled = new SessionHoldingConnection(session);
                Pooled.TryHoldSession((IConnectionSession)session);
            }

            return ValueTask.FromResult(ConnectResult.Connected(Pooled));
        }
    }
}
