using System.Text;

namespace Curl.Conformance.Extended.Mqtt5.Connack;

/// <summary>
/// Builds the CONNACK packets the reference broker answers CONNECT with in the CONNACK cases (BL-4518), from
/// OASIS MQTT Version 5.0 section 3.2: the acknowledge flags, the Connect Reason Code and the properties.
/// </summary>
public static class Mqtt5ConnAckBytes
{
    /// <summary>The Assigned Client Identifier property (section 3.2.2.3.7), a UTF-8 Encoded String.</summary>
    public const byte AssignedClientIdentifier = 0x12;

    /// <summary>The Server Keep Alive property (section 3.2.2.3.14), a Two Byte Integer.</summary>
    public const byte ServerKeepAlive = 0x13;

    /// <summary>The Receive Maximum property (section 3.2.2.3.3), a Two Byte Integer.</summary>
    public const byte ReceiveMaximum = 0x21;

    /// <summary>The Topic Alias Maximum property (section 3.2.2.3.8), a Two Byte Integer.</summary>
    public const byte TopicAliasMaximum = 0x22;

    /// <summary>The Maximum QoS property (section 3.2.2.3.4), a Byte.</summary>
    public const byte MaximumQos = 0x24;

    /// <summary>The Retain Available property (section 3.2.2.3.5), a Byte.</summary>
    public const byte RetainAvailable = 0x25;

    /// <summary>The Maximum Packet Size property (section 3.2.2.3.6), a Four Byte Integer.</summary>
    public const byte MaximumPacketSize = 0x27;

    /// <summary>Gets a CONNACK's bytes.</summary>
    /// <param name="acknowledgeFlags">The Connect Acknowledge Flags byte: bit 0 is Session Present, bits 7-1 reserved.</param>
    /// <param name="reasonCode">The Connect Reason Code.</param>
    /// <param name="properties">Each property's encoded bytes, in order.</param>
    /// <returns>The fixed header, the variable header and its properties.</returns>
    public static byte[] Build(byte acknowledgeFlags, byte reasonCode, params byte[][] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        byte[] propertyBytes = [.. properties.SelectMany(property => property)];

        // Framing the properties as a packet puts their Variable Byte Integer length after one leading byte.
        byte[] lengthAndProperties = Mqtt5WirePacket.Frame(0, propertyBytes)[1..];
        return Mqtt5WirePacket.Frame(0x20, [acknowledgeFlags, reasonCode, .. lengthAndProperties]);
    }

    /// <summary>Gets a property whose value is a Byte.</summary>
    /// <param name="identifier">The property identifier.</param>
    /// <param name="value">The value.</param>
    /// <returns>The identifier and the value.</returns>
    public static byte[] ByteProperty(byte identifier, byte value) => [identifier, value];

    /// <summary>Gets a property whose value is a Two Byte Integer, big-endian (section 1.5.2).</summary>
    /// <param name="identifier">The property identifier.</param>
    /// <param name="value">The value.</param>
    /// <returns>The identifier and the value.</returns>
    public static byte[] TwoByteProperty(byte identifier, ushort value) => [identifier, (byte)(value >> 8), (byte)value];

    /// <summary>Gets a property whose value is a Four Byte Integer, big-endian (section 1.5.3).</summary>
    /// <param name="identifier">The property identifier.</param>
    /// <param name="value">The value.</param>
    /// <returns>The identifier and the value.</returns>
    public static byte[] FourByteProperty(byte identifier, uint value) =>
        [identifier, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    /// <summary>Gets a property whose value is a UTF-8 Encoded String (section 1.5.4).</summary>
    /// <param name="identifier">The property identifier.</param>
    /// <param name="value">The value.</param>
    /// <returns>The identifier, the two-byte length and the UTF-8 bytes.</returns>
    public static byte[] StringProperty(byte identifier, string value)
    {
        byte[] text = Encoding.UTF8.GetBytes(value);
        return [identifier, (byte)(text.Length >> 8), (byte)text.Length, .. text];
    }
}
