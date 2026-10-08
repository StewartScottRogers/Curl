using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="TransferTimers" />: the <c>-m</c> and <c>--connect-timeout</c> timers curl 8.21.0's multi
/// waits on for a response, nearest first, at their configured delays (measured, BL-1258 Notes).
/// </summary>
[TestClass]
public sealed class TransferTimersTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void WaitLines_UnderTheMulti_WritesEveryExpiresInNearestFirstThenTheNearestsGives()
    {
        Diagnostics.Arrange("max time / connect timeout / traces expiry", "5 s / 1 s / True");
        TransferTimers timers = new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));

        IReadOnlyList<string> lines = timers.WaitLines(tracesExpiry: true);
        Diagnostics.Act("wait lines", string.Join(" | ", lines));

        Diagnostics.Assert(
            "wait lines",
            "[TIMER] [CONNECTTIMEOUT] expires in 1000000ns | [TIMER] [TIMEOUT] expires in 5000000ns | [TIMER] [CONNECTTIMEOUT] gives multi timeout in 1000ms",
            string.Join(" | ", lines));
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
        Diagnostics.Arrange("max time / connect timeout / traces expiry", "2 s / 2 s / False");
        TransferTimers timers = new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

        IReadOnlyList<string> lines = timers.WaitLines(tracesExpiry: false);
        Diagnostics.Act("wait lines", string.Join(" | ", lines));

        Diagnostics.Assert("wait lines", "[TIMER] [TIMEOUT] gives multi timeout in 2000ms", string.Join(" | ", lines));
        CollectionAssert.AreEqual(new[] { "[TIMER] [TIMEOUT] gives multi timeout in 2000ms" }, lines.ToArray());
    }

    [TestMethod]
    public void WaitLines_NoTimer_AreNone()
    {
        Diagnostics.Arrange("max time / connect timeout", "none / none");
        TransferTimers timers = new(null, null);
        string actual = $"{timers.WaitLines(tracesExpiry: true).Count} / {timers.Count} / {timers.InternalTimeout}";
        Diagnostics.Act("wait lines / count / internal timeout", actual);

        Diagnostics.Assert("wait lines / count / internal timeout", "0 / 0 / -1", actual);
        Assert.IsEmpty(timers.WaitLines(tracesExpiry: true));
        Assert.AreEqual(0, timers.Count);
        Assert.AreEqual("-1", timers.InternalTimeout);
    }

    [TestMethod]
    public void InternalTimeout_APartialMillisecond_IsRoundedUp()
    {
        Diagnostics.Arrange("max time / connect timeout", "none / 100500 microseconds");
        TransferTimers timers = new(null, TimeSpan.FromMicroseconds(100500));
        Diagnostics.Act("internal timeout / count", $"{timers.InternalTimeout} / {timers.Count}");

        Diagnostics.Assert("internal timeout / count", "101 / 1", $"{timers.InternalTimeout} / {timers.Count}");
        Assert.AreEqual("101", timers.InternalTimeout);
        Assert.AreEqual(1, timers.Count);
    }
}
