using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// The MQTT 3.1.1 packet types a transfer meets, and the packets it sends, built byte for
/// byte as curl 8.21.0 builds them.
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

    private const byte CleanSessionFlag = 0x02;

    private const byte PasswordFlag = 0x40;

    private const byte UserNameFlag = 0x80;

    /// <summary>The most bytes a user name or password's two-byte length field can carry.</summary>
    private const int MaximumCredentialLength = 0xFFFF;

    /// <summary>
    /// The largest remaining length curl publishes: its <c>MAX_MQTT_MESSAGE_SIZE</c>,
    /// 0xFFFFFFF, less the first byte and the four bytes a length that large encodes to.
    /// </summary>
    private const long MaximumPublishRemainingLength = 0xFFFFFFF - 1 - 4;

    /// <summary>
    /// Builds the CONNECT curl sends: protocol name <c>MQTT</c>, level 4, the clean
    /// session flag, a 60-second keep-alive and the client identifier, then the user name
    /// and the password, each only when it is not empty and each flagged when present.
    /// </summary>
    /// <param name="clientIdentifier">The client identifier, <c>curl</c> and eight characters.</param>
    /// <param name="credentials">The user name and password, or <see langword="null" /> for neither.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="MqttTransferException">
    /// The user name or the password is over 65535 bytes in UTF-8 (exit 8, as curl).
    /// </exception>
    internal static byte[] BuildConnect(string clientIdentifier, NetworkCredential? credentials)
    {
        byte[] userName = EncodeCredential(credentials?.UserName, MqttTransferMessages.UserNameTooLong);
        byte[] password = EncodeCredential(credentials?.Password, MqttTransferMessages.PasswordTooLong);
        byte flags = CleanSessionFlag;
        flags |= userName.Length > 0 ? UserNameFlag : (byte)0;
        flags |= password.Length > 0 ? PasswordFlag : (byte)0;

        return Assemble(
            ConnectType,
            [
                0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, flags, 0x00, 0x3C,
                .. LengthPrefixed(Encoding.ASCII.GetBytes(clientIdentifier)),
                .. LengthPrefixedUnlessEmpty(userName),
                .. LengthPrefixedUnlessEmpty(password),
            ]);
    }

    /// <summary>
    /// Builds the PUBLISH curl sends for <c>-d</c>: QoS 0, no retain, no packet identifier,
    /// the topic and then the payload.
    /// </summary>
    /// <param name="topic">The decoded topic.</param>
    /// <param name="payload">The payload, sent as given.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="MqttTransferException">
    /// The packet would be over 268435455 bytes, curl's limit (exit 100).
    /// </exception>
    internal static byte[] BuildPublish(byte[] topic, ReadOnlyMemory<byte> payload)
    {
        long remainingLength = 2L + topic.Length + payload.Length;
        if (remainingLength > MaximumPublishRemainingLength)
        {
            throw new MqttTransferException(CurlExitCode.TooLarge, MqttTransferMessages.TooLarge);
        }

        return Assemble(PublishType, [.. LengthPrefixed(topic), .. payload.Span]);
    }

    /// <summary>
    /// Builds the DISCONNECT curl sends once its PUBLISH is sent.
    /// </summary>
    /// <returns>The packet: <c>E0 00</c>.</returns>
    internal static byte[] BuildDisconnect() => [DisconnectType, 0x00];

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

    private static byte[] EncodeCredential(string? value, Func<int, string> tooLongMessage)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(value ?? string.Empty);
        if (encoded.Length > MaximumCredentialLength)
        {
            throw new MqttTransferException(CurlExitCode.WeirdServerReply, tooLongMessage(encoded.Length));
        }

        return encoded;
    }

    private static byte[] LengthPrefixedUnlessEmpty(byte[] value) =>
        value.Length > 0 ? LengthPrefixed(value) : [];

    private static byte[] LengthPrefixed(byte[] value) =>
        [(byte)(value.Length >> 8), (byte)value.Length, .. value];
}
