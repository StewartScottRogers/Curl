using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Read_TruncatingToTheMillisecond_EndsTheFractionInThreeZeros()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("truncatesToMillisecond", true);
        diagnostics.Arrange("clock UTC", Clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        string stamp = TraceTimeStamp.Read(Clock, truncatesToMillisecond: true);

        diagnostics.Act("stamp", stamp);
        diagnostics.Diff("stamp", "03:30:30.939000 ", stamp);
        Assert.AreEqual("03:30:30.939000 ", stamp);
    }

    [TestMethod]
    public void Read_NotTruncating_ShowsTheMicroseconds()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("truncatesToMillisecond", false);
        diagnostics.Arrange("clock UTC", Clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        string stamp = TraceTimeStamp.Read(Clock, truncatesToMillisecond: false);

        diagnostics.Act("stamp", stamp);
        diagnostics.Diff("stamp", "03:30:30.939512 ", stamp);
        Assert.AreEqual("03:30:30.939512 ", stamp);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Read_OnWindows_TruncatesToTheMillisecond()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("clock UTC", Clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        string stamp = TraceTimeStamp.Read(Clock);

        diagnostics.Act("stamp", stamp);
        diagnostics.Diff("stamp", "03:30:30.939000 ", stamp);
        Assert.AreEqual("03:30:30.939000 ", stamp);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Read_OffWindows_ShowsTheMicroseconds()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("clock UTC", Clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        string stamp = TraceTimeStamp.Read(Clock);

        diagnostics.Act("stamp", stamp);
        diagnostics.Diff("stamp", "03:30:30.939512 ", stamp);
        Assert.AreEqual("03:30:30.939512 ", stamp);
    }
}
