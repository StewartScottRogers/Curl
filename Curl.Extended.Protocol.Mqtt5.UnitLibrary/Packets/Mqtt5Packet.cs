using Curl.Extended.Protocol.Mqtt5.Encoding;

namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>An MQTT 5 control packet (chapter 3), which writes itself with its fixed header.</summary>
internal abstract class Mqtt5Packet
{
    /// <summary>The packet's type.</summary>
    public abstract Mqtt5PacketType PacketType { get; }

    /// <summary>The low four bits of the fixed header's first byte.</summary>
    protected virtual byte FixedHeaderFlags => Mqtt5FixedHeader.RequiredFlags(PacketType);

    /// <summary>The whole packet: fixed header, variable header and payload.</summary>
    public byte[] ToBytes()
    {
        var body = new Mqtt5Writer();
        WriteBody(body);
        var packet = new Mqtt5Writer();
        packet.WriteByte((byte)(((int)PacketType << 4) | FixedHeaderFlags));
        packet.WriteVariableByteInteger(body.Length);
        packet.WriteBytes(body.ToArray());
        return packet.ToArray();
    }

    /// <summary>Writes the variable header and payload.</summary>
    protected abstract void WriteBody(Mqtt5Writer writer);
}
