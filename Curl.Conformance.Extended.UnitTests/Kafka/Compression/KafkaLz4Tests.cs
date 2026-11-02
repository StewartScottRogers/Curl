using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka.Compression;
using static Curl.Conformance.Extended.UnitTests.Kafka.Compression.KafkaCompressionCases;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression;

/// <summary>Fetch of LZ4-compressed record batches (codec 3): frames of compressed, stored and linked blocks, with checksums.</summary>
[TestClass]
public sealed class KafkaLz4Tests
{
    private const int BlockMaximum = 64 * 1024;

    [TestMethod]
    [SpecRequirement("Kafka:LZ4-Frame-1")]
    [SpecRequirement("Kafka:LZ4-Frame-2")]
    [SpecRequirement("Kafka:LZ4-Frame-5")]
    [SpecRequirement("Kafka:LZ4-Block-1")]
    [SpecRequirement("Kafka:LZ4-Block-4")]
    public Task Fetch_Lz4FrameOfOneBlock_WritesEveryRecord() =>
        Known.RunCaseAsync(["Kafka:LZ4-Frame-1", "Kafka:LZ4-Frame-2", "Kafka:LZ4-Frame-5", "Kafka:LZ4-Block-1", "Kafka:LZ4-Block-4"], async () =>
        {
            byte[][] values = [Ascii("lz4 lz4 lz4 lz4 lz4 lz4 block"), Ascii("second")];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Lz4, records => new KafkaReferenceLz4FrameWriter().Write(records, BlockMaximum), values), values);
        });

    [TestMethod]
    [SpecRequirement("Kafka:LZ4-Block-2")]
    [SpecRequirement("Kafka:LZ4-Block-3")]
    public Task Fetch_LongLiteralsAndLongOverlappingMatch_WritesTheRecord() =>
        Known.RunCaseAsync(["Kafka:LZ4-Block-2", "Kafka:LZ4-Block-3"], async () =>
        {
            byte[] value = [.. Unmatched(400, 4), .. Enumerable.Repeat((byte)'q', 600), .. Ascii("tail end")];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Lz4, records => new KafkaReferenceLz4FrameWriter().Write(records, BlockMaximum), value), value);
        });

    [TestMethod]
    [SpecRequirement("Kafka:LZ4-Frame-6")]
    [SpecRequirement("Kafka:LZ4-Frame-7")]
    [SpecRequirement("Kafka:LZ4-Frame-9")]
    public Task Fetch_LinkedBlocksWithBlockAndContentChecksums_WritesEveryRecord() =>
        Known.RunCaseAsync(["Kafka:LZ4-Frame-6", "Kafka:LZ4-Frame-7", "Kafka:LZ4-Frame-9"], async () =>
        {
            byte[][] values = [Ascii("linked blocks copy from earlier ones"), Ascii("linked blocks copy from earlier ones again")];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Lz4, records => new KafkaReferenceLz4FrameWriter(blockChecksums: true, contentChecksum: true, linkedBlocks: true).Write(records, 40), values), values);
        });

    [TestMethod]
    [SpecRequirement("Kafka:LZ4-Frame-4")]
    public Task Fetch_StoredBlocks_WritesEveryRecord() =>
        Known.RunCaseAsync(["Kafka:LZ4-Frame-4"], async () =>
        {
            byte[][] values = [Ascii("stored, not compressed"), Ascii("another")];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Lz4, records => new KafkaReferenceLz4FrameWriter(blockChecksums: true).Write(records, 16, storedBlocks: true), values), values);
        });

    [TestMethod]
    [SpecRequirement("Kafka:LZ4-Frame-11")]
    public Task Fetch_Lz4WithAWrongMagic_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(["Kafka:LZ4-Frame-11"], async () =>
            AssertCorrupt(await FetchAsync(KafkaCompressedRecordBatch.Lz4, records => [0x00, .. new KafkaReferenceLz4FrameWriter().Write(records, BlockMaximum)[1..]], Ascii("lost")), "lz4"));

    [TestMethod]
    [SpecRequirement("Kafka:LZ4-Block-5")]
    public Task Fetch_Lz4MatchWithOffsetZero_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(["Kafka:LZ4-Block-5"], async () =>
        {
            byte[] header = [0x04, 0x22, 0x4D, 0x18, 0x60, 0x40];
            byte[] frame = [.. header, (byte)(KafkaReferenceXxHash.Hash32(header.AsSpan(4)) >> 8), 4, 0, 0, 0, 0x10, (byte)'a', 0, 0, 0, 0, 0, 0];

            AssertCorrupt(await FetchAsync(KafkaCompressedRecordBatch.Lz4, _ => frame, Ascii("lost")), "lz4");
        });
}
