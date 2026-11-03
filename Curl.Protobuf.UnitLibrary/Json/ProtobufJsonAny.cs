using System.Text.Json;
using Curl.Protobuf.DynamicMessages;
using Curl.Protobuf.Wire;

namespace Curl.Protobuf.Json;

/// <summary>
/// The JSON form of <c>google.protobuf.Any</c>: an object holding <c>"@type"</c>, the type URL,
/// beside the embedded message's own members, or beside <c>"value"</c> holding the embedded
/// message's JSON form when that is a well-known type whose form is not an object of its fields.
/// The embedded type is the type URL's last path segment, resolved in the Any type's catalogue.
/// An empty Any is <c>{}</c>.
/// </summary>
internal static class ProtobufJsonAny
{
    private const string AnyName = "google.protobuf.Any";

    private const string TypeMember = "@type";

    private const string ValueMember = "value";

    /// <summary>Whether messages of this type are <c>google.protobuf.Any</c>.</summary>
    public static bool IsAny(DynamicMessageType type) => type.FullName == AnyName;

    /// <summary>Reads an Any from its JSON object; the object's nesting has already been counted in <paramref name="depth" />.</summary>
    public static ProtobufJsonError Read(JsonElement element, DynamicMessage any, int depth)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return ProtobufJsonError.WrongType;
        }

        if (!element.TryGetProperty(TypeMember, out var typeUrl))
        {
            // Only an empty object is an Any without a type.
            return element.EnumerateObject().Any() ? ProtobufJsonError.InvalidValue : ProtobufJsonError.None;
        }

        return ReadTyped(element, typeUrl, any, depth);
    }

    // Reads an Any whose object has an "@type" member.
    private static ProtobufJsonError ReadTyped(JsonElement element, JsonElement typeUrl, DynamicMessage any, int depth)
    {
        if (typeUrl.ValueKind != JsonValueKind.String)
        {
            return ProtobufJsonError.WrongType;
        }

        var url = typeUrl.GetString()!;
        if (!TryResolve(any.Type, url, out var embeddedType))
        {
            return ProtobufJsonError.InvalidValue;
        }

        var embedded = new DynamicMessage(embeddedType!);
        var error = ReadEmbedded(element, embedded, depth);
        if (error == ProtobufJsonError.None)
        {
            any.SetSingular(any.Type.FindField(1)!, url);
            any.SetSingular(any.Type.FindField(2)!, DynamicMessageEncoder.Encode(embedded));
        }

        return error;
    }

    /// <summary>
    /// Writes an Any as its JSON object. Throws <see cref="FormatException" /> when its type URL
    /// names no type in the catalogue, its value is not a message of that type, or Anys nest
    /// deeper than <see cref="ProtobufReader.MaximumDepth" />.
    /// </summary>
    public static void Write(Utf8JsonWriter writer, DynamicMessage any)
    {
        if (writer.CurrentDepth >= ProtobufReader.MaximumDepth)
        {
            throw new FormatException("An Any nests too deeply to be written as JSON.");
        }

        var url = (string?)any.FindValue(1) ?? string.Empty;
        var value = (byte[]?)any.FindValue(2) ?? [];
        writer.WriteStartObject();
        if (url.Length > 0 || value.Length > 0)
        {
            WriteEmbedded(writer, url, DecodeEmbedded(any.Type, url, value));
        }

        writer.WriteEndObject();
    }

    // Well-known types whose JSON form is not an object of their fields go under "value".
    private static bool HasValueForm(DynamicMessageType type) =>
        ProtobufJsonTimeTypes.HasStringForm(type) || ProtobufJsonWrapperTypes.IsWrapper(type) || ProtobufJsonFieldMask.IsFieldMask(type)
        || ProtobufJsonStructTypes.IsStructType(type) || IsAny(type);

    private static bool TryResolve(DynamicMessageType anyType, string url, out DynamicMessageType? type)
    {
        var slash = url.LastIndexOf('/');
        type = null;
        return slash >= 0 && anyType.Catalogue.TryGetMessageType(url[(slash + 1)..], out type);
    }

    private static ProtobufJsonError ReadEmbedded(JsonElement element, DynamicMessage embedded, int depth)
    {
        var valueForm = HasValueForm(embedded.Type);
        var error = ProtobufJsonError.None;
        using var members = element.EnumerateObject();
        while (error == ProtobufJsonError.None && members.MoveNext())
        {
            var member = members.Current;
            error = member.Name == TypeMember ? ProtobufJsonError.None
                : !valueForm ? ProtobufJsonParser.ReadMember(member, embedded, depth)
                : member.Name == ValueMember ? ProtobufJsonParser.ReadMessageValue(member.Value, embedded, depth)
                : ProtobufJsonError.UnknownField;
        }

        return error;
    }

    private static DynamicMessage DecodeEmbedded(DynamicMessageType anyType, string url, byte[] value)
    {
        DynamicMessage? embedded = null;
        if (!TryResolve(anyType, url, out var type) || DynamicMessageDecoder.Decode(type!, value, out embedded) != ProtobufDecodeError.None)
        {
            throw new FormatException($"The Any's value is not a message of the type {url} names.");
        }

        return embedded!;
    }

    private static void WriteEmbedded(Utf8JsonWriter writer, string url, DynamicMessage embedded)
    {
        writer.WriteString(TypeMember, url);
        if (HasValueForm(embedded.Type))
        {
            writer.WritePropertyName(ValueMember);
            ProtobufJsonFormatter.WriteMessageValue(writer, embedded);
        }
        else
        {
            ProtobufJsonFormatter.WriteFields(writer, embedded);
        }
    }
}
