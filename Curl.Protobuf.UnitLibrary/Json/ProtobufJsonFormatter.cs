using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// Writes a <see cref="DynamicMessage" /> as JSON under the proto3 JSON mapping: fields by their
/// JSON names, default values of fields without presence and empty repeated and map fields
/// left out, and unknown fields dropped, since JSON has no form for them.
/// </summary>
public static class ProtobufJsonFormatter
{
    /// <summary>The message as JSON text, on one line or indented.</summary>
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
        foreach (var field in message.Type.Fields)
        {
            if (message.FindValue(field.Number) is { } value && IsWritten(field, value))
            {
                writer.WritePropertyName(ProtobufJsonNames.JsonName(field));
                WriteField(writer, field, value);
            }
        }

        writer.WriteEndObject();
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
                WriteMessage(writer, (DynamicMessage)value);
                break;
            case DynamicValueKind.Enum:
                ProtobufJsonScalars.WriteEnum(writer, field.EnumType!, (int)value);
                break;
            default:
                ProtobufJsonScalars.Write(writer, value);
                break;
        }
    }
}
