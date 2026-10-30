namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>One category's block types and block counts in a meta-block, switching type when a block runs out (RFC 7932 section 6).</summary>
internal sealed class BrotliBlockSwitch
{
    private readonly BrotliPrefixCode? _typeCode;
    private readonly BrotliPrefixCode? _countCode;
    private int _previousType = 1;
    private int _remaining;

    private BrotliBlockSwitch(int typeCount, BrotliPrefixCode? typeCode, BrotliPrefixCode? countCode, int remaining)
    {
        TypeCount = typeCount;
        _typeCode = typeCode;
        _countCode = countCode;
        _remaining = remaining;
    }

    /// <summary>NBLTYPES, the number of block types.</summary>
    public int TypeCount { get; }

    /// <summary>The block type in force.</summary>
    public int CurrentType { get; private set; }

    /// <summary>Reads NBLTYPES and, when there are two or more, the block type code, the block count code and the first block count.</summary>
    public static BrotliBlockSwitch Read(BrotliBitReader reader)
    {
        var typeCount = BrotliLengthCodes.ReadVariableLengthByte(reader) + 1;
        if (typeCount == 1)
        {
            return new BrotliBlockSwitch(1, null, null, int.MaxValue);
        }

        var typeCode = BrotliPrefixCodeReader.Read(reader, typeCount + 2);
        var countCode = BrotliPrefixCodeReader.Read(reader, BrotliLengthCodes.BlockLengthAlphabetSize);
        return new BrotliBlockSwitch(typeCount, typeCode, countCode, BrotliLengthCodes.ReadBlockLength(countCode, reader));
    }

    /// <summary>Takes one symbol from the current block, first reading a block switch command when the block is used up.</summary>
    public void CountDown(BrotliBitReader reader)
    {
        if (_remaining == 0)
        {
            var symbol = _typeCode!.Decode(reader);
            var type = symbol switch
            {
                0 => _previousType,
                1 => (CurrentType + 1) % TypeCount,
                _ => symbol - 2,
            };
            _previousType = CurrentType;
            CurrentType = type;
            _remaining = BrotliLengthCodes.ReadBlockLength(_countCode!, reader);
        }

        _remaining--;
    }
}
