using System.Net;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// What a failed or timed-out HTTP/2 transfer does when the peer stops taking what is written,
/// so its RST_STREAM never goes out: it gives the write up after one second on its
/// <see cref="TimeProvider" /> and ends with its own exit code (BL-6006, ADR-1421).
/// </summary>
[TestClass]
public sealed class Http2StalledStreamCleanupTests
{
    private static readonly TimeSpan CleanupLimit = TimeSpan.FromSeconds(1);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(true, DisplayName = "the connection holds the session")]
    [DataRow(false, DisplayName = "the handler closes the session itself")]
    public async Task ExecuteAsync_TheResetOfAStreamFFailedNeverGoesOut_EndsWithExit22AfterTheCleanupLimit(bool holdsSession)
    {
        StalledResetConnection stalled = new(new StalledConnection(Response("500"), 65536));
        SessionHoldingConnection pooled = new(stalled);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Diagnostics.Arrange("server", $"500 with the stream left open, then takes no RST_STREAM; the connection holds the session: {holdsSession}");

        Task<TransferResult> transfer = Handler(holdsSession ? pooled : stalled).ExecuteAsync(Context(time, HttpFailMode.Fail)).AsTask();
        await stalled.ResetStalled;
        bool endedBeforeTheLimit = transfer.IsCompleted;
        time.Advance(CleanupLimit);
        TransferResult result = await transfer;

        Diagnostics.Act("ended before the limit, exit code, writes", $"{endedBeforeTheLimit}, {result.ExitCode}, {stalled.StalledWriteCount} stalled");
        Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.IsFalse(endedBeforeTheLimit);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(1, stalled.StalledWriteCount);
        Assert.AreEqual(0, pooled.ReturnedReusableCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_TheResetOfATimedOutStreamNeverGoesOut_EndsWithExit28AfterTheCleanupLimit()
    {
        StalledConnection silent = new(Response("200"), 65536);
        StalledResetConnection stalled = new(silent);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        TimeSpan maxTime = TimeSpan.FromSeconds(5);
        Diagnostics.Arrange("server", "stalls after the head of a 200, then takes no RST_STREAM; max time 5000 ms");

        Task<TransferResult> transfer = Handler(new SessionHoldingConnection(stalled)).ExecuteAsync(Context(time, HttpFailMode.None, maxTime)).AsTask();
        await silent.Stalled;
        time.Advance(maxTime);
        await stalled.ResetStalled;
        bool endedBeforeTheLimit = transfer.IsCompleted;
        time.Advance(CleanupLimit);
        TransferResult result = await transfer;

        Diagnostics.Act("ended before the limit, exit code", $"{endedBeforeTheLimit}, {result.ExitCode}");
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.IsFalse(endedBeforeTheLimit);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_TheResetIsCancelledByAnotherThanTheCleanupLimit_LetsTheCancellationThrough()
    {
        OperationCanceledException other = new("not the cleanup limit");
        StalledResetConnection failing = new(new StalledConnection(Response("500"), 65536)) { ResetFailure = other };
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Diagnostics.Arrange("server", "500 with the stream left open; the RST_STREAM write is cancelled by someone else");

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => Handler(new SessionHoldingConnection(failing)).ExecuteAsync(Context(time, HttpFailMode.Fail)).AsTask());

        Diagnostics.Act("thrown", thrown.Message);
        Diagnostics.Assert("thrown", other.Message, thrown.Message);
        Assert.AreSame(other, thrown);
    }

    private static HttpProtocolHandler Handler(IConnection connection) => new(QueueConnector.For(connection), new SilentAuthenticator());

    private static TransferContext Context(TimeProvider time, HttpFailMode fail, TimeSpan? maxTime = null) => new()
    {
        Url = CurlUrl.Parse("http://example.com/"),
        Output = new MemoryStream(),
        Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, Fail = fail },
        TimeProvider = time,
        MaxTime = maxTime,
        Events = new RecordingTransferEvents(),
    };

    /// <summary>Gives a server's empty SETTINGS followed by a head on stream 1 that leaves the stream open.</summary>
    private static byte[] Response(string status) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode([new(":status", status)]), isEndStream: false, isEndHeaders: true)),
    ];

    /// <summary>
    /// Reads from <paramref name="inner" /> and takes every write until the first RST_STREAM, which,
    /// with everything after it, never completes until it is cancelled, as a write to a peer that
    /// has stopped reading does.
    /// </summary>
    private sealed class StalledResetConnection(IConnection inner) : IConnection
    {
        private const int RstStreamFrameType = 3;

        private readonly TaskCompletionSource resetStalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ResetStalled => resetStalled.Task;

        /// <summary>Gets what the RST_STREAM write throws at once, or <see langword="null" /> to stall it.</summary>
        public Exception? ResetFailure { get; init; }

        /// <summary>Gets how many writes have stalled.</summary>
        public int StalledWriteCount { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            bool isReset = buffer.Length > RstStreamFrameType && buffer.Span[RstStreamFrameType] == RstStreamFrameType;
            if (isReset && ResetFailure is not null)
            {
                throw ResetFailure;
            }

            if (isReset || ResetStalled.IsCompleted)
            {
                StalledWriteCount++;
                resetStalled.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
