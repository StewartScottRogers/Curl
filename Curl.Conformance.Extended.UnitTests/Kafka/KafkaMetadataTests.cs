using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka;

/// <summary>
/// Metadata v4: the partition's leader, on this broker or another one, and topic and partition error codes. The
/// reference broker serves no Fetch in this part, so a transfer that reaches its Fetch ends when the broker closes.
/// </summary>
[TestClass]
public sealed class KafkaMetadataTests
{
    private static readonly string[] Arguments = ["-sS", "-m", "10", $"kafka://localhost/{Topic}/0"];

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Metadata-2")]
    [SpecRequirement("Kafka:Guide-Types-5")]
    public Task Metadata_LeaderIsThisBroker_FetchesOverTheSameConnection() =>
        Known.RunCaseAsync(["Kafka:Guide-Metadata-2", "Kafka:Guide-Types-5"], async () =>
        {
            KafkaReferenceBroker broker = new(Metadata(1));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, broker)], Arguments);

            Assert.HasCount(1, result.Exchanges);
            Assert.AreEqual((short)1, broker.Requests[1].ApiKey);
            Assert.AreEqual(2, broker.Requests[1].CorrelationId);
            Assert.AreEqual(56, result.ExitCode);
            Assert.AreEqual("curl: (56) Connection disconnected" + Environment.NewLine, Text(result.StandardError));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Metadata-3")]
    public Task Metadata_LeaderIsAnotherBroker_ReconnectsToItsHostAndPort() =>
        Known.RunCaseAsync(["Kafka:Guide-Metadata-3"], async () =>
        {
            KafkaReferenceNode other = new(2, "broker2", 19092);
            KafkaReferenceBroker first = new(Metadata(2, other));
            KafkaReferenceBroker leader = new(Metadata(2, other));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, first), ReferenceRoute.Tcp(19092, leader)], Arguments);

            Assert.HasCount(2, result.Exchanges);
            Assert.AreEqual(19092, result.Exchanges[1].Port);
            Assert.HasCount(1, first.Requests);
            Assert.AreEqual(((short)1, 1), (leader.Requests[0].ApiKey, leader.Requests[0].CorrelationId));
            Assert.AreEqual(56, result.ExitCode);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Metadata-4")]
    [SpecRequirement("Kafka:Guide-Errors-1")]
    public Task Metadata_UnknownTopic_FailsNamingTheErrorCode() =>
        Known.RunCaseAsync(["Kafka:Guide-Metadata-4", "Kafka:Guide-Errors-1"], async () =>
        {
            KafkaReferenceBroker broker = new(new KafkaMetadataApi([ThisBroker], []));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, broker)], Arguments);

            Assert.AreEqual(8, result.ExitCode);
            Assert.HasCount(1, broker.Requests);
            Assert.AreEqual($"curl: (8) Kafka broker answered error 3 (UNKNOWN_TOPIC_OR_PARTITION) for topic {Topic} partition 0" + Environment.NewLine, Text(result.StandardError));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Metadata-5")]
    public Task Metadata_PartitionLeaderNotAvailable_FailsNamingTheErrorCode() =>
        Known.RunCaseAsync(["Kafka:Guide-Metadata-5"], async () =>
        {
            KafkaReferenceBroker broker = new(new KafkaMetadataApi([ThisBroker], [new KafkaReferenceTopic(Topic, 0, [new KafkaReferencePartition(0, 5, -1)])]));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, broker)], Arguments);

            Assert.AreEqual(8, result.ExitCode);
            Assert.AreEqual($"curl: (8) Kafka broker answered error 5 (LEADER_NOT_AVAILABLE) for topic {Topic} partition 0" + Environment.NewLine, Text(result.StandardError));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Tls-1")]
    public Task Kafkas_TlsListener_CarriesTheSameRequests() =>
        Known.RunCaseAsync(["Kafka:Guide-Tls-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            KafkaReferenceBroker broker = new(new KafkaMetadataApi([ThisBroker with { Port = 9093 }], []));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tls(9093, broker, certificate)], ["-sS", "-k", "-m", "10", $"kafkas://localhost/{Topic}/0"]);

            Assert.AreEqual(ReferenceTransport.Tls, result.Exchanges.Single().Transport);
            Assert.AreEqual(((short)3, 1), (broker.Requests[0].ApiKey, broker.Requests[0].CorrelationId));
            Assert.AreEqual(8, result.ExitCode);
        });
}
