using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// Writes a <see cref="DynamicMessage" /> as JSON under the proto3 JSON mapping: fields by their
/// JSON names, default values of fields without presence and empty repeated and map fields
/// left out, and unknown fields dropped, since JSON has no form for them. The well-known types
/// are written in their own forms: Timestamp and Duration as strings (<see cref="ProtobufJsonTimeTypes" />),
/// the wrapper types as their wrapped values (<see cref="ProtobufJsonWrapperTypes" />), FieldMask as
/// one string of paths (<see cref="ProtobufJsonFieldMask" />), and Struct, Value, ListValue and
/// NullValue as any JSON object, value, array and <c>null</c> (<see cref="ProtobufJsonStructTypes" />).
/// </summary>
public static class ProtobufJsonFormatter
{
    /// <summary>
    /// The message as JSON text, on one line or indented. Throws <see cref="FormatException" />
    /// when a Timestamp or Duration field holds a value outside its type's range, or an Any's
    /// type URL or value is not a message type of the catalogue and a message of it.
    /// </summary>
    public static string Format(DynamicMessage message, bool indented)
    {
        ArgumentNullException.ThrowIfNull(message);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = indented, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            WriteMessage(writer, message);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteMessage(Utf8JsonWriter writer, DynamicMessage message)
    {
        writer.WriteStartObject();
        WriteFields(writer, message);
        writer.WriteEndObject();
    }

    /// <summary>Writes the message's fields as members of the object being written.</summary>
    internal static void WriteFields(Utf8JsonWriter writer, DynamicMessage message)
    {
        foreach (var field in message.Type.Fields)
        {
            if (message.FindValue(field.Number) is { } value && IsWritten(field, value))
            {
                writer.WritePropertyName(ProtobufJsonNames.JsonName(field));
                WriteField(writer, field, value);
            }
        }
    }

    private static bool IsWritten(DynamicField field, object value) => value switch
    {
        List<object> items => items.Count > 0,
        Dictionary<object, object> map => map.Count > 0,
        _ => field.HasPresence || !IsDefault(field, value),
    };

    private static bool IsDefault(DynamicField field, object value) => field.Kind switch
    {
        DynamicValueKind.String => ((string)value).Length == 0,
        DynamicValueKind.Bytes => ((byte[])value).Length == 0,
        _ => field.Codec!.ToWire(value) == 0,
    };

    private static void WriteField(Utf8JsonWriter writer, DynamicField field, object value)
    {
        if (field.IsMap)
        {
            WriteMap(writer, field.MessageType!.FindField(2)!, (Dictionary<object, object>)value);
        }
        else if (field.IsRepeated)
        {
            writer.WriteStartArray();
            foreach (var item in (List<object>)value)
            {
                WriteValue(writer, field, item);
            }

            writer.WriteEndArray();
        }
        else
        {
            WriteValue(writer, field, value);
        }
    }

    private static void WriteMap(Utf8JsonWriter writer, DynamicField valueField, Dictionary<object, object> map)
    {
        writer.WriteStartObject();
        foreach (var (key, value) in map)
        {
            writer.WritePropertyName(ProtobufJsonScalars.MapKeyText(key));
            WriteValue(writer, valueField, value);
        }

        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, DynamicField field, object value)
    {
        switch (field.Kind)
        {
            case DynamicValueKind.Message:
                WriteMessageValue(writer, (DynamicMessage)value);
                break;
            case DynamicValueKind.Enum when ProtobufJsonStructTypes.IsNullValue(field):
                writer.WriteNullValue();
                break;
            case DynamicValueKind.Enum:
                ProtobufJsonScalars.WriteEnum(writer, field.EnumType!, (int)value);
                break;
            default:
                ProtobufJsonScalars.Write(writer, value);
                break;
        }
    }

    /// <summary>A well-known type in its own JSON form, or any other message as an object.</summary>
    internal static void WriteMessageValue(Utf8JsonWriter writer, DynamicMessage message)
    {
        if (ProtobufJsonTimeTypes.HasStringForm(message.Type))
        {
            writer.WriteStringValue(ProtobufJsonTimeTypes.Format(message));
        }
        else if (ProtobufJsonWrapperTypes.IsWrapper(message.Type))
        {
            ProtobufJsonWrapperTypes.Write(writer, message);
        }
        else if (ProtobufJsonFieldMask.IsFieldMask(message.Type))
        {
            writer.WriteStringValue(ProtobufJsonFieldMask.Format(message));
        }
        else if (ProtobufJsonStructTypes.IsStructType(message.Type))
        {
            ProtobufJsonStructTypes.Write(writer, message);
        }
        else if (ProtobufJsonAny.IsAny(message.Type))
        {
            ProtobufJsonAny.Write(writer, message);
        }
        else
        {
            WriteMessage(writer, message);
        }
    }
}
