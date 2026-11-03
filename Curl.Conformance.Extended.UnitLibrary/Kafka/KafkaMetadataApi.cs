namespace Curl.Conformance.Extended.Kafka;

/// <summary>
/// Metadata v4 (api_key 3): lists the brokers and, for each topic the request names, its error code and partitions with
/// their leaders. A topic the cluster does not hold is answered with UNKNOWN_TOPIC_OR_PARTITION.
/// </summary>
/// <param name="brokers">The brokers, the first being the controller.</param>
/// <param name="topics">The topics the cluster holds.</param>
public sealed class KafkaMetadataApi(IReadOnlyList<KafkaReferenceNode> brokers, IReadOnlyList<KafkaReferenceTopic> topics) : IKafkaReferenceApi
{
    /// <summary>The error code UNKNOWN_TOPIC_OR_PARTITION.</summary>
    public const short UnknownTopicOrPartition = 3;

    /// <inheritdoc/>
    public short ApiKey => 3;

    /// <inheritdoc/>
    public byte[] Answer(KafkaReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        KafkaWireReader reader = new(request.Body);
        List<string> names = [];
        for (int count = reader.Int32(); count > 0; count--)
        {
            names.Add(reader.String());
        }

        KafkaWireWriter writer = new KafkaWireWriter().Int32(0).Int32(brokers.Count);
        foreach (KafkaReferenceNode broker in brokers)
        {
            writer.Int32(broker.NodeId).String(broker.Host).Int32(broker.Port).String(null);
        }

        writer.String(null).Int32(brokers[0].NodeId).Int32(names.Count);
        foreach (string name in names)
        {
            WriteTopic(writer, topics.FirstOrDefault(topic => topic.Name == name) ?? new KafkaReferenceTopic(name, UnknownTopicOrPartition, []));
        }

        return writer.ToArray();
    }

    private static void WriteTopic(KafkaWireWriter writer, KafkaReferenceTopic topic)
    {
        writer.Int16(topic.ErrorCode).String(topic.Name).Int8(0).Int32(topic.Partitions.Count);
        foreach (KafkaReferencePartition partition in topic.Partitions)
        {
            writer.Int16(partition.ErrorCode).Int32(partition.Index).Int32(partition.LeaderId)
                .Int32(1).Int32(partition.LeaderId)
                .Int32(1).Int32(partition.LeaderId);
        }
    }
}
