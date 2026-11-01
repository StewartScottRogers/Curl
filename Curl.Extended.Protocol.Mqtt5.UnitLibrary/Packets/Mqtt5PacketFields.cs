using Curl.Extended.Protocol.Mqtt5.Encoding;

namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>Fields several MQTT 5 packets share: the Packet Identifier, an optional reason code and properties, a payload list.</summary>
internal static class Mqtt5PacketFields
{
    /// <summary>Writes a Packet Identifier (section 2.2.1), which is never 0.</summary>
    public static void WritePacketIdentifier(Mqtt5Writer writer, ushort packetIdentifier)
    {
        CheckPacketIdentifier(packetIdentifier);
        writer.WriteTwoByteInteger(packetIdentifier);
    }

    /// <summary>Reads a Packet Identifier; 0 is a Protocol Error.</summary>
    public static ushort ReadPacketIdentifier(Mqtt5Reader reader)
    {
        ushort packetIdentifier = reader.ReadTwoByteInteger();
        CheckPacketIdentifier(packetIdentifier);
        return packetIdentifier;
    }

    /// <summary>
    /// Writes a reason code and properties the way PUBACK to PUBCOMP, DISCONNECT and AUTH may shorten them: nothing
    /// for Success with no properties, the reason code alone when there are no properties.
    /// </summary>
    public static void WriteReasonAndProperties(Mqtt5Writer writer, Mqtt5ReasonCode reasonCode, IReadOnlyList<Mqtt5Property> properties, Mqtt5PropertyScope scope)
    {
        if (reasonCode == Mqtt5ReasonCode.Success && properties.Count == 0)
        {
            return;
        }

        writer.WriteByte((byte)reasonCode);
        if (properties.Count > 0)
        {
            Mqtt5PropertyCodec.Write(writer, properties, scope);
        }
    }

    /// <summary>Reads what <see cref="WriteReasonAndProperties"/> writes: a missing reason code is Success, missing properties none.</summary>
    public static (Mqtt5ReasonCode ReasonCode, IReadOnlyList<Mqtt5Property> Properties) ReadReasonAndProperties(Mqtt5Reader reader, Mqtt5PropertyScope scope)
    {
        Mqtt5ReasonCode reasonCode = reader.IsAtEnd ? Mqtt5ReasonCode.Success : Mqtt5ReasonCodes.Read(reader);
        IReadOnlyList<Mqtt5Property> properties = reader.IsAtEnd ? [] : Mqtt5PropertyCodec.Read(reader, scope);
        return (reasonCode, properties);
    }

    /// <summary>A SUBSCRIBE, SUBACK, UNSUBSCRIBE or UNSUBACK with no payload entries is a Protocol Error.</summary>
    public static void CheckPayloadNotEmpty(bool isEmpty, Mqtt5PacketType packetType)
    {
        if (isEmpty)
        {
            throw Mqtt5MalformedPacketException.ProtocolError($"{packetType} has no payload.");
        }
    }

    private static void CheckPacketIdentifier(ushort packetIdentifier)
    {
        if (packetIdentifier == 0)
        {
            throw Mqtt5MalformedPacketException.ProtocolError("A Packet Identifier is never 0.");
        }
    }
}
