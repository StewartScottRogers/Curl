using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka;
using static Curl.Conformance.Extended.UnitTests.Kafka.KafkaCases;

namespace Curl.Conformance.Extended.UnitTests.Kafka;

/// <summary>SaslHandshake v1 and SaslAuthenticate v0 with the RFC 4616 PLAIN message, under <c>-u</c>.</summary>
[TestClass]
public sealed class KafkaSaslTests
{
    private static readonly string[] Arguments = ["-sS", "-m", "10", "-u", "alice:secret", $"kafka://localhost/{Topic}/0"];

    private static KafkaReferenceBroker Broker(params string[] mechanisms) =>
        new(new KafkaSaslHandshakeApi(mechanisms), new KafkaSaslPlainAuthenticateApi("alice", "secret"), Metadata(1));

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Sasl-1")]
    [SpecRequirement("Kafka:Guide-Sasl-3")]
    [SpecRequirement("Kafka:Guide-Header-2")]
    public Task Sasl_PlainOffered_AuthenticatesBeforeMetadata() =>
        Known.RunCaseAsync(["Kafka:Guide-Sasl-1", "Kafka:Guide-Sasl-3", "Kafka:Guide-Header-2"], async () =>
        {
            KafkaReferenceBroker broker = Broker("SCRAM-SHA-256", "PLAIN");

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, broker)], Arguments);

            CollectionAssert.AreEqual(new short[] { 17, 36, 3, 1 }, broker.Requests.Select(request => request.ApiKey).ToArray());
            CollectionAssert.AreEqual(new short[] { 1, 0, 4, 4 }, broker.Requests.Select(request => request.ApiVersion).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, broker.Requests.Select(request => request.CorrelationId).ToArray());
            Assert.AreEqual("PLAIN", new KafkaWireReader(broker.Requests[0].Body).String());
            Assert.AreEqual("\0alice\0secret", Text(new KafkaWireReader(broker.Requests[1].Body).Bytes()));
            Assert.AreEqual(56, result.ExitCode);
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Sasl-2")]
    public Task Sasl_PlainNotOffered_FailsWithLoginDenied() =>
        Known.RunCaseAsync(["Kafka:Guide-Sasl-2"], async () =>
        {
            KafkaReferenceBroker broker = Broker("SCRAM-SHA-256");

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, broker)], Arguments);

            Assert.AreEqual(67, result.ExitCode);
            Assert.HasCount(1, broker.Requests);
            Assert.AreEqual("curl: (67) Kafka broker does not enable SASL PLAIN, it enables: SCRAM-SHA-256" + Environment.NewLine, Text(result.StandardError));
        });

    [TestMethod]
    [SpecRequirement("Kafka:Guide-Sasl-4")]
    public Task Sasl_AuthenticateRefused_FailsWithLoginDenied() =>
        Known.RunCaseAsync(["Kafka:Guide-Sasl-4"], async () =>
        {
            KafkaReferenceBroker broker = new(new KafkaSaslHandshakeApi("PLAIN"), new KafkaSaslPlainAuthenticateApi("alice", "other"), Metadata(1));

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(Port, broker)], Arguments);

            Assert.AreEqual(67, result.ExitCode);
            Assert.HasCount(2, broker.Requests);
            Assert.AreEqual($"curl: (67) Kafka SASL PLAIN authentication failed: {KafkaSaslPlainAuthenticateApi.FailureMessage}" + Environment.NewLine, Text(result.StandardError));
        });
}
