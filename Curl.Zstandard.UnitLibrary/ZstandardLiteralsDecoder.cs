using System.Buffers.Binary;

namespace Curl.Zstandard;

/// <summary>
/// Decodes the literals section of each compressed block in a frame (RFC 8878 section
/// 3.1.1.3.1), keeping the Huffman table of the last compressed literals for the treeless
/// literals of later blocks.
/// </summary>
internal sealed class ZstandardLiteralsDecoder
{
    /// <summary>libzstd's <c>MIN_LITERALS_FOR_4_STREAMS</c>: four Huffman streams regenerate at least 6 bytes.</summary>
    private const int MinimumFourStreamSize = 6;

    /// <summary>Four streams start with a jump table of three 2-byte stream sizes.</summary>
    private const int JumpTableLength = 6;

    private readonly byte[] literals;

    private ZstandardHuffmanTable? previousTable;

    private int literalsLength;

    /// <summary>Creates a decoder whose literals regenerate at most <paramref name="blockSizeLimit" /> bytes per block.</summary>
    public ZstandardLiteralsDecoder(int blockSizeLimit) => literals = new byte[blockSizeLimit];

    /// <summary>The literals the last successful <see cref="Decode" /> regenerated.</summary>
    public ReadOnlySpan<byte> Literals => literals.AsSpan(0, literalsLength);

    /// <summary>Forgets the previous Huffman table, as a new frame starts.</summary>
    public void StartFrame() => previousTable = null;

    /// <summary>
    /// Decodes the literals section at the start of <paramref name="block" />, which is not empty, allowing at
    /// most <paramref name="maxLiteralsSize" /> literals, and sets <paramref name="sectionLength" />
    /// to its length; returns why it is invalid, or <see cref="ZstandardDecodeError.None" />.
    /// </summary>
    public ZstandardDecodeError Decode(ReadOnlySpan<byte> block, int maxLiteralsSize, out int sectionLength)
    {
        sectionLength = 0;
        if (!ZstandardLiteralsHeader.TryRead(block, out var header))
        {
            return ZstandardDecodeError.CorruptionDetected;
        }

        var error = CheckHeader(header, block.Length, maxLiteralsSize);
        if (error != ZstandardDecodeError.None)
        {
            return error;
        }

        sectionLength = header.HeaderLength + header.ContentLength;
        literalsLength = header.RegeneratedSize;
        var content = block.Slice(header.HeaderLength, header.ContentLength);
        return DecodeContent(header, content, literals.AsSpan(0, literalsLength))
            ? ZstandardDecodeError.None
            : ZstandardDecodeError.CorruptionDetected;
    }

    /// <summary>Returns what is wrong with <paramref name="header" /> in a block of <paramref name="blockLength" /> bytes, or <see cref="ZstandardDecodeError.None" />.</summary>
    private static ZstandardDecodeError CheckHeader(ZstandardLiteralsHeader header, int blockLength, int maxLiteralsSize)
    {
        if (header.RegeneratedSize > maxLiteralsSize || header.ContentLength > blockLength - header.HeaderLength)
        {
            return ZstandardDecodeError.CorruptionDetected;
        }

        return header.StreamCount == 4 && header.RegeneratedSize < MinimumFourStreamSize
            ? ZstandardDecodeError.LiteralsHeaderWrong
            : ZstandardDecodeError.None;
    }

    private bool DecodeContent(ZstandardLiteralsHeader header, ReadOnlySpan<byte> content, Span<byte> destination)
    {
        switch (header.Type)
        {
            case ZstandardLiteralsType.Raw:
                content.CopyTo(destination);
                return true;
            case ZstandardLiteralsType.Rle:
                destination.Fill(content[0]);
                return true;
            default:
                return DecodeHuffman(header, content, destination);
        }
    }

    private bool DecodeHuffman(ZstandardLiteralsHeader header, ReadOnlySpan<byte> content, Span<byte> destination)
    {
        var treeLength = 0;
        var table = header.Type == ZstandardLiteralsType.Compressed ? ZstandardHuffmanTable.Read(content, out treeLength) : previousTable;
        if (table is null)
        {
            return false;
        }

        previousTable = table;
        var streams = content[treeLength..];
        return header.StreamCount == 1 ? table.TryDecodeStream(streams, destination) : DecodeFourStreams(table, streams, destination);
    }

    /// <summary>
    /// RFC 8878 section 3.1.1.3.1.6: a jump table gives the first three streams' sizes and the
    /// fourth takes the rest; each of the first three regenerates <c>(Regenerated_Size + 3) / 4</c>
    /// bytes and the fourth the remainder.
    /// </summary>
    private static bool DecodeFourStreams(ZstandardHuffmanTable table, ReadOnlySpan<byte> content, Span<byte> destination)
    {
        if (content.Length < JumpTableLength)
        {
            return false;
        }

        var segmentLength = (destination.Length + 3) / 4;
        var streams = content[JumpTableLength..];
        for (var stream = 0; stream < 3; stream++)
        {
            var streamLength = BinaryPrimitives.ReadUInt16LittleEndian(content[(stream * 2)..]);
            if (!TryDecodeNextStream(table, ref streams, streamLength, destination[..segmentLength]))
            {
                return false;
            }

            destination = destination[segmentLength..];
        }

        return table.TryDecodeStream(streams, destination);
    }

    /// <summary>Decodes the first <paramref name="streamLength" /> bytes of <paramref name="streams" /> into <paramref name="segment" /> and moves past them.</summary>
    private static bool TryDecodeNextStream(ZstandardHuffmanTable table, ref ReadOnlySpan<byte> streams, int streamLength, Span<byte> segment)
    {
        if (streamLength > streams.Length || !table.TryDecodeStream(streams[..streamLength], segment))
        {
            return false;
        }

        streams = streams[streamLength..];
        return true;
    }
}
