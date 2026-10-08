using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// How a <c>-z</c> time is written into the <c>If-Modified-Since</c> or
/// <c>If-Unmodified-Since</c> header. Year 40000 was measured with curl 8.18.0's OpenSSL build
/// on Linux (BL-1426 Notes): <c>-z "Mon, 01 Jan 40000 00:00:00 GMT"</c> sends
/// <c>Sat, 01 Jan 40000 00:00:00 GMT</c>.
/// </summary>
[TestClass]
public sealed class HttpConditionDateTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(1200110860800L, "Sat, 01 Jan 40000 00:00:00 GMT", DisplayName = "year 40000, as curl sent it")]
    [DataRow(253402300800L, "Sat, 01 Jan 10000 00:00:00 GMT", DisplayName = "the first second past 9999")]
    [DataRow(253407488523L, "Wed, 01 Mar 10000 01:02:03 GMT", DisplayName = "after 29 Feb of leap year 10000")]
    public void Format_PastYear9999_WritesTheWholeYearAndItsOwnWeekday(long unixSeconds, string expected)
    {
        Diagnostics.Arrange("unix seconds", unixSeconds);

        string formatted = HttpConditionDate.Format(TimeCondition.FromUnixSeconds(unixSeconds, TimeConditionKind.IfModifiedSince));

        Diagnostics.Act("formatted", formatted);
        Diagnostics.Assert("formatted", expected, formatted);
        Assert.AreEqual(expected, formatted);
    }

    [TestMethod]
    public void Format_TheLastSecondOf9999_IsRfc1123()
    {
        Diagnostics.Arrange("unix seconds", 253402300799L);

        string formatted = HttpConditionDate.Format(TimeCondition.FromUnixSeconds(253402300799L, TimeConditionKind.IfModifiedSince));

        Diagnostics.Act("formatted", formatted);
        Diagnostics.Assert("formatted", "Fri, 31 Dec 9999 23:59:59 GMT", formatted);
        Assert.AreEqual(
            "Fri, 31 Dec 9999 23:59:59 GMT",
            formatted);
    }
}
