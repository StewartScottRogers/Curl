using Curl.Testing;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// Pins <see cref="FakeTimeProvider" />, the clock that moves only when a test moves it.
/// </summary>
[TestClass]
public sealed class FakeTimeProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Advance_ByThreeSeconds_MovesUtcNowAndTimestampTogether()
    {
        DateTimeOffset start = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider time = new(start);
        long before = time.GetTimestamp();
        Diagnostics.Arrange("start", start.ToString("O"));
        Diagnostics.Arrange("advance by", TimeSpan.FromSeconds(3));

        time.Advance(TimeSpan.FromSeconds(3));

        Diagnostics.Act("UTC now", time.GetUtcNow().ToString("O"));
        Diagnostics.Act("elapsed since the first timestamp", time.GetElapsedTime(before));
        Diagnostics.Assert("elapsed since the first timestamp", TimeSpan.FromSeconds(3), time.GetElapsedTime(before));
        Assert.AreEqual(start.AddSeconds(3), time.GetUtcNow());
        Assert.AreEqual(TimeSpan.FromSeconds(3), time.GetElapsedTime(before));
    }

    [TestMethod]
    public void Advance_NegativeDuration_Throws()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        Diagnostics.Arrange("advance by", TimeSpan.FromTicks(-1));

        ArgumentOutOfRangeException thrown = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => time.Advance(TimeSpan.FromTicks(-1)));

        Diagnostics.Act("exception", thrown.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), thrown.GetType().Name);
    }

    [TestMethod]
    public void TimerCreatedAsync_ForTheSecondTimer_CompletesOnlyOnceTwoAreCreated()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        TimeSpan second = TimeSpan.FromSeconds(1);
        using ITimer first = time.CreateTimer(_ => { }, null, second, Timeout.InfiniteTimeSpan);
        using ITimer other = time.CreateTimer(_ => { }, null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
        Diagnostics.Arrange("timers created before waiting", "one of 1 s, one of 2 s");

        Task secondCreated = time.TimerCreatedAsync(second, 2);
        bool completedEarly = secondCreated.IsCompleted;
        using ITimer again = time.CreateTimer(_ => { }, null, second, Timeout.InfiniteTimeSpan);

        Diagnostics.Act("completed before the second 1 s timer", completedEarly);
        Diagnostics.Act("completed after the second 1 s timer", secondCreated.IsCompleted);
        Diagnostics.Assert("completed before the second 1 s timer", false, completedEarly);
        Assert.IsFalse(completedEarly);
        Assert.IsTrue(secondCreated.IsCompleted);
        Assert.IsTrue(time.TimerCreatedAsync(second, 2).IsCompleted);
        Assert.IsTrue(time.TimerCreatedAsync(second).IsCompleted);
    }
}
