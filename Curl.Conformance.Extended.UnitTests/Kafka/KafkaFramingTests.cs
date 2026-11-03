using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka;

/// <summary>The protocol guide's request and response framing and request header v1.</summary>
[TestClass]
public sealed class KafkaFramingTests
{
    // The first request: size 27, Metadata (3) v4, correlation id 1, client id "curl", one topic, no auto-creation.
    private static readonly byte[] MetadataRequest =
        new KafkaWireWriter().Int32(27).Int16(3).Int16(4).Int32(1).String("curl").Int32(1).String(Topic).Int8(0).ToArray();

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Framing-1")]
    [SpecRequirement("Kafka:Guide-Header-1")]
    [SpecRequirement("Kafka:Guide-Types-1")]
    [SpecRequirement("Kafka:Guide-Types-2")]
    [SpecRequirement("Kafka:Guide-Types-3")]
    [SpecRequirement("Kafka:Guide-Types-4")]
    [SpecRequirement("Kafka:Guide-Metadata-1")]
    public Task Request_MetadataFirst_IsSizePrefixedWithHeaderV1() =>
        Known.RunCaseAsync(["Kafka:Guide-Framing-1", "Kafka:Guide-Header-1", "Kafka:Guide-Metadata-1"], async () =>
        {
            KafkaReferenceBroker broker = new(Metadata(1));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, broker)], ["-sS", "-m", "10", $"kafka://localhost/{Topic}/0"]);

            CollectionAssert.AreEqual(MetadataRequest, result.Exchanges[0].ClientBytes[..MetadataRequest.Length]);
            KafkaReferenceRequest first = broker.Requests[0];
            Assert.AreEqual((3, 4, 1, "curl"), (first.ApiKey, first.ApiVersion, first.CorrelationId, first.ClientId));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Framing-2")]
    public Task Response_WrongCorrelationId_FailsWithWeirdServerReply() =>
        Known.RunCaseAsync(["Kafka:Guide-Framing-2"], async () =>
        {
            byte[] body = Metadata(1).Answer(new KafkaReferenceRequest(3, 4, 1, "curl", new KafkaWireWriter().Int32(1).String(Topic).Int8(0).ToArray()));
            byte[] response = new KafkaWireWriter().Int32(4 + body.Length).Int32(2).Raw(body).ToArray();
            ScriptedReferenceServer server = new(ScriptStep.ReceiveUntil(Topic + "\0"), ScriptStep.Send(response));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, server)], ["-sS", "-m", "10", $"kafka://localhost/{Topic}/0"]);

            Assert.AreEqual(8, result.ExitCode);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual("curl: (8) Kafka response has correlation id 2, not 1" + Environment.NewLine, Text(result.StandardError));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Framing-3")]
    public Task Response_TruncatedBeforeItsSize_FailsWithRecvError() =>
        Known.RunCaseAsync(["Kafka:Guide-Framing-3"], async () =>
        {
            ScriptedReferenceServer server = new(ScriptStep.ReceiveUntil(Topic + "\0"), ScriptStep.Send(new KafkaWireWriter().Int32(100).Int32(1).Int32(0).ToArray()));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, server)], ["-sS", "-m", "10", $"kafka://localhost/{Topic}/0"]);

            Assert.AreEqual(56, result.ExitCode);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual("curl: (56) Connection disconnected" + Environment.NewLine, Text(result.StandardError));
        });
}
