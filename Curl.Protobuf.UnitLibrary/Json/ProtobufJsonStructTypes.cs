using System.Text.Json;
using Curl.Protobuf.DynamicMessages;
using Curl.Protobuf.Wire;

namespace Curl.Protobuf.Json;

/// <summary>
/// The JSON forms of <c>google.protobuf.Struct</c> (any JSON object), <c>google.protobuf.Value</c>
/// (any JSON value), <c>google.protobuf.ListValue</c> (any JSON array) and the enum
/// <c>google.protobuf.NullValue</c> (<c>null</c>). Struct and ListValue nest at most
/// <see cref="ProtobufReader.MaximumDepth" /> deep, as other messages do.
/// </summary>
internal static class ProtobufJsonStructTypes
{
    private const string StructName = "google.protobuf.Struct";
    private const string ValueName = "google.protobuf.Value";
    private const string ListValueName = "google.protobuf.ListValue";
    private const int NullValueNumber = 1;
    private const int NumberValueNumber = 2;
    private const int StringValueNumber = 3;
    private const int BoolValueNumber = 4;
    private const int StructValueNumber = 5;
    private const int ListValueNumber = 6;

    /// <summary>Whether messages of this type are a Struct, a Value or a ListValue.</summary>
    public static bool IsStructType(DynamicMessageType type) => type.FullName is StructName or ValueName or ListValueName;

    /// <summary>
    /// Whether a field is of the enum <c>google.protobuf.NullValue</c>, written as <c>null</c>. An enum descriptor
    /// keeps only its own name, so it is recognised by its shape: named NullValue, with the one value NULL_VALUE.
    /// </summary>
    public static bool IsNullValue(DynamicField field) =>
        field.EnumType is { Name: "NullValue", Values: [{ Name: "NULL_VALUE" }] };

    /// <summary>Whether a JSON <c>null</c> is a value of this field rather than leaving it unset: a singular Value or NullValue field.</summary>
    public static bool AcceptsNull(DynamicField field) =>
        !field.IsRepeated && (IsNullValue(field) || field.MessageType?.FullName == ValueName);

    /// <summary>Writes a Struct, Value or ListValue; throws <see cref="FormatException" /> for a Value with no kind set.</summary>
    public static void Write(Utf8JsonWriter writer, DynamicMessage message)
    {
        switch (message.Type.FullName)
        {
            case StructName:
                WriteStruct(writer, message);
                break;
            case ListValueName:
                WriteList(writer, message);
                break;
            default:
                WriteValue(writer, message);
                break;
        }
    }

    /// <summary>Reads a Struct, Value or ListValue from any JSON value of its kind.</summary>
    public static ProtobufJsonError TryRead(JsonElement element, DynamicMessage message, int depth) => message.Type.FullName switch
    {
        StructName => TryReadStruct(element, message, depth),
        ListValueName => TryReadList(element, message, depth),
        _ => TryReadValue(element, message, depth),
    };

