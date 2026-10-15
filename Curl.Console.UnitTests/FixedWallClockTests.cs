namespace Curl.Console;

/// <summary>Pins <see cref="FixedWallClock" />: a wall clock that stands still (ADR-0493, BL-2234).</summary>
[TestClass]
public sealed class FixedWallClockTests
{
    [TestMethod]
    public void GetUtcNow_AfterTimePasses_ReturnsTheFixedInstant()
    {
        DateTimeOffset instant = DateTimeOffset.FromUnixTimeSeconds(1728465947);
        FixedWallClock clock = new(instant);

        DateTimeOffset first = clock.GetUtcNow();
        DateTimeOffset second = clock.GetUtcNow();

        Assert.AreEqual(instant, first);
        Assert.AreEqual(instant, second);
    }
}
