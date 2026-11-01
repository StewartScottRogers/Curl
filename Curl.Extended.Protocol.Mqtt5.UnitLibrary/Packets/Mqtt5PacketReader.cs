using System.Diagnostics.CodeAnalysis;
using Curl.Extended.Protocol.Mqtt5.Encoding;

namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>
/// Reads MQTT 5 control packets: one whole packet from a buffer, framed by its fixed header (section 2.1), into the
/// <see cref="Mqtt5Packet"/> subclass of its type. Bytes left after a packet's last field are a Malformed Packet.
/// </summary>
internal static class Mqtt5PacketReader
{
    private static readonly Dictionary<Mqtt5PacketType, Func<Mqtt5FixedHeader, Mqtt5Reader, Mqtt5Packet>> BodyReaders = new()
    {
        [Mqtt5PacketType.Connect] = (_, reader) => Mqtt5Connect.ReadBody(reader),
        [Mqtt5PacketType.ConnAck] = (_, reader) => Mqtt5ConnAck.ReadBody(reader),
        [Mqtt5PacketType.Publish] = Mqtt5Publish.ReadBody,
        [Mqtt5PacketType.PubAck] = Mqtt5PublishResponse.ReadBody,
        [Mqtt5PacketType.PubRec] = Mqtt5PublishResponse.ReadBody,
        [Mqtt5PacketType.PubRel] = Mqtt5PublishResponse.ReadBody,
        [Mqtt5PacketType.PubComp] = Mqtt5PublishResponse.ReadBody,
        [Mqtt5PacketType.Subscribe] = (_, reader) => Mqtt5Subscribe.ReadBody(reader),
        [Mqtt5PacketType.SubAck] = Mqtt5SubscriptionAcknowledgement.ReadBody,
        [Mqtt5PacketType.Unsubscribe] = (_, reader) => Mqtt5Unsubscribe.ReadBody(reader),
        [Mqtt5PacketType.UnsubAck] = Mqtt5SubscriptionAcknowledgement.ReadBody,
        [Mqtt5PacketType.PingReq] = (header, _) => new Mqtt5Ping(header.PacketType),
        [Mqtt5PacketType.PingResp] = (header, _) => new Mqtt5Ping(header.PacketType),
        [Mqtt5PacketType.Disconnect] = (_, reader) => Mqtt5Disconnect.ReadBody(reader),
        [Mqtt5PacketType.Auth] = (_, reader) => Mqtt5Auth.ReadBody(reader),
    };

    /// <summary>
    /// Reads the packet at the start of <paramref name="buffer"/>; false, reading nothing, when the buffer ends before
    /// the packet does. <paramref name="packetLength"/> is how many bytes the packet took.
    /// </summary>
    public static bool TryRead(ReadOnlyMemory<byte> buffer, [NotNullWhen(true)] out Mqtt5Packet? packet, out int packetLength)
    {
        packet = null;
        packetLength = 0;
        if (!Mqtt5FixedHeader.TryRead(buffer.Span, out Mqtt5FixedHeader header, out int headerLength)
            || buffer.Length - headerLength < header.RemainingLength)
        {
            return false;
        }

        packet = ReadBody(header, buffer.Slice(headerLength, header.RemainingLength));
        packetLength = headerLength + header.RemainingLength;
        return true;
    }

    /// <summary>Reads <paramref name="packetBytes"/> as exactly one packet; a Remaining Length that disagrees is a Malformed Packet.</summary>
    public static Mqtt5Packet Read(ReadOnlyMemory<byte> packetBytes)
    {
        if (!TryRead(packetBytes, out Mqtt5Packet? packet, out int packetLength) || packetLength != packetBytes.Length)
        {
            throw Mqtt5MalformedPacketException.Malformed($"The Remaining Length does not match the packet's {packetBytes.Length} bytes.");
        }

        return packet;
    }

    private static Mqtt5Packet ReadBody(Mqtt5FixedHeader header, ReadOnlyMemory<byte> body)
    {
        var reader = new Mqtt5Reader(body);
        Mqtt5Packet packet = BodyReaders[header.PacketType](header, reader);
        if (!reader.IsAtEnd)
        {
            throw Mqtt5MalformedPacketException.Malformed($"{header.PacketType} has {reader.RemainingByteCount} bytes after its last field.");
        }

        return packet;
    }
}
