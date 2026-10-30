namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>Reads a complex prefix code's symbol lengths through its code length code, with the repeat codes 16 and 17 (RFC 7932 section 3.5).</summary>
internal sealed class BrotliSymbolLengthReader(BrotliPrefixCode codeLengthCode, int alphabetSize)
{
    private const int FullSpace = 1 << 15;

    private readonly int[] _lengths = new int[alphabetSize];
    private int _symbol;
    private int _space = FullSpace;
    private int _previousLength = 8;
    private int _repeat;
    private int _repeatLength;

    /// <summary>Reads lengths until the code is complete; the symbols left have length 0.</summary>
    public int[] Read(BrotliBitReader reader)
    {
        while (_symbol < alphabetSize && _space > 0)
        {
            var codeLength = codeLengthCode.Decode(reader);
            if (codeLength < 16)
            {
                AddLength(codeLength);
            }
            else
            {
                AddRepeat(reader, codeLength);
            }
        }

        if (_space != 0)
        {
            throw new BrotliFormatException("A Brotli prefix code's lengths do not make a complete code (RFC 7932 section 3.5).");
        }

        return _lengths;
    }

    private void AddLength(int length)
    {
        _repeat = 0;
        _lengths[_symbol++] = length;
        if (length != 0)
        {
            _previousLength = length;
            _space -= FullSpace >> length;
        }
    }

    // Code 16 repeats the previous non-zero length, code 17 repeats zero; consecutive repeats of one kind multiply.
    private void AddRepeat(BrotliBitReader reader, int codeLength)
    {
        var length = codeLength == 16 ? _previousLength : 0;
        var added = ExtendRepeat(reader, codeLength == 16 ? 2 : 3, length);
        if (added > alphabetSize - _symbol)
        {
            throw new BrotliFormatException("A Brotli repeat code runs past the end of its alphabet (RFC 7932 section 3.5).");
        }

        _lengths.AsSpan(_symbol, added).Fill(length);
        _symbol += added;
        _space -= length == 0 ? 0 : added << (15 - length);
    }

    // Returns how many symbols the repeat adds: a repeat right after one of the same length extends it.
    private int ExtendRepeat(BrotliBitReader reader, int extraBits, int length)
    {
        if (_repeatLength != length)
        {
            _repeat = 0;
            _repeatLength = length;
        }

        var oldRepeat = _repeat;
        if (_repeat > 0)
        {
            _repeat = (_repeat - 2) << extraBits;
        }

        _repeat += reader.ReadBits(extraBits) + 3;
        return _repeat - oldRepeat;
    }
}
