namespace Curl.Extension.ContentDigest;

/// <summary>
/// Reads and writes a <c>Content-Digest</c> or <c>Repr-Digest</c> field value (RFC 9530 §2, §3): a Structured
/// Fields Dictionary (RFC 8941 §3.2) whose keys are hashing algorithm names and whose values are Byte Sequences
/// (RFC 8941 §3.3.5). Parameters on a member are ignored, a later member replaces an earlier one with the same
/// key (RFC 8941 §4.2.2), and a value that does not parse is ignored whole (RFC 8941 §4.2).
/// </summary>
internal static class DigestField
{
    private const string KeyFirstCharacters = "abcdefghijklmnopqrstuvwxyz*";

    private const string KeyCharacters = KeyFirstCharacters + "0123456789_-.";

    /// <summary>Gives the digests a field value names, by algorithm, or none when it does not parse.</summary>
    /// <param name="value">The field value, every field line joined with commas, or <see langword="null" /> when the field was not sent.</param>
    /// <returns>The digests by algorithm name.</returns>
    internal static IReadOnlyDictionary<string, byte[]> Parse(string? value)
    {
        Dictionary<string, byte[]> digests = [];
        foreach (var member in (value ?? string.Empty).Split(','))
        {
            if (ParseMember(member.Trim(' ', '\t')) is not { } digest)
            {
                return new Dictionary<string, byte[]>();
            }

            digests[digest.Key] = digest.Value;
        }

        return digests;
    }

    /// <summary>Formats one digest as a field value with a single member, as <c>sha-256=:base64:</c>.</summary>
    /// <param name="algorithm">The algorithm's name.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>The field value.</returns>
    internal static string Format(string algorithm, byte[] digest) => algorithm + "=:" + Convert.ToBase64String(digest) + ":";

    private static KeyValuePair<string, byte[]>? ParseMember(string member)
    {
        var item = member.Split(';')[0];
        var equals = item.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0)
        {
            return null;
        }

        var key = item[..equals];
        return IsKey(key) && ByteSequenceOf(item[(equals + 1)..]) is { } bytes ? new(key, bytes) : null;
    }

    private static bool IsKey(string key) =>
        KeyFirstCharacters.Contains(key[0], StringComparison.Ordinal) && key.All(character => KeyCharacters.Contains(character, StringComparison.Ordinal));

    private static byte[]? ByteSequenceOf(string value)
    {
        if (value.Length < 2 || value[0] != ':' || value[^1] != ':')
        {
            return null;
        }

        var buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value[1..^1], buffer, out var written) ? buffer[..written] : null;
    }
}
