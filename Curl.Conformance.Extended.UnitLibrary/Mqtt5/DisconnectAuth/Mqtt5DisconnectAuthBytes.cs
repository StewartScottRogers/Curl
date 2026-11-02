namespace Curl.Conformance.Extended.Mqtt5.DisconnectAuth;

/// <summary>
/// Builds the server packets the DISCONNECT, AUTH and error handling cases (BL-4519) send, from OASIS MQTT Version
/// 5.0 sections 3.3, 3.9, 3.14 and 3.15: a SUBACK granting QoS 0, a QoS 0 PUBLISH, a DISCONNECT and an AUTH, each
/// with the fixed header flags given so a case can break the reserved ones on purpose.
/// </summary>
public static class Mqtt5DisconnectAuthBytes
{
    /// <summary>The Reason String property (section 3.14.2.2.3), a UTF-8 Encoded String.</summary>
    public const byte ReasonString = 0x1F;

    /// <summary>The Authentication Method property (section 3.15.2.2.2), a UTF-8 Encoded String.</summary>
    public const byte AuthenticationMethod = 0x15;

    /// <summary>Gets a SUBACK for Packet Identifier 1 granting QoS 0, the answer to curl's one SUBSCRIBE.</summary>
    /// <returns>The fixed header, the Packet Identifier, an empty property length and Reason Code 0x00.</returns>
    public static byte[] SubAckGrantingQos0() => Mqtt5WirePacket.Frame(0x90, 0x00, 0x01, 0x00, 0x00);

    /// <summary>Gets a QoS 0 PUBLISH of <paramref name="payload"/> to <paramref name="topic"/> with no properties.</summary>
    /// <param name="topic">The Topic Name, ASCII.</param>
    /// <param name="payload">The Application Message.</param>
    /// <returns>The fixed header, the Topic Name, an empty property length and the payload.</returns>
    public static byte[] Publish(string topic, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);
        return Mqtt5WirePacket.Frame(0x30, [(byte)(topic.Length >> 8), (byte)topic.Length, .. System.Text.Encoding.ASCII.GetBytes(topic), 0x00, .. payload]);
    }

    /// <summary>Gets a DISCONNECT (section 3.14).</summary>
    /// <param name="flags">The fixed header's low four bits; 0 is the only value section 3.14.1 allows.</param>
    /// <param name="reasonCode">The Disconnect Reason Code.</param>
    /// <param name="properties">Each property's encoded bytes, in order.</param>
    /// <returns>The fixed header, the Reason Code, the property length and the properties.</returns>
    public static byte[] Disconnect(byte flags, byte reasonCode, params byte[][] properties) =>
        WithReasonAndProperties((byte)(0xE0 | flags), reasonCode, properties);

    /// <summary>Gets an AUTH (section 3.15).</summary>
    /// <param name="flags">The fixed header's low four bits; 0 is the only value section 3.15.1 allows.</param>
    /// <param name="reasonCode">The Authenticate Reason Code.</param>
    /// <param name="properties">Each property's encoded bytes, in order.</param>
    /// <returns>The fixed header, the Reason Code, the property length and the properties.</returns>
    public static byte[] Auth(byte flags, byte reasonCode, params byte[][] properties) =>
        WithReasonAndProperties((byte)(0xF0 | flags), reasonCode, properties);

    /// <summary>Gets a property whose value is a UTF-8 Encoded String given as raw bytes, which may be ill-formed UTF-8.</summary>
    /// <param name="identifier">The property identifier.</param>
    /// <param name="value">The string's bytes, sent as they are.</param>
    /// <returns>The identifier, the two-byte length and the bytes.</returns>
    public static byte[] RawStringProperty(byte identifier, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return [identifier, (byte)(value.Length >> 8), (byte)value.Length, .. value];
    }

    private static byte[] WithReasonAndProperties(byte firstByte, byte reasonCode, byte[][] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        byte[] propertyBytes = [.. properties.SelectMany(property => property)];

        // Framing the properties as a packet puts their Variable Byte Integer length after one leading byte.
        byte[] lengthAndProperties = Mqtt5WirePacket.Frame(0, propertyBytes)[1..];
        return Mqtt5WirePacket.Frame(firstByte, [reasonCode, .. lengthAndProperties]);
    }
}
