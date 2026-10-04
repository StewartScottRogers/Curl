namespace Curl.Output;

/// <summary>
/// Pins <see cref="TraceTimeStamp"/> to curl 8.21.0's <c>--trace-time</c> stamps: milliseconds
/// followed by <c>000</c> on Windows (<c>GetSystemTime</c>), full microseconds elsewhere
/// (<c>gettimeofday</c>), as measured on 2026-10-02 (BL-1302).
/// </summary>
[TestClass]
public sealed class TraceTimeStampTests
{
    // 03:30:30.939512 local time in UTC, the clock's local zone.
    private static readonly FixedTimeProvider Clock =
        new(new DateTimeOffset(2026, 9, 27, 3, 30, 30, TimeSpan.Zero).AddTicks(9_395_120), TimeZoneInfo.Utc);

    [TestMethod]
    public void Read_TruncatingToTheMillisecond_EndsTheFractionInThreeZeros()
    {
        Assert.AreEqual("03:30:30.939000 ", TraceTimeStamp.Read(Clock, truncatesToMillisecond: true));
    }

    [TestMethod]
    public void Read_NotTruncating_ShowsTheMicroseconds()
    {
        Assert.AreEqual("03:30:30.939512 ", TraceTimeStamp.Read(Clock, truncatesToMillisecond: false));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Read_OnWindows_TruncatesToTheMillisecond()
    {
        Assert.AreEqual("03:30:30.939000 ", TraceTimeStamp.Read(Clock));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Read_OffWindows_ShowsTheMicroseconds()
    {
        Assert.AreEqual("03:30:30.939512 ", TraceTimeStamp.Read(Clock));
    }
}
