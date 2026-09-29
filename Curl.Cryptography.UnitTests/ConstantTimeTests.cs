namespace Curl.Cryptography;

/// <summary>
/// Pins the branch-free helpers in <see cref="ConstantTime" /> that the hand-built
/// primitives share (ADR-0118).
/// </summary>
[TestClass]
public sealed class ConstantTimeTests
{
    [TestMethod]
    [DataRow(0u, 0u)]
    [DataRow(1u, uint.MaxValue)]
    [DataRow(2u, 0u)]
    [DataRow(3u, uint.MaxValue)]
    public void MaskFromBit_ReadsOnlyTheLowestBit(uint bit, uint expectedMask)
    {
        Assert.AreEqual(expectedMask, ConstantTime.MaskFromBit(bit));
    }

    [TestMethod]
    [DataRow(uint.MaxValue, 0x12345678u)]
    [DataRow(0u, 0x9ABCDEF0u)]
    public void Select_ReturnsTheValueTheMaskNames(uint mask, uint expected)
    {
        Assert.AreEqual(expected, ConstantTime.Select(mask, 0x12345678u, 0x9ABCDEF0u));
    }

    [TestMethod]
    [DataRow(3u, 4u, uint.MaxValue)]
    [DataRow(4u, 4u, 0u)]
    [DataRow(5u, 4u, 0u)]
    [DataRow(0u, 0x7fffffffu, uint.MaxValue)]
    public void LessThanMask_IsAllOnesOnlyWhenLeftIsSmaller(uint left, uint right, uint expectedMask)
    {
        Assert.AreEqual(expectedMask, ConstantTime.LessThanMask(left, right));
    }

    [TestMethod]
    [DataRow(4u, 4u, uint.MaxValue)]
    [DataRow(0u, 0u, uint.MaxValue)]
    [DataRow(4u, 5u, 0u)]
    [DataRow(0x80000000u, 0u, 0u)]
    public void EqualMask_IsAllOnesOnlyWhenEqual(uint left, uint right, uint expectedMask)
    {
        Assert.AreEqual(expectedMask, ConstantTime.EqualMask(left, right));
    }

    [TestMethod]
    public void ConditionalSwap_BitOne_SwapsEveryElement()
    {
        uint[] left = [1u, 2u, 3u];
        uint[] right = [4u, 5u, 6u];

        ConstantTime.ConditionalSwap(left, right, 1u);

        CollectionAssert.AreEqual(new uint[] { 4u, 5u, 6u }, left);
        CollectionAssert.AreEqual(new uint[] { 1u, 2u, 3u }, right);
    }

    [TestMethod]
    public void ConditionalSwap_BitZero_LeavesBothUnchanged()
    {
        uint[] left = [1u, 2u, 3u];
        uint[] right = [4u, 5u, 6u];

        ConstantTime.ConditionalSwap(left, right, 0u);

        CollectionAssert.AreEqual(new uint[] { 1u, 2u, 3u }, left);
        CollectionAssert.AreEqual(new uint[] { 4u, 5u, 6u }, right);
    }

    [TestMethod]
    public void ConditionalSwap_DifferentLengths_Throws()
    {
        uint[] left = [1u, 2u];
        uint[] right = [3u];

        Assert.ThrowsExactly<ArgumentException>(() => ConstantTime.ConditionalSwap(left, right, 1u));
    }

    [TestMethod]
    public void IsAllZero_AllZeroBytes_IsTrue()
    {
        Assert.IsTrue(ConstantTime.IsAllZero(new byte[32]));
    }

    [TestMethod]
    public void IsAllZero_Empty_IsTrue()
    {
        Assert.IsTrue(ConstantTime.IsAllZero([]));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(31)]
    public void IsAllZero_OneNonZeroByte_IsFalse(int position)
    {
        byte[] bytes = new byte[32];
        bytes[position] = 0x80;

        Assert.IsFalse(ConstantTime.IsAllZero(bytes));
    }
}
