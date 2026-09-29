namespace Curl.Zstandard;

/// <summary>
/// A literals section header (RFC 8878 section 3.1.1.3.1.1): the literals' type, how many
/// bytes they regenerate, and how many bytes follow the header.
/// </summary>
/// <param name="Type">The <c>Literals_Block_Type</c>.</param>
/// <param name="HeaderLength">The header's own length, 1 to 5 bytes.</param>
/// <param name="RegeneratedSize">The number of literal bytes the section decodes to.</param>
/// <param name="ContentLength">
/// The bytes after the header: <c>Regenerated_Size</c> for raw literals, 1 for RLE, and
/// <c>Compressed_Size</c> (tree description and jump table included) for Huffman-coded ones.
/// </param>
/// <param name="StreamCount">The number of Huffman streams: 1 or 4.</param>
internal readonly record struct ZstandardLiteralsHeader(ZstandardLiteralsType Type, int HeaderLength, int RegeneratedSize, int ContentLength, int StreamCount)
{
    /// <summary>The size fields' width, in bits, of Huffman-coded literals for each <c>Size_Format</c>.</summary>
    private static ReadOnlySpan<byte> HuffmanSizeWidths => [10, 10, 14, 18];

    /// <summary>
    /// Reads the header at the start of <paramref name="section" />, which is not empty;
    /// <see langword="false" /> when <paramref name="section" /> is shorter than the header.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> section, out ZstandardLiteralsHeader header)
    {
        header = default;
        var type = (ZstandardLiteralsType)(section[0] & 3);
        var sizeFormat = (section[0] >> 2) & 3;
        var headerLength = type < ZstandardLiteralsType.Compressed ? UncompressedHeaderLength(sizeFormat) : Math.Max(3, sizeFormat + 2);
        if (section.Length < headerLength)
        {
            return false;
        }

        var fields = ZstandardBitLoad.LittleEndian64(section[..headerLength], 0);
        header = type < ZstandardLiteralsType.Compressed ? Uncompressed(type, headerLength, fields) : Huffman(type, headerLength, fields, sizeFormat);
        return true;
    }

    /// <summary>Raw and RLE: <c>Size_Format</c> 0 and 2 take 1 byte, 1 takes 2 and 3 takes 3.</summary>
    private static int UncompressedHeaderLength(int sizeFormat) => (sizeFormat & 1) == 0 ? 1 : sizeFormat / 2 + 2;

    private static ZstandardLiteralsHeader Uncompressed(ZstandardLiteralsType type, int headerLength, ulong fields)
    {
        var regeneratedSize = (int)(headerLength == 1 ? fields >> 3 : fields >> 4);
        var contentLength = type == ZstandardLiteralsType.Raw ? regeneratedSize : 1;
        return new ZstandardLiteralsHeader(type, headerLength, regeneratedSize, contentLength, 1);
    }

    private static ZstandardLiteralsHeader Huffman(ZstandardLiteralsType type, int headerLength, ulong fields, int sizeFormat)
    {
        var width = HuffmanSizeWidths[sizeFormat];
        var regeneratedSize = (int)ZstandardBitLoad.LowBits(fields >> 4, width);
        var compressedSize = (int)ZstandardBitLoad.LowBits(fields >> (4 + width), width);
        return new ZstandardLiteralsHeader(type, headerLength, regeneratedSize, compressedSize, sizeFormat == 0 ? 1 : 4);
    }
}
