using System.Text;

namespace Curl.Conformance.Extended.Mqtt5.PublishSubscribe;

/// <summary>
/// Builds the packets the reference broker sends in the PUBLISH, QoS and SUBSCRIBE cases (BL-4485), from OASIS MQTT
/// Version 5.0 sections 3.3 to 3.13: PUBLISH at any QoS with properties, PUBACK, PUBREL, SUBACK and PINGRESP.
/// </summary>
public static class Mqtt5PublishSubscribeBytes
{
    /// <summary>The Payload Format Indicator property (section 3.3.2.3.2), a Byte.</summary>
    public const byte PayloadFormatIndicator = 0x01;

    /// <summary>The Message Expiry Interval property (section 3.3.2.3.3), a Four Byte Integer.</summary>
    public const byte MessageExpiryInterval = 0x02;

    /// <summary>The Content Type property (section 3.3.2.3.9), a UTF-8 Encoded String.</summary>
    public const byte ContentType = 0x03;

    /// <summary>The Response Topic property (section 3.3.2.3.5), a UTF-8 Encoded String.</summary>
    public const byte ResponseTopic = 0x08;

    /// <summary>The Correlation Data property (section 3.3.2.3.6), Binary Data.</summary>
    public const byte CorrelationData = 0x09;

    /// <summary>The Subscription Identifier property (section 3.3.2.3.8), a Variable Byte Integer.</summary>
    public const byte SubscriptionIdentifier = 0x0B;

    /// <summary>The Topic Alias property (section 3.3.2.3.4), a Two Byte Integer.</summary>
    public const byte TopicAlias = 0x23;

    /// <summary>The User Property property (section 3.3.2.3.7), a UTF-8 String Pair.</summary>
    public const byte UserProperty = 0x26;

    /// <summary>A PINGRESP (section 3.13).</summary>
    public static readonly byte[] PingResp = [0xD0, 0x00];

    /// <summary>Gets a PUBLISH (section 3.3) with DUP and RETAIN clear.</summary>
    /// <param name="qosBits">The two QoS bits of the fixed header, 0 to 3; 3 is the malformed value on purpose.</param>
    /// <param name="packetIdentifier">The Packet Identifier, sent only when <paramref name="qosBits"/> is not 0.</param>
    /// <param name="topic">The Topic Name, which may be empty.</param>
    /// <param name="payload">The Application Message.</param>
    /// <param name="properties">Each property's encoded bytes, in order.</param>
    /// <returns>The fixed header, the variable header with its properties and the payload.</returns>
    public static byte[] Publish(int qosBits, ushort packetIdentifier, string topic, byte[] payload, params byte[][] properties)
    {
        ArgumentNullException.ThrowIfNull(payload);
        byte[] identifier = qosBits == 0 ? [] : [(byte)(packetIdentifier >> 8), (byte)packetIdentifier];
        return Mqtt5WirePacket.Frame((byte)(0x30 | (qosBits << 1)), [.. Utf8String(topic), .. identifier, .. PropertyBlock(properties), .. payload]);
    }

    /// <summary>Gets a SUBACK (section 3.9) with no properties.</summary>
    /// <param name="packetIdentifier">The SUBSCRIBE's Packet Identifier.</param>
    /// <param name="reasonCodes">One Reason Code per Topic Filter.</param>
    /// <returns>The fixed header, the Packet Identifier, an empty property length and the reason codes.</returns>
    public static byte[] SubAck(ushort packetIdentifier, params byte[] reasonCodes) =>
        Mqtt5WirePacket.Frame(0x90, [(byte)(packetIdentifier >> 8), (byte)packetIdentifier, 0x00, .. reasonCodes]);

    /// <summary>Gets a PUBACK (section 3.4) with a Reason Code and no properties.</summary>
    /// <param name="packetIdentifier">The PUBLISH's Packet Identifier.</param>
    /// <param name="reasonCode">The PUBACK Reason Code.</param>
    /// <returns>The fixed header, the Packet Identifier and the Reason Code.</returns>
    public static byte[] PubAck(ushort packetIdentifier, byte reasonCode) =>
        Mqtt5WirePacket.Frame(0x40, (byte)(packetIdentifier >> 8), (byte)packetIdentifier, reasonCode);

    /// <summary>Gets a PUBREL (section 3.6) with its reserved flags 0010 and Reason Code 0x00 left out.</summary>
    /// <param name="packetIdentifier">The PUBREC's Packet Identifier.</param>
    /// <returns>The fixed header and the Packet Identifier.</returns>
    public static byte[] PubRel(ushort packetIdentifier) =>
        Mqtt5WirePacket.Frame(0x62, (byte)(packetIdentifier >> 8), (byte)packetIdentifier);

    /// <summary>Gets a property whose value is a UTF-8 String Pair (section 1.5.7) or, given one string, Binary Data or a UTF-8 Encoded String.</summary>
    /// <param name="identifier">The property identifier.</param>
    /// <param name="values">The strings, each written with its two-byte length.</param>
    /// <returns>The identifier and each length-prefixed string.</returns>
    public static byte[] StringsProperty(byte identifier, params string[] values) =>
        [identifier, .. values.SelectMany(Utf8String)];

    private static byte[] Utf8String(string value)
    {
        byte[] text = Encoding.UTF8.GetBytes(value);
        return [(byte)(text.Length >> 8), (byte)text.Length, .. text];
    }

    private static byte[] PropertyBlock(byte[][] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        // Framing the properties as a packet puts their Variable Byte Integer length after one leading byte.
        return Mqtt5WirePacket.Frame(0, [.. properties.SelectMany(property => property)])[1..];
    }
}
