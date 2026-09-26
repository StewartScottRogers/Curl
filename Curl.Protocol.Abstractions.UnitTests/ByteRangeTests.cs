namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins curl's three <c>-r</c>/<c>--range</c> forms: which members each one fills, which
/// it deliberately leaves <see langword="null" />, and that the three never collapse into
/// each other.
/// </summary>
[TestClass]
public sealed class ByteRangeTests
{
    [TestMethod]
    public void Bounded_WithFirstAndLastPosition_DescribesAnInclusiveRange()
    {
        var range = ByteRange.Bounded(0, 4);

        Assert.AreEqual(ByteRangeKind.Bounded, range.Kind);
        Assert.AreEqual(0L, range.FirstBytePosition);
        Assert.AreEqual(4L, range.LastBytePosition);
        Assert.AreEqual(5L, range.LastBytePosition - range.FirstBytePosition + 1);
    }

    [TestMethod]
    public void Bounded_WithFirstAndLastPosition_LeavesSuffixLengthNull()
    {
        var range = ByteRange.Bounded(0, 4);

        Assert.IsNull(range.SuffixLength);
    }

    [TestMethod]
    public void Bounded_WithEqualPositions_DescribesASingleByte()
    {
        var range = ByteRange.Bounded(3, 3);

        Assert.AreEqual(ByteRangeKind.Bounded, range.Kind);
        Assert.AreEqual(3L, range.FirstBytePosition);
        Assert.AreEqual(3L, range.LastBytePosition);
    }

    [TestMethod]
    public void Bounded_WithNegativeFirstPosition_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.Bounded(-1, 4));

        Assert.AreEqual("firstBytePosition", exception.ParamName);
    }

    [TestMethod]
    public void Bounded_WithLastPositionBeforeFirst_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.Bounded(5, 4));

        Assert.AreEqual("lastBytePosition", exception.ParamName);
    }

    [TestMethod]
    public void FromOffset_WithFirstPosition_RunsToTheEndOfTheResource()
    {
        var range = ByteRange.FromOffset(6);

        Assert.AreEqual(ByteRangeKind.FromOffset, range.Kind);
        Assert.AreEqual(6L, range.FirstBytePosition);
        Assert.IsNull(range.LastBytePosition);
        Assert.IsNull(range.SuffixLength);
    }

    [TestMethod]
    public void FromOffset_WithZero_IsTheWholeResource()
    {
        var range = ByteRange.FromOffset(0);

        Assert.AreEqual(ByteRangeKind.FromOffset, range.Kind);
        Assert.AreEqual(0L, range.FirstBytePosition);
    }

    [TestMethod]
    public void FromOffset_WithNegativeFirstPosition_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.FromOffset(-1));

        Assert.AreEqual("firstBytePosition", exception.ParamName);
    }

    [TestMethod]
    public void Suffix_WithLength_KnowsNoStartPosition()
    {
        var range = ByteRange.Suffix(5);

        Assert.AreEqual(ByteRangeKind.Suffix, range.Kind);
        Assert.AreEqual(5L, range.SuffixLength);
        Assert.IsNull(range.FirstBytePosition);
        Assert.IsNull(range.LastBytePosition);
    }

    [TestMethod]
    public void Suffix_WithZeroLength_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.Suffix(0));

        Assert.AreEqual("suffixLength", exception.ParamName);
    }

    [TestMethod]
    public void Suffix_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteRange.Suffix(-1));

        Assert.AreEqual("suffixLength", exception.ParamName);
    }

    [TestMethod]
    public void Equals_ForFromOffsetAndSuffixOfTheSameNumber_ReturnsFalse()
    {
        var fromOffset = ByteRange.FromOffset(6);
        var suffix = ByteRange.Suffix(6);

        Assert.AreNotEqual(fromOffset, suffix);
    }

    [TestMethod]
    public void Equals_ForTwoBoundedRangesWithTheSamePositions_ReturnsTrue()
    {
        var first = ByteRange.Bounded(0, 4);
        var second = ByteRange.Bounded(0, 4);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void Equals_ForBoundedAndFromOffsetWithTheSameFirstPosition_ReturnsFalse()
    {
        var bounded = ByteRange.Bounded(6, 6);
        var fromOffset = ByteRange.FromOffset(6);

        Assert.AreNotEqual(bounded, fromOffset);
    }
}
