namespace Curl.Conformance.Extended.Mqtt5;

/// <summary>
/// A CONNECT packet as the reference broker read it from the specification's section 3.1, checking every
/// statement there that binds the client.
/// </summary>
public sealed record Mqtt5ReceivedConnect
{
    // The readers of table 2-4's data types; declared first, as the tables below use them.
    private static readonly Action<Mqtt5WireReader> OneByte = reader => reader.ReadByte();

    private static readonly Action<Mqtt5WireReader> TwoByte = reader => reader.ReadTwoByteInteger();

    private static readonly Action<Mqtt5WireReader> FourByte = reader => reader.ReadFourByteInteger();

    private static readonly Action<Mqtt5WireReader> Utf8String = reader => reader.ReadUtf8String();

    private static readonly Action<Mqtt5WireReader> Binary = reader => reader.ReadBinaryData();

    private static readonly Action<Mqtt5WireReader> StringPair = reader => reader.ReadUtf8StringPair();

    // Table 2-4: the properties CONNECT carries (section 3.1.2.11) and their data types.
    private static readonly Dictionary<int, Action<Mqtt5WireReader>> ConnectPropertyTypes = new()
    {
        [0x11] = FourByte,
        [0x21] = TwoByte,
        [0x27] = FourByte,
        [0x22] = TwoByte,
        [0x19] = OneByte,
        [0x17] = OneByte,
        [0x26] = StringPair,
        [0x15] = Utf8String,
        [0x16] = Binary,
    };

    // Table 2-4: the Will Properties (section 3.1.3.2) and their data types.
    private static readonly Dictionary<int, Action<Mqtt5WireReader>> WillPropertyTypes = new()
    {
        [0x18] = FourByte,
        [0x01] = OneByte,
        [0x02] = FourByte,
        [0x03] = Utf8String,
        [0x08] = Utf8String,
        [0x09] = Binary,
        [0x26] = StringPair,
    };

    private const int UserProperty = 0x26;

    /// <summary>Gets the Connect Flags byte (section 3.1.2.3).</summary>
    public required byte Flags { get; init; }

    /// <summary>Gets whether Clean Start is set.</summary>
    public bool CleanStart => (Flags & 0x02) != 0;

    /// <summary>Gets the Keep Alive in seconds (section 3.1.2.10).</summary>
    public required int KeepAlive { get; init; }

    /// <summary>Gets the CONNECT properties' identifiers in the order sent.</summary>
    public required IReadOnlyList<int> PropertyIdentifiers { get; init; }

    /// <summary>Gets the Client Identifier (section 3.1.3.1).</summary>
    public required string ClientIdentifier { get; init; }

    /// <summary>Gets the Will Topic, or null without the Will Flag.</summary>
    public string? WillTopic { get; init; }

    /// <summary>Gets the User Name, or null without the User Name Flag.</summary>
    public string? UserName { get; init; }

    /// <summary>Gets the Password, or null without the Password Flag.</summary>
    public byte[]? Password { get; init; }

    /// <summary>Reads a CONNECT packet's body and checks it against section 3.1.</summary>
    /// <param name="body">The variable header and payload.</param>
    /// <returns>The fields.</returns>
    public static Mqtt5ReceivedConnect Parse(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        Mqtt5WireReader reader = new(body);
        string protocolName = reader.ReadUtf8String();
        Require(protocolName == "MQTT", "Mqtt5:MQTT-3.1.2-1", $"the Protocol Name is \"{protocolName}\".");
        int version = reader.ReadByte();
        Require(version == 5, "Mqtt5:MQTT5-3.1.2.2-1", $"the Protocol Version is {version}.");
        byte flags = reader.ReadByte();
        CheckFlags(flags);
        int keepAlive = reader.ReadTwoByteInteger();
        IReadOnlyList<int> properties = ReadProperties(reader, ConnectPropertyTypes);
        Mqtt5ReceivedConnect connect = new()
        {
            Flags = flags,
            KeepAlive = keepAlive,
            PropertyIdentifiers = properties,
            ClientIdentifier = reader.ReadUtf8String(),
            WillTopic = ReadWill(reader, flags),
            UserName = (flags & 0x80) != 0 ? reader.ReadUtf8String() : null,
            Password = (flags & 0x40) != 0 ? reader.ReadBinaryData() : null,
        };
        Require(reader.AtEnd, "Mqtt5:MQTT-3.1.3-1", "bytes follow the last payload field the Connect Flags announce.");
        return connect;
    }

    private static void CheckFlags(byte flags)
    {
        Require((flags & 0x01) == 0, "Mqtt5:MQTT-3.1.2-3", "the reserved Connect Flag is set.");
        bool will = (flags & 0x04) != 0;
        int willQos = (flags >> 3) & 0x03;
        Require(will || willQos == 0, "Mqtt5:MQTT-3.1.2-11", $"Will QoS is {willQos} without the Will Flag.");
        Require(willQos != 3, "Mqtt5:MQTT-3.1.2-12", "Will QoS is 3.");
        Require(will || (flags & 0x20) == 0, "Mqtt5:MQTT-3.1.2-13", "Will Retain is set without the Will Flag.");
    }

    private static string? ReadWill(Mqtt5WireReader reader, byte flags)
    {
        if ((flags & 0x04) == 0)
        {
            return null;
        }

        ReadProperties(reader, WillPropertyTypes);
        string topic = reader.ReadUtf8String();
        reader.ReadBinaryData();
        return topic;
    }

    private static List<int> ReadProperties(Mqtt5WireReader reader, Dictionary<int, Action<Mqtt5WireReader>> allowed)
    {
        Mqtt5WireReader properties = new(reader.Take(reader.ReadVariableByteInteger()));
        List<int> identifiers = [];
        while (!properties.AtEnd)
        {
            int identifier = properties.ReadVariableByteInteger();
            Require(allowed.TryGetValue(identifier, out Action<Mqtt5WireReader>? readValue), "Mqtt5:MQTT5-2.2.2-1", $"property 0x{identifier:X2} is not one this packet carries (table 2-4).");
            Require(identifier == UserProperty || !identifiers.Contains(identifier), "Mqtt5:MQTT5-2.2.2-1", $"property 0x{identifier:X2} appears twice.");
            readValue!(properties);
            identifiers.Add(identifier);
        }

        return identifiers;
    }

    private static void Require(bool condition, string requirementId, string detail)
    {
        if (!condition)
        {
            throw new Mqtt5ConformanceViolationException(requirementId, detail);
        }
    }
}
