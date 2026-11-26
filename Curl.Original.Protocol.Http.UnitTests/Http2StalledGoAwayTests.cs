using System.Net;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// What closing an HTTP/2 connection does when the peer stops taking what is written before any
/// write was cancelled, so its GOAWAY never goes out: it gives the closing frames up after one
/// second on the session's <see cref="TimeProvider" /> (BL-6036, ADR-1426).
/// </summary>
[TestClass]
public sealed class Http2StalledGoAwayTests
{
    private const int DataFrameType = 0;

    private const int GoAwayFrameType = 7;

    private static readonly TimeSpan ClosingLimit = TimeSpan.FromSeconds(1);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ShutDownAsync_TheGoAwayNeverGoesOut_EndsAfterTheClosingLimit()
    {
        StallingConnection stalled = new(new ScriptedConnection([], 65536), GoAwayFrameType);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Http2Session session = new(stalled, timeProvider: time);
        await OpenStreamAsync(session);
        Diagnostics.Arrange("peer", "takes the request, then no GOAWAY");

        Task close = session.ShutDownAsync(CancellationToken.None).AsTask();
        await stalled.Stalled;
        bool endedBeforeTheLimit = close.IsCompleted;
        time.Advance(ClosingLimit);
        await close;

        Diagnostics.Act("ended before the limit, accepts new streams", $"{endedBeforeTheLimit}, {session.AcceptsNewStreams}");
        Diagnostics.Assert("ended before the limit", false, endedBeforeTheLimit);
        Assert.IsFalse(endedBeforeTheLimit);
        Assert.IsFalse(session.AcceptsNewStreams);
    }

    [TestMethod]
    public async Task ShutDownAsync_AnotherStalledWriteHoldsTheFrameLock_EndsAfterTheClosingLimit()
    {
        StallingConnection stalled = new(new ScriptedConnection([], 65536), DataFrameType);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Http2Session session = new(stalled, timeProvider: time);
        int streamId = await OpenStreamAsync(session);
        using CancellationTokenSource abandonWrite = new();
        Task<int> write = session.WriteDataAsync(streamId, new byte[] { 1 }, isEndStream: true, abandonWrite.Token).AsTask();
        await stalled.Stalled;
        Diagnostics.Arrange("peer", "takes no DATA; its write holds the frame lock");

        Task close = session.ShutDownAsync(CancellationToken.None).AsTask();
        bool endedBeforeTheLimit = close.IsCompleted;
        time.Advance(ClosingLimit);
        await close;

        Diagnostics.Act("ended before the limit, write still waiting", $"{endedBeforeTheLimit}, {!write.IsCompleted}");
        Diagnostics.Assert("ended before the limit", false, endedBeforeTheLimit);
        Assert.IsFalse(endedBeforeTheLimit);
        Assert.IsFalse(write.IsCompleted);
        await abandonWrite.CancelAsync();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => write);
    }

    [TestMethod]
    public async Task ShutDownAsync_TheCallerCancelsBeforeTheLimit_LetsTheCancellationThrough()
    {
        StallingConnection stalled = new(new ScriptedConnection([], 65536), GoAwayFrameType);
        Http2Session session = new(stalled, timeProvider: new FakeTimeProvider(DateTimeOffset.UnixEpoch));
        await OpenStreamAsync(session);
        using CancellationTokenSource caller = new();
        Diagnostics.Arrange("peer", "takes no GOAWAY; the caller cancels");

        Task close = session.ShutDownAsync(caller.Token).AsTask();
        await stalled.Stalled;
        await caller.CancelAsync();

        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => close);
        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("caller's token cancelled", true, caller.IsCancellationRequested);
    }

    [TestMethod]
    [DataRow(HttpVersionPreference.Http2PriorKnowledge, DisplayName = "prior knowledge")]
    [DataRow(HttpVersionPreference.Http2, DisplayName = "h2c upgrade")]
    public async Task ExecuteAsync_TheGoAwayOfASessionNoConnectionHoldsNeverGoesOut_EndsAfterTheLimitOnTheTransfersClock(HttpVersionPreference version)
    {
        byte[] switching = version == HttpVersionPreference.Http2
            ? "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n"u8.ToArray()
            : [];
        byte[] response =
        [
            .. switching,
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode([new(":status", "200")]), isEndStream: true, isEndHeaders: true)),
        ];
        StallingConnection stalled = new(new ScriptedConnection(response, 65536), GoAwayFrameType);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Version = version },
            TimeProvider = time,
            Events = new RecordingTransferEvents(),
        };
        Diagnostics.Arrange("server", $"200, then takes no GOAWAY; {version}");

        Task<TransferResult> transfer = new HttpProtocolHandler(QueueConnector.For(stalled), new SilentAuthenticator()).ExecuteAsync(context).AsTask();
        await stalled.Stalled;
        bool endedBeforeTheLimit = transfer.IsCompleted;
        time.Advance(ClosingLimit);
        TransferResult result = await transfer;

        Diagnostics.Act("ended before the limit, exit code", $"{endedBeforeTheLimit}, {result.ExitCode}");
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(endedBeforeTheLimit);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    private static ValueTask<int> OpenStreamAsync(Http2Session session) =>
        session.StartStreamAsync(
            (Http2StreamConnection)session.CreateStream("http", 1, ignoresBody: false),
            [new(":method", "POST")],
            isEndStream: false,
            CancellationToken.None);

    /// <summary>
    /// Reads from <paramref name="inner" /> and takes every write until the first frame of type
    /// <paramref name="stalledFrameType" />, which never completes until it is cancelled, as a
    /// write to a peer that has stopped reading does.
    /// </summary>
    private sealed class StallingConnection(IConnection inner, int stalledFrameType) : IConnection
    {
        private const int FrameTypeOffset = 3;

        private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Stalled => stalled.Task;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (buffer.Length > FrameTypeOffset && buffer.Span[FrameTypeOffset] == stalledFrameType)
            {
                stalled.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
