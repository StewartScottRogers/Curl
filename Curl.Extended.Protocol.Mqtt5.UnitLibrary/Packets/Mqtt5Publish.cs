using Curl.Extended.Protocol.Mqtt5.Encoding;

namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>
/// PUBLISH (MQTT 5 section 3.3): DUP, QoS and RETAIN in the fixed header flags, the Topic Name (empty when a Topic
/// Alias stands for it), a Packet Identifier for QoS 1 and 2, properties, then the payload.
/// </summary>
internal sealed class Mqtt5Publish : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType PacketType => Mqtt5PacketType.Publish;

    /// <summary>The DUP flag; never set for QoS 0.</summary>
    public bool Duplicate { get; init; }

    /// <summary>The QoS level.</summary>
    public Mqtt5QualityOfService QualityOfService { get; init; }

    /// <summary>The RETAIN flag.</summary>
    public bool Retain { get; init; }

    /// <summary>The Topic Name; empty only with a Topic Alias property.</summary>
    public string TopicName { get; init; } = "";

    /// <summary>The Packet Identifier for QoS 1 and 2; 0 for QoS 0.</summary>
    public ushort PacketIdentifier { get; init; }

    /// <summary>The PUBLISH properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>The application message.</summary>
    public byte[] Payload { get; init; } = [];

    /// <inheritdoc />
    protected override byte FixedHeaderFlags => (byte)((Duplicate ? 0b1000 : 0) | ((int)QualityOfService << 1) | (Retain ? 1 : 0));

    /// <summary>Reads a PUBLISH's variable header and payload, with the flags of its fixed header.</summary>
    internal static Mqtt5Publish ReadBody(Mqtt5FixedHeader header, Mqtt5Reader reader)
    {
        var qualityOfService = (Mqtt5QualityOfService)((header.Flags >> 1) & 0b11);
        CheckQualityOfService(qualityOfService);
        string topicName = reader.ReadUtf8String();
        var publish = new Mqtt5Publish
        {
            Duplicate = (header.Flags & 0b1000) != 0,
            QualityOfService = qualityOfService,
            Retain = (header.Flags & 1) != 0,
            TopicName = topicName,
            PacketIdentifier = qualityOfService == Mqtt5QualityOfService.AtMostOnce ? (ushort)0 : Mqtt5PacketFields.ReadPacketIdentifier(reader),
            Properties = Mqtt5PropertyCodec.Read(reader, Mqtt5PropertyScope.Publish),
            Payload = reader.ReadRemainingBytes(),
        };
        publish.Validate();
        return publish;
    }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer)
    {
        CheckQualityOfService(QualityOfService);
        Validate();
        writer.WriteUtf8String(TopicName);
        if (QualityOfService != Mqtt5QualityOfService.AtMostOnce)
        {
            Mqtt5PacketFields.WritePacketIdentifier(writer, PacketIdentifier);
        }

        Mqtt5PropertyCodec.Write(writer, Properties, Mqtt5PropertyScope.Publish);
        writer.WriteBytes(Payload);
    }

    private static void CheckQualityOfService(Mqtt5QualityOfService qualityOfService)
    {
        if (qualityOfService > Mqtt5QualityOfService.ExactlyOnce)
        {
            throw Mqtt5MalformedPacketException.Malformed("PUBLISH has QoS 3.");
        }
    }

    private void Validate()
    {
        if (Duplicate && QualityOfService == Mqtt5QualityOfService.AtMostOnce)
        {
            throw Mqtt5MalformedPacketException.ProtocolError("PUBLISH sets DUP with QoS 0.");
        }

        ValidateTopic();
    }

    private Mqtt5Property? FindTopicAlias() =>
        Properties.FirstOrDefault(property => property.Identifier == Mqtt5PropertyIdentifier.TopicAlias);

    private void ValidateTopic()
    {
        Mqtt5Property? topicAlias = FindTopicAlias();
        if (topicAlias?.IntegerValue == 0)
        {
            throw new Mqtt5MalformedPacketException(Mqtt5ReasonCode.TopicAliasInvalid, "PUBLISH has Topic Alias 0.");
        }

        if (TopicName.Length > 0 || topicAlias is null)
        {
            Mqtt5Topics.ValidateTopicName(TopicName);
        }
    }
}
