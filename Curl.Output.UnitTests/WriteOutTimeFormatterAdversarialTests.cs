namespace Curl.Output;

/// <summary>
/// Adversarial black-box tests for <see cref="WriteOutTimeFormatter"/> (BL-1504): the clock at
/// the Unix epoch and at the ends of <see cref="DateTimeOffset"/>'s range, in time zones that
/// push the local time past those ends, and formats that end mid-directive.
/// </summary>
[TestClass]
public sealed class WriteOutTimeFormatterAdversarialTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.CreateCustomTimeZone("Utc0", TimeSpan.Zero, "Utc0", "Utc0");

    private static readonly TimeZoneInfo FourteenHoursAhead =
        TimeZoneInfo.CreateCustomTimeZone("Ahead14", TimeSpan.FromHours(14), "Ahead14", "Ahead14");

    private static readonly TimeZoneInfo TwelveHoursBehind =
        TimeZoneInfo.CreateCustomTimeZone("Behind12", TimeSpan.FromHours(-12), "Behind12", "Behind12");

    [TestMethod]
    [DataRow(WriteOutTimeDialect.Glibc)]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime)]
    public void Format_AtTheUnixEpochInUtc_IsNineteenSeventy(WriteOutTimeDialect dialect)
    {
        FixedTimeProvider clock = new(DateTimeOffset.UnixEpoch, Utc);

        string text = WriteOutTimeFormatter.Format("%Y-%m-%d %H:%M:%S", dialect, clock);

        Assert.AreEqual("1970-01-01 00:00:00", text);
    }

    [TestMethod]
    [DataRow(WriteOutTimeDialect.Glibc)]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime)]
    public void Format_LastTickOfYear9999InUtc_IsYear9999(WriteOutTimeDialect dialect)
    {
        FixedTimeProvider clock = new(DateTimeOffset.MaxValue, Utc);

        string text = WriteOutTimeFormatter.Format("%Y-%m-%d %H:%M:%S", dialect, clock);

        Assert.AreEqual("9999-12-31 23:59:59", text);
    }

    [TestMethod]
    [DataRow(WriteOutTimeDialect.Glibc)]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime)]
    public void Format_LastTickOfYear9999InAZoneAheadOfUtc_ReturnsWithoutThrowing(WriteOutTimeDialect dialect)
    {
        FixedTimeProvider clock = new(DateTimeOffset.MaxValue, FourteenHoursAhead);

        string text = WriteOutTimeFormatter.Format("%Y%m%d%H%M%S%z%Z%c%x%X%j%U%W%a%A%b%B%p%%", dialect, clock);

        Assert.IsNotNull(text);
    }

    [TestMethod]
    [DataRow(WriteOutTimeDialect.Glibc)]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime)]
    public void Format_FirstTickOfYearOneInAZoneBehindUtc_ReturnsWithoutThrowing(WriteOutTimeDialect dialect)
    {
        FixedTimeProvider clock = new(DateTimeOffset.MinValue, TwelveHoursBehind);

        string text = WriteOutTimeFormatter.Format("%Y%m%d%H%M%S%z%Z%c%x%X%j%U%W%a%A%b%B%p%%", dialect, clock);

        Assert.IsNotNull(text);
    }

    [TestMethod]
    [DataRow(WriteOutTimeDialect.Glibc, "")]
    [DataRow(WriteOutTimeDialect.Glibc, "%")]
    [DataRow(WriteOutTimeDialect.Glibc, "%E")]
    [DataRow(WriteOutTimeDialect.Glibc, "%O")]
    [DataRow(WriteOutTimeDialect.Glibc, "%-")]
    [DataRow(WriteOutTimeDialect.Glibc, "%099999999999Y")]
    [DataRow(WriteOutTimeDialect.Glibc, "%\0")]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime, "")]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime, "%")]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime, "%#")]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime, "%Q")]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime, "%\0")]
    public void Format_FormatEndingMidDirectiveOrUnknown_ReturnsWithoutThrowing(WriteOutTimeDialect dialect, string format)
    {
        FixedTimeProvider clock = new(DateTimeOffset.UnixEpoch, Utc);

        string text = WriteOutTimeFormatter.Format(format, dialect, clock);

        Assert.IsNotNull(text);
    }

    [TestMethod]
    [DataRow(WriteOutTimeDialect.Glibc)]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime)]
    public void Format_ResultOf252Bytes_FitsCurlsBuffer(WriteOutTimeDialect dialect)
    {
        // curl -w "[%time{<63 %Y>}]" wrote all 252 digits (curl 8.21.0, Schannel, 2026-10-07).
        FixedTimeProvider clock = new(DateTimeOffset.UnixEpoch, Utc);

        string text = WriteOutTimeFormatter.Format(string.Concat(Enumerable.Repeat("%Y", 63)), dialect, clock);

        Assert.AreEqual(string.Concat(Enumerable.Repeat("1970", 63)), text);
    }

    [TestMethod]
    [DataRow(WriteOutTimeDialect.Glibc, 64)]
    [DataRow(WriteOutTimeDialect.Glibc, 10_000)]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime, 64)]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime, 10_000)]
    public void Format_ResultOf256BytesOrMore_IsEmpty(WriteOutTimeDialect dialect, int directives)
    {
        // curl -w "[%time{<64 %Y>}]" wrote "[]" (curl 8.21.0, Schannel, 2026-10-07).
        FixedTimeProvider clock = new(DateTimeOffset.UnixEpoch, Utc);

        string text = WriteOutTimeFormatter.Format(string.Concat(Enumerable.Repeat("%Y", directives)), dialect, clock);

        Assert.AreEqual(string.Empty, text);
    }

    [TestMethod]
    public void Format_DialectOutsideTheEnum_ThrowsArgumentOutOfRange()
    {
        FixedTimeProvider clock = new(DateTimeOffset.UnixEpoch, Utc);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WriteOutTimeFormatter.Format("%Y", (WriteOutTimeDialect)99, clock));
    }
}
