using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Pins what a telnet session does when a read is cancelled (ADR-0117, Decision 4): once
/// <c>-m</c> has passed on the transfer's clock it ends with curl 8.21.0's own telnet message,
/// exit 28 and <c>Time-out</c> (measured 2026-09-28, BL-511 Notes), and before that, or with no
/// <c>-m</c>, the cancellation escapes for the runner to report. Also pins that the session
/// reports itself started and every byte it writes.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerMaxTimeTests
{
    private static readonly CurlUrl TelnetUrl = CurlUrl.Parse("telnet://example.test/");

    private static readonly byte[] Hello = "hello\r\n"u8.ToArray();

    private readonly SteppedClock clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_ReadCancelledOnceMaxTimePassed_EndsWithCurlsTelnetTimeOut()
    {
        TransferContext context = Context(TimeSpan.FromSeconds(1), operationStarted: null);

        TransferResult result = await ExecuteAsync(context, cancelledAfter: TimeSpan.FromSeconds(1));

        Diagnostics.AssertExitCode(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Diff("error", "Time-out", result.ErrorMessage ?? string.Empty);
        Diagnostics.Assert("bytes transferred", 7L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Time-out", result.ErrorMessage);
        Assert.AreEqual(7, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTimeCountedFromOperationStarted_EndsWithTimeOut()
    {
        clock.Advance(TimeSpan.FromSeconds(5));
        TransferContext context = Context(TimeSpan.FromSeconds(3), operationStarted: TimeSpan.FromSeconds(3).Ticks);
        Diagnostics.Arrange("clock", "5 s; operation started at 3 s");

        TransferResult result = await ExecuteAsync(context, cancelledAfter: TimeSpan.FromSeconds(1));

        Diagnostics.AssertExitCode(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no -m")]
    [DataRow(0, DisplayName = "-m 0")]
    [DataRow(2, DisplayName = "-m not yet passed")]
    public async Task ExecuteAsync_ReadCancelledBeforeAnyMaxTimePassed_LetsTheCancellationOut(int? maxTimeSeconds)
    {
        TimeSpan? maxTime = maxTimeSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null;
        TransferContext context = Context(maxTime, operationStarted: null);

        var thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => ExecuteAsync(context, cancelledAfter: TimeSpan.FromSeconds(1)));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("thrown", nameof(OperationCanceledException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReportsTheTransferStartedAndEveryByteWritten()
    {
        RecordingProgress progress = new();
        var context = new TransferContext
        {
            Url = TelnetUrl,
            Output = new MemoryStream(),
            Upload = new MemoryStream(),
            Progress = progress,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeReads([Hello]);

        await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(new ScriptedRead(Hello)))))
            .ExecuteAsync(context);

        Diagnostics.ActLines("progress reports", progress.Reports);
        Diagnostics.AssertLines("progress reports", ["started", "downloaded 7 of "], progress.Reports);
        CollectionAssert.AreEqual(new[] { "started", "downloaded 7 of " }, progress.Reports);
    }

    private TransferContext Context(TimeSpan? maxTime, long? operationStarted)
    {
        TransferContext context = new()
        {
            Url = TelnetUrl,
            Output = new MemoryStream(),
            Upload = new MemoryStream(),
            MaxTime = maxTime,
            OperationStarted = operationStarted,
            TimeProvider = clock,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.Arrange("operation started (ticks)", operationStarted?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(unset)");
        return context;
    }

    private async Task<TransferResult> ExecuteAsync(TransferContext context, TimeSpan cancelledAfter)
    {
        Diagnostics.Arrange("connection", $"replies hello\\r\\n, then its next read moves the clock {cancelledAfter} and is cancelled");
        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(new CancelledAfterReplyConnection(clock, cancelledAfter))))
            .ExecuteAsync(context);
        Diagnostics.ActResult(result);
        return result;
    }

    /// <summary>A clock that moves only when told to.</summary>
    private sealed class SteppedClock : TimeProvider
    {
        private long ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => ticks;

        public void Advance(TimeSpan duration) => ticks += duration.Ticks;
    }

    /// <summary>
    /// A connection whose first read returns <c>hello\r\n</c> and whose next read moves the clock
    /// on and is cancelled, as the runner's <c>-m</c> watchdog cancels a stalled read.
    /// </summary>
    private sealed class CancelledAfterReplyConnection(SteppedClock clock, TimeSpan cancelledAfter) : IConnection
    {
        private bool replied;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!replied)
            {
                replied = true;
                Hello.CopyTo(buffer);
                return ValueTask.FromResult(Hello.Length);
            }

            clock.Advance(cancelledAfter);
            throw new OperationCanceledException();
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingProgress : ITransferProgress
    {
        public List<string> Reports { get; } = [];

        public void ReportTransferStarted() => Reports.Add("started");

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"downloaded {bytesSoFar} of {expectedTotal}");

        public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"uploaded {bytesSoFar} of {expectedTotal}");
    }
}
