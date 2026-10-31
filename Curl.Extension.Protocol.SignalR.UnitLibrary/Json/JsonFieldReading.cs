using System.Text;
using System.Text.Json;

namespace Curl.Extension.Protocol.SignalR.Json;

/// <summary>Reads JSON objects and arrays with <see cref="Utf8JsonReader"/> into <see cref="JsonField"/>s, keeping each value's JSON text.</summary>
internal static class JsonFieldReading
{
    /// <summary>Reads one JSON object into its members by name; the last of two members with one name wins.</summary>
    /// <exception cref="SignalRWireFormatException">The bytes are not one JSON object.</exception>
    public static JsonObjectFields ReadObject(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            return ReadObjectMembers(utf8Json);
        }
        catch (JsonException exception)
        {
            throw new SignalRWireFormatException($"The JSON is not valid: {exception.Message}", exception);
        }
    }

    /// <summary>Reads the JSON text of one array, as <see cref="ReadObject"/> kept it, into its elements in order.</summary>
    /// <exception cref="SignalRWireFormatException">The JSON text is not an array.</exception>
    public static IReadOnlyList<JsonField> ReadArray(string rawJson)
    {
        byte[] utf8Json = Encoding.UTF8.GetBytes(rawJson);
        var reader = new Utf8JsonReader(utf8Json);
        _ = reader.Read();
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new SignalRWireFormatException($"Expected a JSON array, not {rawJson}.");
        }

        var elements = new List<JsonField>();
        _ = reader.Read();
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            elements.Add(ReadValue(ref reader, utf8Json));
            _ = reader.Read();
        }

        return elements;
    }

    private static JsonObjectFields ReadObjectMembers(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json);
        _ = reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new SignalRWireFormatException("Expected a JSON object.");
        }

        var members = new Dictionary<string, JsonField>(StringComparer.Ordinal);
        _ = reader.Read();
        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            _ = reader.Read();
            members[name] = ReadValue(ref reader, utf8Json);
            _ = reader.Read();
        }

        // After the object the reader returns false at the end, and throws JsonException on anything but whitespace.
        _ = reader.Read();
        return new JsonObjectFields(members);
    }

    private static JsonField ReadValue(ref Utf8JsonReader reader, ReadOnlySpan<byte> utf8Json)
    {
        JsonTokenType tokenType = reader.TokenType;
        int start = (int)reader.TokenStartIndex;
        string? text = tokenType == JsonTokenType.String ? reader.GetString() : null;
        reader.Skip();
        string rawJson = Encoding.UTF8.GetString(utf8Json[start..(int)reader.BytesConsumed]);
        return new JsonField(tokenType, rawJson, text);
    }
}
