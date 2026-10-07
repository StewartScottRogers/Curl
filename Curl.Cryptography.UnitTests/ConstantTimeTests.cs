using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins the branch-free helpers in <see cref="ConstantTime" /> that the hand-built
/// primitives share (ADR-0118).
/// </summary>
[TestClass]
public sealed class ConstantTimeTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0u, 0u)]
    [DataRow(1u, uint.MaxValue)]
    [DataRow(2u, 0u)]
    [DataRow(3u, uint.MaxValue)]
    public void MaskFromBit_ReadsOnlyTheLowestBit(uint bit, uint expectedMask)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("bit", $"0x{bit:X8}");

        uint mask = ConstantTime.MaskFromBit(bit);
        diagnostics.Act("mask", $"0x{mask:X8}");

        diagnostics.Assert("mask", $"0x{expectedMask:X8}", $"0x{mask:X8}");
        Assert.AreEqual(expectedMask, mask);
    }

    [TestMethod]
    [DataRow(uint.MaxValue, 0x12345678u)]
    [DataRow(0u, 0x9ABCDEF0u)]
    public void Select_ReturnsTheValueTheMaskNames(uint mask, uint expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mask", $"0x{mask:X8}");
        diagnostics.Arrange("values", "0x12345678 when all ones, 0x9ABCDEF0 when zero");

        uint selected = ConstantTime.Select(mask, 0x12345678u, 0x9ABCDEF0u);
        diagnostics.Act("selected", $"0x{selected:X8}");

        diagnostics.Assert("selected", $"0x{expected:X8}", $"0x{selected:X8}");
        Assert.AreEqual(expected, selected);
    }

    [TestMethod]
    [DataRow(3u, 4u, uint.MaxValue)]
    [DataRow(4u, 4u, 0u)]
    [DataRow(5u, 4u, 0u)]
    [DataRow(0u, 0x7fffffffu, uint.MaxValue)]
    public void LessThanMask_IsAllOnesOnlyWhenLeftIsSmaller(uint left, uint right, uint expectedMask)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("left", $"0x{left:X8}");
        diagnostics.Arrange("right", $"0x{right:X8}");

        uint mask = ConstantTime.LessThanMask(left, right);
        diagnostics.Act("mask", $"0x{mask:X8}");

        diagnostics.Assert("mask", $"0x{expectedMask:X8}", $"0x{mask:X8}");
        Assert.AreEqual(expectedMask, mask);
    }

    [TestMethod]
    [DataRow(4u, 4u, uint.MaxValue)]
    [DataRow(0u, 0u, uint.MaxValue)]
    [DataRow(4u, 5u, 0u)]
    [DataRow(0x80000000u, 0u, 0u)]
    public void EqualMask_IsAllOnesOnlyWhenEqual(uint left, uint right, uint expectedMask)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("left", $"0x{left:X8}");
        diagnostics.Arrange("right", $"0x{right:X8}");

        uint mask = ConstantTime.EqualMask(left, right);
        diagnostics.Act("mask", $"0x{mask:X8}");

        diagnostics.Assert("mask", $"0x{expectedMask:X8}", $"0x{mask:X8}");
        Assert.AreEqual(expectedMask, mask);
    }

    [TestMethod]
    public void ConditionalSwap_BitOne_SwapsEveryElement()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        uint[] left = [1u, 2u, 3u];
        uint[] right = [4u, 5u, 6u];
        diagnostics.Arrange("left", string.Join(", ", left));
        diagnostics.Arrange("right", string.Join(", ", right));
        diagnostics.Arrange("bit", 1u);

        ConstantTime.ConditionalSwap(left, right, 1u);
        diagnostics.Act("left", string.Join(", ", left));
        diagnostics.Act("right", string.Join(", ", right));

        diagnostics.Assert("left", "4, 5, 6", string.Join(", ", left));
        diagnostics.Assert("right", "1, 2, 3", string.Join(", ", right));
        CollectionAssert.AreEqual(new uint[] { 4u, 5u, 6u }, left);
        CollectionAssert.AreEqual(new uint[] { 1u, 2u, 3u }, right);
    }

    [TestMethod]
    public void ConditionalSwap_BitZero_LeavesBothUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        uint[] left = [1u, 2u, 3u];
        uint[] right = [4u, 5u, 6u];
        diagnostics.Arrange("left", string.Join(", ", left));
        diagnostics.Arrange("right", string.Join(", ", right));
        diagnostics.Arrange("bit", 0u);

        ConstantTime.ConditionalSwap(left, right, 0u);
        diagnostics.Act("left", string.Join(", ", left));
        diagnostics.Act("right", string.Join(", ", right));

        diagnostics.Assert("left", "1, 2, 3", string.Join(", ", left));
        diagnostics.Assert("right", "4, 5, 6", string.Join(", ", right));
        CollectionAssert.AreEqual(new uint[] { 1u, 2u, 3u }, left);
        CollectionAssert.AreEqual(new uint[] { 4u, 5u, 6u }, right);
    }

    [TestMethod]
    public void ConditionalSwap_DifferentLengths_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        uint[] left = [1u, 2u];
        uint[] right = [3u];
        diagnostics.Arrange("left length", left.Length);
        diagnostics.Arrange("right length", right.Length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => ConstantTime.ConditionalSwap(left, right, 1u));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void IsAllZero_AllZeroBytes_IsTrue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] bytes = new byte[32];
        diagnostics.Bytes("input", bytes);
        diagnostics.Arrange("input length", bytes.Length);

        bool allZero = ConstantTime.IsAllZero(bytes);
        diagnostics.Act("all zero", allZero);

        diagnostics.Assert("all zero", true, allZero);
        Assert.IsTrue(allZero);
    }

    [TestMethod]
    public void IsAllZero_Empty_IsTrue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input length", 0);

        bool allZero = ConstantTime.IsAllZero([]);
        diagnostics.Act("all zero", allZero);

        diagnostics.Assert("all zero", true, allZero);
        Assert.IsTrue(allZero);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(31)]
    public void IsAllZero_OneNonZeroByte_IsFalse(int position)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] bytes = new byte[32];
        bytes[position] = 0x80;
        diagnostics.Arrange("non-zero position", position);
        diagnostics.Bytes("input", bytes);

        bool allZero = ConstantTime.IsAllZero(bytes);
        diagnostics.Act("all zero", allZero);

        diagnostics.Assert("all zero", false, allZero);
        Assert.IsFalse(allZero);
    }
}