    /// <summary>Reads a NullValue: <c>null</c>, or the enum's own name or number.</summary>
    public static ProtobufJsonError TryReadNullValue(JsonElement element, DynamicField field, out object? value)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            value = 0;
            return ProtobufJsonError.None;
        }

        return ProtobufJsonScalars.TryReadEnum(element, field.EnumType!, out value);
    }

    private static void WriteStruct(Utf8JsonWriter writer, DynamicMessage message)
    {
        writer.WriteStartObject();
        foreach (var (key, value) in (message.FindValue(1) as Dictionary<object, object>) ?? [])
        {
            writer.WritePropertyName((string)key);
            WriteValue(writer, (DynamicMessage)value);
        }

        writer.WriteEndObject();
    }

    private static void WriteList(Utf8JsonWriter writer, DynamicMessage message)
    {
        writer.WriteStartArray();
        foreach (var value in (message.FindValue(1) as List<object>) ?? [])
        {
            WriteValue(writer, (DynamicMessage)value);
        }

        writer.WriteEndArray();
    }

    private static void WriteValue(Utf8JsonWriter writer, DynamicMessage message)
    {
        var kind = message.Type.Fields.FirstOrDefault(field => message.FindValue(field.Number) is not null)
            ?? throw new FormatException("google.protobuf.Value has no kind set.");
        var value = message.FindValue(kind.Number)!;
        switch (kind.Number)
        {
            case NullValueNumber:
                writer.WriteNullValue();
                break;
            case StructValueNumber:
                WriteStruct(writer, (DynamicMessage)value);
                break;
            case ListValueNumber:
                WriteList(writer, (DynamicMessage)value);
                break;
            default:
                ProtobufJsonScalars.Write(writer, value);
                break;
        }
    }

    private static ProtobufJsonError TryReadStruct(JsonElement element, DynamicMessage message, int depth)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return ProtobufJsonError.WrongType;
        }

        if (depth >= ProtobufReader.MaximumDepth)
        {
            return ProtobufJsonError.NestingTooDeep;
        }

        var fields = message.Map(message.Type.FindField(1)!);
        var valueType = message.Type.FindField(1)!.MessageType!.FindField(2)!.MessageType!;
        var error = ProtobufJsonError.None;
        using var members = element.EnumerateObject();
        while (error == ProtobufJsonError.None && members.MoveNext())
        {
            var value = new DynamicMessage(valueType);
            error = TryReadValue(members.Current.Value, value, depth + 1);
            fields[members.Current.Name] = value;
        }

        return error;
    }

    private static ProtobufJsonError TryReadList(JsonElement element, DynamicMessage message, int depth)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return ProtobufJsonError.WrongType;
        }

        if (depth >= ProtobufReader.MaximumDepth)
        {
            return ProtobufJsonError.NestingTooDeep;
        }

        var valuesField = message.Type.FindField(1)!;
        var values = message.Repeated(valuesField);
        var error = ProtobufJsonError.None;
        using var elements = element.EnumerateArray();
        while (error == ProtobufJsonError.None && elements.MoveNext())
        {
            var value = new DynamicMessage(valuesField.MessageType!);
            error = TryReadValue(elements.Current, value, depth + 1);
            values.Add(value);
        }

        return error;
    }

    private static ProtobufJsonError TryReadValue(JsonElement element, DynamicMessage message, int depth)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                message.SetSingular(message.Type.FindField(NullValueNumber)!, 0);
                return ProtobufJsonError.None;
            case JsonValueKind.Number:
                return TryReadNumber(element, message);
            case JsonValueKind.String:
                message.SetSingular(message.Type.FindField(StringValueNumber)!, element.GetString()!);
                return ProtobufJsonError.None;
            case JsonValueKind.True or JsonValueKind.False:
                message.SetSingular(message.Type.FindField(BoolValueNumber)!, element.GetBoolean());
                return ProtobufJsonError.None;
            default:
                return TryReadContainer(element, message, depth);
        }
    }

    private static ProtobufJsonError TryReadNumber(JsonElement element, DynamicMessage message)
    {
        // Every JSON number reads as a double; one beyond its range reads as infinity, which JSON cannot hold.
        var number = element.GetDouble();
        if (!double.IsFinite(number))
        {
            return ProtobufJsonError.InvalidValue;
        }

        message.SetSingular(message.Type.FindField(NumberValueNumber)!, number);
        return ProtobufJsonError.None;
    }

    // An object or an array: a nested Struct or ListValue.
    private static ProtobufJsonError TryReadContainer(JsonElement element, DynamicMessage message, int depth)
    {
        var isObject = element.ValueKind == JsonValueKind.Object;
        var field = message.Type.FindField(isObject ? StructValueNumber : ListValueNumber)!;
        var nested = new DynamicMessage(field.MessageType!);
        message.SetSingular(field, nested);
        return isObject ? TryReadStruct(element, nested, depth) : TryReadList(element, nested, depth);
    }
}
