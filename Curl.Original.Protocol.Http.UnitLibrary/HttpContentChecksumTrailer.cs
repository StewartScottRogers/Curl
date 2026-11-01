using System.Buffers.Binary;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// The checksum trailer that ends a gzip member or a zlib stream, computed from the bytes it
/// decoded to, and found among the encoded bytes to tell where the stream ended.
/// </summary>
/// <remarks>
/// The BCL decompression streams read their source in blocks and never say how much of the
/// last block their stream used, so the end is found from the trailer instead: a gzip member
/// ends with the CRC-32 of its decoded bytes and their count, little-endian (RFC 1952), and a
/// zlib stream with the Adler-32 of its decoded bytes, big-endian (RFC 1950). The first place
/// the trailer appears is taken as the end (BL-281 Notes).
/// </remarks>
/// <param name="isGzip">
/// <see langword="true" /> for a gzip member's CRC-32 and size; <see langword="false" /> for a
/// zlib stream's Adler-32.
/// </param>
internal sealed class HttpContentChecksumTrailer(bool isGzip)
{
    private const uint CrcPolynomial = 0xEDB88320;

    private const uint AdlerModulus = 65521;

    /// <summary>
    /// The most bytes Adler-32 sums before its sums must be reduced to stay within 32 bits.
    /// </summary>
    private const int AdlerBlockSize = 5552;

    /// <summary>
    /// The length of a CRC-32 or Adler-32, and of a gzip member's size.
    /// </summary>
    private const int ChecksumLength = 4;

    /// <summary>
    /// The most encoded bytes that can follow the read that decoded a deflate stream's last
    /// byte before its trailer: the rest of a final match code, and an end-of-block code of up
    /// to 15 bits.
    /// </summary>
    private const int MostBytesAfterOutput = 3;

    private static readonly uint[] CrcTable = BuildCrcTable();

    private uint crc = uint.MaxValue;

    private uint size;

    private uint adlerLow = 1;

    private uint adlerHigh;

    /// <summary>
    /// Gets how many bytes the trailer holds, less one: the most of it that can have arrived
    /// before the encoded bytes it is searched for in.
    /// </summary>
    internal int CarriedLength => (isGzip ? 8 : 4) - 1;

    /// <summary>
    /// Adds decoded bytes to the checksum.
    /// </summary>
    /// <param name="decoded">The next decoded bytes.</param>
    internal void Append(ReadOnlySpan<byte> decoded)
    {
        if (isGzip)
        {
            AppendCrc(decoded);
        }
        else
        {
            AppendAdler(decoded);
        }
    }

    /// <summary>
    /// Finds where the trailer for the bytes decoded so far first ends within
    /// <paramref name="encoded" />.
    /// </summary>
    /// <param name="carried">
    /// The last <see cref="CarriedLength" /> encoded bytes before <paramref name="encoded" />,
    /// or fewer when fewer came before.
    /// </param>
    /// <param name="encoded">The encoded bytes to search.</param>
    /// <returns>
    /// How many bytes of <paramref name="encoded" /> the stream used, up to and including
    /// the trailer's last byte, or -1 when the trailer does not end within them.
    /// </returns>
    internal int EndIn(ReadOnlySpan<byte> carried, ReadOnlySpan<byte> encoded)
    {
        ReadOnlySpan<byte> trailer = Trailer();
        ReadOnlySpan<byte> joined = [.. carried, .. encoded[..Math.Min(encoded.Length, CarriedLength)]];
        int index = joined.IndexOf(trailer);
        if (index >= 0)
        {
            return index + trailer.Length - carried.Length;
        }

        index = encoded.IndexOf(trailer);
        return index < 0 ? -1 : index + trailer.Length;
    }

    /// <summary>
    /// Tells which of the trailer's checks the stream failed, from where it failed: zlib reads
    /// the trailer's 4-byte fields one after another, just after the deflate data's last
    /// decoded byte and end-of-block code, and fails as soon as one is complete and wrong.
    /// </summary>
    /// <param name="read">
    /// The encoded bytes read up to and including the one the stream failed on, at least
    /// <see cref="CarriedLength" /> + 1 of them.
    /// </param>
    /// <param name="readSinceOutput">
    /// How many encoded bytes were read after the last read that decoded something.
    /// </param>
    /// <returns>
    /// <see cref="HttpTransferMessages.IncorrectDataCheck" /> when the stream failed where the
    /// checksum ends and the 4 bytes there are not the checksum;
    /// <see cref="HttpTransferMessages.IncorrectLengthCheck" /> when a gzip member failed where
    /// its size ends after the checksum matched; otherwise <see langword="null" />, the failure
    /// being in the deflate data.
    /// </returns>
    internal string? CheckFailed(ReadOnlySpan<byte> read, long readSinceOutput)
    {
        ReadOnlySpan<byte> checksum = Trailer().AsSpan(0, ChecksumLength);
        if (EndsField(readSinceOutput, ChecksumLength) && !read[^ChecksumLength..].SequenceEqual(checksum))
        {
            return HttpTransferMessages.IncorrectDataCheck;
        }

        return isGzip && EndsField(readSinceOutput, 2 * ChecksumLength) && read[^(2 * ChecksumLength)..^ChecksumLength].SequenceEqual(checksum)
            ? HttpTransferMessages.IncorrectLengthCheck
            : null;
    }

    /// <summary>
    /// Tells whether a failure <paramref name="readSinceOutput" /> bytes after the last decoded
    /// output can be where a trailer field ending <paramref name="fieldEnd" /> bytes into the
    /// trailer ends: the deflate data's last decoded byte is followed by at most
    /// <see cref="MostBytesAfterOutput" /> bytes of end-of-block code before the trailer.
    /// </summary>
    private static bool EndsField(long readSinceOutput, int fieldEnd) =>
        readSinceOutput >= fieldEnd && readSinceOutput <= fieldEnd + MostBytesAfterOutput;

    private byte[] Trailer()
    {
        byte[] trailer = new byte[CarriedLength + 1];
        if (isGzip)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(trailer, ~crc);
            BinaryPrimitives.WriteUInt32LittleEndian(trailer.AsSpan(4), size);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(trailer, (adlerHigh << 16) | adlerLow);
        }

        return trailer;
    }

    private void AppendCrc(ReadOnlySpan<byte> decoded)
    {
        foreach (byte value in decoded)
        {
            crc = CrcTable[(byte)(crc ^ value)] ^ (crc >> 8);
        }

        size = unchecked(size + (uint)decoded.Length);
    }

    private void AppendAdler(ReadOnlySpan<byte> decoded)
    {
        while (!decoded.IsEmpty)
        {
            ReadOnlySpan<byte> block = decoded[..Math.Min(decoded.Length, AdlerBlockSize)];
            foreach (byte value in block)
            {
                adlerLow += value;
                adlerHigh += adlerLow;
            }

            adlerLow %= AdlerModulus;
            adlerHigh %= AdlerModulus;
            decoded = decoded[block.Length..];
        }
    }

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            uint entry = index;
            for (int bit = 0; bit < 8; bit++)
            {
                entry = (entry & 1) == 1 ? (entry >> 1) ^ CrcPolynomial : entry >> 1;
            }

            table[index] = entry;
        }

        return table;
    }
}
