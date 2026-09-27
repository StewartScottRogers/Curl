using System.IO.Compression;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Decodes one content coding of a response body as its encoded bytes arrive, with the BCL
/// decompression streams, failing with the exit 61 messages curl 8.21.0 reports (measured,
/// BL-177 Notes).
/// </summary>
/// <remarks>
/// <para>
/// <c>gzip</c> decodes a gzip stream, or a zlib stream, as curl's zlib does when it is told
/// to detect the header; first bytes that are neither are
/// <see cref="HttpTransferMessages.IncorrectHeaderCheck" />, and a gzip or zlib header naming
/// a method other than deflate is <see cref="HttpTransferMessages.UnknownCompressionMethod" />.
/// <c>deflate</c> decodes a zlib stream, or a raw deflate stream when the first two bytes
/// fail the zlib header check, as curl retries it. The first bytes are held until there are
/// enough to tell. Any other corrupt data is exit 61
/// <see cref="HttpTransferMessages.BadContentEncoding" /> (ADR-0031).
/// </para>
/// <para>
/// A body that ends before its coding's stream does is not an error: what was decoded is
/// written and the rest is dropped, as curl does.
/// </para>
/// </remarks>
/// <param name="coding">The coding to decode.</param>
internal sealed class HttpContentCodingDecoder(HttpContentCoding coding) : IDisposable
{
    /// <summary>
    /// The most decoded bytes one piece of output holds.
    /// </summary>
    internal const int OutputSize = 16384;

    private const byte GzipFirstByte = 0x1F;

    private const byte GzipSecondByte = 0x8B;

    private const byte DeflateMethod = 8;

    private readonly HttpContentInput input = new();

    private readonly byte[] output = new byte[OutputSize];

    private byte[] start = [];

    private Stream? decompressor;

    /// <summary>
    /// Decodes the next piece of the encoded body.
    /// </summary>
    /// <param name="encoded">The next encoded bytes.</param>
    /// <returns>
    /// The decoded bytes, in pieces of at most <see cref="OutputSize" />; each piece is valid
    /// only until the next is asked for.
    /// </returns>
    /// <exception cref="HttpTransferException">The encoded bytes are corrupt (exit 61).</exception>
    internal IEnumerable<ReadOnlyMemory<byte>> Decode(ReadOnlyMemory<byte> encoded)
    {
        if (decompressor is null)
        {
            start = [.. start, .. encoded.Span];
            decompressor = Choose(start);
            if (decompressor is null)
            {
                yield break;
            }

            encoded = start;
            start = [];
        }

        input.Pending = encoded;
        int read;
        while ((read = ReadDecoded(decompressor)) > 0)
        {
            yield return output.AsMemory(0, read);
        }
    }

    /// <summary>
    /// Releases the decompression stream, if one was chosen.
    /// </summary>
    public void Dispose() => decompressor?.Dispose();

    /// <summary>
    /// Chooses the decompression stream the first bytes call for, or none while there are
    /// too few of them to tell.
    /// </summary>
    private Stream? Choose(ReadOnlySpan<byte> first) => coding switch
    {
        HttpContentCoding.Brotli => new BrotliStream(input, CompressionMode.Decompress),
        HttpContentCoding.Gzip => ChooseForGzip(first),
        _ => ChooseForDeflate(first),
    };

    private Stream? ChooseForGzip(ReadOnlySpan<byte> first)
    {
        if (first.Length < 2)
        {
            return null;
        }

        if (first[0] == GzipFirstByte && first[1] == GzipSecondByte)
        {
            return first.Length < 3 ? null : Gzip(first[2]);
        }

        return IsZLibHeader(first) ? ZLib(first[0]) : throw Corrupt(HttpTransferMessages.IncorrectHeaderCheck);
    }

    private Stream? ChooseForDeflate(ReadOnlySpan<byte> first)
    {
        if (first.Length < 2)
        {
            return null;
        }

        return IsZLibHeader(first) ? ZLib(first[0]) : new DeflateStream(input, CompressionMode.Decompress);
    }

    private GZipStream Gzip(byte method) =>
        method == DeflateMethod
            ? new GZipStream(input, CompressionMode.Decompress)
            : throw Corrupt(HttpTransferMessages.UnknownCompressionMethod);

    private ZLibStream ZLib(byte methodAndInfo) =>
        (methodAndInfo & 0x0F) == DeflateMethod
            ? new ZLibStream(input, CompressionMode.Decompress)
            : throw Corrupt(HttpTransferMessages.UnknownCompressionMethod);

    /// <summary>
    /// Applies zlib's header check: the first two bytes, read big-endian, are a multiple of 31.
    /// </summary>
    private static bool IsZLibHeader(ReadOnlySpan<byte> first) => ((first[0] << 8) | first[1]) % 31 == 0;

    private int ReadDecoded(Stream stream)
    {
        try
        {
            return stream.Read(output);
        }
        catch (InvalidDataException)
        {
            throw Corrupt(HttpTransferMessages.BadContentEncoding);
        }
        catch (InvalidOperationException)
        {
            throw Corrupt(HttpTransferMessages.BadContentEncoding);
        }
    }

    private static HttpTransferException Corrupt(string message) => new(CurlExitCode.BadContentEncoding, message);
}
