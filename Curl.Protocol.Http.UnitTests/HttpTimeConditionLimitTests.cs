using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Which <c>-z</c> times curl 8.21.0 refuses to write into its HTTP header. The boundary was
/// measured on Windows with <c>Record-CurlExchange.ps1</c> (BL-381 Notes): 3001-01-01
/// 20:59:59 UTC is sent, one second later is exit 43.
/// </summary>
[TestClass]
public sealed class HttpTimeConditionLimitTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Refuses_NoTimeCondition_IsFalse()
    {
        Diagnostics.Arrange("time condition", "null");

        bool refuses = HttpTimeConditionLimit.Refuses(null, windowsRuntime: true);

        Diagnostics.Act("refuses", refuses);
        Diagnostics.Assert("refuses", false, refuses);
        Assert.IsFalse(refuses);
    }

    [TestMethod]
    [DataRow(0, false, DisplayName = "3001-01-01 20:59:59 UTC, the last time gmtime converts")]
    [DataRow(1, true, DisplayName = "one second later")]
    public void Refuses_OnWindows_RefusesEveryTimeAfterTheLastOneGmtimeConverts(int seconds, bool expected)
    {
        Diagnostics.Arrange("seconds past 3001-01-01 20:59:59 UTC", seconds);
        TimeCondition condition = new(new DateTimeOffset(3001, 1, 1, 20, 59, 59, TimeSpan.Zero).AddSeconds(seconds), TimeConditionKind.IfModifiedSince);

        bool refuses = HttpTimeConditionLimit.Refuses(condition, windowsRuntime: true);

        Diagnostics.Act("refuses", refuses);
        Diagnostics.Assert("refuses", expected, refuses);
        Assert.AreEqual(expected, refuses);
    }

    [TestMethod]
    public void Refuses_ElsewhereTheLatestTime_IsFalse()
    {
        Diagnostics.Arrange("time condition", "DateTimeOffset.MaxValue, windowsRuntime false");

        bool refuses = HttpTimeConditionLimit.Refuses(new TimeCondition(DateTimeOffset.MaxValue, TimeConditionKind.IfModifiedSince), windowsRuntime: false);

        Diagnostics.Act("refuses", refuses);
        Diagnostics.Assert("refuses", false, refuses);
        Assert.IsFalse(refuses);
    }
}
