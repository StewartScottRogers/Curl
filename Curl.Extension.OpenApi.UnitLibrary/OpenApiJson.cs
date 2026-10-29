using System.Text.Json;

namespace Curl.Extension.OpenApi;

/// <summary>Reads the members of an OpenAPI description's JSON objects, treating a missing or mistyped member as absent.</summary>
internal static class OpenApiJson
{
    /// <summary>Gives an object member's string value.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>The value, or <see langword="null" /> when the element is no object or the member is no string.</returns>
    internal static string? StringMember(JsonElement element, string name) =>
        Member(element, name) is { ValueKind: JsonValueKind.String } member ? member.GetString() : null;

    /// <summary>Gives an object member's Boolean value.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>The value, or <see langword="null" /> when the element is no object or the member is no Boolean.</returns>
    internal static bool? BooleanMember(JsonElement element, string name) => Member(element, name)?.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    /// <summary>Gives the members of an object member.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>Its members in document order; none when it is absent or no object.</returns>
    internal static IEnumerable<JsonProperty> ObjectMembers(JsonElement element, string name) =>
        Member(element, name) is { ValueKind: JsonValueKind.Object } member ? member.EnumerateObject() : [];

    /// <summary>Gives the items of an array member.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>Its items in order; none when it is absent or no array.</returns>
    internal static IEnumerable<JsonElement> ArrayItems(JsonElement element, string name) =>
        Member(element, name) is { ValueKind: JsonValueKind.Array } member ? member.EnumerateArray() : [];

    /// <summary>Gives an object member.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>The member, or <see langword="null" /> when the element is no object or has no such member.</returns>
    internal static JsonElement? Member(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement member) ? member : null;
}
