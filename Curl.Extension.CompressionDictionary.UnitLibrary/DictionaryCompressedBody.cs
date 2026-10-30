using Curl.Extension.CompressionDictionary.Brotli;
using Curl.Extension.CompressionDictionary.Zstd;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// Decodes a response body in a dictionary-compressed content coding: checks the stream header names the
/// advertised dictionary (RFC 9842 sections 4 and 5), then decodes the rest against it, as a Brotli prefix
/// dictionary for <c>dcb</c> (section 4) or a Zstandard raw content dictionary for <c>dcz</c> (section 5).
/// </summary>
internal static class DictionaryCompressedBody
{
    /// <summary>The coding a <c>Content-Encoding</c> value names; <see langword="null" /> for anything but exactly <c>dcb</c> or <c>dcz</c>.</summary>
    public static DictionaryContentEncoding? CodingOf(string? contentEncoding) => contentEncoding?.Trim().ToLowerInvariant() switch
    {
        "dcb" => DictionaryContentEncoding.DictionaryCompressedBrotli,
        "dcz" => DictionaryContentEncoding.DictionaryCompressedZstandard,
        _ => null,
    };

    /// <summary>Decodes <paramref name="body" /> against <paramref name="dictionary" />.</summary>
    /// <exception cref="InvalidDataException">The header does not name the dictionary, or the compressed data is corrupt.</exception>
    public static byte[] Decode(DictionaryContentEncoding encoding, ReadOnlySpan<byte> body, StoredDictionary dictionary, byte[] content)
    {
        var check = DictionaryStreamHeader.Check(encoding, body, dictionary.Hash);
        if (check != DictionaryStreamHeaderCheck.Matches)
        {
            throw new InvalidDataException("the stream header is " + check);
        }

        var compressed = body[DictionaryStreamHeader.LengthOf(encoding)..];
        try
        {
            return encoding == DictionaryContentEncoding.DictionaryCompressedBrotli
                ? BrotliDictionaryDecoder.Decode(compressed, content)
                : DictionaryZstandardDecompressor.Decompress(compressed, content);
        }
        catch (Exception exception) when (exception is BrotliFormatException or DictionaryZstandardException)
        {
            throw new InvalidDataException(exception.Message, exception);
        }
    }
}
