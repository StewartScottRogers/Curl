namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="WaitSkippingTimeProvider"/>: a clock that stands still until a timer is made,
/// then moves on by the timer's due time and fires it at once.
/// </summary>
[TestClass]
public sealed class WaitSkippingTimeProviderTests
{
    [TestMethod]
    public void GetElapsedTime_WithNoTimer_StandsStill()
    {
        WaitSkippingTimeProvider clock = new();
        long startedAt = clock.GetTimestamp();

        Assert.AreEqual(TimeSpan.Zero, clock.GetElapsedTime(startedAt));
    }

    [TestMethod]
    public async Task Delay_OfAnHour_EndsAtOnceWithTheClockAnHourOn()
    {
        WaitSkippingTimeProvider clock = new();
        long startedAt = clock.GetTimestamp();

        await Task.Delay(TimeSpan.FromHours(1), clock).WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromMinutes(1), clock).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(TimeSpan.FromMinutes(61), clock.GetElapsedTime(startedAt));
    }

    [TestMethod]
    public async Task CreateTimer_FiresTheCallbackWithItsState()
    {
        WaitSkippingTimeProvider clock = new();
        TaskCompletionSource<object?> fired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        object state = new();

        using ITimer timer = clock.CreateTimer(firedState => fired.SetResult(firedState), state, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);

        Assert.AreSame(state, await fired.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [TestMethod]
    public async Task CreateTimer_DueNever_NeverFiresAndLeavesTheClock()
    {
        WaitSkippingTimeProvider clock = new();
        long startedAt = clock.GetTimestamp();
        bool fired = false;

        await using ITimer timer = clock.CreateTimer(_ => fired = true, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        Assert.IsFalse(fired);
        Assert.AreEqual(TimeSpan.Zero, clock.GetElapsedTime(startedAt));
    }

    [TestMethod]
    public void Change_IsRefused()
    {
        using ITimer timer = new WaitSkippingTimeProvider().CreateTimer(_ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        Assert.IsFalse(timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan));
    }

    [TestMethod]
    public void CreateTimer_ThatRepeats_Throws()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => new WaitSkippingTimeProvider().CreateTimer(_ => { }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1)));
    }

    [TestMethod]
    public void CreateTimer_NullCallback_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WaitSkippingTimeProvider().CreateTimer(null!, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan));
    }

    [TestMethod]
    public void TimestampFrequency_IsTicksPerSecond()
    {
        Assert.AreEqual(TimeSpan.TicksPerSecond, new WaitSkippingTimeProvider().TimestampFrequency);
    }
}
