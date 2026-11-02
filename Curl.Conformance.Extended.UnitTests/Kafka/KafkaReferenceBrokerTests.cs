using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka;

namespace Curl.Conformance.Extended.UnitTests.Kafka;

/// <summary>The reference broker's own edges that no curl run reaches.</summary>
[TestClass]
public sealed class KafkaReferenceBrokerTests
{
    [TestMethod]
    public async Task ServeAsync_RequestCutShort_EndsWithoutRecordingIt()
    {
        KafkaReferenceBroker broker = new();
        using MemoryStream stream = new(new KafkaWireWriter().Int32(20).Int16(3).ToArray());

        await broker.ServeAsync(new ReferenceConnection(stream, new WireExchange(9092, ReferenceTransport.Tcp)), CancellationToken.None);

        Assert.HasCount(0, broker.Requests);
    }

    [TestMethod]
    public void Authenticate_MessageWithoutTwoSeparators_IsRefused()
    {
        KafkaReferenceRequest request = new(36, 0, 1, "curl", new KafkaWireWriter().Bytes("alice"u8).ToArray());

        byte[] answer = new KafkaSaslPlainAuthenticateApi("alice", "secret").Answer(request);

        Assert.AreEqual(KafkaSaslPlainAuthenticateApi.SaslAuthenticationFailed, new KafkaWireReader(answer).Int16());
    }
}
