using System.Text.Json;
using Curl.Protobuf.DynamicMessages;
using Curl.Protobuf.Wire;

namespace Curl.Protobuf.Json;

/// <summary>
/// Reads a <see cref="DynamicMessage" /> from JSON under the proto3 JSON mapping. A member is
/// matched by the field's JSON name or its original name, a null member leaves its field
/// unset, and a member that names no field is rejected.
/// </summary>
public static class ProtobufJsonParser
{
    // JSON nesting is bounded by the message depth below, not by the document reader.
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = int.MaxValue };

    /// <summary>Reads a message of this type from UTF-8 JSON; the message is null unless the error is <see cref="ProtobufJsonError.None" />.</summary>
    public static ProtobufJsonError TryParse(DynamicMessageType type, ReadOnlyMemory<byte> json, out DynamicMessage? message)
    {
        ArgumentNullException.ThrowIfNull(type);
        message = null;
        if (!TryParseDocument(json, out var document))
        {
            return ProtobufJsonError.MalformedJson;
        }

        using (document)
        {
            var parsed = new DynamicMessage(type);
            var error = TryReadRoot(document!.RootElement, parsed);
            message = error == ProtobufJsonError.None ? parsed : null;
            return error;
        }
    }

    private static ProtobufJsonError TryReadRoot(JsonElement root, DynamicMessage message)
    {
        try
        {
            return TryReadMessage(root, message, 0);
        }
        catch (InvalidOperationException)
        {
            // The document reader checks UTF-8 and escaped surrogates only when a string is read.
            return ProtobufJsonError.MalformedJson;
        }
    }

    private static bool TryParseDocument(ReadOnlyMemory<byte> json, out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
            return true;
        }
        catch (JsonException)
        {
            document = null;
            return false;
        }
    }

    private static ProtobufJsonError TryReadMessage(JsonElement element, DynamicMessage message, int depth)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return ProtobufJsonError.WrongType;
        }

        foreach (var member in element.EnumerateObject())
        {
            var field = ProtobufJsonNames.FindField(message.Type, member.Name);
            var error = field is null ? ProtobufJsonError.UnknownField : TryReadField(member.Value, message, field, depth);
            if (error != ProtobufJsonError.None)
            {
                return error;
            }
        }

        return ProtobufJsonError.None;
    }

    private static ProtobufJsonError TryReadField(JsonElement element, DynamicMessage message, DynamicField field, int depth)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            message.ClearValue(field.Name);
            return ProtobufJsonError.None;
        }

        if (field.IsMap)
        {
            return TryReadMap(element, message.Map(field), field.MessageType!, depth);
        }

        if (field.IsRepeated)
        {
            return TryReadRepeated(element, message.Repeated(field), field, depth);
        }

        var error = TryReadValue(element, field, depth, out var value);
        if (error == ProtobufJsonError.None)
        {
            message.SetSingular(field, value!);
        }

        return error;
    }

    private static ProtobufJsonError TryReadRepeated(JsonElement element, List<object> items, DynamicField field, int depth)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return ProtobufJsonError.WrongType;
        }

        var error = ProtobufJsonError.None;
        using var elements = element.EnumerateArray();
        while (error == ProtobufJsonError.None && elements.MoveNext())
        {
            error = TryReadValue(elements.Current, field, depth, out var item);
            items.Add(item!);
        }

        return error;
    }

    private static ProtobufJsonError TryReadMap(JsonElement element, Dictionary<object, object> map, DynamicMessageType entryType, int depth)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return ProtobufJsonError.WrongType;
        }

        var keyField = entryType.FindField(1)!;
        var valueField = entryType.FindField(2)!;
        var error = ProtobufJsonError.None;
        using var members = element.EnumerateObject();
        while (error == ProtobufJsonError.None && members.MoveNext())
        {
            error = ProtobufJsonScalars.TryReadMapKey(members.Current.Name, keyField.Kind, out var key);
            object? value = null;
            error = error == ProtobufJsonError.None ? TryReadValue(members.Current.Value, valueField, depth, out value) : error;
            map[key ?? string.Empty] = value!;
        }

        return error;
    }

    private static ProtobufJsonError TryReadValue(JsonElement element, DynamicField field, int depth, out object? value)
    {
        if (field.Kind == DynamicValueKind.Message)
        {
            var nested = new DynamicMessage(field.MessageType!);
            value = nested;
            return depth >= ProtobufReader.MaximumDepth ? ProtobufJsonError.NestingTooDeep : TryReadMessage(element, nested, depth + 1);
        }

        return field.Kind == DynamicValueKind.Enum
            ? ProtobufJsonScalars.TryReadEnum(element, field.EnumType!, out value)
            : ProtobufJsonScalars.TryRead(element, field.Kind, out value);
    }
}
