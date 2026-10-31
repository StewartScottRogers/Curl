namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// Reads the fixed header at the start of a dictionary-compressed stream: a magic number followed by the
/// 32-byte SHA-256 of the dictionary it was compressed with (RFC 9842 sections 4 and 5).
/// </summary>
public static class DictionaryStreamHeader
{
    private static readonly byte[] BrotliMagic = [0xFF, 0x44, 0x43, 0x42];
    private static readonly byte[] ZstandardMagic = [0x5E, 0x2A, 0x4D, 0x18, 0x20, 0x00, 0x00, 0x00];

    /// <summary>The header's length in bytes: 36 for <c>dcb</c>, 40 for <c>dcz</c>.</summary>
    public static int LengthOf(DictionaryContentEncoding encoding) => MagicOf(encoding).Length + DictionaryRequestHeaders.HashLength;

    /// <summary>
    /// Checks the start of a stream in <paramref name="encoding"/> against the advertised dictionary's
    /// <paramref name="dictionaryHash"/>. The compressed data starts <see cref="LengthOf"/> bytes in.
    /// </summary>
    public static DictionaryStreamHeaderCheck Check(DictionaryContentEncoding encoding, ReadOnlySpan<byte> streamStart, ReadOnlySpan<byte> dictionaryHash)
    {
        var magic = MagicOf(encoding);
        if (streamStart.Length < LengthOf(encoding))
        {
            return DictionaryStreamHeaderCheck.Incomplete;
        }

        if (!streamStart[..magic.Length].SequenceEqual(magic))
        {
            return DictionaryStreamHeaderCheck.WrongMagic;
        }

        return streamStart.Slice(magic.Length, DictionaryRequestHeaders.HashLength).SequenceEqual(dictionaryHash)
            ? DictionaryStreamHeaderCheck.Matches
            : DictionaryStreamHeaderCheck.WrongDictionary;
    }

    private static byte[] MagicOf(DictionaryContentEncoding encoding) =>
        encoding == DictionaryContentEncoding.DictionaryCompressedBrotli ? BrotliMagic : ZstandardMagic;
}
