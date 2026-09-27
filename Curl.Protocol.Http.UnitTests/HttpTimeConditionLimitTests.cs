using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Which <c>-z</c> times curl 8.21.0 refuses to write into its HTTP header. The boundary was
/// measured on Windows with <c>Record-CurlExchange.ps1</c> (BL-381 Notes): 3001-01-01
/// 20:59:59 UTC is sent, one second later is exit 43.
/// </summary>
[TestClass]
public sealed class HttpTimeConditionLimitTests
{
    [TestMethod]
    public void Refuses_NoTimeCondition_IsFalse() =>
        Assert.IsFalse(HttpTimeConditionLimit.Refuses(null, windowsRuntime: true));

    [TestMethod]
    [DataRow(0, false, DisplayName = "3001-01-01 20:59:59 UTC, the last time gmtime converts")]
    [DataRow(1, true, DisplayName = "one second later")]
    public void Refuses_OnWindows_RefusesEveryTimeAfterTheLastOneGmtimeConverts(int seconds, bool expected)
    {
        TimeCondition condition = new(new DateTimeOffset(3001, 1, 1, 20, 59, 59, TimeSpan.Zero).AddSeconds(seconds), TimeConditionKind.IfModifiedSince);

        Assert.AreEqual(expected, HttpTimeConditionLimit.Refuses(condition, windowsRuntime: true));
    }

    [TestMethod]
    public void Refuses_ElsewhereTheLatestTime_IsFalse() =>
        Assert.IsFalse(HttpTimeConditionLimit.Refuses(new TimeCondition(DateTimeOffset.MaxValue, TimeConditionKind.IfModifiedSince), windowsRuntime: false));
}
