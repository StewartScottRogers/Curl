namespace Curl.Extended.Protocol.Kafka.Wire;

/// <summary>Checks the Metadata request asks the broker to create an undeclared topic (<c>allow_auto_topic_creation</c>, Metadata v4).</summary>
[TestClass]
public sealed class KafkaMetadataAutoTopicCreationTests
{
    [TestMethod]
    public void Metadata_Request_EndsWithAllowAutoTopicCreationTrue()
    {
        byte[] request = KafkaRequests.Metadata(1, "orders");

        Assert.AreEqual((byte)1, request[^1]);
    }
}
