using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka.Compression;
using static Curl.Conformance.Extended.UnitTests.Kafka.Compression.KafkaCompressionCases;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression;

/// <summary>Fetch of Snappy-compressed record batches (codec 2): raw Snappy and xerial framing, each copy offset width.</summary>
[TestClass]
public sealed class KafkaSnappyTests
{
    private static byte[] Far(int distance) => [.. Ascii("0123456789abcdef"), .. Enumerable.Repeat((byte)'x', distance), .. Ascii("0123456789abcdef")];

    [TestMethod]
    [SpecRequirement("Kafka:Snappy-Format-1")]
    [SpecRequirement("Kafka:Snappy-Format-2")]
    [SpecRequirement("Kafka:Snappy-Format-3")]
    public Task Fetch_RawSnappyWithOneByteOffsetCopiesAndLongLiterals_WritesEveryRecord() =>
        Known.RunCaseAsync(["Kafka:Snappy-Format-1", "Kafka:Snappy-Format-2", "Kafka:Snappy-Format-3"], async () =>
        {
            // The copies in the middle value split the literals into one of about a hundred bytes and one of over 256.
            byte[][] values = [Unmatched(100, 2), Ascii("snappy snappy snappy snappy"), Unmatched(300, 3)];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Snappy, records => KafkaReferenceSnappyEncoder.Compress(records, KafkaSnappyCopy.OneByteOffset), values), values);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Snappy-Format-4")]
    [SpecRequirement("Kafka:Snappy-Format-6")]
    public Task Fetch_TwoByteOffsetCopiesPast2048Bytes_WritesTheRecord() =>
        Known.RunCaseAsync(["Kafka:Snappy-Format-4", "Kafka:Snappy-Format-6"], async () =>
        {
            byte[] value = Far(3000);

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Snappy, records => KafkaReferenceSnappyEncoder.Compress(records, KafkaSnappyCopy.TwoByteOffset), value), value);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Snappy-Format-5")]
    public Task Fetch_FourByteOffsetCopiesPast65535Bytes_WritesTheRecord() =>
        Known.RunCaseAsync(["Kafka:Snappy-Format-5"], async () =>
        {
            byte[] value = Far(70_000);

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Snappy, records => KafkaReferenceSnappyEncoder.Compress(records, KafkaSnappyCopy.FourByteOffset), value), value);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Snappy-Xerial-1")]
    [SpecRequirement("Kafka:Snappy-Xerial-2")]
    public Task Fetch_XerialFramingOfTwoChunks_WritesEveryRecord() =>
        Known.RunCaseAsync(["Kafka:Snappy-Xerial-1", "Kafka:Snappy-Xerial-2"], async () =>
        {
            byte[][] values = [Ascii("xerial xerial xerial"), Ascii("second chunk")];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Snappy, records => KafkaReferenceSnappyEncoder.Xerial(
                KafkaReferenceSnappyEncoder.Compress(records[..10], KafkaSnappyCopy.TwoByteOffset),
                KafkaReferenceSnappyEncoder.Compress(records[10..], KafkaSnappyCopy.TwoByteOffset)), values), values);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Snappy-Format-7")]
    public Task Fetch_SnappyShorterThanItsDeclaredLength_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(["Kafka:Snappy-Format-7"], async () =>
            AssertCorrupt(await FetchAsync(KafkaCompressedRecordBatch.Snappy, records => KafkaReferenceSnappyEncoder.Compress(records, KafkaSnappyCopy.OneByteOffset)[..^1], Ascii("cut short")), "snappy"));

    [TestMethod]
    [SpecRequirement("Kafka:Snappy-Format-8")]
    public Task Fetch_SnappyCopyWithOffsetZero_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(["Kafka:Snappy-Format-8"], async () =>
            AssertCorrupt(await FetchAsync(KafkaCompressedRecordBatch.Snappy, _ => [5, 0x00, (byte)'a', 0x02, 0x00, 0x00], Ascii("lost")), "snappy"));
}
