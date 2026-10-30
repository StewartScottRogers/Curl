namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>The decoded bytes so far, with the prefix dictionary before them as history only distances reach (RFC 9842 section 4).</summary>
internal sealed class BrotliOutput(byte[] prefixDictionary)
{
    private byte[] _buffer = new byte[256];

    /// <summary>The number of bytes decoded.</summary>
    public int Length { get; private set; }

    /// <summary>The number of prefix dictionary bytes.</summary>
    public int PrefixDictionaryLength => prefixDictionary.Length;

    /// <summary>Makes room for <paramref name="count"/> more bytes.</summary>
    public void Reserve(int count)
    {
        if (Length + count > _buffer.Length)
        {
            Array.Resize(ref _buffer, Math.Max(Length + count, _buffer.Length * 2));
        }
    }

    /// <summary>Appends one byte.</summary>
    public void Append(byte value)
    {
        Reserve(1);
        _buffer[Length++] = value;
    }

    /// <summary>Appends bytes.</summary>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        Reserve(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(Length));
        Length += bytes.Length;
    }

    /// <summary>The byte <paramref name="back"/> places before the end, or 0 before the start (RFC 7932 section 7.1).</summary>
    public byte ByteBack(int back) => Length >= back ? _buffer[Length - back] : (byte)0;

    /// <summary>Copies <paramref name="length"/> bytes from <paramref name="distance"/> back; the copy may overlap what it writes.</summary>
    public void CopyBack(int distance, int length)
    {
        Reserve(length);
        for (var index = 0; index < length; index++)
        {
            _buffer[Length] = _buffer[Length - distance];
            Length++;
        }
    }

    /// <summary>Copies <paramref name="length"/> prefix dictionary bytes that end <paramref name="back"/> bytes before the dictionary's end, plus one.</summary>
    public void CopyFromPrefixDictionary(int back, int length)
    {
        var start = prefixDictionary.Length - back;
        if (length > back)
        {
            throw new BrotliFormatException("A Brotli copy from the prefix dictionary runs past its end (RFC 9842 section 4).");
        }

        Append(prefixDictionary.AsSpan(start, length));
    }

    /// <summary>The decoded bytes.</summary>
    public byte[] ToArray() => _buffer.AsSpan(0, Length).ToArray();
}
