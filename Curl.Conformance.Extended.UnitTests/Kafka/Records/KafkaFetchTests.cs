using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka;
using Curl.Conformance.Extended.Kafka.Records;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Records;

/// <summary>Fetch v4 answered with uncompressed record batches v2: offsets, partial batches, CRC, errors.</summary>
[TestClass]
public sealed class KafkaFetchTests
{
    private static async Task<CurlRunResult> ConsumeAsync(KafkaFetchApi fetch, string query = "") =>
        await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, new KafkaReferenceBroker(Metadata(1), fetch))], ["-sS", "-m", "10", $"kafka://localhost/{Topic}/0{query}"]);

    private static KafkaReferenceLog Log(params string[][] batches)
    {
        KafkaReferenceLog log = new();
        foreach (string[] values in batches)
        {
            log.Append(KafkaReferenceRecordBatch.Write(0, [.. values.Select((value, index) => KafkaReferenceRecord.Plain(index, value))]));
        }

        return log;
    }

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Fetch-1")]
    [SpecRequirement("Kafka:Guide-Fetch-2")]
    [SpecRequirement("Kafka:Guide-Fetch-3")]
    [SpecRequirement("Kafka:Batch-Header-3")]
    public Task Fetch_SeveralBatches_WritesEveryRecordToTheHighWatermark() =>
        Known.RunCaseAsync(["Kafka:Guide-Fetch-1", "Kafka:Guide-Fetch-2", "Kafka:Guide-Fetch-3", "Kafka:Batch-Header-3"], async () =>
        {
            KafkaFetchApi fetch = new(Log(["a", "b"], ["c"], ["d", "e"]), int.MaxValue, new KeyValuePair<long, long>(7, 0));

            CurlRunResult result = await ConsumeAsync(fetch);

            Assert.AreEqual((0, "a\nb\nc\nd\ne\n", string.Empty), (result.ExitCode, Text(result.StandardOutput), Text(result.StandardError)));
            Assert.AreEqual(new KafkaReferenceFetch(-1, 500, 1, 1024 * 1024, 0, Topic, 0, 0, 1024 * 1024), fetch.Fetches.Single());
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Fetch-4")]
    public Task Fetch_OffsetInsideABatch_SkipsTheRecordsBeforeIt() =>
        Known.RunCaseAsync(["Kafka:Guide-Fetch-4"], async () =>
        {
            KafkaFetchApi fetch = new(Log(["a", "b", "c"]));

            CurlRunResult result = await ConsumeAsync(fetch, "?offset=1");

            Assert.AreEqual((0, "b\nc\n"), (result.ExitCode, Text(result.StandardOutput)));
            Assert.AreEqual(1L, fetch.Fetches.Single().FetchOffset);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Fetch-5")]
    public Task Fetch_PartialBatchAtTheEnd_IsFetchedAgainFromItsBaseOffset() =>
        Known.RunCaseAsync(["Kafka:Guide-Fetch-5"], async () =>
        {
            KafkaReferenceLog log = Log(["a", "b"], ["c", "d"]);
            int firstLength = KafkaReferenceRecordBatch.Write(0, [KafkaReferenceRecord.Plain(0, "a"), KafkaReferenceRecord.Plain(1, "b")]).Length;
            KafkaFetchApi fetch = new(log, firstLength + 20);

            CurlRunResult result = await ConsumeAsync(fetch);

            Assert.AreEqual((0, "a\nb\nc\nd\n"), (result.ExitCode, Text(result.StandardOutput)));
            Assert.AreEqual((0L, 2L), (fetch.Fetches[0].FetchOffset, fetch.Fetches[1].FetchOffset));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Batch-Crc-2")]
    public Task Fetch_BatchWithABadCrc_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(["Kafka:Batch-Crc-2"], async () =>
        {
            byte[] batch = KafkaReferenceRecordBatch.Write(0, [KafkaReferenceRecord.Plain(0, "a")]);
            batch[17] ^= 0xFF;
            KafkaReferenceLog log = new();
            log.Append(batch);

            CurlRunResult result = await ConsumeAsync(new KafkaFetchApi(log));

            Assert.AreEqual((8, string.Empty), (result.ExitCode, Text(result.StandardOutput)));
            Assert.AreEqual("curl: (8) Kafka record batch fails its CRC-32C check" + Environment.NewLine, Text(result.StandardError));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Fetch-6")]
    public Task Fetch_EmptyPartition_WritesNothingAndSucceeds() =>
        Known.RunCaseAsync(["Kafka:Guide-Fetch-6"], async () =>
        {
            KafkaFetchApi fetch = new(new KafkaReferenceLog());

            CurlRunResult result = await ConsumeAsync(fetch);

            Assert.AreEqual((0, string.Empty, string.Empty), (result.ExitCode, Text(result.StandardOutput), Text(result.StandardError)));
            Assert.HasCount(1, fetch.Fetches);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Batch-Record-2")]
    [SpecRequirement("Kafka:Batch-Record-3")]
    [SpecRequirement("Kafka:Batch-Record-4")]
    public Task Fetch_NullKeysHeadersAndNegativeTimestampDeltas_WritesEachValue() =>
        Known.RunCaseAsync(["Kafka:Batch-Record-2", "Kafka:Batch-Record-3", "Kafka:Batch-Record-4"], async () =>
        {
            KafkaReferenceLog log = new();
            log.Append(KafkaReferenceRecordBatch.Write(0,
            [
                new KafkaReferenceRecord(0, 0, null, "first"u8.ToArray(), [new("trace", "1"u8.ToArray()), new("empty", null)]),
                new KafkaReferenceRecord(1, -5, "k"u8.ToArray(), "second"u8.ToArray(), []),
                new KafkaReferenceRecord(2, -300, null, null, [new("h", []) ]),
            ]));

            CurlRunResult result = await ConsumeAsync(new KafkaFetchApi(log));

            Assert.AreEqual((0, "first\nsecond\n\n"), (result.ExitCode, Text(result.StandardOutput)));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Batch-Control-1")]
    public Task Fetch_ControlBatch_WritesNoRecordOfIt() =>
        Known.RunCaseAsync(["Kafka:Batch-Control-1"], async () =>
        {
            KafkaReferenceLog log = Log(["a"]);
            log.Append(KafkaReferenceRecordBatch.Write(0, [new KafkaReferenceRecord(0, 0, new byte[4], new byte[6], [])], KafkaReferenceRecordBatch.ControlFlag));
            log.Append(KafkaReferenceRecordBatch.Write(0, [KafkaReferenceRecord.Plain(0, "b")]));

            CurlRunResult result = await ConsumeAsync(new KafkaFetchApi(log));

            Assert.AreEqual((0, "a\nb\n"), (result.ExitCode, Text(result.StandardOutput)));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Fetch-7")]
    public Task Fetch_OffsetOutOfRange_FailsNamingTheErrorCode() =>
        Known.RunCaseAsync(["Kafka:Guide-Fetch-7"], async () =>
        {
            CurlRunResult result = await ConsumeAsync(new KafkaFetchApi(Log(["a", "b"])), "?offset=10");

            Assert.AreEqual((8, string.Empty), (result.ExitCode, Text(result.StandardOutput)));
            Assert.AreEqual($"curl: (8) Kafka broker answered error 1 (OFFSET_OUT_OF_RANGE) for topic {Topic} partition 0" + Environment.NewLine, Text(result.StandardError));
        });
}
