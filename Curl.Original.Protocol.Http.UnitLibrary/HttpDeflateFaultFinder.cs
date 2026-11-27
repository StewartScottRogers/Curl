namespace Curl.Original.Protocol.Http;

/// <summary>
/// Walks a raw deflate stream (RFC 1951) the way zlib's inflate does, counting the bytes it
/// decodes rather than keeping them, to name the first fault zlib reports in it: the BCL's
/// streams fail on corrupt data without zlib's text, and curl prints that text (STD-208,
/// BL-6018). A stream that ends cleanly, or whose bytes run out before a fault, has none.
/// </summary>
internal sealed class HttpDeflateFaultFinder
{
    private const int MaximumCodeLength = 15;
    private const int EndOfBlock = 256;
    private const int FirstLengthSymbol = 257;
    private const int LastLengthSymbol = 285;
    private const int MaximumLengthSymbols = 286;
    private const int MaximumDistanceSymbols = 30;
    private const int CodeLengthSymbols = 19;
    private const int RepeatPrevious = 16;
    private const int RepeatZeroShort = 17;
    private const int StoredBlockType = 0;
    private const int FixedBlockType = 1;
    private const int DynamicBlockType = 2;

    private static readonly int[] LengthBase =
        [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258];

    private static readonly int[] LengthExtraBits =
        [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0];

    private static readonly int[] DistanceBase =
        [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577];

    private static readonly int[] DistanceExtraBits =
        [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13];

