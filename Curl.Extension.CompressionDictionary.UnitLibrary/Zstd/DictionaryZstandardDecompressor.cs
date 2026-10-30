using System.Buffers;
using Curl.Zstandard;

namespace Curl.Extension.CompressionDictionary.Zstd;

/// <summary>
/// Decodes the Zstandard frames of a <c>dcz</c> body (RFC 9842 section 5), after its stream
/// header, against the dictionary it names: a raw content dictionary (RFC 8878 section 5) that
/// primes each frame's history, so matches may reach back into it.
/// </summary>
public static class DictionaryZstandardDecompressor
{
    /// <summary>RFC 9842 section 5: a <c>dcz</c> window may always be 8 MiB.</summary>
    public const ulong MinimumWindowLimit = 8UL * 1024 * 1024;

    private const int InitialOutputSize = 64 * 1024;

    /// <summary>
    /// The largest window RFC 9842 section 5 lets a <c>dcz</c> frame declare with a dictionary of
    /// <paramref name="dictionaryLength" /> bytes: 8 MiB or 1.25 times the dictionary, whichever is larger.
    /// </summary>
    public static ulong WindowLimit(int dictionaryLength) => Math.Max(MinimumWindowLimit, (ulong)dictionaryLength * 5 / 4);

    /// <summary>Decodes every frame in <paramref name="frames" /> against <paramref name="dictionary" />.</summary>
    /// <returns>The decoded content.</returns>
    /// <exception cref="DictionaryZstandardException">
    /// A frame breaks RFC 8878, declares a window past <see cref="WindowLimit" />, or is cut short.
    /// </exception>
    public static byte[] Decompress(ReadOnlySpan<byte> frames, ReadOnlyMemory<byte> dictionary)
    {
        var decoder = new ZstandardDecoder(dictionary, WindowLimit(dictionary.Length));
        var output = new byte[InitialOutputSize];
        var written = 0;
        var status = OperationStatus.Done;
        while (!frames.IsEmpty || status == OperationStatus.DestinationTooSmall)
        {
            if (written == output.Length)
            {
                Array.Resize(ref output, output.Length * 2);
            }

            status = decoder.Decompress(frames, output.AsSpan(written), out var consumed, out var count);
            frames = frames[consumed..];
            written += count;
            ThrowIfFailed(status, decoder.LastError);
        }

        return status == OperationStatus.NeedMoreData ? throw new DictionaryZstandardException() : output[..written];
    }

    private static void ThrowIfFailed(OperationStatus status, ZstandardDecodeError error)
    {
        if (status == OperationStatus.InvalidData)
        {
            throw new DictionaryZstandardException(error);
        }
    }
}
