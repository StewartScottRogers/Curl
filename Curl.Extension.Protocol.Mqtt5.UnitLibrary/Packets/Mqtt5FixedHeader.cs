using Curl.Extension.Protocol.Mqtt5.Encoding;

namespace Curl.Extension.Protocol.Mqtt5.Packets;

/// <summary>
/// The fixed header of MQTT 5 section 2.1: the packet type in the high four bits of the first byte, its flags in the
/// low four, then the Remaining Length as a Variable Byte Integer.
/// </summary>
internal readonly record struct Mqtt5FixedHeader(Mqtt5PacketType PacketType, byte Flags, int RemainingLength)
{
    /// <summary>The flags table 2-2 fixes for every type but PUBLISH: 0b0010 for PUBREL, SUBSCRIBE and UNSUBSCRIBE, else 0.</summary>
    public static byte RequiredFlags(Mqtt5PacketType packetType) =>
        packetType is Mqtt5PacketType.PubRel or Mqtt5PacketType.Subscribe or Mqtt5PacketType.Unsubscribe ? (byte)0b0010 : (byte)0;

    /// <summary>
    /// Reads the fixed header at the start of <paramref name="buffer"/>; false when the bytes end inside it. The
    /// reserved type 0 and flags table 2-2 does not allow are a Malformed Packet.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> buffer, out Mqtt5FixedHeader header, out int headerLength)
    {
        header = default;
        headerLength = 0;
        if (buffer.IsEmpty)
        {
            return false;
        }

        var packetType = (Mqtt5PacketType)(buffer[0] >> 4);
        byte flags = (byte)(buffer[0] & 0x0F);
        CheckTypeAndFlags(packetType, flags);
        if (!Mqtt5VariableByteInteger.TryRead(buffer[1..], out int remainingLength, out int lengthLength))
        {
            return false;
        }

        header = new(packetType, flags, remainingLength);
        headerLength = 1 + lengthLength;
        return true;
    }

    private static void CheckTypeAndFlags(Mqtt5PacketType packetType, byte flags)
    {
        if (packetType == 0)
        {
            throw Mqtt5MalformedPacketException.Malformed("Packet type 0 is reserved.");
        }

        if (packetType != Mqtt5PacketType.Publish && flags != RequiredFlags(packetType))
        {
            throw Mqtt5MalformedPacketException.Malformed($"{packetType} has flags 0x{flags:X1}, not 0x{RequiredFlags(packetType):X1}.");
        }
    }
}
