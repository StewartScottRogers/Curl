using System.Text;
using System.Text.Json;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="WriteOutTimeFormatter"/>'s <see cref="WriteOutTimeDialect.Glibc"/> dialect to
/// what curl 8.21.0 built on glibc 2.41 wrote for <c>-w "%time{…}"</c> on 2026-09-27. Every
/// format in <c>Fixtures/glibc-time-format.json</c> was run through that curl at three fixed
/// instants; the commands are in BL-290's Notes.
/// </summary>
[TestClass]
public sealed class GlibcTimeFormatTests
{
    private const string FixtureName = "glibc-time-format.json";

    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [TestMethod]
    public void Format_EveryMeasuredFormat_MatchesCurl()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", FixtureName), Encoding.UTF8));
        long[] instants = [.. fixture.RootElement.GetProperty("instants").EnumerateArray().Select(instant => instant.GetInt64())];
        List<string> mismatches = [];
        int compared = 0;
        foreach (JsonElement row in fixture.RootElement.GetProperty("rows").EnumerateArray())
        {
            string format = row[0].GetString()!;
            for (int index = 0; index < instants.Length; index++)
            {
                string expected = row[index + 1].GetString()!;
                string actual = WriteOutTimeFormatter.Format(format, WriteOutTimeDialect.Glibc, AtCurlTime(instants[index], Utc));
                compared++;
                if (actual != expected)
                {
                    mismatches.Add($"{JsonSerializer.Serialize(format)} at {instants[index]}: expected {JsonSerializer.Serialize(expected)}, got {JsonSerializer.Serialize(actual)}");
                }
            }
        }

        Assert.IsGreaterThan(4000, compared);
        Assert.AreEqual(0, mismatches.Count, string.Join(Environment.NewLine, mismatches.Take(40)));
    }

    [TestMethod]
    [DataRow("%Y-%m-%dT%H:%M:%S.%f%z %Z", "2026-09-27T03:33:20.480000+0000 UTC")]
    [DataRow("%c|%x|%X|%r", "Sun Sep 27 03:33:20 2026|09/27/26|03:33:20|03:33:20 AM")]
    [DataRow("%C %D %e %F %g %G %h %k %l %P %R %T %u %V", "20 09/27/26 27 2026-09-27 26 2026 Sep  3  3 am 03:33 03:33:20 7 39")]
    [DataRow("%-d|%_H|%010Y|%^a|%#b|%#p|%#Z|%5z", "27| 3|0000002026|SUN|SEP|am|gmt|    +00000")]
    [DataRow("%Ey|%Od|%Ed|%q|%%f|%5", "26|27|%Ed|%q|%f|   %5")]
    [DataRow("%s|%-s", "1790480000|1790480000")]
    public void Format_MeasuredConversions_MatchCurl(string format, string expected)
    {
        Assert.AreEqual(expected, WriteOutTimeFormatter.Format(format, WriteOutTimeDialect.Glibc, AtCurlTime(1790480000, Utc)));
    }

    [TestMethod]
    [DataRow(9, "1790480000|1790447600")]
    [DataRow(-5, "1790480000|1790498000")]
    public void Format_SecondsCurlDoesNotRewrite_ReadTheUtcTimeAsLocalStandardTime(int standardOffsetHours, string expected)
    {
        // TZ=JST-9 and TZ=EST5EDT,M3.2.0,M11.1.0 (in daylight saving time at this instant) printed these.
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(standardOffsetHours), "Test", "Test");

        Assert.AreEqual(expected, WriteOutTimeFormatter.Format("%s|%-s", WriteOutTimeDialect.Glibc, AtCurlTime(1790480000, zone)));
    }

    [TestMethod]
    public void Format_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTimeFormatter.Format(null!, WriteOutTimeDialect.Glibc, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTimeFormatter.Format("%Y", WriteOutTimeDialect.Glibc, null!));
    }

    /// <summary>
    /// The clock a debug curl reads under <c>CURL_TIME=<paramref name="seconds"/></c>: those
    /// Unix seconds, and <paramref name="seconds"/> modulo a million as the microseconds.
    /// </summary>
    private static FixedTimeProvider AtCurlTime(long seconds, TimeZoneInfo localTimeZone)
    {
        DateTimeOffset instant = DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(seconds % 1_000_000 * 10);
        return new FixedTimeProvider(instant, localTimeZone);
    }
}
