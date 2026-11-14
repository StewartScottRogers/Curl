using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>
/// Checks that a gRPC reply's messages are written to the output as each one arrives, so a <c>-m</c> timeout keeps the
/// messages already received (EXT-047, ADR-1147).
/// </summary>
[TestClass]
public sealed class GrpcStreamingOutputTests
{
    private const string Url = "grpc://example.com:50051/helloworld.Greeter/SayHello";

    [TestMethod]
    public async Task ExecuteAsync_FirstMessageArrived_IsWrittenBeforeTheLaterMessagesAndTheTrailersArrive()
    {
        var connection = new HangingConnection(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(Reply("a0"))));
        var output = new MemoryStream();
        using var cancel = new CancellationTokenSource();
        Task<TransferResult> call = StartAsync(connection, output, new CountingProgress(), cancel.Token);

        await connection.Hanging.Task;

        Assert.AreEqual(Json("a0"), Encoding.UTF8.GetString(output.ToArray()));
        Assert.IsFalse(call.IsCompleted);
        await cancel.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => call);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTimeAfterThreeOfFiveMessages_KeepsThemAndReportsOperationTimedOutWithTheBytesWritten()
    {
        var clock = new ManualClock();
        using var watchdog = new Curl.Core.MaxTimeWatchdog(TimeSpan.FromSeconds(2), clock);
        var connection = new HangingConnection(GrpcServerReply.Of(
            GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders),
            GrpcServerReply.Message(Reply("a0")),
            GrpcServerReply.Message(Reply("a1")),
            GrpcServerReply.Message(Reply("a2"))));
        var output = new MemoryStream();
        var progress = new CountingProgress();
        Task<TransferResult> call = StartAsync(connection, output, watchdog.WatchProgress(progress), watchdog.Token);

        await connection.Hanging.Task;
        clock.Fire(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<OperationCanceledException>(() => call);

        string expected = Json("a0") + Json("a1") + Json("a2");
        Assert.AreEqual(expected, Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(CurlExitCode.OperationTimedOut, watchdog.Failure.ExitCode);
        Assert.AreEqual(expected.Length, progress.Downloaded);
        Assert.AreEqual($"Operation timed out after 2000 milliseconds with {expected.Length} bytes received", watchdog.Failure.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnaryCall_WritesTheMessageAsBefore()
    {
        (TransferResult result, string text) = await RunAsync(GrpcServerReply.Message(Reply("Hi")), GrpcServerReply.Trailers(0));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(Json("Hi"), text);
        Assert.AreEqual(Json("Hi").Length, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_CompletedServerStreamingCall_WritesEveryMessageInOrder()
    {
        (TransferResult result, string text) = await RunAsync(GrpcServerReply.Message(Reply("a0")), GrpcServerReply.Message(Reply("a1")), GrpcServerReply.Message(Reply("a2")), GrpcServerReply.Trailers(0));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(Json("a0") + Json("a1") + Json("a2"), text);
        Assert.AreEqual(text.Length, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonOkStatusAfterMessages_StillWritesTheMessagesThatPrecededIt()
    {
        (TransferResult result, string text) = await RunAsync(GrpcServerReply.Message(Reply("a0")), GrpcServerReply.Message(Reply("a1")), GrpcServerReply.Trailers(14, "gone"));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(Json("a0") + Json("a1"), text);
        Assert.AreEqual(text.Length, result.BytesTransferred);
        StringAssert.Contains(result.ErrorMessage, "grpc-status 14 (UNAVAILABLE): gone");
    }

    private static byte[] Reply(string text) => [0x0A, (byte)text.Length, .. Encoding.UTF8.GetBytes(text)];

    private static string Json(string text) => $"{{\n  \"message\": \"{text}\"\n}}\n";

    private static async Task<(TransferResult Result, string Text)> RunAsync(params Http2Frame[] frames)
    {
        var output = new MemoryStream();
        var connection = new ScriptedConnection(GrpcServerReply.Of([GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), .. frames]));
        TransferResult result = await StartAsync(connection, output, new CountingProgress(), CancellationToken.None);
        return (result, Encoding.UTF8.GetString(output.ToArray()));
    }

    private static Task<TransferResult> StartAsync(IConnection connection, Stream output, ITransferProgress progress, CancellationToken cancellationToken)
    {
        using var proto = new ProtoFixture();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = output,
            PostData = Encoding.UTF8.GetBytes("{}"),
            Progress = progress,
            CancellationToken = cancellationToken,
        };
        return new GrpcProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection, null)))
            .ExecuteAsync(new GrpcProtoContext(context, proto.GreeterPath)).AsTask();
    }

    private sealed class CountingProgress : ITransferProgress
    {
        public long Downloaded { get; private set; }

        public void ReportTransferStarted()
        {
        }

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Downloaded = bytesSoFar;

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        private TimerCallback? callback;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => ticks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            this.callback = callback;
            return new NoTimer();
        }

        public void Fire(TimeSpan elapsed)
        {
            ticks += elapsed.Ticks;
            callback!(null);
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>A connection that gives its scripted bytes, then waits for cancellation on the next read.</summary>
    private sealed class HangingConnection(byte[] reply) : IConnection
    {
        private readonly ScriptedConnection inner = new(reply);

        public TaskCompletionSource Hanging { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsSecure => false;

        public System.Net.EndPoint? RemoteEndPoint => null;

        public bool IsDisposed => inner.IsDisposed;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int read = await inner.ReadAsync(buffer, cancellationToken);
            if (read > 0)
            {
                return read;
            }

            Hanging.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
