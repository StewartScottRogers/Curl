using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka.Compression;
using Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;
using Curl.Conformance.Extended.Kafka.Compression.Entropy.Sequences;
using static Curl.Conformance.Extended.UnitTests.Kafka.Compression.KafkaCompressionCases;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression.Entropy.Sequences;

/// <summary>
/// Fetch of zstd frames whose compressed blocks carry FSE-coded sequences (RFC 8878 section 3.1.1.3.2): codes in
/// Predefined_Mode, in FSE_Compressed_Mode with the tables described, and in Repeat_Mode reusing the previous
/// block's tables. Every sequence copies from inside the record's value, at an offset above the repeat offsets.
/// </summary>
[TestClass]
public sealed class KafkaZstdFseSequencesTests
{
    private const string Predefined = "Kafka:RFC8878-3.1.1.3.2-2";

    private const string FseCompressed = "Kafka:RFC8878-3.1.1.3.2-3";

    private const string Repeat = "Kafka:RFC8878-3.1.1.3.2-4";

    private const string RepeatWithNoTable = "Kafka:RFC8878-3.1.1.3.2-5";

    private const string MixedModes = "Kafka:RFC8878-3.1.1.3.2-6";

    private const int AccuracyLog = 5;

    // "abcd", then 8 bytes from 4 back; " wx", then 6 bytes from 2 back; "." trails. Codes: literals length 4 and 3,
    // offset 2 (Offset_Value 7 and 5), match length 5 and 3.
    private static readonly KafkaReferenceZstdSequence[] AbcdSequences = [new(4, 4, 8), new(3, 2, 6)];

    private static byte[] Value(string text) => Ascii(text);

    /// <summary>Tables in FSE_Compressed_Mode holding the codes <see cref="AbcdSequences"/> use, beside codes it does not.</summary>
    private static (KafkaReferenceSequenceCodeTable LiteralsLength, KafkaReferenceSequenceCodeTable Offset, KafkaReferenceSequenceCodeTable MatchLength) DescribedTables() =>
        (Described([1, 0, 0, 3, 2]), Described([0, 1, 3]), Described([0, 0, 0, 2, 1, 3]));

    private static KafkaReferenceSequenceCodeTable Described(int[] frequencies) =>
        KafkaReferenceSequenceCodeTable.FseCompressed(new KafkaReferenceFseEncodingTable(KafkaReferenceFseNormalizedCounts.FromFrequencies(frequencies, AccuracyLog), AccuracyLog));

    /// <summary>A frame of a raw block before <paramref name="value"/>, <paramref name="middle"/> for it, and a raw block after it.</summary>
    private static byte[] AroundValue(byte[] records, byte[] value, Func<KafkaReferenceZstdFrameWriter, KafkaReferenceZstdFrameWriter> middle)
    {
        int start = records.AsSpan().IndexOf(value);
        return middle(new KafkaReferenceZstdFrameWriter().Raw(records[..start])).Raw(records[(start + value.Length)..]).ToArray();
    }

    private static Task<CurlRunResult> FetchBlocksAsync(byte[] value, Func<KafkaReferenceZstdFrameWriter, KafkaReferenceZstdFrameWriter> middle) =>
        FetchAsync(KafkaCompressedRecordBatch.Zstd, records => AroundValue(records, value, middle), value);

    [TestMethod]
    [SpecRequirement(Predefined)]
    public Task Fetch_SequencesInPredefinedMode_WritesTheRecord() =>
        Known.RunCaseAsync([Predefined], async () =>
        {
            // "abc", then 9 bytes from 3 back; " xyz", then 6 bytes from 3 back.
            byte[] value = Value("abcabcabcabc xyzxyzxyz");
            byte[] block = KafkaReferenceSequencesSectionWriter.Block(
                Value("abc xyz"),
                [new(3, 3, 9), new(4, 3, 6)],
                KafkaReferenceSequenceCodeTable.PredefinedLiteralsLength,
                KafkaReferenceSequenceCodeTable.PredefinedOffset,
                KafkaReferenceSequenceCodeTable.PredefinedMatchLength);

            AssertDelivered(await FetchBlocksAsync(value, frame => frame.Compressed(block, value)), value);
        });

