using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>Checks how a <c>kafka://</c> URL names a topic, a partition and an offset (ADR-1076).</summary>
[TestClass]
public sealed class KafkaAddressTests
{
    [TestMethod]
    [DataRow("kafka://b/orders", "orders", 0, 0L)]
    [DataRow("kafka://b/orders/3", "orders", 3, 0L)]
    [DataRow("kafka://b/my%20topic/1?offset=42", "my topic", 1, 42L)]
    [DataRow("kafka://b/orders?", "orders", 0, 0L)]
    public void Parse_GivesTopicPartitionAndOffset(string url, string topic, int partition, long offset) =>
        Assert.AreEqual(new KafkaAddress(topic, partition, offset), KafkaAddress.Parse(CurlUrl.Parse(url)));

    [TestMethod]
    [DataRow("kafka://b", KafkaClientMessages.NoTopic)]
    [DataRow("kafka://b/", KafkaClientMessages.NoTopic)]
    [DataRow("kafka://b/t/1/2", KafkaClientMessages.PathTooLong)]
    [DataRow("kafka://b/t/-1", KafkaClientMessages.BadPartition)]
    [DataRow("kafka://b/t/x", KafkaClientMessages.BadPartition)]
    [DataRow("kafka://b/t?offset=", KafkaClientMessages.BadQuery)]
    [DataRow("kafka://b/t?offset=-5", KafkaClientMessages.BadQuery)]
    [DataRow("kafka://b/t?from=5", KafkaClientMessages.BadQuery)]
    public void Parse_Malformed_ThrowsUrlMalformat(string url, string message)
    {
        KafkaClientException thrown = Assert.ThrowsExactly<KafkaClientException>(() => KafkaAddress.Parse(CurlUrl.Parse(url)));

        Assert.AreEqual(CurlExitCode.UrlMalformat, thrown.ExitCode);
        Assert.AreEqual(message, thrown.Message);
    }
}
