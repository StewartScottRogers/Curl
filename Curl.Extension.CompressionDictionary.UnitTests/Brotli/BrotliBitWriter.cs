namespace Curl.Extension.CompressionDictionary.UnitTests.Brotli;

/// <summary>Writes bits least significant first, as a Brotli stream holds them, to hand-build streams for the tests.</summary>
internal sealed class BrotliBitWriter
{
    private readonly List<byte> _bytes = [];
    private int _bitCount;

    public BrotliBitWriter Bits(int value, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if ((_bitCount & 7) == 0)
            {
                _bytes.Add(0);
            }

            _bytes[^1] |= (byte)(((value >> index) & 1) << (_bitCount & 7));
            _bitCount++;
        }

        return this;
    }

    public BrotliBitWriter AlignedBytes(params byte[] bytes)
    {
        _bitCount = _bytes.Count * 8;
        _bytes.AddRange(bytes);
        _bitCount += bytes.Length * 8;
        return this;
    }

    // RFC 7932 section 3.4: a simple prefix code over the given symbols.
    public BrotliBitWriter SimpleCode(int alphabetSize, params int[] symbols)
    {
        Bits(1, 2).Bits(symbols.Length - 1, 2);
        foreach (var symbol in symbols)
        {
            Bits(symbol, 32 - int.LeadingZeroCount(alphabetSize - 1));
        }

        return symbols.Length == 4 ? Bits(0, 1) : this;
    }

    // A last compressed meta-block of the given length with one block type per category, NPOSTFIX and NDIRECT 0,
    // and one-symbol literal and command codes; the commands' bits follow.
    public static BrotliBitWriter LastCompressedMetaBlock(int length, int command, params int[] distanceSymbols) =>
        new BrotliBitWriter()
            .Bits(0, 1)
            .Bits(1, 1).Bits(0, 1).Bits(0, 2).Bits(length - 1, 16)
            .Bits(0, 3).Bits(0, 2).Bits(0, 4).Bits(0, 2).Bits(0, 2)
            .SimpleCode(256, 'a')
            .SimpleCode(704, command)
            .SimpleCode(64, distanceSymbols);

    public byte[] ToArray() => [.. _bytes];
}
