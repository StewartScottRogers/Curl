using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Constructor_LimitBelowOne_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LowSpeedWatchdog(0, TimeSpan.FromSeconds(1), clock));
    }

    [TestMethod]
    public void Constructor_TimeNotPositive_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LowSpeedWatchdog(1, TimeSpan.Zero, clock));
    }

    [TestMethod]
    public void Constructor_NullClock_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LowSpeedWatchdog(1, TimeSpan.FromSeconds(1), null!));
    }

    [TestMethod]
    public void StalledTransfer_StaysBelowTheLimitForTheSpeedTime_IsTooSlowAndCancelsTheToken()
    {
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);

        clock.Tick(2);
        Assert.IsFalse(watchdog.IsTooSlow);
        Assert.IsFalse(watchdog.Token.IsCancellationRequested);

        clock.Tick();
        Assert.IsTrue(watchdog.IsTooSlow);
        Assert.IsTrue(watchdog.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Failure_IsExit28WithCurlsMessageAndTheBytesWritten()
    {
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);
        watchdog.WatchOutput(new MemoryStream()).Write(new byte[7], 0, 7);

        TransferResult failure = watchdog.Failure;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure.ExitCode);
        Assert.AreEqual("Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds", failure.ErrorMessage);
        Assert.AreEqual(7, failure.BytesTransferred);
    }

    [TestMethod]
    public void FastTransfer_NeverTrips_AcrossMoreChecksThanTheSpeedWindow()
    {
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);
        Stream output = watchdog.WatchOutput(new MemoryStream());

        for (int second = 0; second < 10; second++)
        {
            output.Write(new byte[100], 0, 100);
            clock.Tick();
        }

        Assert.IsFalse(watchdog.IsTooSlow);
    }

    [TestMethod]
    public void SlowSpell_EndedByAFastSecond_StartsAgainFromTheNextSlowCheck()
    {
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);
        Stream output = watchdog.WatchOutput(new MemoryStream());

        clock.Tick(2);
        output.Write(new byte[1000], 0, 1000);
        clock.Tick();
        clock.Tick(3);
        Assert.IsFalse(watchdog.IsTooSlow);

        clock.Tick(4);
        Assert.IsTrue(watchdog.IsTooSlow);
    }

    [TestMethod]
    public void Upload_ReportedFastEnough_KeepsTheTransferAlive()
    {
        using LowSpeedWatchdog watchdog = new(100, TimeSpan.FromSeconds(2), clock);
        RecordingProgress recorded = new();
        ITransferProgress progress = watchdog.WatchProgress(recorded);

        for (int second = 1; second <= 5; second++)
        {
            progress.ReportUploaded(second * 100, 1000);
            clock.Tick();
        }

        Assert.IsFalse(watchdog.IsTooSlow);
        Assert.AreEqual("uploaded 500 of 1000", recorded.Last);
    }

    [TestMethod]
    public void WatchProgress_PassesStartedAndDownloadedReportsThrough()
    {
        using LowSpeedWatchdog watchdog = new(1, TimeSpan.FromSeconds(1), clock);
        RecordingProgress recorded = new();
        ITransferProgress progress = watchdog.WatchProgress(recorded);

        progress.ReportTransferStarted();
        Assert.AreEqual("started", recorded.Last);
        progress.ReportDownloaded(3, null);
        Assert.AreEqual("downloaded 3 of ", recorded.Last);
    }

    [TestMethod]
    public void Checks_AfterTheTransferWasTooSlow_ChangeNothing()
    {
        using LowSpeedWatchdog watchdog = new(1, TimeSpan.FromSeconds(1), clock);

        clock.Tick(4);

        Assert.IsTrue(watchdog.IsTooSlow);
    }

    [TestMethod]
    public void Checks_AfterDispose_NeverTrip()
    {
        LowSpeedWatchdog watchdog = new(1, TimeSpan.FromSeconds(1), clock);
        watchdog.Dispose();

        clock.Tick(4);

        Assert.IsFalse(watchdog.IsTooSlow);
        Assert.IsFalse(watchdog.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void StartFromCommandLine_NeitherOption_WatchesNothing()
    {
        Assert.IsNull(LowSpeedWatchdog.StartFromCommandLine(null, null, clock));
    }

    [TestMethod]
    [DataRow(0L, 2L, DisplayName = "-Y 0 -y 2")]
    [DataRow(100L, 0L, DisplayName = "-Y 100 -y 0")]
    [DataRow(0L, null, DisplayName = "-Y 0")]
    [DataRow(null, 0L, DisplayName = "-y 0")]
    public void StartFromCommandLine_ZeroLimitOrTime_WatchesNothing(long? bytesPerSecond, long? seconds)
    {
        Assert.IsNull(LowSpeedWatchdog.StartFromCommandLine(bytesPerSecond, seconds, clock));
    }

    [TestMethod]
    public void StartFromCommandLine_LimitWithoutTime_WatchesForThirtySeconds()
    {
        using LowSpeedWatchdog? watchdog = LowSpeedWatchdog.StartFromCommandLine(100, null, clock);

        Assert.AreEqual("Operation too slow. Less than 100 bytes/sec transferred the last 30 seconds", watchdog!.Failure.ErrorMessage);
        clock.Tick(30);
        Assert.IsFalse(watchdog.IsTooSlow);
        clock.Tick();
        Assert.IsTrue(watchdog.IsTooSlow);
    }

    [TestMethod]
    public void StartFromCommandLine_TimeWithoutLimit_WatchesForOneBytePerSecond()
    {
        using LowSpeedWatchdog? watchdog = LowSpeedWatchdog.StartFromCommandLine(null, 2, clock);

        Assert.AreEqual("Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds", watchdog!.Failure.ErrorMessage);
    }

    [TestMethod]
    public async Task WatchOutput_PassesEveryMemberThroughToTheOutput()
    {
        using LowSpeedWatchdog watchdog = new(1, TimeSpan.FromSeconds(1), clock);
        using MemoryStream inner = new();
        Stream output = watchdog.WatchOutput(inner);

        await output.WriteAsync(new byte[] { 1, 2, 3, 4 }, 0, 4);
        await output.WriteAsync(new ReadOnlyMemory<byte>([5, 6]));
        output.Flush();
        await output.FlushAsync();
        Assert.IsTrue(output.CanRead);
        Assert.IsTrue(output.CanSeek);
        Assert.IsTrue(output.CanWrite);
        Assert.AreEqual(6, output.Length);
        output.Position = 1;
        Assert.AreEqual(1, output.Position);
        Assert.AreEqual(1, output.Read(new byte[1], 0, 1));
        Assert.AreEqual(1, await output.ReadAsync(new byte[1], 0, 1));
        Assert.AreEqual(1, await output.ReadAsync(new Memory<byte>(new byte[1])));
        Assert.AreEqual(0, output.Seek(0, SeekOrigin.Begin));
        output.SetLength(2);
        Assert.AreEqual(2, inner.Length);
        Assert.AreEqual(6, watchdog.Failure.BytesTransferred);
    }

    private sealed class RecordingProgress : ITransferProgress
    {
        public string Last { get; private set; } = string.Empty;

        public void ReportTransferStarted() => Last = "started";

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Last = $"downloaded {bytesSoFar} of {expectedTotal}";

        public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Last = $"uploaded {bytesSoFar} of {expectedTotal}";
    }
}
