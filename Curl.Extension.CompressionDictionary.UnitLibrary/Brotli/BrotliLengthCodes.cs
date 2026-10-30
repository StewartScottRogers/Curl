namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>The block length, insert length and copy length codes (RFC 7932 sections 5 and 6), WBITS (section 9.1) and VarLenUint8 (section 9.2).</summary>
internal static class BrotliLengthCodes
{
    /// <summary>The number of block length codes.</summary>
    public const int BlockLengthAlphabetSize = 26;

    /// <summary>The number of insert-and-copy length codes.</summary>
    public const int CommandAlphabetSize = 704;

    private static readonly int[] BlockLengthBase =
        [1, 5, 9, 13, 17, 25, 33, 41, 49, 65, 81, 97, 113, 145, 177, 209, 241, 305, 369, 497, 753, 1265, 2289, 4337, 8433, 16625];

    private static readonly int[] BlockLengthExtraBits =
        [2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 7, 8, 9, 10, 11, 12, 13, 24];

    private static readonly int[] InsertLengthBase =
        [0, 1, 2, 3, 4, 5, 6, 8, 10, 14, 18, 26, 34, 50, 66, 98, 130, 194, 322, 578, 1090, 2114, 6210, 22594];

    private static readonly int[] InsertLengthExtraBits =
        [0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 7, 8, 9, 10, 12, 14, 24];

    private static readonly int[] CopyLengthBase =
        [2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14, 18, 22, 30, 38, 54, 70, 102, 134, 198, 326, 582, 1094, 2118];

    private static readonly int[] CopyLengthExtraBits =
        [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 7, 8, 9, 10, 24];

    private static readonly int[] InsertCodeBaseByCell = [0, 0, 0, 0, 8, 8, 0, 8, 16, 16, 16];

    private static readonly int[] CopyCodeBaseByCell = [0, 8, 0, 8, 0, 8, 16, 16, 0, 8, 16];

    /// <summary>Reads a block length: its code, then the code's extra bits (section 6).</summary>
    public static int ReadBlockLength(BrotliPrefixCode code, BrotliBitReader reader)
    {
        var symbol = code.Decode(reader);
        return BlockLengthBase[symbol] + reader.ReadBits(BlockLengthExtraBits[symbol]);
    }

    /// <summary>Splits an insert-and-copy length code and reads both extra bit fields (section 5).</summary>
    public static BrotliCommand ReadCommand(int command, BrotliBitReader reader)
    {
        var cell = command >> 6;
        var insertCode = InsertCodeBaseByCell[cell] + ((command >> 3) & 7);
        var copyCode = CopyCodeBaseByCell[cell] + (command & 7);
        var insertLength = InsertLengthBase[insertCode] + reader.ReadBits(InsertLengthExtraBits[insertCode]);
        var copyLength = CopyLengthBase[copyCode] + reader.ReadBits(CopyLengthExtraBits[copyCode]);
        return new BrotliCommand(insertLength, copyLength, cell < 2);
    }

    /// <summary>Reads WBITS and returns the window size less 16, the largest backward distance (section 9.1).</summary>
    public static int ReadMaximumBackwardDistance(BrotliBitReader reader)
    {
        var windowBits = 16;
        if (reader.ReadBit() == 1)
        {
            var code = reader.ReadBits(3);
            windowBits = code != 0 ? 17 + code : ReadShortWindowBits(reader);
        }

        return (1 << windowBits) - 16;
    }

    /// <summary>Reads a VarLenUint8, 0 to 255 (section 9.2).</summary>
    public static int ReadVariableLengthByte(BrotliBitReader reader)
    {
        if (reader.ReadBit() == 0)
        {
            return 0;
        }

        var bitCount = reader.ReadBits(3);
        return bitCount == 0 ? 1 : (1 << bitCount) + reader.ReadBits(bitCount);
    }

    private static int ReadShortWindowBits(BrotliBitReader reader)
    {
        var code = reader.ReadBits(3);
        if (code == 1)
        {
            throw new BrotliFormatException("The Brotli window size code 0010001 is reserved (RFC 7932 section 9.1).");
        }

        return code == 0 ? 17 : 8 + code;
    }
}

/// <summary>One insert-and-copy command: the literals to insert, the bytes to copy, and whether it reuses the last distance (code 0) without a distance symbol.</summary>
internal readonly record struct BrotliCommand(int InsertLength, int CopyLength, bool UsesLastDistance);
