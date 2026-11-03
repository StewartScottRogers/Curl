using System.IO.Compression;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka.Compression;
using static Curl.Conformance.Extended.UnitTests.Kafka.Compression.KafkaCompressionCases;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression;

/// <summary>Fetch of a gzip-compressed record batch (codec 1), encoded by the BCL's <see cref="GZipStream"/>.</summary>
[TestClass]
public sealed class KafkaGzipTests
{
    private static byte[] Gzipped(byte[] records)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(records);
        }

        return output.ToArray();
    }

    [TestMethod]
    [SpecRequirement("Kafka:Batch-Compression-1")]
    [SpecRequirement("Kafka:Batch-Compression-2")]
    [SpecRequirement("Kafka:RFC1952-2.3-1")]
    public Task Fetch_GzipBatch_WritesEveryRecord() =>
        Known.RunCaseAsync(["Kafka:Batch-Compression-1", "Kafka:Batch-Compression-2", "Kafka:RFC1952-2.3-1"], async () =>
        {
            byte[][] values = [Ascii("gzip one"), Ascii("gzip two gzip two gzip two"), Unmatched(300, 1)];

            AssertDelivered(await FetchAsync(KafkaCompressedRecordBatch.Gzip, Gzipped, values), values);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Batch-Compression-3")]
    [SpecRequirement("Kafka:RFC1952-2.3-2")]
    public Task Fetch_CorruptGzip_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(["Kafka:Batch-Compression-3", "Kafka:RFC1952-2.3-2"], async () =>
            AssertCorrupt(await FetchAsync(KafkaCompressedRecordBatch.Gzip, _ => Ascii("this is not a gzip member"), Ascii("lost")), "gzip"));
}
