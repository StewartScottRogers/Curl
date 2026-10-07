using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins how a time in Unix seconds becomes its <see cref="DateTimeOffset" /> view at both
/// ends of <see cref="DateTimeOffset" />'s range (ADR-0410).
/// </summary>
[TestClass]
public sealed class UnixSecondsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ToTimeInRange_ForNull_ReturnsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("seconds", "null");

        DateTimeOffset? result = UnixSeconds.ToTimeInRange(null);

        diagnostics.Act("time", result);
        diagnostics.Assert("time", null, result);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ToTimeInRange_AtBothEndsOfTheRange_ReturnsTheTime()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("min seconds", UnixSeconds.MinDateTimeOffsetSeconds);
        diagnostics.Arrange("max seconds", UnixSeconds.MaxDateTimeOffsetSeconds);

        DateTimeOffset? minTime = UnixSeconds.ToTimeInRange(UnixSeconds.MinDateTimeOffsetSeconds);
        DateTimeOffset? maxTime = UnixSeconds.ToTimeInRange(UnixSeconds.MaxDateTimeOffsetSeconds);

        diagnostics.Act("min time", minTime);
        diagnostics.Act("max time", maxTime);
        diagnostics.Assert("min time", DateTimeOffset.MinValue, minTime);
        diagnostics.Assert("max time", new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero), maxTime);
        Assert.AreEqual(DateTimeOffset.MinValue, minTime);
        Assert.AreEqual(
            new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero),
            maxTime);
    }

    [TestMethod]
    public void ToTimeInRange_JustOutsideEitherEnd_ReturnsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("below min seconds", UnixSeconds.MinDateTimeOffsetSeconds - 1);
        diagnostics.Arrange("above max seconds", UnixSeconds.MaxDateTimeOffsetSeconds + 1);

        DateTimeOffset? below = UnixSeconds.ToTimeInRange(UnixSeconds.MinDateTimeOffsetSeconds - 1);
        DateTimeOffset? above = UnixSeconds.ToTimeInRange(UnixSeconds.MaxDateTimeOffsetSeconds + 1);

        diagnostics.Act("below time", below);
        diagnostics.Act("above time", above);
        diagnostics.Assert("below time", null, below);
        diagnostics.Assert("above time", null, above);
        Assert.IsNull(below);
        Assert.IsNull(above);
    }

    [TestMethod]
    public void ToTimeClamped_InRange_ReturnsTheTime()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("seconds", 0);

        DateTimeOffset result = UnixSeconds.ToTimeClamped(0);

        diagnostics.Act("time", result);
        diagnostics.Assert("time", DateTimeOffset.UnixEpoch, result);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, result);
    }

    [TestMethod]
    public void ToTimeClamped_PastEitherEnd_ReturnsThatEnd()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("above max seconds", UnixSeconds.MaxDateTimeOffsetSeconds + 1);
        diagnostics.Arrange("below min seconds", UnixSeconds.MinDateTimeOffsetSeconds - 1);

        DateTimeOffset above = UnixSeconds.ToTimeClamped(UnixSeconds.MaxDateTimeOffsetSeconds + 1);
        DateTimeOffset below = UnixSeconds.ToTimeClamped(UnixSeconds.MinDateTimeOffsetSeconds - 1);

        diagnostics.Act("above time", above);
        diagnostics.Act("below time", below);
        diagnostics.Assert("above time", DateTimeOffset.MaxValue, above);
        diagnostics.Assert("below time", DateTimeOffset.MinValue, below);
        Assert.AreEqual(DateTimeOffset.MaxValue, above);
        Assert.AreEqual(DateTimeOffset.MinValue, below);
    }
}
