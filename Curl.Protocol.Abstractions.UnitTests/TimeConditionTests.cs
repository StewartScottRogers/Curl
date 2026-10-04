namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two <c>-z</c>/<c>--time-cond</c> spellings: the timestamp a condition
/// carries, and that the direction of the comparison is part of its identity.
/// </summary>
[TestClass]
public sealed class TimeConditionTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    [TestMethod]
    public void Constructor_WithIfModifiedSince_RoundTripsValueAndKind()
    {
        var condition = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);

        Assert.AreEqual(Instant, condition.Value);
        Assert.AreEqual(TimeConditionKind.IfModifiedSince, condition.Kind);
    }

    [TestMethod]
    public void Constructor_WithIfUnmodifiedSince_RoundTripsValueAndKind()
    {
        var condition = new TimeCondition(Instant, TimeConditionKind.IfUnmodifiedSince);

        Assert.AreEqual(Instant, condition.Value);
        Assert.AreEqual(TimeConditionKind.IfUnmodifiedSince, condition.Kind);
    }

    [TestMethod]
    public void Equals_ForOppositeKindsAtTheSameInstant_ReturnsFalse()
    {
        var ifModifiedSince = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);
        var ifUnmodifiedSince = new TimeCondition(Instant, TimeConditionKind.IfUnmodifiedSince);

        Assert.AreNotEqual(ifModifiedSince, ifUnmodifiedSince);
    }

    [TestMethod]
    public void Equals_ForTwoConditionsBuiltTheSameWay_ReturnsTrue()
    {
        var first = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);
        var second = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equals_ForTheSameKindAtDifferentInstants_ReturnsFalse()
    {
        var earlier = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);
        var later = new TimeCondition(Instant.AddSeconds(1), TimeConditionKind.IfModifiedSince);

        Assert.AreNotEqual(earlier, later);
    }

    [TestMethod]
    public void With_SettingEveryProperty_ReturnsCopyWithNewValuesAndLeavesOriginalUnchanged()
    {
        var original = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);
        var later = Instant.AddDays(1);

        var copy = original with { Value = later, Kind = TimeConditionKind.IfUnmodifiedSince };

        Assert.AreEqual(later, copy.Value);
        Assert.AreEqual(TimeConditionKind.IfUnmodifiedSince, copy.Kind);
        Assert.AreEqual(Instant, original.Value);
        Assert.AreEqual(TimeConditionKind.IfModifiedSince, original.Kind);
    }

    [TestMethod]
    public void Constructor_FromADateTimeOffset_StoresItsUnixSeconds()
    {
        var condition = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);

        Assert.AreEqual(Instant.ToUnixTimeSeconds(), condition.ValueUnixSeconds);
    }

    [TestMethod]
    public void FromUnixSeconds_PastYear9999_KeepsTheSecondsAndClampsValue()
    {
        // 40000-01-01T00:00:00Z.
        var condition = TimeCondition.FromUnixSeconds(1200110860800, TimeConditionKind.IfUnmodifiedSince);

        Assert.AreEqual(1200110860800L, condition.ValueUnixSeconds);
        Assert.AreEqual(DateTimeOffset.MaxValue, condition.Value);
        Assert.AreEqual(TimeConditionKind.IfUnmodifiedSince, condition.Kind);
    }

    [TestMethod]
    public void FromUnixSeconds_InRange_EqualsTheConditionBuiltFromTheTime()
    {
        var fromSeconds = TimeCondition.FromUnixSeconds(Instant.ToUnixTimeSeconds(), TimeConditionKind.IfModifiedSince);

        Assert.AreEqual(Instant, fromSeconds.Value);
        Assert.AreEqual(new TimeCondition(Instant, TimeConditionKind.IfModifiedSince), fromSeconds);
    }

    [TestMethod]
    public void Value_WhenSet_DropsTheFractionOfASecond()
    {
        var condition = new TimeCondition(Instant.AddMilliseconds(250), TimeConditionKind.IfModifiedSince);

        Assert.AreEqual(Instant, condition.Value);
    }
}
