using System.Globalization;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="CurlDateParser"/> against the local curl 8.21.0, measured on Windows on
/// 2026-09-26 by bracketing the modification time of a <c>file://</c> source: at the expected
/// instant <c>-z</c> reported "not new enough", one second later it transferred. Every refused
/// spelling below made curl print its illegal-date warning.
/// </summary>
[TestClass]
public sealed class CurlDateParserTests
{
    [TestMethod]
    [DataRow("1 Jan 2030", "2030-01-01T00:00:00Z")]
    [DataRow("Sun, 06 Nov 1994 08:49:37 GMT", "1994-11-06T08:49:37Z")]
    [DataRow("Sunday, 06-Nov-94 08:49:37 GMT", "1994-11-06T08:49:37Z")]
    [DataRow("Sun Nov  6 08:49:37 1994", "1994-11-06T08:49:37Z")]
    [DataRow("Jan 1 2030", "2030-01-01T00:00:00Z")]
    [DataRow("2030 Jan 1", "2030-01-01T00:00:00Z")]
    [DataRow("20300101", "2030-01-01T00:00:00Z")]
    [DataRow("20300101 12:00 +0100", "2030-01-01T11:00:00Z")]
    [DataRow("1 Jan 2030 12:00 -0130", "2030-01-01T13:30:00Z")]
    [DataRow("1 Jan 2030 12:00 +1400", "2029-12-31T22:00:00Z")]
    [DataRow("1 Jan 30", "2030-01-01T00:00:00Z")]
    [DataRow("1 Jan 71", "1971-01-01T00:00:00Z")]
    [DataRow("1 Jan 70", "2070-01-01T00:00:00Z")]
    [DataRow("31 Feb 2030", "2030-03-03T00:00:00Z")]
    [DataRow("1 jan 2030 00:00:60", "2030-01-01T00:01:00Z")]
    [DataRow("1 Jan 2030 EST", "2030-01-01T05:00:00Z")]
    [DataRow("1 Jan 2030 Z", "2030-01-01T00:00:00Z")]
    [DataRow("1 Jan 2030 CEST", "2029-12-31T22:00:00Z")]
    [DataRow("1 Jan 2030 10:20:", "2030-01-01T10:20:00Z")]
    [DataRow("1 Jan 2030 1:2:3", "2030-01-01T01:02:03Z")]
    [DataRow("SATURDAY 1 JAN 2030", "2030-01-01T00:00:00Z")]
    [DataRow("jan 1 2030 MON", "2030-01-01T00:00:00Z")]
    [DataRow("Sun, 06 Nov 1994 08:49:37 GMT garbage", "1994-11-06T08:49:37Z")]
    [DataRow("Sun, 06 Nov 1994 08:49:37 GMT 12:00", "1994-11-06T08:49:37Z")]
    public void TryParse_DateCurlAccepts_ReadsTheInstantCurlReads(string text, string expected)
    {
        bool parsed = CurlDateParser.TryParse(text, out DateTimeOffset value);

        Assert.IsTrue(parsed);
        Assert.AreEqual(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), value);
        Assert.AreEqual(TimeSpan.Zero, value.Offset);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("notadate")]
    [DataRow("1 Jan")]
    [DataRow("1 Foo 2030")]
    [DataRow("Mon Tue 1 Jan 2030")]
    [DataRow("1 Jan Feb 2030")]
    [DataRow("1 Jan 2030 GMT UTC")]
    [DataRow("1 Jan 2030 gmt")]
    [DataRow("1 Jan 2030 cest")]
    [DataRow("1 Jan 2030 J")]
    [DataRow("32 Jan 2030")]
    [DataRow("1 Jan 2030 24:00")]
    [DataRow("1 Jan 2030 23:60")]
    [DataRow("1 Jan 2030 23:59:61")]
    [DataRow("1 Jan 2030 10: 20")]
    [DataRow("1 Jan 1999 12:00 13:00")]
    [DataRow("1 Jan 2030 1")]
    [DataRow("20301301")]
    [DataRow("20300001")]
    [DataRow("1 Jan 100000000")]
    [DataRow("99999999999 Jan 1")]
    [DataRow("1 Jan 2030 12:00 +1500")]
    [DataRow("1 Jan 2030 GMT +0100")]
    public void TryParse_TextCurlRefuses_IsRefused(string text)
    {
        bool parsed = CurlDateParser.TryParse(text, out DateTimeOffset value);

        Assert.IsFalse(parsed);
        Assert.AreEqual(default, value);
    }

    /// <summary>
    /// curl 8.21.0 accepts <c>1 Jan 099999999</c>, computing the instant in a 64-bit
    /// <c>time_t</c>; a <see cref="DateTimeOffset"/> cannot hold it, so it reads as the last whole
    /// second one can (ADR-0073).
    /// </summary>
    [TestMethod]
    [DataRow("1 Jan 099999999")]
    [DataRow("31 Dec 9999 23:00 -1400")]
    public void TryParse_InstantAfterYear9999_IsTheLastWholeSecondOfYear9999(string text)
    {
        bool parsed = CurlDateParser.TryParse(text, out DateTimeOffset value);

        Assert.IsTrue(parsed);
        Assert.AreEqual(new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero), value);
    }

    /// <summary>
    /// <c>00000101</c> is 1 January of year 0, before any <see cref="DateTimeOffset"/>; curl 8.21.0
    /// treats it as not a date (measured 2026-09-27), so it is refused.
    /// </summary>
    [TestMethod]
    public void TryParse_InstantBeforeYear1_IsRefused()
    {
        Assert.IsFalse(CurlDateParser.TryParse("00000101", out _));
    }

    /// <summary><c>curl_getdate</c> returns the epoch for the instant one second before it, -1 being its failure value.</summary>
    [TestMethod]
    public void TryParse_OneSecondBeforeTheEpoch_ReadsTheEpoch()
    {
        bool parsed = CurlDateParser.TryParse("31 Dec 1969 23:59:59 GMT", out DateTimeOffset value);

        Assert.IsTrue(parsed);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, value);
    }

    [TestMethod]
    public void TryParse_TwoSecondsBeforeTheEpoch_ReadsThatInstant()
    {
        bool parsed = CurlDateParser.TryParse("31 Dec 1969 23:59:58 GMT", out DateTimeOffset value);

        Assert.IsTrue(parsed);
        Assert.AreEqual(-2, value.ToUnixTimeSeconds());
    }

    [TestMethod]
    public void TryParse_Null_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CurlDateParser.TryParse(null!, out _));

        Assert.AreEqual("text", exception.ParamName);
    }
}
