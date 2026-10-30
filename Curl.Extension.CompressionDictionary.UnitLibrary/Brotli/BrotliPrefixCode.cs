namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>A canonical prefix code built from its code lengths, decoded one bit at a time (RFC 7932 section 3.2).</summary>
internal sealed class BrotliPrefixCode
{
    private const int MaximumLength = 15;

    private readonly int[] _countPerLength = new int[MaximumLength + 1];
    private readonly int[] _symbolsInCodeOrder;

    /// <summary>Builds the code; a symbol of length 0 is absent, and a code with one symbol takes no bits.</summary>
    public BrotliPrefixCode(ReadOnlySpan<int> lengths)
    {
        foreach (var length in lengths)
        {
            _countPerLength[length]++;
        }

        var offsets = new int[MaximumLength + 1];
        for (var length = 1; length < MaximumLength; length++)
        {
            offsets[length + 1] = offsets[length] + _countPerLength[length];
        }

        _symbolsInCodeOrder = new int[lengths.Length - _countPerLength[0]];
        for (var symbol = 0; symbol < lengths.Length; symbol++)
        {
            if (lengths[symbol] != 0)
            {
                _symbolsInCodeOrder[offsets[lengths[symbol]]++] = symbol;
            }
        }
    }

    /// <summary>Reads one symbol. The code is complete, so the walk always ends on a symbol.</summary>
    public int Decode(BrotliBitReader reader)
    {
        if (_symbolsInCodeOrder.Length == 1)
        {
            return _symbolsInCodeOrder[0];
        }

        var code = 0;
        var first = 0;
        var index = 0;
        for (var length = 1; ; length++)
        {
            code |= reader.ReadBit();
            var count = _countPerLength[length];
            if (code - first < count)
            {
                return _symbolsInCodeOrder[index + code - first];
            }

            index += count;
            first = (first + count) << 1;
            code <<= 1;
        }
    }
}
