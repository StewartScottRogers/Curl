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
}
