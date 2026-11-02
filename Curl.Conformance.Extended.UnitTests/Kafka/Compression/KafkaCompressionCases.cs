using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka;
using Curl.Conformance.Extended.Kafka.Compression;
using Curl.Conformance.Extended.Kafka.Records;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression;

/// <summary>What the compression cases share: a fetch of one compressed batch and the output it should give.</summary>
internal static class KafkaCompressionCases
{
    /// <summary>Fetches partition 0 from a log holding one batch of <paramref name="values"/> compressed by <paramref name="compress"/>.</summary>
    internal static async Task<CurlRunResult> FetchAsync(short codec, Func<byte[], byte[]> compress, params byte[][] values)
    {
        KafkaReferenceLog log = new();
        log.Append(KafkaCompressedRecordBatch.Write([.. values.Select((value, index) => new KafkaReferenceRecord(index, 0, null, value, []))], codec, compress));
        return await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, new KafkaReferenceBroker(Metadata(1), new KafkaFetchApi(log)))], ["-sS", "-m", "10", $"kafka://localhost/{Topic}/0"]);
    }

    /// <summary>Asserts curl wrote each value on its own line, nothing to stderr, and exited 0.</summary>
    internal static void AssertDelivered(CurlRunResult result, params byte[][] values) =>
        Assert.AreEqual((0, string.Concat(values.Select(value => Text(value) + "\n")), string.Empty), (result.ExitCode, Text(result.StandardOutput), Text(result.StandardError)));

    /// <summary>Asserts curl wrote nothing and failed with exit code 8 naming bad data of <paramref name="codecName"/>.</summary>
    internal static void AssertCorrupt(CurlRunResult result, string codecName) =>
        Assert.AreEqual((8, string.Empty, $"curl: (8) Kafka record batch holds bad {codecName} data" + Environment.NewLine), (result.ExitCode, Text(result.StandardOutput), Text(result.StandardError)));

    /// <summary>The bytes of <paramref name="text"/> as ASCII.</summary>
    internal static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    /// <summary><paramref name="count"/> bytes from a seeded generator, so long runs of literals with no match.</summary>
    internal static byte[] Unmatched(int count, int seed)
    {
        byte[] bytes = new byte[count];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }
}
