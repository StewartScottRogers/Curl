using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_MaxTimeNotPositive_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", TimeSpan.Zero);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MaxTimeWatchdog(TimeSpan.Zero, clock));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
        Assert.AreEqual(nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void Constructor_NullClock_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("clock", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new MaxTimeWatchdog(OneSecond, null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
        Assert.AreEqual(nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void Constructor_TakesNowAsOperationStartedAndTimesTheMaxTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("advance", TimeSpan.FromSeconds(3));
        clock.Advance(TimeSpan.FromSeconds(3));

        using MaxTimeWatchdog watchdog = new(OneSecond, clock);

        diagnostics.Act("operation started ticks", watchdog.OperationStarted);
        diagnostics.Assert("operation started ticks", TimeSpan.FromSeconds(3).Ticks, watchdog.OperationStarted);
        Assert.AreEqual(TimeSpan.FromSeconds(3).Ticks, watchdog.OperationStarted);
        diagnostics.Act("due times", string.Join(", ", clock.DueTimes));
        diagnostics.Assert("due times", OneSecond.ToString(), string.Join(", ", clock.DueTimes));
        CollectionAssert.AreEqual(new[] { OneSecond }, clock.DueTimes.ToArray());
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no -m")]
    [DataRow(0, DisplayName = "-m 0")]
    public void StartFromCommandLine_NoLimit_WatchesNothing(int? seconds)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-m seconds", seconds?.ToString() ?? "none");
        TimeSpan? maxTime = seconds is { } given ? TimeSpan.FromSeconds(given) : null;

        var watchdog = MaxTimeWatchdog.StartFromCommandLine(maxTime, clock);

        diagnostics.Act("watchdog is null", watchdog is null);
        diagnostics.Assert("watchdog is null", true, watchdog is null);
        Assert.IsNull(watchdog);
    }

    [TestMethod]
    public void StartFromCommandLine_PositiveLimit_StartsAWatchdog()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", OneSecond);
        using MaxTimeWatchdog? watchdog = MaxTimeWatchdog.StartFromCommandLine(OneSecond, clock);

        diagnostics.Act("watchdog is null", watchdog is null);
        diagnostics.Assert("watchdog is not null", true, watchdog is not null);
        Assert.IsNotNull(watchdog);
        diagnostics.Act("has timed out", watchdog.HasTimedOut);
        diagnostics.Assert("has timed out", false, watchdog.HasTimedOut);
        Assert.IsFalse(watchdog.HasTimedOut);
    }

    [TestMethod]
    public void TimerFired_MaxTimePassed_TimesOutAndCancelsTheToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", OneSecond);
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);

        diagnostics.Arrange("advance", OneSecond);
        clock.Advance(OneSecond);
        clock.Fire();

        diagnostics.Act("has timed out", watchdog.HasTimedOut);
        diagnostics.Act("cancelled", watchdog.Token.IsCancellationRequested);
        diagnostics.Assert("has timed out", true, watchdog.HasTimedOut);
        Assert.IsTrue(watchdog.HasTimedOut);
        diagnostics.Assert("cancelled", true, watchdog.Token.IsCancellationRequested);
        Assert.IsTrue(watchdog.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void TimerFired_BeforeMaxTimePassedOnTheClock_SetsItselfAgainForTheTimeLeft()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", OneSecond);
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);

        diagnostics.Arrange("advance", TimeSpan.FromMilliseconds(990));
        clock.Advance(TimeSpan.FromMilliseconds(990));
        clock.Fire();

        diagnostics.Act("has timed out", watchdog.HasTimedOut);
        diagnostics.Act("cancelled", watchdog.Token.IsCancellationRequested);
        diagnostics.Act("last due time", clock.DueTimes[^1]);
        diagnostics.Assert("has timed out", false, watchdog.HasTimedOut);
        Assert.IsFalse(watchdog.HasTimedOut);
        diagnostics.Assert("cancelled", false, watchdog.Token.IsCancellationRequested);
        Assert.IsFalse(watchdog.Token.IsCancellationRequested);
        diagnostics.Assert("last due time", TimeSpan.FromMilliseconds(10), clock.DueTimes[^1]);
        Assert.AreEqual(TimeSpan.FromMilliseconds(10), clock.DueTimes[^1]);
    }

    [TestMethod]
    public void TimerFired_AgainAfterTimingOut_KeepsTheFirstElapsedTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", OneSecond);
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);
        diagnostics.Arrange("advance", OneSecond);
        clock.Advance(OneSecond);
        clock.Fire();

        diagnostics.Arrange("advance again", OneSecond);
        clock.Advance(OneSecond);
        clock.Fire();

        diagnostics.Act("error message", watchdog.Failure.ErrorMessage);
        diagnostics.Assert("error message", "Connection timed out after 1000 milliseconds", watchdog.Failure.ErrorMessage);
        Assert.AreEqual("Connection timed out after 1000 milliseconds", watchdog.Failure.ErrorMessage);
    }

    [TestMethod]
    public void TimerFired_AfterDispose_DoesNotTimeOut()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", OneSecond);
        MaxTimeWatchdog watchdog = new(OneSecond, clock);
        watchdog.Dispose();

        diagnostics.Arrange("advance", OneSecond);
        clock.Advance(OneSecond);
        clock.Fire();

        diagnostics.Act("has timed out", watchdog.HasTimedOut);
        diagnostics.Act("cancelled", watchdog.Token.IsCancellationRequested);
        diagnostics.Assert("has timed out", false, watchdog.HasTimedOut);
        Assert.IsFalse(watchdog.HasTimedOut);
        diagnostics.Assert("cancelled", false, watchdog.Token.IsCancellationRequested);
        Assert.IsFalse(watchdog.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Failure_BeforeTheTransferStarted_IsTheConnectMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", TimeSpan.FromSeconds(2));
        using MaxTimeWatchdog watchdog = new(TimeSpan.FromSeconds(2), clock);
        diagnostics.Arrange("advance", TimeSpan.FromMilliseconds(2003));
        clock.Advance(TimeSpan.FromMilliseconds(2003));
        clock.Fire();

        TransferResult failure = watchdog.Failure;

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("error message", failure.ErrorMessage);
        diagnostics.Act("bytes transferred", failure.BytesTransferred);
        diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure.ExitCode);
        diagnostics.Assert("error message", "Connection timed out after 2003 milliseconds", failure.ErrorMessage);
        Assert.AreEqual("Connection timed out after 2003 milliseconds", failure.ErrorMessage);
        diagnostics.Assert("bytes transferred", 0, failure.BytesTransferred);
        Assert.AreEqual(0, failure.BytesTransferred);
    }

    [TestMethod]
    public void Failure_AfterBytesOfUnknownSize_IsTheOperationMessageWithTheBytesReceived()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", OneSecond);
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);
        ITransferProgress progress = watchdog.WatchProgress(NoTransferProgress.Instance);
        progress.ReportTransferStarted();
        diagnostics.Arrange("downloaded", "7 of unknown");
        progress.ReportDownloaded(7, null);
        diagnostics.Arrange("advance", OneSecond);
        clock.Advance(OneSecond);
        clock.Fire();

        TransferResult failure = watchdog.Failure;

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("error message", failure.ErrorMessage);
        diagnostics.Act("bytes transferred", failure.BytesTransferred);
        diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure.ExitCode);
        diagnostics.Assert("error message", "Operation timed out after 1000 milliseconds with 7 bytes received", failure.ErrorMessage);
        Assert.AreEqual("Operation timed out after 1000 milliseconds with 7 bytes received", failure.ErrorMessage);
        diagnostics.Assert("bytes transferred", 7, failure.BytesTransferred);
        Assert.AreEqual(7, failure.BytesTransferred);
    }

    [TestMethod]
    public void Failure_AfterBytesOfAKnownSize_SaysOutOfTheExpectedSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", OneSecond);
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);
        ITransferProgress progress = watchdog.WatchProgress(NoTransferProgress.Instance);
        progress.ReportTransferStarted();
        diagnostics.Arrange("downloaded", "5 of 5");
        progress.ReportDownloaded(5, 5);
        diagnostics.Arrange("advance", OneSecond);
        clock.Advance(OneSecond);
        clock.Fire();

        diagnostics.Act("error message", watchdog.Failure.ErrorMessage);
        diagnostics.Assert("error message", "Operation timed out after 1000 milliseconds with 5 out of 5 bytes received", watchdog.Failure.ErrorMessage);
        Assert.AreEqual("Operation timed out after 1000 milliseconds with 5 out of 5 bytes received", watchdog.Failure.ErrorMessage);
    }

    [TestMethod]
    public void WatchProgress_PassesEveryReportOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("max time", OneSecond);
        using MaxTimeWatchdog watchdog = new(OneSecond, clock);
        RecordingProgress inner = new();
        ITransferProgress progress = watchdog.WatchProgress(inner);

        progress.ReportTransferStarted();
        diagnostics.Act("after started", inner.Last);
        diagnostics.Assert("after started", "started", inner.Last);
        Assert.AreEqual("started", inner.Last);
        diagnostics.Arrange("downloaded", "3 of 9");
        progress.ReportDownloaded(3, 9);
        diagnostics.Act("after downloaded", inner.Last);
        diagnostics.Assert("after downloaded", "downloaded 3 of 9", inner.Last);
        Assert.AreEqual("downloaded 3 of 9", inner.Last);
        diagnostics.Arrange("uploaded", "4 of unknown");
        progress.ReportUploaded(4, null);
        diagnostics.Act("after uploaded", inner.Last);
        diagnostics.Assert("after uploaded", "uploaded 4 of ", inner.Last);
        Assert.AreEqual("uploaded 4 of ", inner.Last);
        progress.ReportTransferDone();
        diagnostics.Act("after done", inner.Last);
        diagnostics.Assert("after done", "done", inner.Last);
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