    /// <summary>The order a dynamic block gives the code lengths of its code of code lengths in.</summary>
    private static readonly int[] CodeLengthOrder = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];

    /// <summary>The fixed literal/length code; symbols 286 and 287 have codes but are invalid, as in zlib.</summary>
    private static readonly PrefixCode FixedLiterals = Code(FixedLiteralLengths());

    /// <summary>The fixed distance code: 30 symbols of 5 bits, so codes 30 and 31 are invalid, as in zlib.</summary>
    private static readonly PrefixCode FixedDistances = Code(Enumerable.Repeat(5, MaximumDistanceSymbols).ToArray());

    private readonly byte[] data;

    /// <summary>The next bit to read, counted from the first bit of <see cref="data" />.</summary>
    private int position;

    /// <summary>How many bytes the stream has decoded so far, which a copy may not reach further back than.</summary>
    private long decoded;

    private bool ranOut;

    private string? fault;

    private HttpDeflateFaultFinder(byte[] data) => this.data = data;

    /// <summary>
    /// Names zlib's text for the first fault in the raw deflate stream <paramref name="deflate" />
    /// starts, or returns <see langword="null" /> when the stream ends cleanly or its bytes run out first.
    /// </summary>
    internal static string? Find(ReadOnlySpan<byte> deflate)
    {
        HttpDeflateFaultFinder finder = new(deflate.ToArray());
        finder.WalkBlocks();
        return finder.fault;
    }

    private bool Continues => fault is null && !ranOut;

    private void WalkBlocks()
    {
        bool last = false;
        while (!last && Continues)
        {
            last = Bits(1) == 1;
            WalkBlock(Bits(2));
        }
    }

    // Plain conditions rather than a switch: a switch on two bits leaves the compiler's
    // out-of-range edge, which no input can take, as an uncovered branch.
    private void WalkBlock(int type)
    {
        if (type == StoredBlockType)
        {
            WalkStored();
        }
        else if (type == FixedBlockType)
        {
            WalkData(FixedLiterals, FixedDistances);
        }
        else if (type == DynamicBlockType)
        {
            WalkDynamic();
        }
        else
        {
            Fail(HttpTransferMessages.InvalidBlockType);
        }
    }

    private void WalkStored()
    {
        position = (position + 7) & ~7;
        int length = Bits(16);
        int complement = Bits(16);
        if (length != (~complement & 0xFFFF))
        {
            Fail(HttpTransferMessages.InvalidStoredBlockLengths);
            return;
        }

        position += length * 8;
        decoded += length;
        ranOut |= position > data.Length * 8;
    }

    private void WalkDynamic()
    {
        int literalCount = Bits(5) + FirstLengthSymbol;
        int distanceCount = Bits(5) + 1;
        int codeLengthCount = Bits(4) + 4;
        if (literalCount > MaximumLengthSymbols || distanceCount > MaximumDistanceSymbols)
        {
            Fail(HttpTransferMessages.TooManyLengthOrDistanceSymbols);
            return;
        }

        int[] codeLengthLengths = new int[CodeLengthSymbols];
        for (int i = 0; i < codeLengthCount; i++)
        {
            codeLengthLengths[CodeLengthOrder[i]] = Bits(3);
        }

        PrefixCode? codeLengthCode = Build(codeLengthLengths, allowsOneBitIncomplete: false);
        if (codeLengthCode is null)
        {
            Fail(HttpTransferMessages.InvalidCodeLengthsSet);
            return;
        }

        int[] lengths = ReadLengths(codeLengthCode, literalCount + distanceCount);
        if (Continues)
        {
            WalkDynamicData(lengths, literalCount);
        }
    }

    private int[] ReadLengths(PrefixCode codeLengthCode, int count)
    {
        int[] lengths = new int[count];
        int index = 0;
        while (index < count && Continues)
        {
            index = ReadLength(codeLengthCode, lengths, index);
        }

        return lengths;
    }

    /// <summary>
    /// Reads the next code length, or run of them, into <paramref name="lengths" /> at
    /// <paramref name="index" />, and returns the index after it. A code of code lengths with no
    /// codes at all reads every length as 0, as zlib's table for it does.
    /// </summary>
    private int ReadLength(PrefixCode codeLengthCode, int[] lengths, int index)
    {
        int symbol = Decode(codeLengthCode);
        if (symbol < RepeatPrevious)
        {
            lengths[index] = Math.Max(symbol, 0);
            return index + 1;
        }

        return ReadRepeat(symbol, lengths, index);
    }

    /// <summary>
    /// Reads the run code length symbol <paramref name="symbol" /> (16, 17 or 18) gives into
    /// <paramref name="lengths" /> at <paramref name="index" />, and returns the index after it.
    /// </summary>
    private int ReadRepeat(int symbol, int[] lengths, int index)
    {
        if (symbol == RepeatPrevious && index == 0)
        {
            Fail(HttpTransferMessages.InvalidBitLengthRepeat);
            return index;
        }

        int value = symbol == RepeatPrevious ? lengths[index - 1] : 0;
        int repeat = RepeatLength(symbol);
        if (index + repeat > lengths.Length)
        {
            Fail(HttpTransferMessages.InvalidBitLengthRepeat);
            return index;
        }

        lengths.AsSpan(index, repeat).Fill(value);
        return index + repeat;
    }

    private int RepeatLength(int symbol) => symbol switch
    {
        RepeatPrevious => 3 + Bits(2),
        RepeatZeroShort => 3 + Bits(3),
        _ => 11 + Bits(7),
    };

    private void WalkDynamicData(int[] lengths, int literalCount)
    {
        if (lengths[EndOfBlock] == 0)
        {
            Fail(HttpTransferMessages.MissingEndOfBlock);
            return;
        }

        PrefixCode? literals = Build(lengths.AsSpan(0, literalCount), allowsOneBitIncomplete: true);
        if (literals is null)
        {
            Fail(HttpTransferMessages.InvalidLiteralLengthsSet);
            return;
        }

        PrefixCode? distances = Build(lengths.AsSpan(literalCount), allowsOneBitIncomplete: true);
        if (distances is null)
        {
            Fail(HttpTransferMessages.InvalidDistancesSet);
            return;
        }

        WalkData(literals, distances);
    }

    private void WalkData(PrefixCode literals, PrefixCode distances)
    {
        int symbol = 0;
        while (symbol != EndOfBlock && Continues)
        {
            symbol = Decode(literals);
            WalkSymbol(symbol, distances);
        }
    }

    private void WalkSymbol(int symbol, PrefixCode distances)
    {
        if (symbol is < 0 or > LastLengthSymbol)
        {
            Fail(HttpTransferMessages.InvalidLiteralLengthCode);
        }
        else if (symbol < EndOfBlock)
        {
            decoded++;
        }
        else if (symbol > EndOfBlock)
        {
            WalkCopy(symbol - FirstLengthSymbol, distances);
        }
    }

    private void WalkCopy(int lengthIndex, PrefixCode distances)
    {
        int length = LengthBase[lengthIndex] + Bits(LengthExtraBits[lengthIndex]);
        int distanceSymbol = Decode(distances);
        if (distanceSymbol < 0)
        {
            Fail(HttpTransferMessages.InvalidDistanceCode);
            return;
        }

        int distance = DistanceBase[distanceSymbol] + Bits(DistanceExtraBits[distanceSymbol]);
        if (distance > decoded)
        {
            Fail(HttpTransferMessages.InvalidDistanceTooFarBack);
            return;
        }

        decoded += length;
    }

    /// <summary>Records <paramref name="message" /> as the fault, unless the bytes ran out first.</summary>
    private void Fail(string message) => fault = ranOut ? null : message;

    /// <summary>
    /// Reads the next <paramref name="count" /> bits, first bit lowest; past the last byte the
    /// bits read as 0 and the walk stops.
    /// </summary>
    private int Bits(int count)
    {
        int value = 0;
        for (int i = 0; i < count; i++, position++)
        {
            if (position >= data.Length * 8)
            {
                ranOut = true;
                return 0;
            }

            value |= ((data[position >> 3] >> (position & 7)) & 1) << i;
        }

        return value;
    }

    /// <summary>
    /// Reads the next code of <paramref name="code" />, first bit highest, and returns its
    /// symbol, or -1 for a code no symbol has.
    /// </summary>
    private int Decode(PrefixCode code)
    {
        int value = 0;
        int first = 0;
        int index = 0;
        for (int length = 1; length <= code.Longest; length++)
        {
            value |= Bits(1);
            int count = code.Counts[length];
            if (value - first < count)
            {
                return code.Symbols[index + value - first];
            }

            index += count;
            first = (first + count) << 1;
            value <<= 1;
        }

        return -1;
    }

    /// <summary>
    /// Builds the code <paramref name="lengths" /> give, or returns <see langword="null" /> where
    /// zlib refuses it: over-subscribed, or incomplete unless it has no codes at all or, where
    /// <paramref name="allowsOneBitIncomplete" />, only codes one bit long.
    /// </summary>
    private static PrefixCode? Build(ReadOnlySpan<int> lengths, bool allowsOneBitIncomplete)
    {
        PrefixCode code = Code(lengths);
        int left = CodeSpaceLeft(code.Counts);
        bool incompleteAllowed = code.Longest == 0 || (allowsOneBitIncomplete && code.Longest == 1);
        return left < 0 || (left > 0 && !incompleteAllowed) ? null : code;
    }

    /// <summary>
    /// Tells how much code space <paramref name="counts" /> leaves unused: negative when
    /// over-subscribed, positive when incomplete, 0 when complete.
    /// </summary>
    private static int CodeSpaceLeft(int[] counts)
    {
        int left = 1;
        for (int length = 1; length <= MaximumCodeLength && left >= 0; length++)
        {
            left = (left << 1) - counts[length];
        }

        return left;
    }

    /// <summary>Assigns the canonical codes <paramref name="lengths" /> give, unchecked.</summary>
    private static PrefixCode Code(ReadOnlySpan<int> lengths)
    {
        int[] counts = new int[MaximumCodeLength + 1];
        foreach (int length in lengths)
        {
            counts[length]++;
        }

        counts[0] = 0;
        int[] offsets = new int[MaximumCodeLength + 1];
        for (int length = 1; length < MaximumCodeLength; length++)
        {
            offsets[length + 1] = offsets[length] + counts[length];
        }

        int[] symbols = new int[lengths.Length];
        for (int symbol = 0; symbol < lengths.Length; symbol++)
        {
            if (lengths[symbol] != 0)
            {
                symbols[offsets[lengths[symbol]]++] = symbol;
            }
        }

        return new PrefixCode(counts, symbols, counts.AsSpan(1).LastIndexOfAnyExcept(0) + 1);
    }

    private static int[] FixedLiteralLengths()
    {
        int[] lengths = new int[288];
        lengths.AsSpan(0, 144).Fill(8);
        lengths.AsSpan(144, 112).Fill(9);
        lengths.AsSpan(256, 24).Fill(7);
        lengths.AsSpan(280, 8).Fill(8);
        return lengths;
    }

    /// <summary>A canonical prefix code: how many codes each length has, and the symbols in code order.</summary>
    private sealed record PrefixCode(int[] Counts, int[] Symbols, int Longest);
}
