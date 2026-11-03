using System.Text;
using Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression.Entropy.Fse;

/// <summary>The FSE encoding table, its predefined distributions and the bit stream, decoded by the test decoder.</summary>
[TestClass]
public sealed class KafkaReferenceFseEncoderTests
{
    private static readonly byte[] Text = Encoding.ASCII.GetBytes("abracadabra, a banana bandana; abba aced a cab");

    [TestMethod]
    public void Predefined_Distributions_AreRfc8878s()
    {
        CollectionAssert.AreEqual(
            new short[] { 4, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 2, 1, 1, 1, 1, 1, -1, -1, -1, -1 },
            KafkaReferenceFseEncodingTable.PredefinedLiteralsLength.NormalizedCounts.ToArray());
        CollectionAssert.AreEqual(
            new short[] { 1, 4, 3, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1, -1, -1 },
            KafkaReferenceFseEncodingTable.PredefinedMatchLength.NormalizedCounts.ToArray());
        CollectionAssert.AreEqual(
            new short[] { 1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1 },
            KafkaReferenceFseEncodingTable.PredefinedOffset.NormalizedCounts.ToArray());
        Assert.AreEqual((6, 6, 5), (KafkaReferenceFseEncodingTable.PredefinedLiteralsLength.AccuracyLog, KafkaReferenceFseEncodingTable.PredefinedMatchLength.AccuracyLog, KafkaReferenceFseEncodingTable.PredefinedOffset.AccuracyLog));
    }

    [TestMethod]
    public void Predefined_FirstStates_AreRfc8878AppendixAs()
    {
        byte[] FirstSymbols(KafkaReferenceFseEncodingTable table, int count) => [.. Enumerable.Range(0, count).Select(table.Symbol)];

        CollectionAssert.AreEqual(new byte[] { 0, 0, 1, 3, 4, 6, 7, 9 }, FirstSymbols(KafkaReferenceFseEncodingTable.PredefinedLiteralsLength, 8));
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 5, 6, 8 }, FirstSymbols(KafkaReferenceFseEncodingTable.PredefinedMatchLength, 7));
        CollectionAssert.AreEqual(new byte[] { 0, 6, 9, 15, 21, 3, 7, 12, 18, 23, 5, 8 }, FirstSymbols(KafkaReferenceFseEncodingTable.PredefinedOffset, 12));
    }

    [TestMethod]
    public void Table_EveryState_MatchesTheTestDecodersSpread()
    {
        KafkaReferenceFseEncodingTable table = KafkaReferenceFseEncodingTable.PredefinedMatchLength;
        short[] counts = [.. table.NormalizedCounts];

        CollectionAssert.AreEqual(FseTestDecoder.Symbols(counts, 6), Enumerable.Range(0, 64).Select(table.Symbol).ToArray());
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void Encode_TextOnItsOwnCounts_DecodesBack(int stateCount)
    {
        int[] frequencies = new int[128];
        foreach (byte symbol in Text)
        {
            frequencies[symbol]++;
        }

        short[] counts = KafkaReferenceFseNormalizedCounts.FromFrequencies(frequencies, 6);
        KafkaReferenceFseEncodingTable table = new(counts, 6);

        CollectionAssert.AreEqual(Text, FseTestDecoder.Decode(counts, 6, KafkaReferenceFseEncoder.Encode(table, Text, stateCount), Text.Length, stateCount));
    }

    [TestMethod]
    [DataRow(1, DisplayName = "one state")]
    [DataRow(2, DisplayName = "two states, odd length")]
    public void Encode_PredefinedOffsets_DecodesBack(int stateCount)
    {
        byte[] symbols = [0, 24, 6, 28, 7, 7, 1, 23, 26, 8, 0];
        KafkaReferenceFseEncodingTable table = KafkaReferenceFseEncodingTable.PredefinedOffset;

        CollectionAssert.AreEqual(symbols, FseTestDecoder.Decode([.. table.NormalizedCounts], 5, KafkaReferenceFseEncoder.Encode(table, symbols, stateCount), symbols.Length, stateCount));
    }

    [TestMethod]
    public void Encode_TwoStatesOnTwoSymbols_IsJustTheInitialStates()
    {
        KafkaReferenceFseEncodingTable table = new([20, 12], 5);
        byte[] stream = KafkaReferenceFseEncoder.Encode(table, [1, 0], 2);

        Assert.HasCount(2, stream);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, FseTestDecoder.Decode([20, 12], 5, stream, 2, 2));
    }

    [TestMethod]
    public void Encode_PredefinedLiteralsAndMatchLengths_DecodeBack()
    {
        byte[] literals = [0, 35, 4, 25, 0, 32, 1];
        byte[] matches = [52, 1, 2, 46, 0, 1];

        CollectionAssert.AreEqual(literals, FseTestDecoder.Decode([.. KafkaReferenceFseEncodingTable.PredefinedLiteralsLength.NormalizedCounts], 6, KafkaReferenceFseEncoder.Encode(KafkaReferenceFseEncodingTable.PredefinedLiteralsLength, literals, 1), literals.Length, 1));
        CollectionAssert.AreEqual(matches, FseTestDecoder.Decode([.. KafkaReferenceFseEncodingTable.PredefinedMatchLength.NormalizedCounts], 6, KafkaReferenceFseEncoder.Encode(KafkaReferenceFseEncodingTable.PredefinedMatchLength, matches, 1), matches.Length, 1));
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(3, 3)]
    [DataRow(2, 1)]
    public void Encode_StateCountNotOneOrTwoOrTooFewSymbols_Throws(int stateCount, int symbolCount)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceFseEncoder.Encode(KafkaReferenceFseEncodingTable.PredefinedOffset, new byte[symbolCount], stateCount));
    }

    [TestMethod]
    public void Table_SymbolWithNoState_Throws()
    {
        KafkaReferenceFseEncodingTable table = new([16, 16], 5);

        Assert.ThrowsExactly<ArgumentException>(() => table.InitialState(2));
        Assert.ThrowsExactly<ArgumentException>(() => table.Encode(0, 2, new KafkaReferenceFseBackwardBitWriter()));
    }

    [TestMethod]
    public void BitWriter_Fields_SitBelowTheMarker()
    {
        KafkaReferenceFseBackwardBitWriter writer = new();
        byte[] empty = writer.ToArray();
        writer.WriteBits(0b101, 3);
        writer.WriteBits(0x1F, 5);

        CollectionAssert.AreEqual(new byte[] { 0x01 }, empty);
        Assert.AreEqual(8, writer.BitCount);
        CollectionAssert.AreEqual(new byte[] { 0xFD, 0x01 }, writer.ToArray());
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(32)]
    public void BitWriter_WidthOutsideZeroTo31_Throws(int width)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new KafkaReferenceFseBackwardBitWriter().WriteBits(0, width));
    }
}
