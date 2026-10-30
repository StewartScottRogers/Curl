using System.Text.Encodings.Web;
using System.Text.Json;

namespace Curl.Extension.OpenApi;

/// <summary>
/// The media type an operation's Request Body Object takes (OpenAPI 3.1 §4.8.13, §4.8.14) and the types its schema
/// gives the properties, so the <c>name=value</c> arguments that are no parameter become the body.
/// </summary>
/// <param name="MediaType">The media type chosen: the first JSON, form or multipart one in <c>content</c>, else the first.</param>
/// <param name="Properties">The schema type of each property the schema names.</param>
internal sealed record OpenApiRequestBody(string MediaType, IReadOnlyDictionary<string, OpenApiPropertyType> Properties)
{
    private const string FormMediaType = "application/x-www-form-urlencoded";
    private const string MultipartMediaType = "multipart/form-data";

    /// <summary>Tells whether a media type is one the body can be built for.</summary>
    /// <param name="mediaType">The media type, as a <c>content</c> key.</param>
    /// <returns><see langword="true" /> for JSON (<c>application/json</c> or a <c>+json</c> type), form and multipart.</returns>
    internal static bool IsSupported(string mediaType) => IsJson(mediaType) || mediaType is FormMediaType or MultipartMediaType;

    /// <summary>Gives the curl arguments that send the properties as the body.</summary>
    /// <param name="values">Each property's values, in command-line order; none sends no body.</param>
    /// <returns>A <c>Content-Type</c> header and <c>--data-binary</c> for JSON, <c>--data-urlencode</c> per value for a form, <c>--form-string</c> per value for multipart.</returns>
    internal IReadOnlyList<string> Arguments(IReadOnlyDictionary<string, List<string>> values)
    {
        if (values.Count == 0)
        {
            return [];
        }

        if (IsJson(MediaType))
        {
            return ["-H", $"Content-Type: {MediaType}", "--data-binary", Json(values)];
        }

        string option = MediaType switch
        {
            FormMediaType => "--data-urlencode",
            MultipartMediaType => "--form-string",
            _ => throw OpenApiFailure.Of($"request body media type {MediaType} is not supported"),
        };
        return [.. values.SelectMany(property => property.Value.SelectMany(value => new[] { option, $"{property.Key}={value}" }))];
    }

    private static bool IsJson(string mediaType) =>
        mediaType == "application/json" || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);

    private string Json(IReadOnlyDictionary<string, List<string>> values)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, List<string>> property in values)
            {
                writer.WritePropertyName(property.Key);
                WriteProperty(writer, Properties.GetValueOrDefault(property.Key, OpenApiPropertyType.Untyped), property.Value);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteProperty(Utf8JsonWriter writer, OpenApiPropertyType type, List<string> values)
    {
        if (type.Type != "array" && values.Count == 1)
        {
            WriteValue(writer, type.Type, values[0]);
            return;
        }

        string? itemType = type.Type == "array" ? type.ItemType : type.Type;
        writer.WriteStartArray();
        values.ForEach(value => WriteValue(writer, itemType, value));
        writer.WriteEndArray();
    }

    private static void WriteValue(Utf8JsonWriter writer, string? type, string value)
    {
        if (IsLiteralOf(type, value))
        {
            writer.WriteRawValue(value);
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }

    private static bool IsLiteralOf(string? type, string value)
    {
        JsonValueKind kind;
        try
        {
            using JsonDocument literal = JsonDocument.Parse(value);
            kind = literal.RootElement.ValueKind;
        }
        catch (JsonException)
        {
            return false;
        }

        return (type, kind) switch
        {
            ("integer" or "number", JsonValueKind.Number) => true,
            ("boolean", JsonValueKind.True or JsonValueKind.False) => true,
            ("null", JsonValueKind.Null) => true,
            ("object", JsonValueKind.Object) => true,
            _ => false,
        };
    }
}
