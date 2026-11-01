using Curl.Extended.Protocol.Mqtt5.Packets;

namespace Curl.Extended.Protocol.Mqtt5;

/// <summary>
/// Thrown when bytes do not follow the MQTT 5 wire format (OASIS MQTT Version 5.0): a packet read is malformed
/// (section 4.13) or breaks a protocol rule, or a packet to write would. Carries the reason code a DISCONNECT for it
/// would send (section 2.4).
/// </summary>
internal sealed class Mqtt5MalformedPacketException : FormatException
{
    /// <summary>Creates the exception with the reason code and the message naming what is wrong.</summary>
    public Mqtt5MalformedPacketException(Mqtt5ReasonCode reasonCode, string message)
        : base(message)
    {
        ReasonCode = reasonCode;
    }

    /// <summary>The reason code for the fault: Malformed Packet, Protocol Error, Topic Name invalid and the like.</summary>
    public Mqtt5ReasonCode ReasonCode { get; }

    /// <summary>Creates a Malformed Packet (0x81) exception.</summary>
    public static Mqtt5MalformedPacketException Malformed(string message) => new(Mqtt5ReasonCode.MalformedPacket, message);

    /// <summary>Creates a Protocol Error (0x82) exception.</summary>
    public static Mqtt5MalformedPacketException ProtocolError(string message) => new(Mqtt5ReasonCode.ProtocolError, message);
}
