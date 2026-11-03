using Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression.Entropy.Fse;

/// <summary>Normalised counts and the FSE table description (RFC 8878 section 4.1.1), read back by the test decoder.</summary>
[TestClass]
public sealed class KafkaReferenceFseTableDescriptionWriterTests
{
    [TestMethod]
    public void Write_TwoEqualSymbols_IsTheHandWorkedBytes()
    {
        // 4 bits of 0 (log 5); 17 in 5 bits (below the maximum 30); then 17 moved past threshold 16 to 31, in 5 bits.
        CollectionAssert.AreEqual(new byte[] { 0x10, 0x3F }, KafkaReferenceFseTableDescriptionWriter.Write([16, 16], 5));
    }

    [TestMethod]
    [DataRow(new short[] { 10, 0, 0, 0, 5, 0, 0, 0, 0, 4, -1, 3, 0, 9 }, 5, DisplayName = "zero runs of 3, 4 and 0, a less-than-one")]
    [DataRow(new short[] { 1, 31 }, 5, DisplayName = "a count at the threshold")]
    [DataRow(new short[] { 32 }, 5, DisplayName = "one symbol")]
    [DataRow(new short[] { -1, -1, 100, 0, 0, 0, 0, 0, 0, 154 }, 8, DisplayName = "accuracy log 8, a zero run of 6")]
    [DataRow(new short[] { 500, -1, 3, 1, 0, 7 }, 9, DisplayName = "accuracy log 9")]
    public void Write_ReadBack_GivesTheCounts(short[] counts, int accuracyLog)
    {
        (short[] read, int readLog) = FseTestDecoder.ReadDescription(KafkaReferenceFseTableDescriptionWriter.Write(counts, accuracyLog));

        Assert.AreEqual(accuracyLog, readLog);
        CollectionAssert.AreEqual(counts, read);
    }

    [TestMethod]
    public void Write_TrailingZeroCounts_AreLeftOut()
    {
        (short[] read, _) = FseTestDecoder.ReadDescription(KafkaReferenceFseTableDescriptionWriter.Write([16, 16, 0, 0], 5));

        CollectionAssert.AreEqual(new short[] { 16, 16 }, read);
    }

    [TestMethod]
    public void Write_CountsNotAddingUp_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => KafkaReferenceFseTableDescriptionWriter.Write([16, 15], 5));
    }

    [TestMethod]
    public void CheckValid_CountBelowMinusOne_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceFseNormalizedCounts.CheckValid([34, -2], 5));
    }

    [TestMethod]
    public void FromFrequencies_RoundsDownTooFar_TakesFromTheLargest()
    {
        CollectionAssert.AreEqual(new short[] { 28, 1, 1, 0, 1, 1 }, KafkaReferenceFseNormalizedCounts.FromFrequencies([1000, 1, 1, 0, 1, 1], 5));
    }

    [TestMethod]
    public void FromFrequencies_RoundsDownShort_AddsToTheLargest()
    {
        CollectionAssert.AreEqual(new short[] { 12, 10, 10 }, KafkaReferenceFseNormalizedCounts.FromFrequencies([1, 1, 1], 5));
    }

    [TestMethod]
    public void FromFrequencies_NoSymbolPresent_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => KafkaReferenceFseNormalizedCounts.FromFrequencies([0, 0], 5));
    }

    [TestMethod]
    public void FromFrequencies_MoreSymbolsThanStates_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => KafkaReferenceFseNormalizedCounts.FromFrequencies([.. Enumerable.Repeat(1, 33)], 5));
    }

    [TestMethod]
    public void FromFrequencies_NegativeFrequency_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceFseNormalizedCounts.FromFrequencies([1, -1], 5));
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(10)]
    public void FromFrequencies_AccuracyLogOutsideFiveToNine_Throws(int accuracyLog)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceFseNormalizedCounts.FromFrequencies([1, 1], accuracyLog));
    }
}
