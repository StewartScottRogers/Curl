using Curl.Extended.Protocol.Mqtt5.Encoding;

namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>
/// Reads and writes a property section (MQTT 5 section 2.2.2): a Variable Byte Integer length, then each property's
/// identifier and value. A property not valid where it appears is a Malformed Packet; a second copy of one that may
/// appear once is a Protocol Error.
/// </summary>
internal static class Mqtt5PropertyCodec
{
    /// <summary>Reads a property section valid in <paramref name="scope"/>.</summary>
    public static IReadOnlyList<Mqtt5Property> Read(Mqtt5Reader reader, Mqtt5PropertyScope scope)
    {
        Mqtt5Reader section = reader.ReadSection(reader.ReadVariableByteInteger());
        var properties = new List<Mqtt5Property>();
        var seen = new HashSet<Mqtt5PropertyIdentifier>();
        while (!section.IsAtEnd)
        {
            var identifier = (Mqtt5PropertyIdentifier)section.ReadVariableByteInteger();
            Mqtt5PropertyType type = Check(identifier, scope, seen);
            properties.Add(ReadValue(section, identifier, type));
        }

        return properties;
    }

    /// <summary>Writes <paramref name="properties"/> as a property section valid in <paramref name="scope"/>.</summary>
    public static void Write(Mqtt5Writer writer, IReadOnlyList<Mqtt5Property> properties, Mqtt5PropertyScope scope)
    {
        var section = new Mqtt5Writer();
        var seen = new HashSet<Mqtt5PropertyIdentifier>();
        foreach (Mqtt5Property property in properties)
        {
            Mqtt5PropertyType type = Check(property.Identifier, scope, seen);
            section.WriteVariableByteInteger((int)property.Identifier);
            WriteValue(section, property, type);
        }

        writer.WriteVariableByteInteger(section.Length);
        writer.WriteBytes(section.ToArray());
    }

    private static Mqtt5PropertyType Check(Mqtt5PropertyIdentifier identifier, Mqtt5PropertyScope scope, HashSet<Mqtt5PropertyIdentifier> seen)
    {
        Mqtt5PropertyDefinition definition = Mqtt5PropertyDefinitions.Get(identifier, scope);
        if (!seen.Add(identifier) && (definition.RepeatScope & scope) == 0)
        {
            throw Mqtt5MalformedPacketException.ProtocolError($"Property {identifier} appears more than once.");
        }

        return definition.Type;
    }

    private static Mqtt5Property ReadValue(Mqtt5Reader reader, Mqtt5PropertyIdentifier identifier, Mqtt5PropertyType type) => type switch
    {
        Mqtt5PropertyType.Byte => Mqtt5Property.FromInteger(identifier, reader.ReadByte()),
        Mqtt5PropertyType.TwoByteInteger => Mqtt5Property.FromInteger(identifier, reader.ReadTwoByteInteger()),
        Mqtt5PropertyType.FourByteInteger => Mqtt5Property.FromInteger(identifier, reader.ReadFourByteInteger()),
        Mqtt5PropertyType.VariableByteInteger => Mqtt5Property.FromInteger(identifier, (uint)reader.ReadVariableByteInteger()),
        Mqtt5PropertyType.Utf8String => Mqtt5Property.FromString(identifier, reader.ReadUtf8String()),
        Mqtt5PropertyType.BinaryData => Mqtt5Property.FromBinary(identifier, reader.ReadBinaryData()),
        _ => ReadUserProperty(reader),
    };

    private static Mqtt5Property ReadUserProperty(Mqtt5Reader reader)
    {
        KeyValuePair<string, string> pair = reader.ReadUtf8StringPair();
        return Mqtt5Property.UserProperty(pair.Key, pair.Value);
    }

    private static void WriteValue(Mqtt5Writer writer, Mqtt5Property property, Mqtt5PropertyType type)
    {
        switch (type)
        {
            case Mqtt5PropertyType.Byte:
                writer.WriteByte((byte)property.IntegerValue);
                break;
            case Mqtt5PropertyType.TwoByteInteger:
                writer.WriteTwoByteInteger((ushort)property.IntegerValue);
                break;
            case Mqtt5PropertyType.FourByteInteger:
                writer.WriteFourByteInteger(property.IntegerValue);
                break;
            case Mqtt5PropertyType.VariableByteInteger:
                writer.WriteVariableByteInteger((int)property.IntegerValue);
                break;
            case Mqtt5PropertyType.BinaryData:
                writer.WriteBinaryData(property.BinaryValue!);
                break;
            default:
                WriteStrings(writer, property);
                break;
        }
    }

    private static void WriteStrings(Mqtt5Writer writer, Mqtt5Property property)
    {
        writer.WriteUtf8String(property.StringValue!);
        if (property.PairValue is not null)
        {
            writer.WriteUtf8String(property.PairValue);
        }
    }
}
