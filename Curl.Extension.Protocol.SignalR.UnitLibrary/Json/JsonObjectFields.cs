using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Curl.Extension.Protocol.SignalR.Json;

/// <summary>
/// The members of one JSON object by name, read by <see cref="JsonFieldReading.ReadObject"/>, with typed accessors that
/// throw <see cref="SignalRWireFormatException"/> naming the member when it is missing or of the wrong type.
/// </summary>
internal sealed class JsonObjectFields(IReadOnlyDictionary<string, JsonField> members)
{
    /// <summary>The member's string, or <see langword="null"/> when it is absent or JSON null.</summary>
    public string? OptionalString(string name)
    {
        if (!members.TryGetValue(name, out JsonField? field) || field.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return field.Text ?? throw new SignalRWireFormatException($"'{name}' is not a string: {field.RawJson}.");
    }

    /// <summary>The member's string.</summary>
    public string RequiredString(string name) =>
        OptionalString(name) ?? throw new SignalRWireFormatException($"'{name}' is missing.");

    /// <summary>The member's JSON text as sent, or <see langword="null"/> when it is absent.</summary>
    public string? OptionalRawJson(string name) => members.TryGetValue(name, out JsonField? field) ? field.RawJson : null;

    /// <summary>The member's JSON text as sent.</summary>
    public string RequiredRawJson(string name) =>
        OptionalRawJson(name) ?? throw new SignalRWireFormatException($"'{name}' is missing.");

    /// <summary>The member's whole number, or <see langword="null"/> when it is absent.</summary>
    public int? OptionalInt32(string name)
    {
        if (!members.TryGetValue(name, out JsonField? field))
        {
            return null;
        }

        return field.TokenType == JsonTokenType.Number && int.TryParse(field.RawJson, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new SignalRWireFormatException($"'{name}' is not a whole number: {field.RawJson}.");
    }

    /// <summary>The member's whole number.</summary>
    public int RequiredInt32(string name) =>
        OptionalInt32(name) ?? throw new SignalRWireFormatException($"'{name}' is missing.");

    /// <summary>The member's true or false, or <see langword="false"/> when it is absent.</summary>
    public bool OptionalBoolean(string name)
    {
        if (!members.TryGetValue(name, out JsonField? field))
        {
            return false;
        }

        return field.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            _ => throw new SignalRWireFormatException($"'{name}' is not true or false: {field.RawJson}."),
        };
    }

    /// <summary>The JSON text of each element of the member's array, or none when it is absent.</summary>
    public IReadOnlyList<string> OptionalRawJsonArray(string name) =>
        ArrayElements(name).Select(element => element.RawJson).ToArray();

    /// <summary>The strings of the member's array, or none when it is absent.</summary>
    public IReadOnlyList<string> OptionalStringArray(string name) =>
        ArrayElements(name)
            .Select(element => element.Text ?? throw new SignalRWireFormatException($"'{name}' holds {element.RawJson}, not a string."))
            .ToArray();

    /// <summary>The objects of the member's array, or none when it is absent.</summary>
    public IReadOnlyList<JsonObjectFields> OptionalObjectArray(string name) =>
        ArrayElements(name).Select(element => JsonFieldReading.ReadObject(Encoding.UTF8.GetBytes(element.RawJson))).ToArray();

    private IReadOnlyList<JsonField> ArrayElements(string name) =>
        members.TryGetValue(name, out JsonField? field) ? JsonFieldReading.ReadArray(field.RawJson) : [];
}
