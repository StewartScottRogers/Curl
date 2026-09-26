using System.Text;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// The MQTT 3.1.1 packet types a subscribe meets, and the packets it sends, built byte
/// for byte as curl 8.21.0 builds them.
/// </summary>
internal static class MqttPackets
{
    /// <summary>The packet-type nibble of PUBLISH, whatever its flags.</summary>
    internal const byte PublishType = 0x30;

    /// <summary>The packet-type nibble of SUBACK.</summary>
    internal const byte SubackType = 0x90;

    /// <summary>The packet-type nibble of PINGRESP.</summary>
    internal const byte PingResponseType = 0xD0;

    /// <summary>The packet-type nibble of DISCONNECT.</summary>
    internal const byte DisconnectType = 0xE0;

    /// <summary>
    /// The packet identifier of curl's one SUBSCRIBE: its per-connection counter, which
    /// starts at zero and is incremented before use.
    /// </summary>
    internal const byte SubscribePacketIdentifier = 1;

    private const byte ConnectType = 0x10;

    private const byte SubscribeType = 0x82;

    /// <summary>
    /// Builds the CONNECT curl sends: protocol name <c>MQTT</c>, level 4, the clean
    /// session flag, a 60-second keep-alive and the client identifier, with no user name
    /// or password.
    /// </summary>
    /// <param name="clientIdentifier">The client identifier, <c>curl</c> and eight characters.</param>
    /// <returns>The packet.</returns>
    internal static byte[] BuildConnect(string clientIdentifier) =>
        Assemble(
            ConnectType,
            [
                0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0x02, 0x00, 0x3C,
                .. LengthPrefixed(Encoding.ASCII.GetBytes(clientIdentifier)),
            ]);

    /// <summary>
    /// Builds the SUBSCRIBE curl sends: packet identifier 1 and one topic filter at QoS 0.
    /// </summary>
    /// <param name="topic">The decoded topic.</param>
    /// <returns>The packet.</returns>
    internal static byte[] BuildSubscribe(byte[] topic) =>
        Assemble(SubscribeType, [0x00, SubscribePacketIdentifier, .. LengthPrefixed(topic), 0x00]);

    /// <summary>
    /// Encodes a remaining length as MQTT 3.1.1 section 2.2.3 specifies: seven bits per
    /// byte, least significant first, the high bit set on every byte but the last.
    /// </summary>
    /// <param name="length">The length, zero or more.</param>
    /// <returns>One to four bytes for any length a packet here can have.</returns>
    internal static byte[] EncodeRemainingLength(int length)
    {
        List<byte> encoded = [];
        do
        {
            byte digit = (byte)(length % 0x80);
            length /= 0x80;
            encoded.Add(length > 0 ? (byte)(digit | 0x80) : digit);
        }
        while (length > 0);

        return [.. encoded];
    }

    private static byte[] Assemble(byte firstByte, byte[] body) =>
        [firstByte, .. EncodeRemainingLength(body.Length), .. body];

    private static byte[] LengthPrefixed(byte[] value) =>
        [(byte)(value.Length >> 8), (byte)value.Length, .. value];
}
