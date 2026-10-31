namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// Builds the request header field values that advertise a dictionary: <c>Available-Dictionary</c>
/// (RFC 9842 section 2.2) and <c>Dictionary-ID</c> (section 2.3).
/// </summary>
public static class DictionaryRequestHeaders
{
    /// <summary>The length of the SHA-256 hash that identifies a dictionary.</summary>
    public const int HashLength = 32;

    /// <summary>
    /// The <c>Available-Dictionary</c> value: the dictionary's SHA-256 as an RFC 8941 byte sequence
    /// (<c>:base64:</c>, section 3.3.5).
    /// </summary>
    /// <exception cref="ArgumentException">The hash is not 32 bytes long.</exception>
    public static string BuildAvailableDictionary(ReadOnlySpan<byte> sha256)
    {
        if (sha256.Length != HashLength)
        {
            throw new ArgumentException("A dictionary hash is 32 bytes of SHA-256.", nameof(sha256));
        }

        return ":" + Convert.ToBase64String(sha256) + ":";
    }

    /// <summary>
    /// The <c>Dictionary-ID</c> value: <paramref name="id"/> as an RFC 8941 sf-string (section 3.3.3), with
    /// <c>"</c> and <c>\</c> escaped. Null when the id is empty (the field is then not sent) or holds a character
    /// an sf-string cannot carry.
    /// </summary>
    public static string? BuildDictionaryId(string id)
    {
        if (id.Length == 0 || id.Any(character => character is < ' ' or > '~'))
        {
            return null;
        }

        return "\"" + id.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
