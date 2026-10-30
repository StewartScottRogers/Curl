namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>Reads a Brotli stream's bits, least significant bit of each byte first (RFC 7932 section 2).</summary>
internal sealed class BrotliBitReader(byte[] data)
{
    private long _bitPosition;

    /// <summary>Whether every byte of the stream has been read.</summary>
    public bool IsAtEnd => _bitPosition >> 3 == data.Length;

    /// <summary>Reads one bit.</summary>
    public int ReadBit()
    {
        var byteIndex = _bitPosition >> 3;
        if (byteIndex >= data.Length)
        {
            throw new BrotliFormatException("The Brotli stream ends before its last meta-block does (RFC 7932 section 9.1).");
        }

        var bit = (data[byteIndex] >> (int)(_bitPosition & 7)) & 1;
        _bitPosition++;
        return bit;
    }

    /// <summary>Reads <paramref name="count"/> bits (at most 24) as an integer, the first bit read the least significant.</summary>
    public int ReadBits(int count)
    {
        var value = 0;
        for (var index = 0; index < count; index++)
        {
            value |= ReadBit() << index;
        }

        return value;
    }

    /// <summary>Skips to the next byte boundary; the bits skipped must be zero (RFC 7932 section 9.2).</summary>
    public void AlignToByte()
    {
        while ((_bitPosition & 7) != 0)
        {
            if (ReadBit() != 0)
            {
                throw new BrotliFormatException("A Brotli padding bit is not zero (RFC 7932 section 9.2).");
            }
        }
    }

    /// <summary>Reads <paramref name="count"/> whole bytes; call <see cref="AlignToByte"/> first.</summary>
    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        var start = (int)(_bitPosition >> 3);
        if (count > data.Length - start)
        {
            throw new BrotliFormatException("The Brotli stream ends inside a meta-block's bytes (RFC 7932 section 9.2).");
        }

        _bitPosition += (long)count << 3;
        return data.AsSpan(start, count);
    }
}
