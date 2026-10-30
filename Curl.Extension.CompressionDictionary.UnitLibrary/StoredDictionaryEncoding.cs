using System.Buffers;
using System.Globalization;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// Encodes a <see cref="StoredDictionary"/> as the text of its metadata file: one line each for the hash in hex,
/// the URL, the match pattern, the id, the fetch time in Unix milliseconds and the destinations separated by tabs.
/// sf-strings carry no control characters, so no line can hold a newline or a tab.
/// </summary>
internal static class StoredDictionaryEncoding
{
    private const int LineCount = 6;

    public static string Encode(StoredDictionary dictionary) => string.Join('\n',
        Convert.ToHexStringLower(dictionary.Hash),
        dictionary.Url.AbsoluteUri,
        dictionary.Directives.Match,
        dictionary.Directives.Id,
        dictionary.FetchedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
        string.Join('\t', dictionary.Directives.MatchDestinations));

    /// <summary>Decodes a metadata file's text; null when it is corrupt or truncated.</summary>
    public static StoredDictionary? Decode(string text)
    {
        var lines = text.Split('\n');
        if (lines.Length != LineCount)
        {
            return null;
        }

        var hash = ReadHash(lines[0]);
        if (hash is null
            || !Uri.TryCreate(lines[1], UriKind.Absolute, out var url)
            || !long.TryParse(lines[4], NumberStyles.None, CultureInfo.InvariantCulture, out var fetchedAt))
        {
            return null;
        }

        string[] destinations = lines[5].Length == 0 ? [] : lines[5].Split('\t');
        return new StoredDictionary(url, new UseAsDictionary(lines[2], destinations, lines[3]), hash, DateTimeOffset.FromUnixTimeMilliseconds(fetchedAt));
    }

    private static byte[]? ReadHash(string hex)
    {
        var hash = new byte[DictionaryRequestHeaders.HashLength];
        var status = Convert.FromHexString(hex, hash, out _, out var written);
        return status == OperationStatus.Done && written == hash.Length ? hash : null;
    }
}
