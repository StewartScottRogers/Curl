using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the progress meter's fields against curl 8.21.0's <c>lib/progress.c</c>: <c>max6out</c>,
/// <c>time2str</c>, <c>pgrs_est_percent</c> and <c>trspeed</c> (task BL-131).
/// </summary>
[TestClass]
public sealed class ProgressMeterFieldsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(0L, "     0")]
    [DataRow(10L, "    10")]
    [DataRow(99999L, " 99999")]
    [DataRow(100000L, "97.65k")]
    [DataRow(199999L, "195.3k")]
    [DataRow(48780288L, "46.52M")]
    [DataRow(1023999L, "999.9k")]
    [DataRow(1024000L, " 0.97M")]
    [DataRow(long.MaxValue, " 7.99E")]
    public void Size_Bytes_IsCurlsMax6Out(long bytes, string expected)
    {
        Diagnostics.Arrange("bytes", bytes);

        string size = ProgressMeterFields.Size(bytes);
        Diagnostics.Act("size field", $"\"{size}\"");

        Diagnostics.Assert("size field", $"\"{expected}\"", $"\"{size}\"");
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [DataRow(0L, "       ")]
    [DataRow(-5L, "       ")]
    [DataRow(59L, "  00:59")]
    [DataRow(3599L, "  59:59")]
    [DataRow(3661L, "1:01:01")]
    [DataRow(36000L + 120L, "10h 02m")]
    [DataRow(99L * 3600L + 3599L, "99h 59m")]
    [DataRow(100L * 3600L, " 4d 04h")]
    [DataRow(100L * 86400L, "   100d")]
    [DataRow(999L * 86400L, "   999d")]
    [DataRow(1000L * 86400L, "    33m")]
    [DataRow(30000L * 86400L, "    82y")]
    [DataRow(29999L * 86400L, "   999m")]
    [DataRow(99999L * 365L * 86400L, " 99999y")]
    [DataRow(100000L * 365L * 86400L, ">99999y")]
    public void Time_Seconds_IsCurlsTime2Str(long seconds, string expected)
    {
        Diagnostics.Arrange("seconds", seconds);

        string time = ProgressMeterFields.Time(seconds);
        Diagnostics.Act("time field", $"\"{time}\"");

        Diagnostics.Assert("time field", $"\"{expected}\"", $"\"{time}\"");
        Assert.AreEqual(expected, time);
    }

    [TestMethod]
    [DataRow(0L, 0L, 0L)]
    [DataRow(10L, 10L, 100L)]
    [DataRow(10L, 3L, 30L)]
    [DataRow(10000L, 5000L, 50L)]
    [DataRow(20000L, 19999L, 99L)]
    [DataRow(10001L, 10001L, 100L)]
    public void Percent_TotalAndCurrent_IsCurlsPgrsEstPercent(long total, long current, long expected)
    {
        Diagnostics.Arrange("total", total);
        Diagnostics.Arrange("current", current);

        long percent = ProgressMeterFields.Percent(total, current);
        Diagnostics.Act("percent", percent);

        Diagnostics.Assert("percent", expected, percent);
        Assert.AreEqual(expected, percent);
    }

    [TestMethod]
    [DataRow(10L, 0L, 10000000L)]
    [DataRow(10L, 40000L, 250L)]
    [DataRow(long.MaxValue / 1000000, 2000000L, long.MaxValue / 1000000 / 2)]
    [DataRow(long.MaxValue / 1000000, 999999L, long.MaxValue)]
    public void Speed_SizeAndMicroseconds_IsCurlsTrspeed(long size, long microseconds, long expected)
    {
        Diagnostics.Arrange("size", size);
        Diagnostics.Arrange("microseconds", microseconds);

        long speed = ProgressMeterFields.Speed(size, microseconds);
        Diagnostics.Act("speed", speed);

        Diagnostics.Assert("speed", expected, speed);
        Assert.AreEqual(expected, speed);
    }
}
