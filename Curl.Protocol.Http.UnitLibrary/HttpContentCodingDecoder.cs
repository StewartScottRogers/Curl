using System.Buffers;
using System.IO.Compression;
using Curl.Protocol.Abstractions;
using Curl.Zstandard;

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
/// <para>
/// A body with bytes after the end of its gzip member, zlib stream or Brotli stream, a second
/// gzip member included, has the stream decoded and then fails with exit 23
/// <see cref="HttpTransferMessages.ReceivedDataWriteFailed" />, as curl does; bytes after a
/// raw deflate stream are dropped (measured, BL-281 Notes). Brotli is decoded with
/// <see cref="BrotliDecoder" />, which says how many bytes its stream used; the end of a gzip
/// member or a zlib stream is found from its <see cref="HttpContentChecksumTrailer" />.
/// </para>
/// <para>
/// <c>zstd</c> is decoded with <see cref="ZstandardDecoder" />, frame after frame; bytes
/// after the last frame are read as the start of another, so <c>junk</c> there is exit 61
/// <see cref="HttpTransferMessages.BadContentEncoding" />, after the frame's content is
/// written (measured, BL-861 Notes).
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

    private const int StoredBlockType = 0;

    private const int ReservedBlockType = 3;

    /// <summary>
    /// A raw stored block's first byte and its LEN and NLEN fields.
    /// </summary>
    private const int StoredBlockHeaderLength = 5;

    private readonly HttpContentInput input = new();

    private readonly byte[] output = new byte[OutputSize];

    private readonly ZstandardDecoder zstandard = new();

    private byte[] start = [];

    private Stream? decompressor;

    private BrotliDecoder brotli;

    private HttpContentChecksumTrailer? trailer;

    private byte[] carried = [];

    private bool ended;

    /// <summary>
    /// Decodes the next piece of the encoded body.
    /// </summary>
    /// <param name="encoded">The next encoded bytes.</param>
    /// <returns>
    /// The decoded bytes, in pieces of at most <see cref="OutputSize" />; each piece is valid
    /// only until the next is asked for.
    /// </returns>
    /// <exception cref="HttpTransferException">
    /// The encoded bytes are corrupt (exit 61), or go on after the end of the stream (exit 23).
    /// </exception>
    internal IEnumerable<ReadOnlyMemory<byte>> Decode(ReadOnlyMemory<byte> encoded)
    {
        if (ended)
        {
            throw BytesAfterTheEnd();
        }

        return coding switch
        {
            HttpContentCoding.Brotli => DecodeBrotli(encoded),
            HttpContentCoding.Zstandard => DecodeZstandard(encoded),
            _ => DecodeDeflate(encoded),
        };
    }

    /// <summary>
    /// Releases the decompression stream, if one was chosen, and the Brotli decoder.
    /// </summary>
    public void Dispose()
    {
        decompressor?.Dispose();
        brotli.Dispose();
    }

    private IEnumerable<ReadOnlyMemory<byte>> DecodeBrotli(ReadOnlyMemory<byte> encoded)
    {
        OperationStatus status;
        do
        {
            status = brotli.Decompress(encoded.Span, output, out int consumed, out int written);
            encoded = encoded[consumed..];
            if (written > 0)
            {
                yield return output.AsMemory(0, written);
            }
        }
        while (status == OperationStatus.DestinationTooSmall);

        if (status == OperationStatus.InvalidData)
        {
            throw Corrupt(HttpTransferMessages.BadContentEncoding);
        }

        ended = status == OperationStatus.Done;
        if (!encoded.IsEmpty)
        {
            throw BytesAfterTheEnd();
        }
    }

    /// <summary>
    /// Decodes Zstandard frames one after another, as libzstd does for curl: bytes after a
    /// frame start the next one, so bytes that are no frame are corrupt (exit 61), never
    /// bytes after the end.
    /// </summary>
    private IEnumerable<ReadOnlyMemory<byte>> DecodeZstandard(ReadOnlyMemory<byte> encoded)
    {
        OperationStatus status;
        do
        {
            status = zstandard.Decompress(encoded.Span, output, out int consumed, out int written);
            encoded = encoded[consumed..];
            if (written > 0)
            {
                yield return output.AsMemory(0, written);
            }
        }
        while (status == OperationStatus.DestinationTooSmall || (status == OperationStatus.Done && !encoded.IsEmpty));

        if (status == OperationStatus.InvalidData)
        {
            throw Corrupt(HttpTransferMessages.BadContentEncoding);
        }
    }

    /// <summary>
    /// Decodes a gzip, zlib or raw deflate stream, whichever the first bytes call for.
    /// </summary>
    private IEnumerable<ReadOnlyMemory<byte>> DecodeDeflate(ReadOnlyMemory<byte> encoded)
    {
        encoded = ChooseOnceEnoughArrived(encoded);
        if (decompressor is null)
        {
            yield break;
        }

        input.Pending = encoded;
        int read;
        while ((read = ReadDecoded(decompressor)) > 0)
        {
            trailer?.Append(output.AsSpan(0, read));
            yield return output.AsMemory(0, read);

            // GZipStream goes on to decode a second gzip member; stop before it does.
            if (decompressor is GZipStream)
            {
                FindEnd(encoded.Span);
            }
        }

        if (trailer is not null)
        {
            FindEndOnceFinished(encoded.Span);
            Carry(encoded.Span);
        }
    }

    /// <summary>
    /// Holds the first bytes until there are enough to choose the decompression stream, and
    /// returns the bytes to decode: none while they are held, then every byte held.
    /// </summary>
    private ReadOnlyMemory<byte> ChooseOnceEnoughArrived(ReadOnlyMemory<byte> encoded)
    {
        if (decompressor is not null)
        {
            return encoded;
        }

        start = [.. start, .. encoded.Span];
        decompressor = Choose(start);
        if (decompressor is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        byte[] held = start;
        start = [];
        return held;
    }

    /// <summary>
    /// Keeps the last encoded bytes, as many as the trailer can have begun in before the next
    /// encoded bytes.
    /// </summary>
    private void Carry(ReadOnlySpan<byte> encoded)
    {
        carried = [.. carried, .. encoded];
        carried = carried[Math.Max(0, carried.Length - trailer!.CarriedLength)..];
    }

    /// <summary>
    /// Chooses the decompression stream the first bytes call for, or none while there are
    /// too few of them to tell.
    /// </summary>
    private Stream? Choose(ReadOnlySpan<byte> first) =>
        coding == HttpContentCoding.Gzip ? ChooseForGzip(first) : ChooseForDeflate(first);

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

        return IsZLibHeader(first) ? ZLib(first[0]) : RawDeflate(first);
    }

    /// <summary>
    /// Chooses a raw deflate stream for a <c>deflate</c> body without a zlib header, as curl
    /// does when zlib refuses the header, first failing with zlib's own text for a first block
    /// zlib refuses at once: a reserved block type, or a stored block whose length and its
    /// complement disagree (upstream test223, BL-1810).
    /// </summary>
    private DeflateStream? RawDeflate(ReadOnlySpan<byte> first)
    {
        int blockType = (first[0] >> 1) & 0x03;
        if (blockType == ReservedBlockType)
        {
            throw Corrupt(HttpTransferMessages.InvalidBlockType);
        }

        if (blockType == StoredBlockType)
        {
            if (first.Length < StoredBlockHeaderLength)
            {
                return null;
            }

            int length = first[1] | (first[2] << 8);
            int complement = first[3] | (first[4] << 8);
            if (length != (~complement & 0xFFFF))
            {
                throw Corrupt(HttpTransferMessages.InvalidStoredBlockLengths);
            }
        }

        return new DeflateStream(input, CompressionMode.Decompress);
    }

    private GZipStream Gzip(byte method)
    {
        if (method != DeflateMethod)
        {
            throw Corrupt(HttpTransferMessages.UnknownCompressionMethod);
        }

        trailer = new HttpContentChecksumTrailer(isGzip: true);
        return new GZipStream(input, CompressionMode.Decompress);
    }

    private ZLibStream ZLib(byte methodAndInfo)
    {
        if ((methodAndInfo & 0x0F) != DeflateMethod)
        {
            throw Corrupt(HttpTransferMessages.UnknownCompressionMethod);
        }

        trailer = new HttpContentChecksumTrailer(isGzip: false);
        return new ZLibStream(input, CompressionMode.Decompress);
    }

    /// <summary>
    /// Applies zlib's header check: the first two bytes, read big-endian, are a multiple of 31.
    /// </summary>
    private static bool IsZLibHeader(ReadOnlySpan<byte> first) => ((first[0] << 8) | first[1]) % 31 == 0;

    /// <summary>
    /// Looks for the end of the stream once every byte of <paramref name="encoded" /> has been
    /// read: always for a gzip member, whose 8-byte trailer is not found by chance, and for a
    /// zlib stream, whose trailer is 4 bytes, only once the stream says it has finished.
    /// </summary>
    private void FindEndOnceFinished(ReadOnlySpan<byte> encoded)
    {
        if (decompressor is GZipStream || HasFinished(decompressor!))
        {
            FindEnd(encoded);
        }
    }

    /// <summary>
    /// Tells whether <paramref name="stream" /> has reached the end of its stream: one that
    /// has not reads its source again when read, and one that has does not.
    /// </summary>
    private bool HasFinished(Stream stream)
    {
        int reads = input.ReadCount;
        _ = ReadDecoded(stream);
        return input.ReadCount == reads;
    }

    /// <summary>
    /// Records the end of the stream when its trailer ends within <paramref name="encoded" />,
    /// and fails when bytes follow it.
    /// </summary>
    private void FindEnd(ReadOnlySpan<byte> encoded)
    {
        int end = ended ? -1 : trailer!.EndIn(carried, encoded);
        if (end < 0)
        {
            return;
        }

        ended = true;
        if (end < encoded.Length)
        {
            throw BytesAfterTheEnd();
        }
    }

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
    }

    private static HttpTransferException Corrupt(string message) => new(CurlExitCode.BadContentEncoding, message);

    private static HttpTransferException BytesAfterTheEnd() =>
        new(CurlExitCode.WriteError, HttpTransferMessages.ReceivedDataWriteFailed);
}
