using Curl.Extension.Protocol.Mqtt5.Encoding;

namespace Curl.Extension.Protocol.Mqtt5.Packets;

/// <summary>UNSUBSCRIBE (MQTT 5 section 3.10): the Packet Identifier, properties, then one or more topic filters.</summary>
internal sealed class Mqtt5Unsubscribe : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType PacketType => Mqtt5PacketType.Unsubscribe;

    /// <summary>The Packet Identifier.</summary>
    public ushort PacketIdentifier { get; init; }

    /// <summary>The UNSUBSCRIBE properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>The topic filters, at least one.</summary>
    public IReadOnlyList<string> TopicFilters { get; init; } = [];

    /// <summary>Reads an UNSUBSCRIBE's variable header and payload.</summary>
    internal static Mqtt5Unsubscribe ReadBody(Mqtt5Reader reader)
    {
        ushort packetIdentifier = Mqtt5PacketFields.ReadPacketIdentifier(reader);
        IReadOnlyList<Mqtt5Property> properties = Mqtt5PropertyCodec.Read(reader, Mqtt5PropertyScope.Unsubscribe);
        Mqtt5PacketFields.CheckPayloadNotEmpty(reader.IsAtEnd, Mqtt5PacketType.Unsubscribe);
        var topicFilters = new List<string>();
        while (!reader.IsAtEnd)
        {
            string topicFilter = reader.ReadUtf8String();
            Mqtt5Topics.ValidateTopicFilter(topicFilter);
            topicFilters.Add(topicFilter);
        }

        return new Mqtt5Unsubscribe { PacketIdentifier = packetIdentifier, Properties = properties, TopicFilters = topicFilters };
    }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer)
    {
        Mqtt5PacketFields.WritePacketIdentifier(writer, PacketIdentifier);
        Mqtt5PropertyCodec.Write(writer, Properties, Mqtt5PropertyScope.Unsubscribe);
        Mqtt5PacketFields.CheckPayloadNotEmpty(TopicFilters.Count == 0, PacketType);
        foreach (string topicFilter in TopicFilters)
        {
            Mqtt5Topics.ValidateTopicFilter(topicFilter);
            writer.WriteUtf8String(topicFilter);
        }
    }
}
