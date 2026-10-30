using Curl.Protobuf.Wire;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>
/// Writes a <see cref="DynamicMessage" /> as Protocol Buffers binary: known fields in field
/// number order, then the unknown fields as they were read. A proto3 field with implicit
/// presence is not written when it holds its default; repeated numeric fields are packed.
/// </summary>
public static class DynamicMessageEncoder
{
    /// <summary>The message's binary encoding.</summary>
    public static byte[] Encode(DynamicMessage message)
    {
        var writer = new ProtobufWriter();
        Write(writer, message);
        return writer.ToArray();
    }

    /// <summary>Writes the message's fields to a writer, with no length before them.</summary>
    public static void Write(ProtobufWriter writer, DynamicMessage message)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(message);
        foreach (var field in message.Type.Fields)
        {
            if (message.FindValue(field.Number) is { } value)
            {
                WriteField(writer, field, value);
            }
        }

        foreach (var unknownField in message.UnknownFields)
        {
            writer.WriteField(unknownField);
        }
    }

    private static void WriteField(ProtobufWriter writer, DynamicField field, object value)
    {
        if (field.IsMap)
        {
            WriteMap(writer, field, (Dictionary<object, object>)value);
        }
        else if (field.IsPacked)
        {
            WritePacked(writer, field, (List<object>)value);
        }
        else if (field.IsRepeated)
        {
            WriteUnpacked(writer, field, (List<object>)value);
        }
        else if (field.HasPresence || !IsDefault(field, value))
        {
            WriteValue(writer, field, value);
        }
    }

    private static void WriteUnpacked(ProtobufWriter writer, DynamicField field, List<object> items)
    {
        foreach (var item in items)
        {
            WriteValue(writer, field, item);
        }
    }

    private static void WriteMap(ProtobufWriter writer, DynamicField field, Dictionary<object, object> map)
    {
        foreach (var (key, value) in map)
        {
            var entry = new DynamicMessage(field.MessageType!);
            entry.SetValue("key", key);
            entry.SetValue("value", value);
            writer.WriteBytesField(field.Number, Encode(entry));
        }
    }

    private static void WritePacked(ProtobufWriter writer, DynamicField field, List<object> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var packed = new ProtobufWriter();
        foreach (var item in items)
        {
            WriteScalar(packed, field.Codec!, item);
        }

        writer.WriteBytesField(field.Number, packed.WrittenBytes);
    }

    private static void WriteValue(ProtobufWriter writer, DynamicField field, object value)
    {
        switch (field.Kind)
        {
            case DynamicValueKind.String:
                writer.WriteStringField(field.Number, (string)value);
                break;
            case DynamicValueKind.Bytes:
                writer.WriteBytesField(field.Number, (byte[])value);
                break;
            case DynamicValueKind.Message:
                writer.WriteBytesField(field.Number, Encode((DynamicMessage)value));
                break;
            default:
                writer.WriteTag(field.Number, field.Codec!.WireType);
                WriteScalar(writer, field.Codec, value);
                break;
        }
    }

    private static void WriteScalar(ProtobufWriter writer, DynamicScalarCodec codec, object value)
    {
        var raw = codec.ToWire(value);
        if (codec.WireType == ProtobufWireType.Varint)
        {
            writer.WriteVarint(raw);
        }
        else if (codec.WireType == ProtobufWireType.Fixed64)
        {
            writer.WriteFixed64(raw);
        }
        else
        {
            writer.WriteFixed32(unchecked((uint)raw));
        }
    }

    private static bool IsDefault(DynamicField field, object value) => field.Kind switch
    {
        DynamicValueKind.String => ((string)value).Length == 0,
        DynamicValueKind.Bytes => ((byte[])value).Length == 0,
        _ => field.Codec!.ToWire(value) == 0,
    };
}
