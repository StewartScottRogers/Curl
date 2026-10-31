namespace Curl.Extension.Protocol.Kafka.Wire;

/// <summary>
/// Reads the Metadata v4 response body (after response header v0's <c>correlation_id</c>) for the one topic the
/// request named, and finds the broker that leads one of its partitions.
/// </summary>
internal static class KafkaMetadataResponse
{
    /// <summary>The Kafka error code UNKNOWN_TOPIC_OR_PARTITION, for a partition the topic does not hold.</summary>
    internal const short UnknownTopicOrPartition = 3;

    /// <summary>The Kafka error code LEADER_NOT_AVAILABLE, for a partition whose leader is not among the brokers.</summary>
    internal const short LeaderNotAvailable = 5;

    /// <summary>
    /// Reads the brokers and the topic's partitions and gives the leader of <paramref name="partition"/>: the topic's
    /// error code when it has one, UNKNOWN_TOPIC_OR_PARTITION when the topic holds no such partition, and the
    /// partition's error code (or LEADER_NOT_AVAILABLE) when its leader is not among the brokers.
    /// </summary>
    internal static KafkaMetadataLeader ReadLeader(ReadOnlyMemory<byte> body, int partition)
    {
        KafkaReader reader = new(body);
        reader.Int32();
        Dictionary<int, (string Host, int Port)> brokers = ReadBrokers(reader);
        reader.NullableString();
        reader.Int32();
        int topics = reader.Int32();
        if (topics != 1)
        {
            throw new FormatException($"Kafka response holds {topics} entries where the request named one");
        }

        short topicError = reader.Int16();
        reader.NullableString();
        reader.Int8();
        (short ErrorCode, int LeaderId)? found = FindPartition(reader, partition);
        return topicError != 0 ? new KafkaMetadataLeader(topicError, string.Empty, 0)
            : found is not { } leader ? new KafkaMetadataLeader(UnknownTopicOrPartition, string.Empty, 0)
            : LeaderOf(brokers, leader.ErrorCode, leader.LeaderId);
    }

    private static Dictionary<int, (string Host, int Port)> ReadBrokers(KafkaReader reader)
    {
        Dictionary<int, (string Host, int Port)> brokers = [];
        for (int count = reader.Int32(); count > 0; count--)
        {
            int nodeId = reader.Int32();
            string host = reader.NullableString() ?? throw new FormatException("Kafka broker host is null");
            int port = reader.Int32();
            reader.NullableString();
            brokers[nodeId] = (host, port);
        }

        return brokers;
    }

    private static (short ErrorCode, int LeaderId)? FindPartition(KafkaReader reader, int partition)
    {
        (short ErrorCode, int LeaderId)? found = null;
        for (int count = reader.Int32(); count > 0; count--)
        {
            short errorCode = reader.Int16();
            int index = reader.Int32();
            int leaderId = reader.Int32();
            SkipNodes(reader);
            SkipNodes(reader);
            found = index == partition ? (errorCode, leaderId) : found;
        }

        return found;
    }

    private static void SkipNodes(KafkaReader reader)
    {
        for (int count = reader.Int32(); count > 0; count--)
        {
            reader.Int32();
        }
    }

    private static KafkaMetadataLeader LeaderOf(Dictionary<int, (string Host, int Port)> brokers, short errorCode, int leaderId) =>
        brokers.TryGetValue(leaderId, out (string Host, int Port) broker)
            ? new KafkaMetadataLeader(0, broker.Host, broker.Port)
            : new KafkaMetadataLeader(errorCode != 0 ? errorCode : LeaderNotAvailable, string.Empty, 0);
}
