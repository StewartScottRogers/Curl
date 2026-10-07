using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Attacks <see cref="ByteRange" /> at the limits of <see cref="long" />, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1505).
/// </summary>
[TestClass]
public sealed class ByteRangeAdversarialTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Bounded_WithBothPositionsAtLongMaxValue_DescribesTheLastPossibleByte()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("positions", long.MaxValue);

        ByteRange range = ByteRange.Bounded(long.MaxValue, long.MaxValue);

        diagnostics.Assert("first byte position", long.MaxValue, range.FirstBytePosition);
        Assert.AreEqual(long.MaxValue, range.FirstBytePosition);
        Assert.AreEqual(long.MaxValue, range.LastBytePosition);
    }

    [TestMethod]
    public void Bounded_WithLongMinValueFirst_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("first byte position", long.MinValue);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ByteRange.Bounded(long.MinValue, 0));

        diagnostics.Assert("parameter name", "firstBytePosition", exception.ParamName);
        Assert.AreEqual("firstBytePosition", exception.ParamName);
    }

    [TestMethod]
    public void FromOffset_WithLongMaxValue_RunsFromTheLastPossibleByte()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("first byte position", long.MaxValue);

        ByteRange range = ByteRange.FromOffset(long.MaxValue);

        diagnostics.Assert("first byte position", long.MaxValue, range.FirstBytePosition);
        Assert.AreEqual(long.MaxValue, range.FirstBytePosition);
        Assert.IsNull(range.LastBytePosition);
    }

    [TestMethod]
    [DataRow(1L)]
    [DataRow(long.MaxValue)]
    public void Suffix_WithTheSmallestOrLargestLength_HoldsIt(long length)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("suffix length", length);

        ByteRange range = ByteRange.Suffix(length);

        diagnostics.Assert("suffix length", length, range.SuffixLength);
        Assert.AreEqual(length, range.SuffixLength);
    }

    [TestMethod]
    public void Suffix_WithLongMinValue_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("suffix length", long.MinValue);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ByteRange.Suffix(long.MinValue));

        diagnostics.Assert("parameter name", "suffixLength", exception.ParamName);
        Assert.AreEqual("suffixLength", exception.ParamName);
    }
}
