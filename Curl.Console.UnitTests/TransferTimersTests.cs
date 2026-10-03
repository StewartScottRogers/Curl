namespace Curl.Console;

/// <summary>
/// Pins <see cref="TransferTimers" />: the <c>-m</c> and <c>--connect-timeout</c> timers curl 8.21.0's multi
/// waits on for a response, nearest first, at their configured delays (measured, BL-1258 Notes).
/// </summary>
[TestClass]
public sealed class TransferTimersTests
{
    [TestMethod]
    public void WaitLines_UnderTheMulti_WritesEveryExpiresInNearestFirstThenTheNearestsGives()
    {
        TransferTimers timers = new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));

        IReadOnlyList<string> lines = timers.WaitLines(tracesExpiry: true);

        CollectionAssert.AreEqual(
            new[]
            {
                "[TIMER] [CONNECTTIMEOUT] expires in 1000000ns",
                "[TIMER] [TIMEOUT] expires in 5000000ns",
                "[TIMER] [CONNECTTIMEOUT] gives multi timeout in 1000ms",
            },
            lines.ToArray());
    }

    [TestMethod]
    public void WaitLines_EqualDelays_NameTheMaxTimeFirst()
    {
        TransferTimers timers = new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

        IReadOnlyList<string> lines = timers.WaitLines(tracesExpiry: false);

        CollectionAssert.AreEqual(new[] { "[TIMER] [TIMEOUT] gives multi timeout in 2000ms" }, lines.ToArray());
    }

    [TestMethod]
    public void WaitLines_NoTimer_AreNone()
    {
        TransferTimers timers = new(null, null);

        Assert.IsEmpty(timers.WaitLines(tracesExpiry: true));
        Assert.AreEqual(0, timers.Count);
        Assert.AreEqual("-1", timers.InternalTimeout);
    }

    [TestMethod]
    public void InternalTimeout_APartialMillisecond_IsRoundedUp()
    {
        TransferTimers timers = new(null, TimeSpan.FromMicroseconds(100500));

        Assert.AreEqual("101", timers.InternalTimeout);
        Assert.AreEqual(1, timers.Count);
    }
}
