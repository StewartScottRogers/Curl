using Curl.Extended.Protocol.Mqtt5.Encoding;

namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>
/// PUBACK, PUBREC, PUBREL or PUBCOMP (MQTT 5 sections 3.4 to 3.7): the Packet Identifier, then a reason code and
/// properties that are left out when they are Success and none.
/// </summary>
internal sealed class Mqtt5PublishResponse : Mqtt5Packet
{
    /// <summary>Creates a PUBACK, PUBREC, PUBREL or PUBCOMP.</summary>
    public Mqtt5PublishResponse(Mqtt5PacketType packetType)
    {
        PacketType = packetType is Mqtt5PacketType.PubAck or Mqtt5PacketType.PubRec or Mqtt5PacketType.PubRel or Mqtt5PacketType.PubComp
            ? packetType
            : throw new ArgumentOutOfRangeException(nameof(packetType), packetType, "A publish response is PUBACK, PUBREC, PUBREL or PUBCOMP.");
    }

    /// <inheritdoc />
    public override Mqtt5PacketType PacketType { get; }

    /// <summary>The Packet Identifier of the PUBLISH it answers.</summary>
    public ushort PacketIdentifier { get; init; }

    /// <summary>The reason code.</summary>
    public Mqtt5ReasonCode ReasonCode { get; init; }

    /// <summary>The properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>Reads the variable header of the type <paramref name="header"/> names.</summary>
    internal static Mqtt5PublishResponse ReadBody(Mqtt5FixedHeader header, Mqtt5Reader reader)
    {
        ushort packetIdentifier = Mqtt5PacketFields.ReadPacketIdentifier(reader);
        (Mqtt5ReasonCode reasonCode, IReadOnlyList<Mqtt5Property> properties) =
            Mqtt5PacketFields.ReadReasonAndProperties(reader, Mqtt5PropertyDefinitions.ScopeOf(header.PacketType));
        return new Mqtt5PublishResponse(header.PacketType) { PacketIdentifier = packetIdentifier, ReasonCode = reasonCode, Properties = properties };
    }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer)
    {
        Mqtt5PacketFields.WritePacketIdentifier(writer, PacketIdentifier);
        Mqtt5PacketFields.WriteReasonAndProperties(writer, ReasonCode, Properties, Mqtt5PropertyDefinitions.ScopeOf(PacketType));
    }
}
