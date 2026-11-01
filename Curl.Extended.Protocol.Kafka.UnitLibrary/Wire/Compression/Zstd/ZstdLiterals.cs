namespace Curl.Extended.Protocol.Kafka.Wire.Compression.Zstd;

/// <summary>
/// Reads the literals section of a zstd compressed block (RFC 8878 section 3.1.1.3.1): raw, RLE, Huffman-compressed with
/// one or four streams, or treeless, which reuses the Huffman table of the last compressed section of the same frame.
/// One instance serves one frame.
/// </summary>
internal sealed class ZstdLiterals
{
    private const int RawType = 0;

    private const int RleType = 1;

    private const int CompressedType = 2;

    private const int JumpTableLength = 6;

    // Indexed by the size format.
    private static readonly int[] UncompressedHeaderLengths = [1, 2, 1, 3];

    private static readonly int[] CompressedSizeBits = [10, 10, 14, 18];

    private static readonly int[] CompressedHeaderLengths = [3, 3, 4, 5];

    private ZstdHuffmanTable? _previousTable;

    /// <summary>
    /// Gives the literals the section at the start of <paramref name="block"/> holds; <paramref name="bytesRead"/> is
    /// the section's length in bytes.
    /// </summary>
    internal byte[] Read(ReadOnlySpan<byte> block, out int bytesRead)
    {
        if (block.IsEmpty)
        {
            throw Corrupt("the literals section is missing");
        }

        int type = block[0] & 3;
        int sizeFormat = (block[0] >> 2) & 3;
        return type <= RleType ? ReadUncompressed(block, type, sizeFormat, out bytesRead) : ReadCompressed(block, type, sizeFormat, out bytesRead);
    }

    private static FormatException Corrupt(string message) => new("zstd: " + message);

    private static long ReadLittleEndian(ReadOnlySpan<byte> bytes)
    {
        long value = 0;
        for (int index = bytes.Length - 1; index >= 0; index--)
        {
            value = (value << 8) | bytes[index];
        }

        return value;
    }

    private static byte[] ReadUncompressed(ReadOnlySpan<byte> block, int type, int sizeFormat, out int bytesRead)
    {
        int headerLength = UncompressedHeaderLengths[sizeFormat];
        if (block.Length < headerLength)
        {
            throw Corrupt("the literals header is truncated");
        }

        int size = (int)(ReadLittleEndian(block[..headerLength]) >> (headerLength == 1 ? 3 : 4));
        int payloadLength = type == RawType ? size : 1;
        if (block.Length - headerLength < payloadLength)
        {
            throw Corrupt("the literals are truncated");
        }

        bytesRead = headerLength + payloadLength;
        if (type == RawType)
        {
            return block.Slice(headerLength, size).ToArray();
        }

        byte[] repeated = new byte[size];
        Array.Fill(repeated, block[headerLength]);
        return repeated;
    }

    private byte[] ReadCompressed(ReadOnlySpan<byte> block, int type, int sizeFormat, out int bytesRead)
    {
        int sizeBits = CompressedSizeBits[sizeFormat];
        int headerLength = CompressedHeaderLengths[sizeFormat];
        if (block.Length < headerLength)
        {
            throw Corrupt("the literals header is truncated");
        }

        long sizes = ReadLittleEndian(block[..headerLength]) >> 4;
        long mask = (1L << sizeBits) - 1;
        int regeneratedSize = (int)(sizes & mask);
        int compressedSize = (int)((sizes >> sizeBits) & mask);
        if (compressedSize == 0 || block.Length - headerLength < compressedSize)
        {
            throw Corrupt("the compressed literals are empty or truncated");
        }

        bytesRead = headerLength + compressedSize;
        ReadOnlySpan<byte> payload = block.Slice(headerLength, compressedSize);
        ZstdHuffmanTable table = SelectTable(type, ref payload);
        byte[] literals = new byte[regeneratedSize];
        if (sizeFormat == 0)
        {
            DecodeStream(table, payload, literals);
        }
        else
        {
            DecodeFourStreams(table, payload, literals);
        }

        return literals;
    }

    /// <summary>Reads the tree at the start of <paramref name="payload"/> unless the literals are treeless, and gives the table to decode with.</summary>
    private ZstdHuffmanTable SelectTable(int type, ref ReadOnlySpan<byte> payload)
    {
        if (type == CompressedType)
        {
            _previousTable = ZstdHuffmanTable.Read(payload, out int treeBytes);
            payload = payload[treeBytes..];
        }

        return _previousTable ?? throw Corrupt("treeless literals have no earlier Huffman table");
    }

    private static int StreamLength(ReadOnlySpan<byte> payload, int stream, int streamStart)
    {
        int length = stream < 3 ? (int)ReadLittleEndian(payload.Slice(stream * 2, 2)) : payload.Length - streamStart;
        if (length > payload.Length - streamStart)
        {
            throw Corrupt("a literals stream is longer than the section");
        }

        return length;
    }

    private static void DecodeFourStreams(ZstdHuffmanTable table, ReadOnlySpan<byte> payload, byte[] literals)
    {
        if (payload.Length < JumpTableLength)
        {
            throw Corrupt("the literals jump table is truncated");
        }

        int share = (literals.Length + 3) / 4;
        if (share * 3 > literals.Length)
        {
            throw Corrupt("too few literals for four streams");
        }

        int streamStart = JumpTableLength;
        for (int stream = 0; stream < 4; stream++)
        {
            int streamLength = StreamLength(payload, stream, streamStart);
            int regenerated = stream < 3 ? share : literals.Length - (3 * share);
            DecodeStream(table, payload.Slice(streamStart, streamLength), literals.AsSpan(stream * share, regenerated));
            streamStart += streamLength;
        }
    }

    private static void DecodeStream(ZstdHuffmanTable table, ReadOnlySpan<byte> stream, Span<byte> destination)
    {
        ZstdBackwardBitReader reader = new(stream);
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = table.DecodeSymbol(reader);
        }

        if (reader.RemainingBits != 0)
        {
            throw Corrupt("a literals stream does not end where its symbols do");
        }
    }
}
