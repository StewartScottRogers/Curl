using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins how <see cref="RetryAfterHeader" /> reads <c>Retry-After</c>, each value measured
/// against curl 8.21.0 (mingw, Schannel) on 2026-09-26 by the wait its
/// <c>--retry 1</c> warning announced; the commands are in BL-208's notes.
/// </summary>
[TestClass]
public sealed class RetryAfterHeaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 5, 26, 14, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("3", 3L)]
    [DataRow(" 3", 3L)]
    [DataRow("\t4", 4L)]
    [DataRow("3abc", 3L)]
    [DataRow("2.5", 2L)]
    [DataRow("0", 0L)]
    [DataRow("-3", 0L)]
    [DataRow("abc", 0L)]
    [DataRow("garbage 3", 0L)]
    [DataRow("", 0L)]
    [DataRow("21599", 21599L)]
    [DataRow("21600", 21600L)]
    [DataRow("21601", 21600L)]
    [DataRow("9999999", 21600L)]
    [DataRow("99999999999999999999", 0L)]
    public void ParseSeconds_DelaySeconds_ReadsLeadingDigitsCappedAtSixHours(string value, long expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("Retry-After", value);
        diagnostics.Arrange("now", Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        long seconds = RetryAfterHeader.ParseSeconds(value, Now);

        diagnostics.Act("seconds", seconds);
        diagnostics.Assert("seconds", expected, seconds);
        Assert.AreEqual(expected, seconds);
    }

    [TestMethod]
    [DataRow("Sun, 27 Sep 2026 05:26:19 GMT", 5L)]
    [DataRow("Sunday, 27-Sep-26 05:26:24 GMT", 10L)]
    [DataRow("Sun Sep 27 05:26:44 2026", 30L)]
    [DataRow("Sun Sep 27 12:26:14 2026", 21600L)]
    [DataRow("Sat, 01 Jan 2000 00:00:00 GMT", 0L)]
    public void ParseSeconds_HttpDate_ReadsSecondsFromNowCappedAtSixHours(string value, long expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("Retry-After", value);
        diagnostics.Arrange("now", Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        long seconds = RetryAfterHeader.ParseSeconds(value, Now);

        diagnostics.Act("seconds", seconds);
        diagnostics.Assert("seconds", expected, seconds);
        Assert.AreEqual(expected, seconds);
    }

    /// <summary>
    /// Dates outside RFC 9110 that curl 8.21.0's <c>Curl_getdate_capped</c> still reads, measured
    /// on 2026-09-27 with each date four seconds ahead (BL-393's notes): each waited until the date.
    /// <c>5 Sep</c> has no year, so curl read it as 5 delay-seconds.
    /// </summary>
    [TestMethod]
    [DataRow("27 Sep 2026 05:26:18", 4L)]
    [DataRow("Sep 27 2026 05:26:18", 4L)]
    [DataRow("20260927 05:26:18", 4L)]
    [DataRow("Sun, 27 Sep 2026 05:26:18 +0000", 4L)]
    [DataRow("2026 Sep 27 05:26:18 UTC", 4L)]
    [DataRow("27 Sep 2026 06:26:18 +0100", 4L)]
    [DataRow("1 Jan 2000", 0L)]
    [DataRow("5 Sep", 5L)]
    public void ParseSeconds_LenientDate_ReadsAsCurlGetdateDoes(string value, long expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("Retry-After", value);
        diagnostics.Arrange("now", Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        long seconds = RetryAfterHeader.ParseSeconds(value, Now);

        diagnostics.Act("seconds", seconds);
        diagnostics.Assert("seconds", expected, seconds);
        Assert.AreEqual(expected, seconds);
    }

    [TestMethod]
    public void ParseSeconds_NullValue_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("Retry-After", "(null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => RetryAfterHeader.ParseSeconds(null!, Now));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }
}
