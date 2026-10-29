using Curl.Extension.Mqtt5.Encoding;

namespace Curl.Extension.Mqtt5.Packets;

/// <summary>PINGREQ or PINGRESP (MQTT 5 sections 3.12 and 3.13): a fixed header and nothing else.</summary>
internal sealed class Mqtt5Ping : Mqtt5Packet
{
    /// <summary>Creates a PINGREQ or PINGRESP.</summary>
    public Mqtt5Ping(Mqtt5PacketType packetType)
    {
        PacketType = packetType is Mqtt5PacketType.PingReq or Mqtt5PacketType.PingResp
            ? packetType
            : throw new ArgumentOutOfRangeException(nameof(packetType), packetType, "A ping is PINGREQ or PINGRESP.");
    }

    /// <inheritdoc />
    public override Mqtt5PacketType PacketType { get; }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer)
    {
    }
}
