using System.Text.Json;

namespace Curl.Extension.OAuth2.Wire;

/// <summary>
/// Reads the members of an OAuth 2.0 JSON response body (RFC 6749 §5.1, §5.2; RFC 8628 §3.2) with
/// <see cref="JsonDocument"/>, so a body that is not a JSON object, a missing required member or a
/// member of the wrong type becomes a failed <see cref="OAuth2ParseResult{T}"/> rather than an exception.
/// </summary>
internal static class OAuth2JsonBody
{
    /// <summary>Parses <paramref name="body"/> as a JSON object and reads it with <paramref name="read"/>.</summary>
    /// <typeparam name="T">The value the body holds.</typeparam>
    /// <param name="body">The response body.</param>
    /// <param name="read">Reads the value from the body's top-level object.</param>
    /// <returns>The value, or why the body could not be read.</returns>
    public static OAuth2ParseResult<T> Read<T>(string body, Func<JsonElement, T> read)
        where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? OAuth2ParseResult<T>.Success(read(document.RootElement))
                : OAuth2ParseResult<T>.Malformed("The body is not a JSON object.");
        }
        catch (JsonException)
        {
            return OAuth2ParseResult<T>.Malformed("The body is not valid JSON.");
        }
        catch (OAuth2MemberException exception)
        {
            return OAuth2ParseResult<T>.Malformed(exception.Message);
        }
    }

    /// <summary>Reads a string member the response must have.</summary>
    /// <param name="body">The body's top-level object.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>The member's value.</returns>
    public static string ReadRequiredString(JsonElement body, string name) =>
        ReadOptionalString(body, name) ?? throw new OAuth2MemberException($"The body has no \"{name}\".");

    /// <summary>Reads a string member the response may leave out; a JSON <c>null</c> reads as left out.</summary>
    /// <param name="body">The body's top-level object.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>The member's value, or <see langword="null"/> when it is left out.</returns>
    public static string? ReadOptionalString(JsonElement body, string name)
    {
        if (FindMember(body, name) is not { } value)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new OAuth2MemberException($"The body's \"{name}\" is not a string.");
    }

    /// <summary>Reads an integer member the response must have.</summary>
    /// <param name="body">The body's top-level object.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>The member's value.</returns>
    public static long ReadRequiredInteger(JsonElement body, string name) =>
        ReadOptionalInteger(body, name) ?? throw new OAuth2MemberException($"The body has no \"{name}\".");

    /// <summary>Reads an integer member the response may leave out; a JSON <c>null</c> reads as left out.</summary>
    /// <param name="body">The body's top-level object.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>The member's value, or <see langword="null"/> when it is left out.</returns>
    public static long? ReadOptionalInteger(JsonElement body, string name)
    {
        if (FindMember(body, name) is not { } value)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var integer)
            ? integer
            : throw new OAuth2MemberException($"The body's \"{name}\" is not an integer.");
    }

    private static JsonElement? FindMember(JsonElement body, string name) =>
        body.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;

    /// <summary>A missing required member or a member of the wrong type, caught in <see cref="Read{T}"/>.</summary>
    /// <param name="message">Why the body could not be read.</param>
    private sealed class OAuth2MemberException(string message) : Exception(message);
}
