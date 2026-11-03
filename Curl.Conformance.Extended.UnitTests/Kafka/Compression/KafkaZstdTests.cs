using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka.Compression;
using static Curl.Conformance.Extended.UnitTests.Kafka.Compression.KafkaCompressionCases;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression;

/// <summary>
/// Fetch of zstd-compressed record batches (codec 4): raw, RLE and compressed blocks laid out by hand from RFC 8878,
/// repeat offsets, a content checksum, a skippable frame and a dictionary id the client refuses.
/// </summary>
[TestClass]
public sealed class KafkaZstdTests
{
    /// <summary>A frame of a raw block before <paramref name="value"/>, <paramref name="middle"/> for it, and a raw block after it.</summary>
    private static byte[] AroundValue(byte[] records, byte[] value, Func<KafkaReferenceZstdFrameWriter, KafkaReferenceZstdFrameWriter> middle)
    {
        int start = records.AsSpan().IndexOf(value);
        return middle(new KafkaReferenceZstdFrameWriter().Raw(records[..start])).Raw(records[(start + value.Length)..]).ToArray();
    }

    [TestMethod]
    [SpecRequirement("Kafka:RFC8878-3.1.1-1")]
    [SpecRequirement("Kafka:RFC8878-3.1.1.1-1")]
    [SpecRequirement("Kafka:RFC8878-3.1.1.2-1")]
    public Task Fetch_ZstdFrameOfOneRawBlock_WritesEveryRecord() =>
        Known.RunCaseAsync(["Kafka:RFC8878-3.1.1-1", "Kafka:RFC8878-3.1.1.1-1", "Kafka:RFC8878-3.1.1.2-1"], async () =>
        {
            byte[][] values = [Ascii("zstd raw"), Ascii("block")];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => new KafkaReferenceZstdFrameWriter().Raw(records).ToArray(), values), values);
        });

    [TestMethod]
    [SpecRequirement("Kafka:RFC8878-3.1.1.2-2")]
    [SpecRequirement("Kafka:RFC8878-3.1.1.2-4")]
    public Task Fetch_RleBlockBetweenRawBlocks_WritesTheRecord() =>
        Known.RunCaseAsync(["Kafka:RFC8878-3.1.1.2-2", "Kafka:RFC8878-3.1.1.2-4"], async () =>
        {
            byte[] value = [.. Enumerable.Repeat((byte)'z', 40)];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => AroundValue(records, value, frame => frame.Rle((byte)'z', 40)), value), value);
        });

    [TestMethod]
    [SpecRequirement("Kafka:RFC8878-3.1.1.2-3")]
    [SpecRequirement("Kafka:RFC8878-3.1.1.3.1-1")]
    [SpecRequirement("Kafka:RFC8878-3.1.1.3.2-1")]
    [SpecRequirement("Kafka:RFC8878-3.1.1.4-1")]
    [SpecRequirement("Kafka:RFC8878-3.1.1.5-1")]
    public Task Fetch_CompressedBlockWithRepeatOffsets_WritesTheRecord() =>
        Known.RunCaseAsync(["Kafka:RFC8878-3.1.1.2-3", "Kafka:RFC8878-3.1.1.3.1-1", "Kafka:RFC8878-3.1.1.3.2-1", "Kafka:RFC8878-3.1.1.4-1", "Kafka:RFC8878-3.1.1.5-1"], async () =>
        {
            // Literals length code 4 (4 literals), match length code 1 (4 bytes), offset code 1 (value 2 + one extra bit).
            // Sequence 1: "abcd", value 2 = repeat offset 2, initially 4, copies "abcd"; the offsets become 4, 1, 8.
            // Sequence 2: "wxyz", value 3 = repeat offset 3, 8, copies "abcd" again.
            byte[] value = Ascii("abcdabcdwxyzabcd");
            byte[] block = KafkaReferenceZstdFrameWriter.RleSequencesBlock(Ascii("abcdwxyz"), 4, 1, 1, 2, (0, 1), (1, 1));

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => AroundValue(records, value, frame => frame.Compressed(block, value)), value), value);
        });

    [TestMethod]
    [SpecRequirement("Kafka:RFC8878-3.1.1-3")]
    [SpecRequirement("Kafka:RFC8878-3.1.2-1")]
    public Task Fetch_SkippableFrameThenFrameWithContentChecksum_WritesEveryRecord() =>
        Known.RunCaseAsync(["Kafka:RFC8878-3.1.1-3", "Kafka:RFC8878-3.1.2-1"], async () =>
        {
            byte[][] values = [Ascii("checked"), Ascii("by XXH64")];
            byte[] skippable = [0x50, 0x2A, 0x4D, 0x18, 3, 0, 0, 0, 1, 2, 3];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => [.. skippable, .. new KafkaReferenceZstdFrameWriter(contentChecksum: true).Raw(records).ToArray()], values), values);
        });

    [TestMethod]
    [SpecRequirement("Kafka:RFC8878-3.1.1.1-2")]
    public Task Fetch_FrameNamingADictionary_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(["Kafka:RFC8878-3.1.1.1-2"], async () =>
            AssertCorrupt(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => new KafkaReferenceZstdFrameWriter(dictionaryId: 7).Raw(records).ToArray(), Ascii("lost")), "zstd"));

    [TestMethod]
    [SpecRequirement("Kafka:RFC8878-3.1.1-2")]
    public Task Fetch_ZstdWithAWrongMagic_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(["Kafka:RFC8878-3.1.1-2"], async () =>
            AssertCorrupt(await FetchAsync(KafkaCompressedRecordBatch.Zstd, records => [0x00, .. new KafkaReferenceZstdFrameWriter().Raw(records).ToArray()[1..]], Ascii("lost")), "zstd"));
}
