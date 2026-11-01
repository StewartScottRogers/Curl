using System.Buffers;
using System.IO.Compression;
using Curl.Protocol.Abstractions;
using Curl.Zstandard;

namespace Curl.Original.Protocol.Http;

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
/// enough to tell. A gzip or zlib trailer whose checksum or size does not match has every
/// decoded byte written first and is <see cref="HttpTransferMessages.IncorrectDataCheck" /> or
/// <see cref="HttpTransferMessages.IncorrectLengthCheck" /> (CGR-071, BL-3600). Any other
/// corrupt data is exit 61
/// <see cref="HttpTransferMessages.BadContentEncoding" /> (ADR-0031).
/// </para>
/// <para>
/// A body that ends before its coding's stream does is not an error: what was decoded is
/// written and the rest is dropped, as curl does.
/// </para>
/// <para>
/// A body with bytes after the end of its gzip member, zlib stream or Brotli stream has the
/// stream decoded and then fails with exit 23
/// <see cref="HttpTransferMessages.ReceivedDataWriteFailed" />, as curl does, or with
/// <see cref="HttpTransferMessages.MultiMemberGzipNotSupported" /> when the bytes after a gzip
/// member start a second one in the same read (curl 8.22.0, BL-2169); bytes after a
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

    private const int DynamicBlockType = 2;

    /// <summary>The bits before the dynamic block's code length code lengths: 3 of block header and 14 of counts.</summary>
    private const int DynamicCountsEnd = 17;

    private const int MaximumLengthSymbols = 286;

    private const int MaximumDistanceSymbols = 30;

    private const int ReservedBlockType = 3;

    /// <summary>
    /// A raw stored block's first byte and its LEN and NLEN fields.
    /// </summary>
    private const int StoredBlockHeaderLength = 5;

    /// <summary>
    /// How many encoded bytes at each end of a piece of the body are fed one at a time: a
    /// gzip trailer's length.
    /// </summary>
    private const int SinglyFedLength = 8;

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
    /// How many encoded bytes have been fed to the decompression stream.
    /// </summary>
    private long fedTotal;

    /// <summary>
    /// <see cref="fedTotal" /> when the decompression stream last decoded something, or
    /// <see langword="null" /> before it has.
    /// </summary>
    private long? fedAtLastOutput;

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

        int fed = 0;
        foreach (int length in FeedLengths(encoded.Length))
        {
            input.Pending = encoded.Slice(fed, length);
            fed += length;
            fedTotal += length;
            foreach (ReadOnlyMemory<byte> piece in ReadAll(encoded, length == 1 ? encoded[..fed] : ReadOnlyMemory<byte>.Empty))
            {
                yield return piece;
            }
        }

        if (trailer is not null)
        {
            FindEndOnceFinished(encoded.Span);
            Carry(encoded.Span);
        }
    }

    /// <summary>
    /// Splits <paramref name="length" /> encoded bytes into the pieces they are fed to the
    /// decompression stream in: the first and last <see cref="SinglyFedLength" /> one at a time,
    /// those between at once. The BCL streams drop what a read decoded when it fails, so a
    /// trailer, which ends a body or spans the read before, is fed alone: a checksum that
    /// fails then loses nothing decoded, as with curl (CGR-071, BL-3600).
    /// </summary>
    private static IEnumerable<int> FeedLengths(int length)
    {
        int head = Math.Min(length, SinglyFedLength);
        int tail = Math.Max(head, length - SinglyFedLength);
        for (int at = 0; at < head; at++)
        {
            yield return 1;
        }

        if (tail > head)
        {
            yield return tail - head;
        }

        for (int at = tail; at < length; at++)
        {
            yield return 1;
        }
    }

    /// <summary>
    /// Reads the decompression stream until it has decoded every byte fed to it.
    /// </summary>
    /// <param name="encoded">The encoded bytes of this piece of the body.</param>
    /// <param name="readSingly">
    /// The encoded bytes fed so far when the last was fed alone, to tell a failed trailer check
    /// from other corrupt data; empty otherwise.
    /// </param>
    private IEnumerable<ReadOnlyMemory<byte>> ReadAll(ReadOnlyMemory<byte> encoded, ReadOnlyMemory<byte> readSingly)
    {
        int read;
        while ((read = ReadDecoded(decompressor!, readSingly.Span)) > 0)
        {
            fedAtLastOutput = fedTotal;
            trailer?.Append(output.AsSpan(0, read));
            yield return output.AsMemory(0, read);

            // GZipStream goes on to decode a second gzip member; stop before it does.
            if (decompressor is GZipStream)
            {
                FindEnd(encoded.Span);
            }
        }

        // A trailer fed alone decodes nothing, and the next bytes can start a member
        // GZipStream fails on; find the end before they are fed. Before anything is decoded
        // the trailer sought is all zeros, which compressed zeros can hold.
        if (decompressor is GZipStream && fedAtLastOutput is not null)
        {
            FindEnd(encoded.Span);
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

        bool checkable = blockType switch
        {
            StoredBlockType => StoredHeaderIsChecked(first),
            DynamicBlockType => DynamicHeaderIsChecked(first),
            _ => true,
        };
        return checkable ? new DeflateStream(input, CompressionMode.Decompress) : null;
    }

    /// <summary>
    /// Throws zlib's text for a stored block whose length and its complement disagree; false while too few bytes have arrived to check.
    /// </summary>
    private static bool StoredHeaderIsChecked(ReadOnlySpan<byte> first)
    {
        if (first.Length < StoredBlockHeaderLength)
        {
            return false;
        }

        int length = first[1] | (first[2] << 8);
        int complement = first[3] | (first[4] << 8);
        return length == (~complement & 0xFFFF) ? true : throw Corrupt(HttpTransferMessages.InvalidStoredBlockLengths);
    }

    /// <summary>
    /// Throws zlib's text for a faulty dynamic block header; false while too few bytes have arrived to check.
    /// </summary>
    private static bool DynamicHeaderIsChecked(ReadOnlySpan<byte> first)
    {
        string? fault = DynamicHeaderFault(first, out bool complete);
        return fault is null ? complete : throw Corrupt(fault);
    }

    /// <summary>
    /// Applies zlib's checks of a dynamic block's header to the first bytes: the symbol counts
    /// and the code of code lengths (over-subscribed or incomplete). <paramref name="complete" />
    /// is false while too few bytes have arrived to check.
    /// </summary>
    private static string? DynamicHeaderFault(ReadOnlySpan<byte> first, out bool complete)
    {
        complete = false;
        if (first.Length * 8 < DynamicCountsEnd)
        {
            return null;
        }

        int hclen = ReadBits(first, 13, 4) + 4;
        if (ReadBits(first, 3, 5) + 257 > MaximumLengthSymbols || ReadBits(first, 8, 5) + 1 > MaximumDistanceSymbols)
        {
            complete = true;
            return HttpTransferMessages.TooManyLengthOrDistanceSymbols;
        }

        if (first.Length * 8 < DynamicCountsEnd + (hclen * 3))
        {
            return null;
        }

        complete = true;
        return CodeLengthCodeIsComplete(first, hclen) ? null : HttpTransferMessages.InvalidCodeLengthsSet;
    }

    /// <summary>
    /// Tells whether the 3-bit code lengths that follow the dynamic block's counts form a
    /// complete prefix code, or none at all, as zlib's table builder requires.
    /// </summary>
    private static bool CodeLengthCodeIsComplete(ReadOnlySpan<byte> first, int count)
    {
        Span<int> perLength = stackalloc int[8];
        for (int i = 0; i < count; i++)
        {
            perLength[ReadBits(first, DynamicCountsEnd + (i * 3), 3)]++;
        }

        int left = 1;
        for (int length = 1; length < perLength.Length; length++)
        {
            left = (left << 1) - perLength[length];
            if (left < 0)
            {
                return false;
            }
        }

        return left == 0 || perLength[1..].IndexOfAnyExcept(0) < 0;
    }

    private static int ReadBits(ReadOnlySpan<byte> bytes, int position, int count)
    {
        int value = 0;
        for (int i = 0; i < count; i++)
        {
            int bit = position + i;
            value |= ((bytes[bit >> 3] >> (bit & 7)) & 1) << i;
        }

        return value;
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
        _ = ReadDecoded(stream, []);
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
            throw StartsAnotherGzipMember(encoded[end..]) ? MultiMemberGzip() : BytesAfterTheEnd();
        }
    }

    /// <summary>
    /// Tells whether the bytes after a gzip member's end, as far as they arrived in the same
    /// read, start another gzip member: curl 8.22.0 names that case only when both magic bytes
    /// are there, and a lone <c>1F</c> is any other trailing byte (measured, BL-2169 Notes).
    /// </summary>
    private bool StartsAnotherGzipMember(ReadOnlySpan<byte> after) =>
        decompressor is GZipStream && after.Length >= 2 && after[0] == GzipFirstByte && after[1] == GzipSecondByte;

    private int ReadDecoded(Stream stream, ReadOnlySpan<byte> readSingly)
    {
        try
        {
            return stream.Read(output);
        }
        catch (InvalidDataException)
        {
            throw Corrupt(CheckFailed(readSingly) ?? HttpTransferMessages.BadContentEncoding);
        }
    }

    /// <summary>
    /// Tells which trailer check failed when the stream failed on a byte fed alone after it had
    /// decoded something; a stream without a trailer, one that failed on bytes fed together,
    /// or one that decoded nothing has no such answer.
    /// </summary>
    private string? CheckFailed(ReadOnlySpan<byte> readSingly) =>
        readSingly.IsEmpty || fedAtLastOutput is not long last
            ? null
            : trailer?.CheckFailed([.. carried, .. readSingly], fedTotal - last);

    private static HttpTransferException Corrupt(string message) => new(CurlExitCode.BadContentEncoding, message);

    private static HttpTransferException BytesAfterTheEnd() =>
        new(CurlExitCode.WriteError, HttpTransferMessages.ReceivedDataWriteFailed);

    private static HttpTransferException MultiMemberGzip() =>
        new(CurlExitCode.WriteError, HttpTransferMessages.MultiMemberGzipNotSupported);
}
