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
    public void ParseSeconds_DelaySeconds_ReadsLeadingDigitsCappedAtSixHours(string value, long expected) =>
        Assert.AreEqual(expected, RetryAfterHeader.ParseSeconds(value, Now));

    [TestMethod]
    [DataRow("Sun, 27 Sep 2026 05:26:19 GMT", 5L)]
    [DataRow("Sunday, 27-Sep-26 05:26:24 GMT", 10L)]
    [DataRow("Sun Sep 27 05:26:44 2026", 30L)]
    [DataRow("Sun Sep 27 12:26:14 2026", 21600L)]
    [DataRow("Sat, 01 Jan 2000 00:00:00 GMT", 0L)]
    public void ParseSeconds_HttpDate_ReadsSecondsFromNowCappedAtSixHours(string value, long expected) =>
        Assert.AreEqual(expected, RetryAfterHeader.ParseSeconds(value, Now));

    [TestMethod]
    public void ParseSeconds_NullValue_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => RetryAfterHeader.ParseSeconds(null!, Now));
}
