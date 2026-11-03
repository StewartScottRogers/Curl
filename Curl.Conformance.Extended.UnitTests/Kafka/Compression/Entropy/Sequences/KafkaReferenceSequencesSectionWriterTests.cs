using Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;
using Curl.Conformance.Extended.Kafka.Compression.Entropy.Sequences;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression.Entropy.Sequences;

[TestClass]
public sealed class KafkaReferenceSequencesSectionWriterTests
{
    private static readonly KafkaReferenceSequenceCodeTable PredefinedLiteralsLength = KafkaReferenceSequenceCodeTable.PredefinedLiteralsLength;

    private static readonly KafkaReferenceSequenceCodeTable PredefinedOffset = KafkaReferenceSequenceCodeTable.PredefinedOffset;

    private static readonly KafkaReferenceSequenceCodeTable PredefinedMatchLength = KafkaReferenceSequenceCodeTable.PredefinedMatchLength;

    private static KafkaReferenceSequenceCodeTable Described() =>
        KafkaReferenceSequenceCodeTable.FseCompressed(new KafkaReferenceFseEncodingTable([16, 16], 5));

    [TestMethod]
    public void Sequence_OffsetFour_IsOffsetValueSevenCodeTwoExtraThree()
    {
        KafkaReferenceZstdSequence sequence = new(4, 4, 8);

        Assert.AreEqual(((byte)4, (byte)2, 3, (byte)5), (sequence.LiteralsLengthCode, sequence.OffsetCode, sequence.OffsetExtraBits, sequence.MatchLengthCode));
    }

    [TestMethod]
    public void Section_OneSequence_StartsWithTheCountAndThePredefinedModes()
    {
        byte[] section = KafkaReferenceSequencesSectionWriter.Section([new(0, 1, 3)], PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength);

        Assert.AreEqual((1, 0), (section[0], section[1]));
    }

    [TestMethod]
    public void Section_TwoSequences_ReadsBackTheInitialStatesFromTheStreamsEnd()
    {
        KafkaReferenceZstdSequence[] sequences = [new(1, 1, 3), new(0, 2, 4)];

        byte[] section = KafkaReferenceSequencesSectionWriter.Section(sequences, PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength);

        // The literals length state is the first field read: the 6 bits just below the marker.
        int stream = (section[^1] << 8) | section[^2];
        int marker = 15 - int.LeadingZeroCount(stream << 16);
        int literalsLengthState = (stream >> (marker - 6)) & 0x3F;
        Assert.AreEqual((byte)1, KafkaReferenceFseEncodingTable.PredefinedLiteralsLength.Symbol(literalsLengthState));
    }

    [TestMethod]
    public void Section_FseCompressedAndRepeat_WritesTheModesAndOnlyTheDescribedTable()
    {
        KafkaReferenceSequenceCodeTable described = Described();
        byte[] description = KafkaReferenceFseTableDescriptionWriter.Write([16, 16], 5);

        byte[] section = KafkaReferenceSequencesSectionWriter.Section([new(1, 1, 3)], described, KafkaReferenceSequenceCodeTable.Repeat(PredefinedOffset), PredefinedMatchLength);

        CollectionAssert.AreEqual(new byte[] { 1, (2 << 6) | (3 << 4) }.Concat(description).ToArray(), section[..(2 + description.Length)]);
    }

    [TestMethod]
    public void Repeat_OfADescribedTable_HasItsTableAndNoDescription()
    {
        KafkaReferenceSequenceCodeTable described = Described();

        KafkaReferenceSequenceCodeTable repeat = KafkaReferenceSequenceCodeTable.Repeat(described);

        Assert.AreEqual((KafkaReferenceSequenceCodeTable.RepeatMode, described.Table, 0), (repeat.Mode, repeat.Table, repeat.Description.Count));
    }

    [TestMethod]
    public void Block_RawLiterals_PrefixesTheirOneByteHeader()
    {
        byte[] block = KafkaReferenceSequencesSectionWriter.Block([0x61, 0x62], [new(2, 1, 3)], PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength);

        CollectionAssert.AreEqual(new byte[] { 2 << 3, 0x61, 0x62, 1, 0 }, block[..5]);
    }

    [TestMethod]
    public void Block_ThirtyTwoLiterals_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceSequencesSectionWriter.Block(new byte[32], [new(0, 1, 3)], PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength));

    [TestMethod]
    public void Section_NoSequences_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceSequencesSectionWriter.Section([], PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength));

    [TestMethod]
    public void Section_128Sequences_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceSequencesSectionWriter.Section(Enumerable.Repeat(new KafkaReferenceZstdSequence(0, 1, 3), 128).ToArray(), PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength));

    [TestMethod]
    [DataRow(-1, 1, 3)]
    [DataRow(16, 1, 3)]
    [DataRow(0, 0, 3)]
    [DataRow(0, 1, 2)]
    [DataRow(0, 1, 35)]
    public void Section_SequenceOutOfRange_Throws(int literalsLength, int offset, int matchLength) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaReferenceSequencesSectionWriter.Section([new(literalsLength, offset, matchLength)], PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength));

    [TestMethod]
    public void Section_NullArguments_Throw()
    {
        KafkaReferenceZstdSequence[] sequences = [new(0, 1, 3)];
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceSequencesSectionWriter.Section(null!, PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength));
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceSequencesSectionWriter.Section(sequences, null!, PredefinedOffset, PredefinedMatchLength));
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceSequencesSectionWriter.Section(sequences, PredefinedLiteralsLength, null!, PredefinedMatchLength));
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceSequencesSectionWriter.Section(sequences, PredefinedLiteralsLength, PredefinedOffset, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceSequencesSectionWriter.Block(null!, sequences, PredefinedLiteralsLength, PredefinedOffset, PredefinedMatchLength));
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceSequenceCodeTable.FseCompressed(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaReferenceSequenceCodeTable.Repeat(null!));
    }
}
