namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>
/// Reads a Binary HTTP message (RFC 9292) from the front: variable-length integers (RFC 9000
/// section 16), length-prefixed byte strings and the zero-valued padding at the end (RFC 9292
/// section 3.8). Every read past the end, or of a length longer than what is left, throws a
/// <see cref="BinaryHttpFormatException" />.
/// </summary>
/// <param name="input">The bytes to read.</param>
internal ref struct BinaryHttpReader(ReadOnlySpan<byte> input)
{
    private readonly ReadOnlySpan<byte> input = input;
    private int position;

    /// <summary>Gets a value indicating whether every byte has been read.</summary>
    internal readonly bool IsAtEnd => position == input.Length;

    /// <summary>Reads one variable-length integer.</summary>
    /// <exception cref="BinaryHttpFormatException">The input ends inside the integer.</exception>
    internal long ReadVariableLengthInteger()
    {
        if (IsAtEnd || input.Length - position < 1 << (input[position] >> 6))
        {
            throw new BinaryHttpFormatException("The Binary HTTP message ends inside a variable-length integer.");
        }

        int length = 1 << (input[position] >> 6);
        long value = input[position] & 0x3f;
        for (int index = 1; index < length; index++)
        {
            value = (value << 8) | input[position + index];
        }

        position += length;
        return value;
    }

    /// <summary>Reads a variable-length integer length and then that many bytes.</summary>
    /// <exception cref="BinaryHttpFormatException">The input ends inside the length, or the length is longer than what is left.</exception>
    internal ReadOnlySpan<byte> ReadLengthPrefixed()
    {
        long length = ReadVariableLengthInteger();
        if (length > input.Length - position)
        {
            throw new BinaryHttpFormatException(
                $"A Binary HTTP length of {length} is longer than the {input.Length - position} bytes left in the message.");
        }

        ReadOnlySpan<byte> bytes = input.Slice(position, (int)length);
        position += (int)length;
        return bytes;
    }

    /// <summary>Reads the rest of the input as padding, which must be all zero bytes (RFC 9292 section 3.8).</summary>
    /// <exception cref="BinaryHttpFormatException">A padding byte is not zero.</exception>
    internal void ReadPadding()
    {
        if (input[position..].ContainsAnyExcept((byte)0))
        {
            throw new BinaryHttpFormatException("The Binary HTTP message's padding holds a byte that is not zero.");
        }

        position = input.Length;
    }
}
