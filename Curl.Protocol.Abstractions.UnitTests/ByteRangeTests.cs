using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins curl's three <c>-r</c>/<c>--range</c> forms: which members each one fills, which
/// it deliberately leaves <see langword="null" />, and that the three never collapse into
/// each other.
/// </summary>
[TestClass]
public sealed class ByteRangeTests
{
    public TestContext TestContext { get; set; } = null!;

    private static string Describe(ByteRange range) =>
        $"{range.Kind} first {range.FirstBytePosition?.ToString() ?? "null"}, "
            + $"last {range.LastBytePosition?.ToString() ?? "null"}, "
            + $"suffix {range.SuffixLength?.ToString() ?? "null"}";

    [TestMethod]
    public void Bounded_WithFirstAndLastPosition_DescribesAnInclusiveRange()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", "0-4");

        var range = ByteRange.Bounded(0, 4);

        diagnostics.Act("range", Describe(range));
        diagnostics.Assert("kind", ByteRangeKind.Bounded, range.Kind);
        Assert.AreEqual(ByteRangeKind.Bounded, range.Kind);
        Assert.AreEqual(0L, range.FirstBytePosition);
        Assert.AreEqual(4L, range.LastBytePosition);
        Assert.AreEqual(5L, range.LastBytePosition - range.FirstBytePosition + 1);
    }

    [TestMethod]
    public void Bounded_WithFirstAndLastPosition_LeavesSuffixLengthNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", "0-4");

        var range = ByteRange.Bounded(0, 4);

        diagnostics.Act("range", Describe(range));
        diagnostics.Assert("suffix length is null", true, range.SuffixLength is null);
        Assert.IsNull(range.SuffixLength);
    }

    [TestMethod]
    public void Bounded_WithEqualPositions_DescribesASingleByte()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", "3-3");

        var range = ByteRange.Bounded(3, 3);

        diagnostics.Act("range", Describe(range));
        diagnostics.Assert("kind", ByteRangeKind.Bounded, range.Kind);
        Assert.AreEqual(ByteRangeKind.Bounded, range.Kind);
        Assert.AreEqual(3L, range.FirstBytePosition);
        Assert.AreEqual(3L, range.LastBytePosition);
    }

    [TestMethod]
    public void Bounded_WithNegativeFirstPosition_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", "-1-4");

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.Bounded(-1, 4));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "firstBytePosition", exception.ParamName);
        Assert.AreEqual("firstBytePosition", exception.ParamName);
    }

    [TestMethod]
    public void Bounded_WithLastPositionBeforeFirst_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", "5-4");

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.Bounded(5, 4));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "lastBytePosition", exception.ParamName);
        Assert.AreEqual("lastBytePosition", exception.ParamName);
    }

    [TestMethod]
    public void FromOffset_WithFirstPosition_RunsToTheEndOfTheResource()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", "6-");

        var range = ByteRange.FromOffset(6);

        diagnostics.Act("range", Describe(range));
        diagnostics.Assert("kind", ByteRangeKind.FromOffset, range.Kind);
        Assert.AreEqual(ByteRangeKind.FromOffset, range.Kind);
        Assert.AreEqual(6L, range.FirstBytePosition);
        Assert.IsNull(range.LastBytePosition);
        Assert.IsNull(range.SuffixLength);
    }

    [TestMethod]
    public void FromOffset_WithZero_IsTheWholeResource()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", "0-");

        var range = ByteRange.FromOffset(0);

        diagnostics.Act("range", Describe(range));
        diagnostics.Assert("kind", ByteRangeKind.FromOffset, range.Kind);
        Assert.AreEqual(ByteRangeKind.FromOffset, range.Kind);
        Assert.AreEqual(0L, range.FirstBytePosition);
    }

    [TestMethod]
    public void FromOffset_WithNegativeFirstPosition_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("first position", -1);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.FromOffset(-1));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "firstBytePosition", exception.ParamName);
        Assert.AreEqual("firstBytePosition", exception.ParamName);
    }

    [TestMethod]
    public void Suffix_WithLength_KnowsNoStartPosition()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", "-5");

        var range = ByteRange.Suffix(5);

        diagnostics.Act("range", Describe(range));
        diagnostics.Assert("kind", ByteRangeKind.Suffix, range.Kind);
        Assert.AreEqual(ByteRangeKind.Suffix, range.Kind);
        Assert.AreEqual(5L, range.SuffixLength);
        Assert.IsNull(range.FirstBytePosition);
        Assert.IsNull(range.LastBytePosition);
    }

    [TestMethod]
    public void Suffix_WithZeroLength_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("suffix length", 0);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.Suffix(0));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "suffixLength", exception.ParamName);
        Assert.AreEqual("suffixLength", exception.ParamName);
    }

    [TestMethod]
    public void Suffix_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("suffix length", -1);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.Suffix(-1));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "suffixLength", exception.ParamName);
        Assert.AreEqual("suffixLength", exception.ParamName);
    }

    [TestMethod]
    public void Equals_ForFromOffsetAndSuffixOfTheSameNumber_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ranges", "6- and -6");
        var fromOffset = ByteRange.FromOffset(6);
        var suffix = ByteRange.Suffix(6);

        diagnostics.Act("equal", fromOffset.Equals(suffix));
        diagnostics.Assert("equal", false, fromOffset.Equals(suffix));
        Assert.AreNotEqual(fromOffset, suffix);
    }

    [TestMethod]
    public void Equals_ForTwoBoundedRangesWithTheSamePositions_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ranges", "0-4 and 0-4");
        var first = ByteRange.Bounded(0, 4);
        var second = ByteRange.Bounded(0, 4);

        diagnostics.Act("equal", first.Equals(second));
        diagnostics.Act("hash codes equal", first.GetHashCode() == second.GetHashCode());
        diagnostics.Assert("equal", true, first.Equals(second));
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equals_ForBoundedAndFromOffsetWithTheSameFirstPosition_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ranges", "6-6 and 6-");
        var bounded = ByteRange.Bounded(6, 6);
        var fromOffset = ByteRange.FromOffset(6);

        diagnostics.Act("equal", bounded.Equals(fromOffset));
        diagnostics.Assert("equal", false, bounded.Equals(fromOffset));
        Assert.AreNotEqual(bounded, fromOffset);
    }

    [TestMethod]
    public void With_NoPropertiesSet_ReturnsAnEqualCopyThatIsANewInstance()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", "0-4");
        var original = ByteRange.Bounded(0, 4);

        var copy = original with { };

        diagnostics.Act("copy", Describe(copy));
        diagnostics.Act("same instance", ReferenceEquals(original, copy));
        diagnostics.Assert("equal", true, original.Equals(copy));
        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original, copy);
        Assert.AreEqual(ByteRangeKind.Bounded, copy.Kind);
        Assert.AreEqual(0L, copy.FirstBytePosition);
        Assert.AreEqual(4L, copy.LastBytePosition);
        Assert.IsNull(copy.SuffixLength);
    }
}
