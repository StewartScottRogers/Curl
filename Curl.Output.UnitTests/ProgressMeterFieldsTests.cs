using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins the progress meter's fields against curl 8.21.0's <c>lib/progress.c</c>: <c>max6out</c>,
/// <c>time2str</c>, <c>pgrs_est_percent</c> and <c>trspeed</c> (task BL-131), and against every
/// row of upstream test1636's table (BL-2900).
/// </summary>
[TestClass]
public sealed class ProgressMeterFieldsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    /// <summary>test1636's <c>time2str</c> rows: the field for 2^<c>i</c> - 1 seconds, <c>i</c> from 0 to 62.</summary>
    private static readonly string[] Test1636TimeFields =
    [
        "       ",
        "  00:01",
        "  00:03",
        "  00:07",
        "  00:15",
        "  00:31",
        "  01:03",
        "  02:07",
        "  04:15",
        "  08:31",
        "  17:03",
        "  34:07",
        "1:08:15",
        "2:16:31",
        "4:33:03",
        "9:06:07",
        "18h 12m",
        "36h 24m",
        "72h 49m",
        " 6d 01h",
        "12d 03h",
        "24d 06h",
        "48d 13h",
        "97d 02h",
        "   194d",
        "   388d",
        "   776d",
        "    51m",
        "   103m",
        "   207m",
        "   414m",
        "   828m",
        "   136y",
        "   272y",
        "   544y",
        "  1089y",
        "  2179y",
        "  4358y",
        "  8716y",
        " 17432y",
        " 34865y",
        " 69730y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
        ">99999y",
    ];

    /// <summary>test1636's <c>max6out</c> rows: the field for 2^<c>i</c> - 1 bytes, <c>i</c> from 0 to 62.</summary>
    private static readonly string[] Test1636SizeFields =
    [
        "     0",
        "     1",
        "     3",
        "     7",
        "    15",
        "    31",
        "    63",
        "   127",
        "   255",
        "   511",
        "  1023",
        "  2047",
        "  4095",
        "  8191",
        " 16383",
        " 32767",
        " 65535",
        "127.9k",
        "255.9k",
        "511.9k",
        " 0.99M",
        " 1.99M",
        " 3.99M",
        " 7.99M",
        "15.99M",
        "31.99M",
        "63.99M",
        "127.9M",
        "255.9M",
        "511.9M",
        " 0.99G",
        " 1.99G",
        " 3.99G",
        " 7.99G",
        "15.99G",
        "31.99G",
        "63.99G",
        "127.9G",
        "255.9G",
        "511.9G",
        " 0.99T",
        " 1.99T",
        " 3.99T",
        " 7.99T",
        "15.99T",
        "31.99T",
        "63.99T",
        "127.9T",
        "255.9T",
        "511.9T",
        " 0.99P",
        " 1.99P",
        " 3.99P",
        " 7.99P",
        "15.99P",
        "31.99P",
        "63.99P",
        "127.9P",
        "255.9P",
        "511.9P",
        " 0.99E",
        " 1.99E",
        " 3.99E",
    ];

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

    [TestMethod]
    public void Time_Test1636Table_IsCurlsTime2Str()
    {
        for (int bits = 0; bits < Test1636TimeFields.Length; bits++)
        {
            long seconds = (1L << bits) - 1;
            Diagnostics.Arrange("seconds", seconds);

            string time = ProgressMeterFields.Time(seconds);
            Diagnostics.Act("time field", $"\"{time}\"");

            Diagnostics.Assert("time field", $"\"{Test1636TimeFields[bits]}\"", $"\"{time}\"");
            Assert.AreEqual(Test1636TimeFields[bits], time, $"time2str({seconds})");
        }
    }

    [TestMethod]
    public void Size_Test1636Table_IsCurlsMax6Out()
    {
        for (int bits = 0; bits < Test1636SizeFields.Length; bits++)
        {
            long bytes = (1L << bits) - 1;
            Diagnostics.Arrange("bytes", bytes);

            string size = ProgressMeterFields.Size(bytes);
            Diagnostics.Act("size field", $"\"{size}\"");

            Diagnostics.Assert("size field", $"\"{Test1636SizeFields[bits]}\"", $"\"{size}\"");
            Assert.AreEqual(Test1636SizeFields[bits], size, $"max6out({bytes})");
        }
    }

    [TestMethod]
    [DataRow(131072L, "128.0k")]
    [DataRow(12645826L, "12.05M")]
    [DataRow(1073741824L, " 1.00G")]
    [DataRow(12938588979L, "12.04G")]
    [DataRow(1099445657078333L, "999.9T")]
    public void Size_Test1636ByteCounts_IsCurlsMax6Out(long bytes, string expected)
    {
        Diagnostics.Arrange("bytes", bytes);

        string size = ProgressMeterFields.Size(bytes);
        Diagnostics.Act("size field", $"\"{size}\"");

        Diagnostics.Assert("size field", $"\"{expected}\"", $"\"{size}\"");
        Assert.AreEqual(expected, size);
    }
}
