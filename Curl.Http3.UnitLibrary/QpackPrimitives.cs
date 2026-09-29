using System.Text;
using Curl.Http2;

namespace Curl.Http3;

/// <summary>
/// QPACK's two primitive types: prefixed integers (RFC 9204 section 4.1.1, HPACK's
/// integers widened to 62 bits) and string literals (RFC 9204 section 4.1.2, whose Huffman
/// flag sits immediately above a prefix of any size).
/// </summary>
internal static class QpackPrimitives
{
    /// <summary>The largest integer QPACK carries: 2^62 - 1, a QUIC variable-length integer's limit.</summary>
    public const long LargestInteger = (1L << 62) - 1;

    private const int LargestContinuationShift = 56;

    /// <summary>
    /// Appends <paramref name="value" /> as an integer with an N-bit prefix, the first byte
    /// carrying <paramref name="firstByteFlags" /> in the bits above the prefix.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="value">The integer, zero or more.</param>
    /// <param name="prefixBits">The prefix size N, 1 to 8.</param>
    /// <param name="firstByteFlags">The bits above the prefix in the first byte.</param>
    public static void WriteInteger(List<byte> output, long value, int prefixBits, byte firstByteFlags)
    {
        var prefixLimit = (1L << prefixBits) - 1;
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
    /// <param name="input">The instructions or field lines.</param>
    /// <param name="position">Where the integer starts; on return, where it ended.</param>
    /// <param name="prefixBits">The prefix size N, 1 to 8.</param>
    /// <param name="error">The connection error an integer past 2^62 - 1 calls for.</param>
    /// <returns>The integer.</returns>
    /// <exception cref="QpackIncompleteInstructionException">The input ends inside the integer.</exception>
    /// <exception cref="QpackException">The integer is larger than 2^62 - 1.</exception>
    public static long ReadInteger(ReadOnlySpan<byte> input, ref int position, int prefixBits, QpackErrorCode error)
    {
        var prefixLimit = (1UL << prefixBits) - 1;
        var value = ReadByte(input, ref position) & prefixLimit;
        if (value < prefixLimit)
        {
            return (long)value;
        }

        for (var shift = 0; ; shift += 7)
        {
            ThrowIf(shift > LargestContinuationShift, error, "an integer is longer than 62 bits");
            var octet = ReadByte(input, ref position);
            value += (octet & 0x7FUL) << shift;
            ThrowIf(value > LargestInteger, error, "an integer is larger than 2^62 - 1");
            if ((octet & 0x80) == 0)
            {
                return (long)value;
            }
        }
    }

    /// <summary>
    /// Appends a string literal whose length has an N-bit prefix and whose Huffman flag is
    /// the bit above it. The string is Huffman-coded when <paramref name="huffmanCodeWhenShorter" />
    /// is set and coding makes it strictly shorter than its raw bytes.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="text">The string; each character is one byte.</param>
    /// <param name="prefixBits">The length's prefix size N, 3 to 7.</param>
    /// <param name="firstByteFlags">The bits above the Huffman flag in the first byte.</param>
    /// <param name="huffmanCodeWhenShorter">Whether Huffman coding may be used.</param>
    public static void WriteString(List<byte> output, string text, int prefixBits, byte firstByteFlags, bool huffmanCodeWhenShorter)
    {
        var raw = Encoding.Latin1.GetBytes(text);
        if (huffmanCodeWhenShorter && HpackHuffman.GetEncodedLength(raw) < raw.Length)
        {
            var coded = HpackHuffman.Encode(raw);
            WriteInteger(output, coded.Length, prefixBits, (byte)(firstByteFlags | (1 << prefixBits)));
            output.AddRange(coded);
            return;
        }

        WriteInteger(output, raw.Length, prefixBits, firstByteFlags);
        output.AddRange(raw);
    }

    /// <summary>
    /// Reads a string literal whose length has an N-bit prefix and whose Huffman flag is the
    /// bit above it, and moves <paramref name="position" /> past it.
    /// </summary>
    /// <param name="input">The instructions or field lines.</param>
    /// <param name="position">Where the literal starts; on return, where it ended.</param>
    /// <param name="prefixBits">The length's prefix size N, 3 to 7.</param>
    /// <param name="error">The connection error an invalid literal calls for.</param>
    /// <returns>The string; each character is one byte.</returns>
    /// <exception cref="QpackIncompleteInstructionException">The input ends inside the literal.</exception>
    /// <exception cref="QpackException">The length is too large, or the Huffman coding is invalid.</exception>
    public static string ReadString(ReadOnlySpan<byte> input, ref int position, int prefixBits, QpackErrorCode error)
    {
        var isHuffmanCoded = (PeekByte(input, position) & (1 << prefixBits)) != 0;
        var length = ReadInteger(input, ref position, prefixBits, error);
        if (length > input.Length - position)
        {
            throw new QpackIncompleteInstructionException();
        }

        var bytes = input.Slice(position, (int)length);
        position += (int)length;
        return isHuffmanCoded ? DecodeHuffman(bytes, error) : Encoding.Latin1.GetString(bytes);
    }

    /// <summary>
    /// Gets the byte at <paramref name="position" /> without moving past it.
    /// </summary>
    /// <param name="input">The instructions or field lines.</param>
    /// <param name="position">Where the byte is.</param>
    /// <returns>The byte.</returns>
    /// <exception cref="QpackIncompleteInstructionException">The input ends before the byte.</exception>
    public static byte PeekByte(ReadOnlySpan<byte> input, int position)
    {
        if (position >= input.Length)
        {
            throw new QpackIncompleteInstructionException();
        }

        return input[position];
    }

    /// <summary>
    /// Throws <see cref="QpackException" /> with <paramref name="error" /> when
    /// <paramref name="condition" /> holds.
    /// </summary>
    /// <param name="condition">Whether the input broke the rule.</param>
    /// <param name="error">The connection error breaking it calls for.</param>
    /// <param name="reason">The rule, as a phrase.</param>
    public static void ThrowIf(bool condition, QpackErrorCode error, string reason)
    {
        if (condition)
        {
            throw new QpackException(error, reason);
        }
    }

    private static byte ReadByte(ReadOnlySpan<byte> input, ref int position)
    {
        var octet = PeekByte(input, position);
        position++;
        return octet;
    }

    private static string DecodeHuffman(ReadOnlySpan<byte> coded, QpackErrorCode error)
    {
        try
        {
            return Encoding.Latin1.GetString(HpackHuffman.Decode(coded));
        }
        catch (HpackDecodingException exception)
        {
            throw new QpackException(error, $"a Huffman-coded string is invalid ({exception.Error})");
        }
    }
}
