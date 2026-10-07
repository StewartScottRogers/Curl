using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_WithIfModifiedSince_RoundTripsValueAndKind()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("instant and kind", $"{Instant:O} {TimeConditionKind.IfModifiedSince}");

        var condition = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);

        diagnostics.Act("value", condition.Value);
        diagnostics.Act("kind", condition.Kind);
        diagnostics.Assert("value", Instant, condition.Value);
        diagnostics.Assert("kind", TimeConditionKind.IfModifiedSince, condition.Kind);
        Assert.AreEqual(Instant, condition.Value);
        Assert.AreEqual(TimeConditionKind.IfModifiedSince, condition.Kind);
    }

    [TestMethod]
    public void Constructor_WithIfUnmodifiedSince_RoundTripsValueAndKind()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("instant and kind", $"{Instant:O} {TimeConditionKind.IfUnmodifiedSince}");

        var condition = new TimeCondition(Instant, TimeConditionKind.IfUnmodifiedSince);

        diagnostics.Act("value", condition.Value);
        diagnostics.Act("kind", condition.Kind);
        diagnostics.Assert("value", Instant, condition.Value);
        diagnostics.Assert("kind", TimeConditionKind.IfUnmodifiedSince, condition.Kind);
        Assert.AreEqual(Instant, condition.Value);
        Assert.AreEqual(TimeConditionKind.IfUnmodifiedSince, condition.Kind);
    }

    [TestMethod]
    public void Equals_ForOppositeKindsAtTheSameInstant_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("instant", Instant.ToString("O"));

        var ifModifiedSince = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);
        var ifUnmodifiedSince = new TimeCondition(Instant, TimeConditionKind.IfUnmodifiedSince);

        diagnostics.Act("if-modified-since", ifModifiedSince);
        diagnostics.Act("if-unmodified-since", ifUnmodifiedSince);
        diagnostics.Assert("are equal", false, ifModifiedSince.Equals(ifUnmodifiedSince));
        Assert.AreNotEqual(ifModifiedSince, ifUnmodifiedSince);
    }

    [TestMethod]
    public void Equals_ForTwoConditionsBuiltTheSameWay_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("instant and kind", $"{Instant:O} {TimeConditionKind.IfModifiedSince}");

        var first = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);
        var second = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);

        diagnostics.Act("first", first);
        diagnostics.Act("second", second);
        diagnostics.Assert("hash codes", first.GetHashCode(), second.GetHashCode());
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equals_ForTheSameKindAtDifferentInstants_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("instant and offset", $"{Instant:O} plus 1 second");

        var earlier = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);
        var later = new TimeCondition(Instant.AddSeconds(1), TimeConditionKind.IfModifiedSince);

        diagnostics.Act("earlier", earlier);
        diagnostics.Act("later", later);
        diagnostics.Assert("are equal", false, earlier.Equals(later));
        Assert.AreNotEqual(earlier, later);
    }

    [TestMethod]
    public void With_SettingEveryProperty_ReturnsCopyWithNewValuesAndLeavesOriginalUnchanged()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var original = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);
        var later = Instant.AddDays(1);
        diagnostics.Arrange("original", original);
        diagnostics.Arrange("new value", later);

        var copy = original with { Value = later, Kind = TimeConditionKind.IfUnmodifiedSince };

        diagnostics.Act("copy", copy);
        diagnostics.Act("original", original);
        diagnostics.Assert("copy value", later, copy.Value);
        diagnostics.Assert("copy kind", TimeConditionKind.IfUnmodifiedSince, copy.Kind);
        diagnostics.Assert("original value", Instant, original.Value);
        diagnostics.Assert("original kind", TimeConditionKind.IfModifiedSince, original.Kind);
        Assert.AreEqual(later, copy.Value);
        Assert.AreEqual(TimeConditionKind.IfUnmodifiedSince, copy.Kind);
        Assert.AreEqual(Instant, original.Value);
        Assert.AreEqual(TimeConditionKind.IfModifiedSince, original.Kind);
    }

    [TestMethod]
    public void Constructor_FromADateTimeOffset_StoresItsUnixSeconds()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("instant", Instant.ToString("O"));

        var condition = new TimeCondition(Instant, TimeConditionKind.IfModifiedSince);

        diagnostics.Act("unix seconds", condition.ValueUnixSeconds);
        diagnostics.Assert("unix seconds", Instant.ToUnixTimeSeconds(), condition.ValueUnixSeconds);
        Assert.AreEqual(Instant.ToUnixTimeSeconds(), condition.ValueUnixSeconds);
    }

    [TestMethod]
    public void FromUnixSeconds_PastYear9999_KeepsTheSecondsAndClampsValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("unix seconds", 1200110860800L);

        // 40000-01-01T00:00:00Z.
        var condition = TimeCondition.FromUnixSeconds(1200110860800, TimeConditionKind.IfUnmodifiedSince);

        diagnostics.Act("unix seconds", condition.ValueUnixSeconds);
        diagnostics.Act("value", condition.Value);
        diagnostics.Act("kind", condition.Kind);
        diagnostics.Assert("unix seconds", 1200110860800L, condition.ValueUnixSeconds);
        diagnostics.Assert("value", DateTimeOffset.MaxValue, condition.Value);
        diagnostics.Assert("kind", TimeConditionKind.IfUnmodifiedSince, condition.Kind);
        Assert.AreEqual(1200110860800L, condition.ValueUnixSeconds);
        Assert.AreEqual(DateTimeOffset.MaxValue, condition.Value);
        Assert.AreEqual(TimeConditionKind.IfUnmodifiedSince, condition.Kind);
    }

    [TestMethod]
    public void FromUnixSeconds_InRange_EqualsTheConditionBuiltFromTheTime()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("unix seconds", Instant.ToUnixTimeSeconds());

        var fromSeconds = TimeCondition.FromUnixSeconds(Instant.ToUnixTimeSeconds(), TimeConditionKind.IfModifiedSince);

        diagnostics.Act("condition", fromSeconds);
        diagnostics.Assert("value", Instant, fromSeconds.Value);
        Assert.AreEqual(Instant, fromSeconds.Value);
        Assert.AreEqual(new TimeCondition(Instant, TimeConditionKind.IfModifiedSince), fromSeconds);
    }

    [TestMethod]
    public void Value_WhenSet_DropsTheFractionOfASecond()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("instant plus milliseconds", $"{Instant:O} plus 250 ms");

        var condition = new TimeCondition(Instant.AddMilliseconds(250), TimeConditionKind.IfModifiedSince);

        diagnostics.Act("value", condition.Value);
        diagnostics.Assert("value", Instant, condition.Value);
        Assert.AreEqual(Instant, condition.Value);
    }
}
