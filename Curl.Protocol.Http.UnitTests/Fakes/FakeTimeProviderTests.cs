namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// Pins <see cref="FakeTimeProvider" />, the clock that moves only when a test moves it.
/// </summary>
[TestClass]
public sealed class FakeTimeProviderTests
{
    [TestMethod]
    public void Advance_ByThreeSeconds_MovesUtcNowAndTimestampTogether()
    {
        DateTimeOffset start = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider time = new(start);
        long before = time.GetTimestamp();

        time.Advance(TimeSpan.FromSeconds(3));

        Assert.AreEqual(start.AddSeconds(3), time.GetUtcNow());
        Assert.AreEqual(TimeSpan.FromSeconds(3), time.GetElapsedTime(before));
    }

    [TestMethod]
    public void Advance_NegativeDuration_Throws()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => time.Advance(TimeSpan.FromTicks(-1)));
    }

    [TestMethod]
    public void TimerCreatedAsync_ForTheSecondTimer_CompletesOnlyOnceTwoAreCreated()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        TimeSpan second = TimeSpan.FromSeconds(1);
        using ITimer first = time.CreateTimer(_ => { }, null, second, Timeout.InfiniteTimeSpan);
        using ITimer other = time.CreateTimer(_ => { }, null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);

        Task secondCreated = time.TimerCreatedAsync(second, 2);
        bool completedEarly = secondCreated.IsCompleted;
        using ITimer again = time.CreateTimer(_ => { }, null, second, Timeout.InfiniteTimeSpan);

        Assert.IsFalse(completedEarly);
        Assert.IsTrue(secondCreated.IsCompleted);
        Assert.IsTrue(time.TimerCreatedAsync(second, 2).IsCompleted);
        Assert.IsTrue(time.TimerCreatedAsync(second).IsCompleted);
    }
}
