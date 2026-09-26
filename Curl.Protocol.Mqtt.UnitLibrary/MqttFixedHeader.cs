namespace Curl.Protocol.Mqtt;

/// <summary>
/// The fixed header every MQTT 3.1.1 packet starts with (section 2.2): its first byte and
/// the length of everything after the header.
/// </summary>
/// <param name="firstByte">The packet type in the high nibble and its flags in the low one.</param>
/// <param name="remainingLength">The number of bytes that follow the fixed header.</param>
internal readonly struct MqttFixedHeader(byte firstByte, int remainingLength)
{
    /// <summary>
    /// Gets the packet type in the high nibble and its flags in the low one.
    /// </summary>
    internal byte FirstByte { get; } = firstByte;

    /// <summary>
    /// Gets the number of bytes that follow the fixed header.
    /// </summary>
    internal int RemainingLength { get; } = remainingLength;

    /// <summary>
    /// Gets the packet type: <see cref="FirstByte" /> with its flag bits cleared.
    /// </summary>
    internal byte PacketType => (byte)(FirstByte & 0xF0);

    /// <summary>
    /// Gets a value indicating whether this is a DISCONNECT or PINGRESP that carries a
    /// body or sets a flag bit, both of which MQTT 3.1.1 sections 3.13.1 and 3.14.1 forbid.
    /// </summary>
    internal bool IsMalformedControlPacket =>
        PacketType is MqttPackets.DisconnectType or MqttPackets.PingResponseType
        && (RemainingLength != 0 || FirstByte != PacketType);
}
