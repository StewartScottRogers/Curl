using System.Buffers.Binary;

namespace Curl.Protocol.Http;

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
