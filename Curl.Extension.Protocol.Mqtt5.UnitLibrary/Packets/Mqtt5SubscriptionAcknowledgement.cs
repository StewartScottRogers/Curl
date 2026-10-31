using Curl.Extension.Protocol.Mqtt5.Encoding;

namespace Curl.Extension.Protocol.Mqtt5.Packets;

/// <summary>
/// SUBACK or UNSUBACK (MQTT 5 sections 3.9 and 3.11): the Packet Identifier, properties, then one reason code per
/// topic filter of the SUBSCRIBE or UNSUBSCRIBE it answers.
/// </summary>
internal sealed class Mqtt5SubscriptionAcknowledgement : Mqtt5Packet
{
    /// <summary>Creates a SUBACK or UNSUBACK.</summary>
    public Mqtt5SubscriptionAcknowledgement(Mqtt5PacketType packetType)
    {
        PacketType = packetType is Mqtt5PacketType.SubAck or Mqtt5PacketType.UnsubAck
            ? packetType
            : throw new ArgumentOutOfRangeException(nameof(packetType), packetType, "A subscription acknowledgement is SUBACK or UNSUBACK.");
    }

    /// <inheritdoc />
    public override Mqtt5PacketType PacketType { get; }

    /// <summary>The Packet Identifier of the packet it answers.</summary>
    public ushort PacketIdentifier { get; init; }

    /// <summary>The properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>One reason code per topic filter, at least one.</summary>
    public IReadOnlyList<Mqtt5ReasonCode> ReasonCodes { get; init; } = [];

    /// <summary>Reads the variable header and payload of the type <paramref name="header"/> names.</summary>
    internal static Mqtt5SubscriptionAcknowledgement ReadBody(Mqtt5FixedHeader header, Mqtt5Reader reader)
    {
        ushort packetIdentifier = Mqtt5PacketFields.ReadPacketIdentifier(reader);
        IReadOnlyList<Mqtt5Property> properties = Mqtt5PropertyCodec.Read(reader, Mqtt5PropertyDefinitions.ScopeOf(header.PacketType));
        Mqtt5PacketFields.CheckPayloadNotEmpty(reader.IsAtEnd, header.PacketType);
        var reasonCodes = new List<Mqtt5ReasonCode>();
        while (!reader.IsAtEnd)
        {
            reasonCodes.Add(Mqtt5ReasonCodes.Read(reader));
        }

        return new Mqtt5SubscriptionAcknowledgement(header.PacketType) { PacketIdentifier = packetIdentifier, Properties = properties, ReasonCodes = reasonCodes };
    }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer)
    {
        Mqtt5PacketFields.WritePacketIdentifier(writer, PacketIdentifier);
        Mqtt5PropertyCodec.Write(writer, Properties, Mqtt5PropertyDefinitions.ScopeOf(PacketType));
        Mqtt5PacketFields.CheckPayloadNotEmpty(ReasonCodes.Count == 0, PacketType);
        foreach (Mqtt5ReasonCode reasonCode in ReasonCodes)
        {
            writer.WriteByte((byte)reasonCode);
        }
    }
}
