namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins how a time in Unix seconds becomes its <see cref="DateTimeOffset" /> view at both
/// ends of <see cref="DateTimeOffset" />'s range (ADR-0410).
/// </summary>
[TestClass]
public sealed class UnixSecondsTests
{
    [TestMethod]
    public void ToTimeInRange_ForNull_ReturnsNull()
    {
        Assert.IsNull(UnixSeconds.ToTimeInRange(null));
    }

    [TestMethod]
    public void ToTimeInRange_AtBothEndsOfTheRange_ReturnsTheTime()
    {
        Assert.AreEqual(DateTimeOffset.MinValue, UnixSeconds.ToTimeInRange(UnixSeconds.MinDateTimeOffsetSeconds));
        Assert.AreEqual(
            new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero),
            UnixSeconds.ToTimeInRange(UnixSeconds.MaxDateTimeOffsetSeconds));
    }

    [TestMethod]
    public void ToTimeInRange_JustOutsideEitherEnd_ReturnsNull()
    {
        Assert.IsNull(UnixSeconds.ToTimeInRange(UnixSeconds.MinDateTimeOffsetSeconds - 1));
        Assert.IsNull(UnixSeconds.ToTimeInRange(UnixSeconds.MaxDateTimeOffsetSeconds + 1));
    }

    [TestMethod]
    public void ToTimeClamped_InRange_ReturnsTheTime()
    {
        Assert.AreEqual(DateTimeOffset.UnixEpoch, UnixSeconds.ToTimeClamped(0));
    }

    [TestMethod]
    public void ToTimeClamped_PastEitherEnd_ReturnsThatEnd()
    {
        Assert.AreEqual(DateTimeOffset.MaxValue, UnixSeconds.ToTimeClamped(UnixSeconds.MaxDateTimeOffsetSeconds + 1));
        Assert.AreEqual(DateTimeOffset.MinValue, UnixSeconds.ToTimeClamped(UnixSeconds.MinDateTimeOffsetSeconds - 1));
    }
}
