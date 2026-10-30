namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// One dictionary in a <see cref="DictionaryStore"/>: where it was fetched from, its <c>Use-As-Dictionary</c>
/// directives, its SHA-256 and when it was fetched.
/// </summary>
public sealed record StoredDictionary(Uri Url, UseAsDictionary Directives, byte[] Hash, DateTimeOffset FetchedAt);
