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
}
