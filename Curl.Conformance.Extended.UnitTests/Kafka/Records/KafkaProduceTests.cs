using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka;
using Curl.Conformance.Extended.Kafka.Records;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Records;

/// <summary>Produce v3 carrying one uncompressed record batch v2, checked field by field and by CRC-32C.</summary>
[TestClass]
public sealed class KafkaProduceTests
{
    private static readonly string[] Arguments = ["-sS", "-m", "10", "-d", "hello", $"kafka://localhost/{Topic}/0"];

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Produce-1")]
    [SpecRequirement("Kafka:Guide-Produce-2")]
    [SpecRequirement("Kafka:Batch-Header-1")]
    [SpecRequirement("Kafka:Batch-Header-2")]
    [SpecRequirement("Kafka:Batch-Crc-1")]
    [SpecRequirement("Kafka:Batch-Record-1")]
    [SpecRequirement("Kafka:Guide-Types-8")]
    public Task Produce_OneValue_SendsABatchTheBrokerVerifiesFieldByFieldAndByCrc32C() =>
        Known.RunCaseAsync(["Kafka:Guide-Produce-1", "Kafka:Guide-Produce-2", "Kafka:Batch-Header-1", "Kafka:Batch-Header-2", "Kafka:Batch-Crc-1", "Kafka:Batch-Record-1", "Kafka:Guide-Types-8"], async () =>
        {
            KafkaReferenceLog log = new();
            KafkaProduceApi produce = new(log);
            KafkaReferenceBroker broker = new(Metadata(1), produce);

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, broker)], Arguments);

            Assert.AreEqual((0, string.Empty), (result.ExitCode, Text(result.StandardError)));
            Assert.AreEqual(((short)0, (short)3, 2), (broker.Requests[1].ApiKey, broker.Requests[1].ApiVersion, broker.Requests[1].CorrelationId));
            KafkaReferenceProduce request = produce.Produced.Single();
            Assert.AreEqual((null, (short)-1, Topic, 0), (request.TransactionalId, request.Acks, request.Topic, request.Partition));
            Assert.IsGreaterThan(0, request.TimeoutMilliseconds);
            KafkaReferenceRecordBatch batch = request.Batch;
            Assert.AreEqual((0L, (sbyte)2, true, (short)0, 0), (batch.BaseOffset, batch.Magic, batch.CrcMatches, batch.Attributes, batch.LastOffsetDelta));
            Assert.AreEqual((-1L, (short)-1, -1), (batch.ProducerId, batch.ProducerEpoch, batch.BaseSequence));
            Assert.AreEqual(batch.BaseTimestamp, batch.MaxTimestamp);
            KafkaReferenceRecord record = batch.Records.Single();
            Assert.AreEqual((0, 0L, 0), (record.OffsetDelta, record.TimestampDelta, record.Headers.Count));
            Assert.IsNull(record.Key);
            Assert.AreEqual("hello", Encoding.UTF8.GetString(record.Value!));
            Assert.AreEqual(1L, log.HighWatermark);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Produce-3")]
    public Task Produce_PartitionErrorCode_FailsNamingTheErrorCode() =>
        Known.RunCaseAsync(["Kafka:Guide-Produce-3"], async () =>
        {
            KafkaReferenceLog log = new();
            KafkaReferenceBroker broker = new(Metadata(1), new KafkaProduceApi(log, 2));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, broker)], Arguments);

            Assert.AreEqual(8, result.ExitCode);
            Assert.AreEqual($"curl: (8) Kafka broker answered error 2 (CORRUPT_MESSAGE) for topic {Topic} partition 0" + Environment.NewLine, Text(result.StandardError));
            Assert.AreEqual(0L, log.HighWatermark);
        });
}
