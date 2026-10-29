namespace Curl.Extension.Kafka.Wire;

/// <summary>Checks the Metadata v4 request and response against the protocol guide's Metadata API.</summary>
[TestClass]
public sealed class KafkaMetadataTests
{
    [TestMethod]
    public void Metadata_Topic_IsHeaderV1AndOneTopicWithoutAutoCreation()
    {
        byte[] request = KafkaRequests.Metadata(7, "t");

        byte[] body = new KafkaWriter().Int16(3).Int16(4).Int32(7).String("curl").Int32(1).String("t").Int8(0).ToArray();
        CollectionAssert.AreEqual(new KafkaWriter().Int32(body.Length).Raw(body).ToArray(), request);
    }

    [TestMethod]
    public void ReadLeader_PartitionLedByAKnownBroker_GivesItsHostAndPort()
    {
        byte[] response = KafkaTestBytes.MetadataResponse(1, 0, [(1, "one", 9001), (2, "two", 9002)], (0, 0, 1), (9, 1, 2), (0, 2, 1));

        KafkaMetadataLeader leader = KafkaMetadataResponse.ReadLeader(KafkaTestBytes.BodyOf(response), 1);

        Assert.AreEqual(new KafkaMetadataLeader(0, "two", 9002), leader);
    }

    [TestMethod]
    [DataRow((short)0, (short)5)]
    [DataRow((short)5, (short)5)]
    [DataRow((short)6, (short)6)]
    public void ReadLeader_LeaderNotAmongTheBrokers_GivesThePartitionsErrorOrLeaderNotAvailable(short partitionError, short expected)
    {
        byte[] response = KafkaTestBytes.MetadataResponse(1, 0, [(1, "one", 9001)], (partitionError, 0, -1));

        KafkaMetadataLeader leader = KafkaMetadataResponse.ReadLeader(KafkaTestBytes.BodyOf(response), 0);

        Assert.AreEqual(expected, leader.ErrorCode);
    }

    [TestMethod]
    public void ReadLeader_PartitionTheTopicDoesNotHold_GivesUnknownTopicOrPartition()
    {
        byte[] response = KafkaTestBytes.MetadataLeader(1, "one", 9001);

        KafkaMetadataLeader leader = KafkaMetadataResponse.ReadLeader(KafkaTestBytes.BodyOf(response), 3);

        Assert.AreEqual(KafkaMetadataResponse.UnknownTopicOrPartition, leader.ErrorCode);
    }

    [TestMethod]
    public void ReadLeader_TopicError_GivesTheTopicsError()
    {
        byte[] response = KafkaTestBytes.MetadataResponse(1, 29, [(1, "one", 9001)]);

        KafkaMetadataLeader leader = KafkaMetadataResponse.ReadLeader(KafkaTestBytes.BodyOf(response), 0);

        Assert.AreEqual((short)29, leader.ErrorCode);
    }

    [TestMethod]
    public void ReadLeader_TwoTopics_IsMalformed()
    {
        byte[] body = new KafkaWriter().Int32(0).Int32(0).NullString().Int32(0).Int32(2).ToArray();

        FormatException malformed = Assert.ThrowsExactly<FormatException>(() => KafkaMetadataResponse.ReadLeader(body, 0));

        Assert.AreEqual("Kafka response holds 2 entries where the request named one", malformed.Message);
    }

    [TestMethod]
    public void ReadLeader_NullBrokerHost_IsMalformed()
    {
        byte[] body = new KafkaWriter().Int32(0).Int32(1).Int32(1).NullString().ToArray();

        FormatException malformed = Assert.ThrowsExactly<FormatException>(() => KafkaMetadataResponse.ReadLeader(body, 0));

        Assert.AreEqual("Kafka broker host is null", malformed.Message);
    }
}
