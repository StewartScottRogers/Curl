using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins <see cref="MaxTimeWatchdog" /> ending an attempt once <c>-m</c> has passed on
/// <see cref="HandFiredTimeProvider" />, with the messages curl 8.21.0 prints (measured
/// 2026-09-28, BL-511 Notes, and for the connect phase ADR-0117).
/// </summary>
[TestClass]
public sealed class MaxTimeWatchdogTests
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private readonly HandFiredTimeProvider clock = new();

    [TestMethod]
    public void Constructor_MaxTimeNotPositive_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MaxTimeWatchdog(TimeSpan.Zero, clock));
    }

    [TestMethod]
    public void Constructor_NullClock_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MaxTimeWatchdog(OneSecond, null!));
    }

    [TestMethod]
    public void Constructor_TakesNowAsOperationStartedAndTimesTheMaxTime()
    {
        clock.Advance(TimeSpan.FromSeconds(3));

        using MaxTimeWatchdog watchdog = new(OneSecond, clock);

        Assert.AreEqual(TimeSpan.FromSeconds(3).Ticks, watchdog.OperationStarted);
        CollectionAssert.AreEqual(new[] { OneSecond }, clock.DueTimes.ToArray());
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no -m")]
    [DataRow(0, DisplayName = "-m 0")]
    public void StartFromCommandLine_NoLimit_WatchesNothing(int? seconds)
    {
        TimeSpan? maxTime = seconds is { } given ? TimeSpan.FromSeconds(given) : null;

        Assert.IsNull(MaxTimeWatchdog.StartFromCommandLine(maxTime, clock));
    }

    [TestMethod]
    public void StartFromCommandLine_PositiveLimit_StartsAWatchdog()
    {
        using MaxTimeWatchdog? watchdog = MaxTimeWatchdog.StartFromCommandLine(OneSecond, clock);

        Assert.IsNotNull(watchdog);
        Assert.IsFalse(watchdog.HasTimedOut);
    }

    [TestMethod]
    public void TimerFired_MaxTimePassed_TimesOutAndCancelsTheToken()
    {
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);

        clock.Advance(OneSecond);
        clock.Fire();

        Assert.IsTrue(watchdog.HasTimedOut);
        Assert.IsTrue(watchdog.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void TimerFired_BeforeMaxTimePassedOnTheClock_SetsItselfAgainForTheTimeLeft()
    {
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);

        clock.Advance(TimeSpan.FromMilliseconds(990));
        clock.Fire();

        Assert.IsFalse(watchdog.HasTimedOut);
        Assert.IsFalse(watchdog.Token.IsCancellationRequested);
        Assert.AreEqual(TimeSpan.FromMilliseconds(10), clock.DueTimes[^1]);
    }

    [TestMethod]
    public void TimerFired_AgainAfterTimingOut_KeepsTheFirstElapsedTime()
    {
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);
        clock.Advance(OneSecond);
        clock.Fire();

        clock.Advance(OneSecond);
        clock.Fire();

        Assert.AreEqual("Connection timed out after 1000 milliseconds", watchdog.Failure.ErrorMessage);
    }

    [TestMethod]
    public void TimerFired_AfterDispose_DoesNotTimeOut()
    {
        MaxTimeWatchdog watchdog = new(OneSecond, clock);
        watchdog.Dispose();

        clock.Advance(OneSecond);
        clock.Fire();

        Assert.IsFalse(watchdog.HasTimedOut);
        Assert.IsFalse(watchdog.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Failure_BeforeTheTransferStarted_IsTheConnectMessage()
    {
        using MaxTimeWatchdog watchdog = new(TimeSpan.FromSeconds(2), clock);
        clock.Advance(TimeSpan.FromMilliseconds(2003));
        clock.Fire();

        TransferResult failure = watchdog.Failure;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure.ExitCode);
        Assert.AreEqual("Connection timed out after 2003 milliseconds", failure.ErrorMessage);
        Assert.AreEqual(0, failure.BytesTransferred);
    }

    [TestMethod]
    public void Failure_AfterBytesOfUnknownSize_IsTheOperationMessageWithTheBytesReceived()
    {
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);
        ITransferProgress progress = watchdog.WatchProgress(NoTransferProgress.Instance);
        progress.ReportTransferStarted();
        progress.ReportDownloaded(7, null);
        clock.Advance(OneSecond);
        clock.Fire();

        TransferResult failure = watchdog.Failure;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure.ExitCode);
        Assert.AreEqual("Operation timed out after 1000 milliseconds with 7 bytes received", failure.ErrorMessage);
        Assert.AreEqual(7, failure.BytesTransferred);
    }

    [TestMethod]
    public void Failure_AfterBytesOfAKnownSize_SaysOutOfTheExpectedSize()
    {
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);
        ITransferProgress progress = watchdog.WatchProgress(NoTransferProgress.Instance);
        progress.ReportTransferStarted();
        progress.ReportDownloaded(5, 5);
        clock.Advance(OneSecond);
        clock.Fire();

        Assert.AreEqual("Operation timed out after 1000 milliseconds with 5 out of 5 bytes received", watchdog.Failure.ErrorMessage);
    }

    [TestMethod]
    public void WatchProgress_PassesEveryReportOn()
    {
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);
        RecordingProgress inner = new();
        ITransferProgress progress = watchdog.WatchProgress(inner);

        progress.ReportTransferStarted();
        Assert.AreEqual("started", inner.Last);
        progress.ReportDownloaded(3, 9);
        Assert.AreEqual("downloaded 3 of 9", inner.Last);
        progress.ReportUploaded(4, null);
        Assert.AreEqual("uploaded 4 of ", inner.Last);
        progress.ReportTransferDone();
        Assert.AreEqual("done", inner.Last);
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
