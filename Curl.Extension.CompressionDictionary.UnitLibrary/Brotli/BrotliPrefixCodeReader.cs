namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>Reads a prefix code's description, simple or complex (RFC 7932 sections 3.4 and 3.5).</summary>
internal static class BrotliPrefixCodeReader
{
    private static readonly int[] CodeLengthCodeOrder = [1, 2, 3, 4, 0, 5, 17, 6, 16, 7, 8, 9, 10, 11, 12, 13, 14, 15];

    private static readonly int[][] SimpleCodeLengths = [[1], [1, 1], [1, 2, 2], [2, 2, 2, 2], [1, 2, 3, 3]];

    /// <summary>Reads the description of a code over <paramref name="alphabetSize"/> symbols.</summary>
    public static BrotliPrefixCode Read(BrotliBitReader reader, int alphabetSize)
    {
        var codeKind = reader.ReadBits(2);
        return codeKind == 1 ? ReadSimple(reader, alphabetSize) : ReadComplex(reader, alphabetSize, codeKind);
    }

    // RFC 7932 section 3.4: NSYM symbols of ALPHABET_BITS each, lengths fixed by NSYM and the tree-select bit.
    private static BrotliPrefixCode ReadSimple(BrotliBitReader reader, int alphabetSize)
    {
        var symbolCount = reader.ReadBits(2) + 1;
        var alphabetBits = 32 - int.LeadingZeroCount(alphabetSize - 1);
        var symbols = new int[symbolCount];
        for (var index = 0; index < symbolCount; index++)
        {
            symbols[index] = reader.ReadBits(alphabetBits);
        }

        var pattern = SimpleCodeLengths[symbolCount == 4 ? 3 + reader.ReadBit() : symbolCount - 1];
        var lengths = new int[alphabetSize];
        for (var index = 0; index < symbolCount; index++)
        {
            if (symbols[index] >= alphabetSize || lengths[symbols[index]] != 0)
            {
                throw new BrotliFormatException("A Brotli simple prefix code repeats a symbol or names one outside its alphabet (RFC 7932 section 3.4).");
            }

            lengths[symbols[index]] = pattern[index];
        }

        return new BrotliPrefixCode(lengths);
    }

    // RFC 7932 section 3.5: the code length code's lengths, then the symbols' lengths in that code.
    private static BrotliPrefixCode ReadComplex(BrotliBitReader reader, int alphabetSize, int skip)
    {
        var codeLengthCode = ReadCodeLengthCode(reader, skip);
        return new BrotliPrefixCode(new BrotliSymbolLengthReader(codeLengthCode, alphabetSize).Read(reader));
    }

    private static BrotliPrefixCode ReadCodeLengthCode(BrotliBitReader reader, int skip)
    {
        var lengths = new int[CodeLengthCodeOrder.Length];
        var space = 32;
        var nonZeroCount = 0;
        for (var index = skip; index < CodeLengthCodeOrder.Length && space > 0; index++)
        {
            var length = ReadCodeLengthCodeLength(reader);
            lengths[CodeLengthCodeOrder[index]] = length;
            if (length != 0)
            {
                space -= 32 >> length;
                nonZeroCount++;
            }
        }

        if (nonZeroCount != 1 && space != 0)
        {
            throw new BrotliFormatException("A Brotli code length code is not complete (RFC 7932 section 3.5).");
        }

        return new BrotliPrefixCode(lengths);
    }

    // The fixed code of RFC 7932 section 3.5, read right to left: 00 is 0, 0111 is 1, 011 is 2, 10 is 3, 01 is 4, 1111 is 5.
    private static int ReadCodeLengthCodeLength(BrotliBitReader reader) => reader.ReadBits(2) switch
    {
        0 => 0,
        1 => 4,
        2 => 3,
        _ => reader.ReadBit() == 0 ? 2 : 1 + (4 * reader.ReadBit()),
    };
}
