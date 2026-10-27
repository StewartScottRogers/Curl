using Curl.Core.Fakes;

namespace Curl.Core;

/// <summary>
/// Pins CGR-039 (BL-3568) in <see cref="MaxTimeWatchdog" />: a <c>-m</c> past the longest wait a .NET
/// timer takes, which curl's 64-bit <c>long</c> accepts on Linux and macOS, arms the timer for that
/// longest wait instead of failing, and each early firing sets it again for the time left.
/// </summary>
[TestClass]
public sealed class Cgr039Tests
{
    private static readonly TimeSpan SixtyDays = TimeSpan.FromDays(60);

    [TestMethod]
    public void Constructor_MaxTimePastTheLongestTimerWaitOnTheSystemClock_DoesNotThrow()
    {
        using var watchdog = new MaxTimeWatchdog(TimeSpan.FromSeconds(4294968), TimeProvider.System);

        Assert.IsFalse(watchdog.HasTimedOut);
    }

    [TestMethod]
    public void Constructor_MaxTimePastTheLongestTimerWait_ArmsTheTimerForTheLongestWait()
    {
        var clock = new HandFiredTimeProvider();

        using var watchdog = new MaxTimeWatchdog(SixtyDays, clock);

        Assert.AreEqual(TimeSpan.FromMilliseconds(4294967294), clock.DueTimes[0]);
    }

    [TestMethod]
    public void TimerFiredEarly_TimeLeftPastTheLongestTimerWait_SetsTheTimerForTheLongestWait()
    {
        var clock = new HandFiredTimeProvider();
        using var watchdog = new MaxTimeWatchdog(SixtyDays, clock);
        clock.Advance(TimeSpan.FromDays(1));

        clock.Fire();

        Assert.AreEqual(MaxTimeWatchdog.LongestTimerWait, clock.DueTimes[^1]);
        Assert.IsFalse(watchdog.HasTimedOut);
    }

    [TestMethod]
    public void TimerFiredEarly_TimeLeftWithinTheLongestTimerWait_SetsTheTimerForTheTimeLeft()
    {
        var clock = new HandFiredTimeProvider();
        using var watchdog = new MaxTimeWatchdog(SixtyDays, clock);
        clock.Advance(TimeSpan.FromDays(50));

        clock.Fire();

        Assert.AreEqual(TimeSpan.FromDays(10), clock.DueTimes[^1]);
    }

    [TestMethod]
    public void TimerFired_MaxTimePastTheLongestTimerWaitPassed_TimesOut()
    {
        var clock = new HandFiredTimeProvider();
        using var watchdog = new MaxTimeWatchdog(SixtyDays, clock);
        clock.Advance(SixtyDays);

        clock.Fire();

        Assert.IsTrue(watchdog.HasTimedOut);
    }
}
