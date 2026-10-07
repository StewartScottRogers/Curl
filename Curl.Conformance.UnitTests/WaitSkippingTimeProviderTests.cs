using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="WaitSkippingTimeProvider"/>: a clock that stands still until a timer is made,
/// then moves on by the timer's due time and fires it at once.
/// </summary>
[TestClass]
public sealed class WaitSkippingTimeProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void GetElapsedTime_WithNoTimer_StandsStill()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timers", "none");
        WaitSkippingTimeProvider clock = new();
        long startedAt = clock.GetTimestamp();

        TimeSpan elapsed = clock.GetElapsedTime(startedAt);

        diagnostics.Act("elapsed", elapsed);
        diagnostics.Assert("elapsed", TimeSpan.Zero, elapsed);
        Assert.AreEqual(TimeSpan.Zero, clock.GetElapsedTime(startedAt));
    }

    [TestMethod]
    public async Task Delay_OfAnHour_EndsAtOnceWithTheClockAnHourOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("delays", "1 hour, then 1 minute");
        WaitSkippingTimeProvider clock = new();
        long startedAt = clock.GetTimestamp();

        await Task.Delay(TimeSpan.FromHours(1), clock).WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromMinutes(1), clock).WaitAsync(TimeSpan.FromSeconds(10));

        TimeSpan elapsed = clock.GetElapsedTime(startedAt);
        diagnostics.Act("elapsed", elapsed);
        diagnostics.Assert("elapsed", TimeSpan.FromMinutes(61), elapsed);
        Assert.AreEqual(TimeSpan.FromMinutes(61), clock.GetElapsedTime(startedAt));
    }

    [TestMethod]
    public async Task CreateTimer_FiresTheCallbackWithItsState()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timer", "due in 5 seconds, no period");
        WaitSkippingTimeProvider clock = new();
        TaskCompletionSource<object?> fired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        object state = new();

        using ITimer timer = clock.CreateTimer(firedState => fired.SetResult(firedState), state, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);

        object? receivedState = await fired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        diagnostics.Act("callback received the state", ReferenceEquals(state, receivedState));
        diagnostics.Assert("callback received the state", true, ReferenceEquals(state, receivedState));
        Assert.AreSame(state, receivedState);
    }

    [TestMethod]
    public async Task CreateTimer_DueNever_NeverFiresAndLeavesTheClock()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timer", "due never, no period");
        WaitSkippingTimeProvider clock = new();
        long startedAt = clock.GetTimestamp();
        bool fired = false;

        await using ITimer timer = clock.CreateTimer(_ => fired = true, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        TimeSpan elapsed = clock.GetElapsedTime(startedAt);
        diagnostics.Act("fired", fired);
        diagnostics.Act("elapsed", elapsed);
        diagnostics.Assert("fired", false, fired);
        diagnostics.Assert("elapsed", TimeSpan.Zero, elapsed);
        Assert.IsFalse(fired);
        Assert.AreEqual(TimeSpan.Zero, clock.GetElapsedTime(startedAt));
    }

    [TestMethod]
    public void Change_IsRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("change", "due zero, no period");
        using ITimer timer = new WaitSkippingTimeProvider().CreateTimer(_ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        bool changed = timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);

        diagnostics.Act("changed", changed);
        diagnostics.Assert("changed", false, changed);
        Assert.IsFalse(changed);
    }

    [TestMethod]
    public void CreateTimer_ThatRepeats_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timer", "due zero, period 1 second");

        var exception = Assert.ThrowsExactly<NotSupportedException>(() => new WaitSkippingTimeProvider().CreateTimer(_ => { }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1)));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", nameof(NotSupportedException), exception.GetType().Name);
    }

    [TestMethod]
    public void CreateTimer_NullCallback_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("callback", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new WaitSkippingTimeProvider().CreateTimer(null!, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void TimestampFrequency_IsTicksPerSecond()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected frequency", TimeSpan.TicksPerSecond);

        long frequency = new WaitSkippingTimeProvider().TimestampFrequency;

        diagnostics.Act("frequency", frequency);
        diagnostics.Assert("frequency", TimeSpan.TicksPerSecond, frequency);
        Assert.AreEqual(TimeSpan.TicksPerSecond, new WaitSkippingTimeProvider().TimestampFrequency);
    }
}
