using System.Text.Json;

namespace Curl.Extension.OAuth2.Cache;

/// <summary>
/// Writes and reads the token cache file's JSON (ADR-1034): <c>{"version":1,"entries":[...]}</c>, each entry holding its
/// key and token with every member present, <c>null</c> where it has no value, and <c>expires_at</c> in Unix seconds.
/// </summary>
internal static class OAuth2TokenCacheFormat
{
    /// <summary>The format version written and the only one read.</summary>
    public const int Version = 1;

    /// <summary>Writes <paramref name="entries"/> as UTF-8 JSON.</summary>
    /// <param name="entries">The cached tokens by key.</param>
    /// <returns>The file's bytes.</returns>
    public static byte[] Write(IReadOnlyDictionary<OAuth2TokenCacheKey, OAuth2CachedToken> entries)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WriteStartArray("entries");
            foreach (var (key, token) in entries)
            {
                WriteEntry(writer, key, token);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Reads the file's text; text that is not this format, of another version, or with an entry missing a member or
    /// holding one of the wrong type reads as no entries.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The cached tokens by key.</returns>
    public static Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken> Read(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.GetProperty("version").GetInt32() != Version)
            {
                return [];
            }

            Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken> entries = [];
            foreach (var entry in root.GetProperty("entries").EnumerateArray())
            {
                entries[ReadKey(entry)] = ReadToken(entry);
            }

            return entries;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    private static void WriteEntry(Utf8JsonWriter writer, OAuth2TokenCacheKey key, OAuth2CachedToken token)
    {
        writer.WriteStartObject();
        writer.WriteString("token_endpoint", key.TokenEndpoint.AbsoluteUri);
        writer.WriteString("client_id", key.ClientId);
        writer.WriteString("scope", key.Scope);
        writer.WriteString("access_token", token.AccessToken);
        writer.WriteString("token_type", token.TokenType);
        writer.WritePropertyName("expires_at");
        WriteUnixSeconds(writer, token.ExpiresAt);
        writer.WriteString("refresh_token", token.RefreshToken);
        writer.WriteString("granted_scope", token.Scope);
        writer.WriteEndObject();
    }

    private static void WriteUnixSeconds(Utf8JsonWriter writer, DateTimeOffset? instant)
    {
        if (instant is { } value)
        {
            writer.WriteNumberValue(value.ToUnixTimeSeconds());
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    // UriFormatException derives from FormatException, so a token_endpoint that is not an absolute URI reads as corrupt.
    private static OAuth2TokenCacheKey ReadKey(JsonElement entry) => new(
        new Uri(ReadRequiredString(entry, "token_endpoint"), UriKind.Absolute),
        ReadRequiredString(entry, "client_id"),
        entry.GetProperty("scope").GetString());

    private static OAuth2CachedToken ReadToken(JsonElement entry) => new(
        ReadRequiredString(entry, "access_token"),
        ReadRequiredString(entry, "token_type"),
        ReadUnixSeconds(entry.GetProperty("expires_at")),
        entry.GetProperty("refresh_token").GetString(),
        entry.GetProperty("granted_scope").GetString());

    private static string ReadRequiredString(JsonElement entry, string name) =>
        entry.GetProperty(name).GetString() ?? throw new FormatException($"The entry's \"{name}\" is null.");

    private static DateTimeOffset? ReadUnixSeconds(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? null : DateTimeOffset.FromUnixTimeSeconds(value.GetInt64());
}
