using System.Globalization;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="CurlDateParser"/>. The accepted and refused spellings were measured on curl 8.21.0
/// (Windows) on 2026-09-26 through <c>-z</c> by bracketing the modification time of a <c>file://</c>
/// source: at the expected instant <c>-z</c> reported "not new enough", one second later it
/// transferred, and every refused spelling printed the illegal-date warning. The year limits were
/// measured through <c>Set-Cookie: n=v; Expires=&lt;date&gt;</c> and the <c>-c</c> jar (see
/// <c>Curl.Cookies</c>' <c>SetCookieParserTests</c>): 1582, <c>0 Jan 2030</c> and <c>20300101 5</c>
/// are refused and the cookie stays a session cookie, 1583 is read and the cookie is already expired,
/// and year 10000 is read and capped; and through <c>-z</c> on 2026-09-27: <c>1 Jan 1500</c> and
/// <c>00000101</c> print the illegal-date warning, <c>1 Jan 1583</c> does not.
/// </summary>
[TestClass]
public sealed class CurlDateParserTests
{
    public TestContext TestContext { get; set; } = null!;

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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", text);
        diagnostics.Arrange("expected instant", expected);

        bool parsed = CurlDateParser.TryParse(text, out long unixSeconds);

        long expectedSeconds = DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture).ToUnixTimeSeconds();
        diagnostics.Act("parsed", parsed);
        diagnostics.Act("unix seconds", unixSeconds);
        diagnostics.Assert("parsed", true, parsed);
        diagnostics.Assert("unix seconds", expectedSeconds, unixSeconds);
        Assert.IsTrue(parsed);
        Assert.AreEqual(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture).ToUnixTimeSeconds(), unixSeconds);
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
    [DataRow("1 Jan 1582")]
    [DataRow("1 Jan 1500")]
    [DataRow("31 Dec 1582 23:59:59 GMT")]
    [DataRow("00000101")]
    [DataRow("0 Jan 2030")]
    [DataRow("20300101 5")]
    public void TryParse_TextCurlRefuses_IsRefused(string text)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", text);

        bool parsed = CurlDateParser.TryParse(text, out long unixSeconds);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("unix seconds", unixSeconds);
        diagnostics.Assert("parsed", false, parsed);
        diagnostics.Assert("unix seconds", 0L, unixSeconds);
        Assert.IsFalse(parsed);
        Assert.AreEqual(0, unixSeconds);
    }

    [TestMethod]
    [DataRow("Wed, 09 Jun 1583 10:18:14 GMT", -12_198_778_906L)]
    [DataRow("Wed, 09 Jun 2027 10:18:14 GMT", 1_812_536_294L)]
    [DataRow("Wed, 09 Jun 10000 10:18:14 GMT", 253_416_161_894L)]
    [DataRow("1 Jan 099999999", 3_155_633_001_244_800L)]
    public void TryParse_YearsOutsideDateTimeOffset_AreRead(string text, long expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", text);
        diagnostics.Arrange("expected unix seconds", expected);

        bool parsed = CurlDateParser.TryParse(text, out long unixSeconds);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("unix seconds", unixSeconds);
        diagnostics.Assert("parsed", true, parsed);
        diagnostics.Assert("unix seconds", expected, unixSeconds);
        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, unixSeconds);
    }

    /// <summary><c>Curl_getdate_capped</c> returns the epoch for the instant one second before it, -1 being its failure value.</summary>
    [TestMethod]
    public void TryParse_OneSecondBeforeTheEpoch_ReadsTheEpoch()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", "31 Dec 1969 23:59:59 GMT");

        bool parsed = CurlDateParser.TryParse("31 Dec 1969 23:59:59 GMT", out long unixSeconds);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("unix seconds", unixSeconds);
        diagnostics.Assert("parsed", true, parsed);
        diagnostics.Assert("unix seconds", 0L, unixSeconds);
        Assert.IsTrue(parsed);
        Assert.AreEqual(0, unixSeconds);
    }

    [TestMethod]
    public void TryParse_TwoSecondsBeforeTheEpoch_ReadsThatInstant()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", "31 Dec 1969 23:59:58 GMT");

        bool parsed = CurlDateParser.TryParse("31 Dec 1969 23:59:58 GMT", out long unixSeconds);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("unix seconds", unixSeconds);
        diagnostics.Assert("parsed", true, parsed);
        diagnostics.Assert("unix seconds", -2L, unixSeconds);
        Assert.IsTrue(parsed);
        Assert.AreEqual(-2, unixSeconds);
    }

    [TestMethod]
    public void TryParse_Null_ThrowsArgumentNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CurlDateParser.TryParse(null!, out _));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
        diagnostics.Assert("parameter name", "text", exception.ParamName);
        Assert.AreEqual("text", exception.ParamName);
    }
}
