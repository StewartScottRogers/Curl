using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// CGR-206: <c>-z</c> with a date before 1970. Windows curl 8.22.0 (Schannel) fails with exit 43
/// for a time before 1969-12-31 12:00:00 UTC and names every earlier-than-1970 time it does send
/// <c>Thu</c>; elsewhere curl sends the calendar-correct date (measured, BL-3735).
/// </summary>
[TestClass]
public sealed class Cgr206Tests
{
    [TestMethod]
    [DataRow(-2209075200L, true)]
    [DataRow(-86400L, true)]
    [DataRow(-43201L, true)]
    [DataRow(-43200L, false)]
    [DataRow(-2L, false)]
    [DataRow(0L, false)]
    public void Refuses_OnWindows_RefusesTimesBeforeTwelveHoursBefore1970(long unixSeconds, bool expected)
    {
        TimeCondition condition = TimeCondition.FromUnixSeconds(unixSeconds, TimeConditionKind.IfModifiedSince);

        Assert.AreEqual(expected, HttpTimeConditionLimit.Refuses(condition, windowsRuntime: true));
    }

    [TestMethod]
    public void Refuses_Elsewhere_NeverRefusesTimeBefore1970()
    {
        TimeCondition condition = TimeCondition.FromUnixSeconds(-2209075200L, TimeConditionKind.IfModifiedSince);

        Assert.IsFalse(HttpTimeConditionLimit.Refuses(condition, windowsRuntime: false));
    }

    [TestMethod]
    public void Format_OnWindowsBefore1970_NamesTheDayThu()
    {
        TimeCondition condition = TimeCondition.FromUnixSeconds(-2L, TimeConditionKind.IfModifiedSince);

        Assert.AreEqual("Thu, 31 Dec 1969 23:59:58 GMT", HttpConditionDate.Format(condition, windowsRuntime: true));
    }

    [TestMethod]
    public void Format_ElsewhereBefore1970_NamesTheCalendarWeekday()
    {
        TimeCondition condition = TimeCondition.FromUnixSeconds(-2L, TimeConditionKind.IfModifiedSince);

        Assert.AreEqual("Wed, 31 Dec 1969 23:59:58 GMT", HttpConditionDate.Format(condition, windowsRuntime: false));
    }

    [TestMethod]
    public void Format_OnWindowsAt1970_KeepsTheCalendarWeekday()
    {
        TimeCondition condition = TimeCondition.FromUnixSeconds(86400L, TimeConditionKind.IfModifiedSince);

        Assert.AreEqual("Fri, 02 Jan 1970 00:00:00 GMT", HttpConditionDate.Format(condition, windowsRuntime: true));
    }
}
