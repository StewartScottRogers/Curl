using System.Security.Cryptography;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// Keeps dictionaries in a directory, two files each named by the dictionary's SHA-256 in hex:
/// <c>&lt;hash&gt;.dictionary</c> holds its bytes and <c>&lt;hash&gt;.metadata</c> its <see cref="StoredDictionary"/>.
/// Picks the dictionary to advertise for a request by RFC 9842 section 2.2.2.
/// </summary>
public sealed class DictionaryStore(string directory, TimeProvider timeProvider)
{
    private const string ContentExtension = ".dictionary";
    private const string MetadataExtension = ".metadata";

    /// <summary>
    /// Stores <paramref name="content"/>, fetched from <paramref name="dictionaryUrl"/> with
    /// <paramref name="directives"/>, replacing any dictionary with the same hash.
    /// </summary>
    public async Task<StoredDictionary> StoreAsync(Uri dictionaryUrl, UseAsDictionary directives, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var dictionary = new StoredDictionary(dictionaryUrl, directives, SHA256.HashData(content.Span), timeProvider.GetUtcNow());
        Directory.CreateDirectory(directory);
        var path = PathOf(dictionary.Hash);
        await File.WriteAllBytesAsync(path + ContentExtension, content, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(path + MetadataExtension, StoredDictionaryEncoding.Encode(dictionary), cancellationToken).ConfigureAwait(false);
        return dictionary;
    }

    /// <summary>
    /// The dictionary to advertise for <paramref name="requestUrl"/>: of those whose pattern matches it, the one with
    /// the longest <c>match</c>, and of equally long ones the most recently fetched (RFC 9842 section 2.2.2). Curl
    /// has no request destinations, so <c>match-dest</c> takes no part. Null when none matches.
    /// </summary>
    public async Task<StoredDictionary?> FindBestMatchAsync(Uri requestUrl, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        StoredDictionary? best = null;
        foreach (var metadataPath in Directory.EnumerateFiles(directory, "*" + MetadataExtension))
        {
            var candidate = StoredDictionaryEncoding.Decode(await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false));
            if (candidate is not null && Matches(candidate, requestUrl) && IsBetter(candidate, best))
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>The bytes of <paramref name="dictionary"/>; null when its file is gone.</summary>
    public async Task<byte[]?> ReadContentAsync(StoredDictionary dictionary, CancellationToken cancellationToken)
    {
        var path = PathOf(dictionary.Hash) + ContentExtension;
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null;
    }

    private string PathOf(byte[] hash) => Path.Combine(directory, Convert.ToHexStringLower(hash));

    private static bool Matches(StoredDictionary candidate, Uri requestUrl) =>
        DictionaryMatchPattern.Compile(candidate.Url, candidate.Directives.Match)?.Matches(requestUrl) == true;

    /// <summary>Whether <paramref name="candidate"/> wins over <paramref name="best"/>: a longer match, or as long and fetched later.</summary>
    internal static bool IsBetter(StoredDictionary candidate, StoredDictionary? best)
    {
        if (best is null)
        {
            return true;
        }

        var lengthOrder = candidate.Directives.Match.Length.CompareTo(best.Directives.Match.Length);
        return lengthOrder > 0 || (lengthOrder == 0 && candidate.FetchedAt > best.FetchedAt);
    }
}
