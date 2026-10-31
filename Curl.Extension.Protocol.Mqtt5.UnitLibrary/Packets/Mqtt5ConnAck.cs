using Curl.Extension.Protocol.Mqtt5.Encoding;

namespace Curl.Extension.Protocol.Mqtt5.Packets;

/// <summary>CONNACK (MQTT 5 section 3.2): Session Present, the Connect Reason Code and properties.</summary>
internal sealed class Mqtt5ConnAck : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType PacketType => Mqtt5PacketType.ConnAck;

    /// <summary>The Session Present flag.</summary>
    public bool SessionPresent { get; init; }

    /// <summary>The Connect Reason Code.</summary>
    public Mqtt5ReasonCode ReasonCode { get; init; }

    /// <summary>The CONNACK properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>Reads a CONNACK's variable header; reserved acknowledge flags set are a Malformed Packet.</summary>
    internal static Mqtt5ConnAck ReadBody(Mqtt5Reader reader)
    {
        byte flags = reader.ReadByte();
        if ((flags & 0xFE) != 0)
        {
            throw Mqtt5MalformedPacketException.Malformed("CONNACK's reserved acknowledge flags are set.");
        }

        return new Mqtt5ConnAck
        {
            SessionPresent = flags == 1,
            ReasonCode = Mqtt5ReasonCodes.Read(reader),
            Properties = Mqtt5PropertyCodec.Read(reader, Mqtt5PropertyScope.ConnAck),
        };
    }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer)
    {
        writer.WriteByte(SessionPresent ? (byte)1 : (byte)0);
        writer.WriteByte((byte)ReasonCode);
        Mqtt5PropertyCodec.Write(writer, Properties, Mqtt5PropertyScope.ConnAck);
    }
}
