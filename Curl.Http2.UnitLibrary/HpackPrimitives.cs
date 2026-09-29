using System.Text;

namespace Curl.Http2;

/// <summary>
/// HPACK's two primitive types: prefixed integers (RFC 7541 section 5.1) and string
/// literals (RFC 7541 section 5.2).
/// </summary>
internal static class HpackPrimitives
{
    private const int LargestContinuationShift = 28;

    /// <summary>
    /// Appends <paramref name="value" /> as an integer with an N-bit prefix, the first byte
    /// carrying <paramref name="firstByteFlags" /> in the bits above the prefix.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="value">The integer, zero or more.</param>
    /// <param name="prefixBits">The prefix size N, 1 to 8.</param>
    /// <param name="firstByteFlags">The bits above the prefix in the first byte.</param>
    public static void WriteInteger(List<byte> output, int value, int prefixBits, byte firstByteFlags)
    {
        var prefixLimit = (1 << prefixBits) - 1;
        if (value < prefixLimit)
        {
            output.Add((byte)(firstByteFlags | value));
            return;
        }

        output.Add((byte)(firstByteFlags | prefixLimit));
        var remainder = value - prefixLimit;
        while (remainder >= 0x80)
        {
            output.Add((byte)((remainder & 0x7F) | 0x80));
            remainder >>= 7;
        }

        output.Add((byte)remainder);
    }

    /// <summary>
    /// Reads an integer with an N-bit prefix starting at <paramref name="position" />, and
    /// moves <paramref name="position" /> past it.
    /// </summary>
    /// <param name="block">The header block.</param>
    /// <param name="position">Where the integer starts; on return, where it ended.</param>
    /// <param name="prefixBits">The prefix size N, 1 to 8.</param>
    /// <returns>The integer.</returns>
    /// <exception cref="HpackDecodingException">
    /// The block ends inside the integer, or the integer does not fit in 31 bits.
    /// </exception>
    public static int ReadInteger(ReadOnlySpan<byte> block, ref int position, int prefixBits)
    {
        var prefixLimit = (1 << prefixBits) - 1;
        var value = (long)(ReadByte(block, ref position) & prefixLimit);
        if (value < prefixLimit)
        {
            return (int)value;
        }

        for (var shift = 0; ; shift += 7)
        {
            ThrowIf(shift > LargestContinuationShift, HpackDecodingError.IntegerOverflow);
            var octet = ReadByte(block, ref position);
            value += (long)(octet & 0x7F) << shift;
            ThrowIf(value > int.MaxValue, HpackDecodingError.IntegerOverflow);
            if ((octet & 0x80) == 0)
            {
                return (int)value;
            }
        }
    }

    /// <summary>
    /// Appends a string literal, Huffman-coded when that is strictly shorter than the raw
    /// bytes, as nghttp2's deflater chooses.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="text">The string; each character is one byte.</param>
    public static void WriteString(List<byte> output, string text)
    {
        var raw = Encoding.Latin1.GetBytes(text);
        if (HpackHuffman.GetEncodedLength(raw) < raw.Length)
        {
            var coded = HpackHuffman.Encode(raw);
            WriteInteger(output, coded.Length, 7, 0x80);
            output.AddRange(coded);
            return;
        }

        WriteInteger(output, raw.Length, 7, 0x00);
        output.AddRange(raw);
    }

    /// <summary>
    /// Reads a string literal starting at <paramref name="position" />, and moves
    /// <paramref name="position" /> past it.
    /// </summary>
    /// <param name="block">The header block.</param>
    /// <param name="position">Where the literal starts; on return, where it ended.</param>
    /// <returns>The string; each character is one byte.</returns>
    /// <exception cref="HpackDecodingException">
    /// The block ends inside the literal, or its Huffman coding is invalid.
    /// </exception>
    public static string ReadString(ReadOnlySpan<byte> block, ref int position)
    {
        ThrowIf(position >= block.Length, HpackDecodingError.TruncatedBlock);
        var isHuffmanCoded = (block[position] & 0x80) != 0;
        var length = ReadInteger(block, ref position, 7);
        ThrowIf(length > block.Length - position, HpackDecodingError.TruncatedBlock);
        var bytes = block.Slice(position, length);
        position += length;
        return isHuffmanCoded
            ? Encoding.Latin1.GetString(HpackHuffman.Decode(bytes))
            : Encoding.Latin1.GetString(bytes);
    }

    /// <summary>
    /// Throws <see cref="HpackDecodingException" /> with <paramref name="error" /> when
    /// <paramref name="condition" /> holds.
    /// </summary>
    /// <param name="condition">Whether the input broke the rule.</param>
    /// <param name="error">The rule it broke.</param>
    public static void ThrowIf(bool condition, HpackDecodingError error)
    {
        if (condition)
        {
            throw new HpackDecodingException(error);
        }
    }

    private static byte ReadByte(ReadOnlySpan<byte> block, ref int position)
    {
        ThrowIf(position >= block.Length, HpackDecodingError.TruncatedBlock);
        return block[position++];
    }
}