    [TestMethod]
    [SpecRequirement(FseCompressed)]
    public Task Fetch_SequencesInFseCompressedMode_WritesTheRecord() =>
        Known.RunCaseAsync([FseCompressed], async () =>
        {
            byte[] value = Value("abcdabcdabcd wxwxwxwx.");
            (KafkaReferenceSequenceCodeTable literalsLength, KafkaReferenceSequenceCodeTable offset, KafkaReferenceSequenceCodeTable matchLength) = DescribedTables();
            byte[] block = KafkaReferenceSequencesSectionWriter.Block(Value("abcd wx."), AbcdSequences, literalsLength, offset, matchLength);

            AssertDelivered(await FetchBlocksAsync(value, frame => frame.Compressed(block, value)), value);
        });

    [TestMethod]
    [SpecRequirement(Repeat)]
    public Task Fetch_SequencesInRepeatModeReusingThePreviousBlocksTables_WritesTheRecord() =>
        Known.RunCaseAsync([Repeat], async () =>
        {
            byte[] first = Value("abcdabcdabcd wxwxwxwx.");
            byte[] second = Value("efghefghefgh yzyzyzyz!");
            byte[] value = [.. first, .. second];
            (KafkaReferenceSequenceCodeTable literalsLength, KafkaReferenceSequenceCodeTable offset, KafkaReferenceSequenceCodeTable matchLength) = DescribedTables();
            byte[] described = KafkaReferenceSequencesSectionWriter.Block(Value("abcd wx."), AbcdSequences, literalsLength, offset, matchLength);
            byte[] repeated = KafkaReferenceSequencesSectionWriter.Block(
                Value("efgh yz!"),
                AbcdSequences,
                KafkaReferenceSequenceCodeTable.Repeat(literalsLength),
                KafkaReferenceSequenceCodeTable.Repeat(offset),
                KafkaReferenceSequenceCodeTable.Repeat(matchLength));

            AssertDelivered(await FetchBlocksAsync(value, frame => frame.Compressed(described, first).Compressed(repeated, second)), value);
        });

    [TestMethod]
    [SpecRequirement(MixedModes)]
    public Task Fetch_SequencesMixingRepeatPredefinedAndFseCompressedModes_WritesTheRecord() =>
        Known.RunCaseAsync([MixedModes], async () =>
        {
            byte[] first = Value("abcabcabcabc xyzxyzxyz");
            byte[] second = Value("abcdabcdabcd wxwxwxwx.");
            byte[] value = [.. first, .. second];
            (_, KafkaReferenceSequenceCodeTable offset, _) = DescribedTables();
            byte[] predefined = KafkaReferenceSequencesSectionWriter.Block(
                Value("abc xyz"),
                [new(3, 3, 9), new(4, 3, 6)],
                KafkaReferenceSequenceCodeTable.PredefinedLiteralsLength,
                KafkaReferenceSequenceCodeTable.PredefinedOffset,
                KafkaReferenceSequenceCodeTable.PredefinedMatchLength);
            byte[] mixed = KafkaReferenceSequencesSectionWriter.Block(
                Value("abcd wx."),
                AbcdSequences,
                KafkaReferenceSequenceCodeTable.Repeat(KafkaReferenceSequenceCodeTable.PredefinedLiteralsLength),
                offset,
                KafkaReferenceSequenceCodeTable.PredefinedMatchLength);

            AssertDelivered(await FetchBlocksAsync(value, frame => frame.Compressed(predefined, first).Compressed(mixed, second)), value);
        });

    [TestMethod]
    [SpecRequirement(RepeatWithNoTable)]
    public Task Fetch_SequencesInRepeatModeWithNoEarlierTable_FailsWithBadZstdData() =>
        Known.RunCaseAsync([RepeatWithNoTable], async () =>
        {
            byte[] value = Value("abcdabcdabcd wxwxwxwx.");
            (KafkaReferenceSequenceCodeTable literalsLength, KafkaReferenceSequenceCodeTable offset, KafkaReferenceSequenceCodeTable matchLength) = DescribedTables();
            byte[] block = KafkaReferenceSequencesSectionWriter.Block(
                Value("abcd wx."),
                AbcdSequences,
                KafkaReferenceSequenceCodeTable.Repeat(literalsLength),
                KafkaReferenceSequenceCodeTable.Repeat(offset),
                KafkaReferenceSequenceCodeTable.Repeat(matchLength));

            AssertCorrupt(await FetchBlocksAsync(value, frame => frame.Compressed(block, value)), "zstd");
        });
}
