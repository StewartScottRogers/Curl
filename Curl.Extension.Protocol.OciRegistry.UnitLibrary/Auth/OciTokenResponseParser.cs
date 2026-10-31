using System.Text.Json;

namespace Curl.Extension.Protocol.OciRegistry.Auth;

/// <summary>
/// Reads the bearer token from a token endpoint's JSON answer (Docker/Distribution token authentication specification,
/// "Token Response Fields"): <c>token</c>, else <c>access_token</c>, the name OAuth 2.0 gives it.
/// </summary>
public static class OciTokenResponseParser
{
    /// <summary>Reads the token from <paramref name="body"/>.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>
    /// The first non-empty string of <c>token</c> and <c>access_token</c>, or <see langword="null"/> when the body is not a JSON
    /// object or has neither.
    /// </returns>
    public static string? ParseToken(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object ? ReadString(root, "token") ?? ReadString(root, "access_token") : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var member) && member.ValueKind == JsonValueKind.String && member.GetString() is { Length: > 0 } value
            ? value
            : null;
}
