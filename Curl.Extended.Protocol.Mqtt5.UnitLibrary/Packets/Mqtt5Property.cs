using Curl.Extended.Protocol.Mqtt5.Encoding;

namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>
/// One MQTT 5 property (section 2.2.2): its identifier and the value of the identifier's type, an integer, a string,
/// binary data, or for a User Property a name (<see cref="StringValue"/>) and value (<see cref="PairValue"/>).
/// </summary>
internal sealed class Mqtt5Property
{
    private Mqtt5Property(Mqtt5PropertyIdentifier identifier, uint integerValue, string? stringValue, string? pairValue, byte[]? binaryValue)
    {
        Identifier = identifier;
        IntegerValue = integerValue;
        StringValue = stringValue;
        PairValue = pairValue;
        BinaryValue = binaryValue;
    }

    /// <summary>The property identifier.</summary>
    public Mqtt5PropertyIdentifier Identifier { get; }

    /// <summary>The value of a Byte, Two Byte, Four Byte or Variable Byte Integer property; 0 otherwise.</summary>
    public uint IntegerValue { get; }

    /// <summary>The value of a UTF-8 Encoded String property, or a User Property's name.</summary>
    public string? StringValue { get; }

    /// <summary>A User Property's value.</summary>
    public string? PairValue { get; }

    /// <summary>The value of a Binary Data property.</summary>
    public byte[]? BinaryValue { get; }

    /// <summary>An integer property; the identifier must be of an integer type and the value must fit it.</summary>
    public static Mqtt5Property FromInteger(Mqtt5PropertyIdentifier identifier, uint value)
    {
        uint maximum = Mqtt5PropertyDefinitions.TypeOf(identifier) switch
        {
            Mqtt5PropertyType.Byte => byte.MaxValue,
            Mqtt5PropertyType.TwoByteInteger => ushort.MaxValue,
            Mqtt5PropertyType.FourByteInteger => uint.MaxValue,
            Mqtt5PropertyType.VariableByteInteger => Mqtt5VariableByteInteger.MaximumValue,
            _ => throw new ArgumentException($"Property {identifier} does not hold an integer.", nameof(identifier)),
        };
        return value <= maximum
            ? new(identifier, value, null, null, null)
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Property {identifier} holds at most {maximum}.");
    }

    /// <summary>A UTF-8 Encoded String property.</summary>
    public static Mqtt5Property FromString(Mqtt5PropertyIdentifier identifier, string value)
    {
        RequireType(identifier, Mqtt5PropertyType.Utf8String);
        return new(identifier, 0, value, null, null);
    }

    /// <summary>A Binary Data property.</summary>
    public static Mqtt5Property FromBinary(Mqtt5PropertyIdentifier identifier, byte[] value)
    {
        RequireType(identifier, Mqtt5PropertyType.BinaryData);
        return new(identifier, 0, null, null, value);
    }

    /// <summary>A User Property (0x26), a name and value pair.</summary>
    public static Mqtt5Property UserProperty(string name, string value) =>
        new(Mqtt5PropertyIdentifier.UserProperty, 0, name, value, null);

    private static void RequireType(Mqtt5PropertyIdentifier identifier, Mqtt5PropertyType type)
    {
        if (Mqtt5PropertyDefinitions.TypeOf(identifier) != type)
        {
            throw new ArgumentException($"Property {identifier} does not hold {type}.", nameof(identifier));
        }
    }
}
