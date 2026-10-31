namespace Curl.Extension.CompressionDictionary;

/// <summary>The two dictionary-compressed content codings of RFC 9842.</summary>
public enum DictionaryContentEncoding
{
    /// <summary><c>dcb</c>: Dictionary-Compressed Brotli (section 4).</summary>
    DictionaryCompressedBrotli,

    /// <summary><c>dcz</c>: Dictionary-Compressed Zstandard (section 5).</summary>
    DictionaryCompressedZstandard,
}
