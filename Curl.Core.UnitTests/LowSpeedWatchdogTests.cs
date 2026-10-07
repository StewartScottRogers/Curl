using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins <see cref="LowSpeedWatchdog" /> ending a transfer that stays below <c>-Y</c> for
/// <c>-y</c> on <see cref="TickingTimeProvider" />, one check per simulated second, with the
/// message curl 8.21.0 prints (measured 2026-09-27, BL-400 Notes).
/// </summary>
[TestClass]
public sealed class LowSpeedWatchdogTests
{
    private readonly TickingTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_LimitBelowOne_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 0);
        diagnostics.Arrange("time", TimeSpan.FromSeconds(1));

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LowSpeedWatchdog(0, TimeSpan.FromSeconds(1), clock));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
        Assert.AreEqual(nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void Constructor_TimeNotPositive_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 1);
        diagnostics.Arrange("time", TimeSpan.Zero);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LowSpeedWatchdog(1, TimeSpan.Zero, clock));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
        Assert.AreEqual(nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void Constructor_NullClock_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("clock", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new LowSpeedWatchdog(1, TimeSpan.FromSeconds(1), null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
        Assert.AreEqual(nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void StalledTransfer_StaysBelowTheLimitForTheSpeedTime_IsTooSlowAndCancelsTheToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 100);
        diagnostics.Arrange("time", TimeSpan.FromSeconds(2));
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);

        diagnostics.Arrange("tick seconds", 2);
        clock.Tick(2);
        diagnostics.Act("too slow after 2 s", watchdog.IsTooSlow);
        diagnostics.Act("cancelled after 2 s", watchdog.Token.IsCancellationRequested);
        diagnostics.Assert("too slow after 2 s", false, watchdog.IsTooSlow);
        Assert.IsFalse(watchdog.IsTooSlow);
        diagnostics.Assert("cancelled after 2 s", false, watchdog.Token.IsCancellationRequested);
        Assert.IsFalse(watchdog.Token.IsCancellationRequested);

        diagnostics.Arrange("tick seconds", 1);
        clock.Tick();
        diagnostics.Act("too slow after 3 s", watchdog.IsTooSlow);
        diagnostics.Act("cancelled after 3 s", watchdog.Token.IsCancellationRequested);
        diagnostics.Assert("too slow after 3 s", true, watchdog.IsTooSlow);
        Assert.IsTrue(watchdog.IsTooSlow);
        diagnostics.Assert("cancelled after 3 s", true, watchdog.Token.IsCancellationRequested);
        Assert.IsTrue(watchdog.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Failure_IsExit28WithCurlsMessageAndTheBytesWritten()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("bytes written", 7);
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);
        watchdog.WatchOutput(new MemoryStream()).Write(new byte[7], 0, 7);

        TransferResult failure = watchdog.Failure;

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("error message", failure.ErrorMessage);
        diagnostics.Act("bytes transferred", failure.BytesTransferred);
        diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure.ExitCode);
        diagnostics.Assert("error message", "Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds", failure.ErrorMessage);
        Assert.AreEqual("Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds", failure.ErrorMessage);
        diagnostics.Assert("bytes transferred", 7, failure.BytesTransferred);
        Assert.AreEqual(7, failure.BytesTransferred);
    }

    [TestMethod]
    public void FastTransfer_NeverTrips_AcrossMoreChecksThanTheSpeedWindow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 100);
        diagnostics.Arrange("bytes per second", 100);
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);
        Stream output = watchdog.WatchOutput(new MemoryStream());

        for (int second = 0; second < 10; second++)
        {
            output.Write(new byte[100], 0, 100);
            clock.Tick();
        }

        diagnostics.Arrange("ticked seconds", 10);
        diagnostics.Act("too slow", watchdog.IsTooSlow);
        diagnostics.Assert("too slow", false, watchdog.IsTooSlow);
        Assert.IsFalse(watchdog.IsTooSlow);
    }

    [TestMethod]
    public void SlowSpell_EndedByAFastSecond_StartsAgainFromTheNextSlowCheck()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 100);
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);
        Stream output = watchdog.WatchOutput(new MemoryStream());

        diagnostics.Arrange("tick seconds", 2);
        clock.Tick(2);
        diagnostics.Arrange("bytes written", 1000);
        output.Write(new byte[1000], 0, 1000);
        diagnostics.Arrange("tick seconds", 1);
        clock.Tick();
        diagnostics.Arrange("tick seconds", 3);
        clock.Tick(3);
        diagnostics.Act("too slow after the fast second", watchdog.IsTooSlow);
        diagnostics.Assert("too slow after the fast second", false, watchdog.IsTooSlow);
        Assert.IsFalse(watchdog.IsTooSlow);

        diagnostics.Arrange("tick seconds", 4);
        clock.Tick(4);
        diagnostics.Act("too slow after the next slow spell", watchdog.IsTooSlow);
        diagnostics.Assert("too slow after the next slow spell", true, watchdog.IsTooSlow);
        Assert.IsTrue(watchdog.IsTooSlow);
    }

    [TestMethod]
    public void Upload_ReportedFastEnough_KeepsTheTransferAlive()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 100);
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);
        RecordingProgress recorded = new();
        ITransferProgress progress = watchdog.WatchProgress(recorded);

        for (int second = 1; second <= 5; second++)
        {
            progress.ReportUploaded(second * 100, 1000);
            clock.Tick();
        }

        diagnostics.Arrange("uploaded per second", 100);
        diagnostics.Arrange("ticked seconds", 5);
        diagnostics.Act("too slow", watchdog.IsTooSlow);
        diagnostics.Act("last report", recorded.Last);
        diagnostics.Assert("too slow", false, watchdog.IsTooSlow);
        Assert.IsFalse(watchdog.IsTooSlow);
        diagnostics.Assert("last report", "uploaded 500 of 1000", recorded.Last);
        Assert.AreEqual("uploaded 500 of 1000", recorded.Last);
    }

    [TestMethod]
    public void WatchProgress_PassesStartedDownloadedAndDoneReportsThrough()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 1);
        using LowSpeedWatchdog watchdog = new(1, TimeSpan.FromSeconds(1), clock);
        RecordingProgress recorded = new();
        ITransferProgress progress = watchdog.WatchProgress(recorded);

        progress.ReportTransferStarted();
        diagnostics.Act("after started", recorded.Last);
        diagnostics.Assert("after started", "started", recorded.Last);
        Assert.AreEqual("started", recorded.Last);
        diagnostics.Arrange("downloaded", 3);
        progress.ReportDownloaded(3, null);
        diagnostics.Act("after downloaded", recorded.Last);
        diagnostics.Assert("after downloaded", "downloaded 3 of ", recorded.Last);
        Assert.AreEqual("downloaded 3 of ", recorded.Last);
        progress.ReportTransferDone();
        diagnostics.Act("after done", recorded.Last);
        diagnostics.Assert("after done", "done", recorded.Last);
        Assert.AreEqual("done", recorded.Last);
    }

    [TestMethod]
    public void Checks_AfterTheTransferWasTooSlow_ChangeNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 1);
        using LowSpeedWatchdog watchdog = new(1, TimeSpan.FromSeconds(1), clock);

        diagnostics.Arrange("tick seconds", 4);
        clock.Tick(4);

        diagnostics.Act("too slow", watchdog.IsTooSlow);
        diagnostics.Assert("too slow", true, watchdog.IsTooSlow);
        Assert.IsTrue(watchdog.IsTooSlow);
    }

    [TestMethod]
    public void Checks_AfterDispose_NeverTrip()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 1);
        LowSpeedWatchdog watchdog = new(1, TimeSpan.FromSeconds(1), clock);
        watchdog.Dispose();

        diagnostics.Arrange("tick seconds", 4);
        clock.Tick(4);

        diagnostics.Act("too slow", watchdog.IsTooSlow);
        diagnostics.Act("cancelled", watchdog.Token.IsCancellationRequested);
        diagnostics.Assert("too slow", false, watchdog.IsTooSlow);
        Assert.IsFalse(watchdog.IsTooSlow);
        diagnostics.Assert("cancelled", false, watchdog.Token.IsCancellationRequested);
        Assert.IsFalse(watchdog.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void StartFromCommandLine_NeitherOption_WatchesNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-Y", "none");
        diagnostics.Arrange("-y", "none");

        var watchdog = LowSpeedWatchdog.StartFromCommandLine(null, null, clock);

        diagnostics.Act("watchdog is null", watchdog is null);
        diagnostics.Assert("watchdog is null", true, watchdog is null);
        Assert.IsNull(watchdog);
    }

    [TestMethod]
    [DataRow(0L, 2L, DisplayName = "-Y 0 -y 2")]
    [DataRow(100L, 0L, DisplayName = "-Y 100 -y 0")]
    [DataRow(0L, null, DisplayName = "-Y 0")]
    [DataRow(null, 0L, DisplayName = "-y 0")]
    public void StartFromCommandLine_ZeroLimitOrTime_WatchesNothing(long? bytesPerSecond, long? seconds)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-Y", bytesPerSecond?.ToString() ?? "none");
        diagnostics.Arrange("-y", seconds?.ToString() ?? "none");

        var watchdog = LowSpeedWatchdog.StartFromCommandLine(bytesPerSecond, seconds, clock);

        diagnostics.Act("watchdog is null", watchdog is null);
        diagnostics.Assert("watchdog is null", true, watchdog is null);
        Assert.IsNull(watchdog);
    }

    [TestMethod]
    public void StartFromCommandLine_LimitWithoutTime_WatchesForThirtySeconds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-Y", 100);
        diagnostics.Arrange("-y", "none");
        using LowSpeedWatchdog? watchdog = LowSpeedWatchdog.StartFromCommandLine(100, null, clock);

        diagnostics.Act("error message", watchdog!.Failure.ErrorMessage);
        diagnostics.Assert("error message", "Operation too slow. Less than 100 bytes/sec transferred the last 30 seconds", watchdog.Failure.ErrorMessage);
        Assert.AreEqual("Operation too slow. Less than 100 bytes/sec transferred the last 30 seconds", watchdog!.Failure.ErrorMessage);
        diagnostics.Arrange("tick seconds", 30);
        clock.Tick(30);
        diagnostics.Act("too slow after 30 s", watchdog.IsTooSlow);
        diagnostics.Assert("too slow after 30 s", false, watchdog.IsTooSlow);
        Assert.IsFalse(watchdog.IsTooSlow);
        diagnostics.Arrange("tick seconds", 1);
        clock.Tick();
        diagnostics.Act("too slow after 31 s", watchdog.IsTooSlow);
        diagnostics.Assert("too slow after 31 s", true, watchdog.IsTooSlow);
        Assert.IsTrue(watchdog.IsTooSlow);
    }

    [TestMethod]
    public void StartFromCommandLine_TimeWithoutLimit_WatchesForOneBytePerSecond()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-Y", "none");
        diagnostics.Arrange("-y", 2);
        using LowSpeedWatchdog? watchdog = LowSpeedWatchdog.StartFromCommandLine(null, 2, clock);

        diagnostics.Act("error message", watchdog!.Failure.ErrorMessage);
        diagnostics.Assert("error message", "Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds", watchdog.Failure.ErrorMessage);
        Assert.AreEqual("Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds", watchdog!.Failure.ErrorMessage);
    }

    [TestMethod]
    public async Task WatchOutput_PassesEveryMemberThroughToTheOutput()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("limit", 1);
        using LowSpeedWatchdog watchdog = new(1, TimeSpan.FromSeconds(1), clock);
        using MemoryStream inner = new();
        Stream output = watchdog.WatchOutput(inner);

        diagnostics.Arrange("bytes written", 6);
        await output.WriteAsync(new byte[] { 1, 2, 3, 4 }, 0, 4);
        await output.WriteAsync(new ReadOnlyMemory<byte>([5, 6]));
        output.Flush();
        await output.FlushAsync();
        diagnostics.Act("can read", output.CanRead);
        diagnostics.Act("can seek", output.CanSeek);
        diagnostics.Act("can write", output.CanWrite);
        diagnostics.Act("length", output.Length);
        diagnostics.Assert("can read", true, output.CanRead);
        Assert.IsTrue(output.CanRead);
        diagnostics.Assert("can seek", true, output.CanSeek);
        Assert.IsTrue(output.CanSeek);
        diagnostics.Assert("can write", true, output.CanWrite);
        Assert.IsTrue(output.CanWrite);
        diagnostics.Assert("length", 6L, output.Length);
        Assert.AreEqual(6, output.Length);
        diagnostics.Arrange("position", 1);
        output.Position = 1;
        diagnostics.Act("position", output.Position);
        diagnostics.Assert("position", 1L, output.Position);
        Assert.AreEqual(1, output.Position);
        var read = output.Read(new byte[1], 0, 1);
        diagnostics.Act("read", read);
        diagnostics.Assert("read", 1, read);
        Assert.AreEqual(1, read);
        var readAsync = await output.ReadAsync(new byte[1], 0, 1);
        diagnostics.Act("read async", readAsync);
        diagnostics.Assert("read async", 1, readAsync);
        Assert.AreEqual(1, readAsync);
        var readMemory = await output.ReadAsync(new Memory<byte>(new byte[1]));
        diagnostics.Act("read memory", readMemory);
        diagnostics.Assert("read memory", 1, readMemory);
        Assert.AreEqual(1, readMemory);
        var seek = output.Seek(0, SeekOrigin.Begin);
        diagnostics.Act("seek", seek);
        diagnostics.Assert("seek", 0L, seek);
        Assert.AreEqual(0, seek);
        diagnostics.Arrange("set length", 2);
        output.SetLength(2);
        diagnostics.Act("inner length", inner.Length);
        diagnostics.Assert("inner length", 2L, inner.Length);
        Assert.AreEqual(2, inner.Length);
        diagnostics.Act("bytes transferred", watchdog.Failure.BytesTransferred);
        diagnostics.Assert("bytes transferred", 6L, watchdog.Failure.BytesTransferred);
        Assert.AreEqual(6, watchdog.Failure.BytesTransferred);
    }

    private sealed class RecordingProgress : ITransferProgress
    {
        public string Last { get; private set; } = string.Empty;

        public void ReportTransferStarted() => Last = "started";

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Last = $"downloaded {bytesSoFar} of {expectedTotal}";

        public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Last = $"uploaded {bytesSoFar} of {expectedTotal}";

        public void ReportTransferDone() => Last = "done";
    }
}
