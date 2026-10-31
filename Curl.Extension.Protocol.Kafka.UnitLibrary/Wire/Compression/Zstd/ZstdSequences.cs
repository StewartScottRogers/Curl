namespace Curl.Extension.Protocol.Kafka.Wire.Compression.Zstd;

/// <summary>
/// Reads the sequences section of a zstd compressed block (RFC 8878 section 3.1.1.3.2) and executes it: each sequence
/// copies literals, then a match from earlier output. Keeps the last FSE tables (for repeat mode) and the three recent
/// offsets across the blocks of one frame, so one instance serves one frame.
/// </summary>
internal sealed class ZstdSequences
{
    private const int PredefinedMode = 0;

    private const int RleMode = 1;

    private const int FseMode = 2;

    private const int MaximumLiteralLengthSymbol = 35;

    private const int MaximumOffsetSymbol = 31;

    private const int MaximumMatchLengthSymbol = 52;

    private const int MaximumLiteralLengthAccuracyLog = 9;

    private const int MaximumOffsetAccuracyLog = 8;

    private const int MaximumMatchLengthAccuracyLog = 9;

    private static readonly int[] LiteralLengthExtraBits =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 3, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
    ];

    private static readonly int[] MatchLengthExtraBits =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 2, 2, 3, 3, 4, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
    ];

    private static readonly int[] LiteralLengthBaselines = BuildBaselines(LiteralLengthExtraBits, 0);

    private static readonly int[] MatchLengthBaselines = BuildBaselines(MatchLengthExtraBits, 3);

    private ZstdFiniteStateEntropyTable? _literalLengthTable;

    private ZstdFiniteStateEntropyTable? _offsetTable;

    private ZstdFiniteStateEntropyTable? _matchLengthTable;

    private int _recentOffset0 = 1;

    private int _recentOffset1 = 4;

    private int _recentOffset2 = 8;

    /// <summary>
    /// Executes the sequences section <paramref name="section"/> over <paramref name="literals"/>, appending to
    /// <paramref name="output"/>, which also holds the earlier output that matches may copy from. The literals left
    /// after the last sequence are appended too.
    /// </summary>
    internal void Execute(ReadOnlySpan<byte> section, byte[] literals, List<byte> output)
    {
        int count = ReadCount(section, out int position);
        if (count == 0)
        {
            output.AddRange(literals);
            return;
        }

        if (position >= section.Length)
        {
            throw Corrupt("the sequence compression modes are missing");
        }

        byte modes = section[position++];
        if ((modes & 3) != 0)
        {
            throw Corrupt("the reserved bits of the sequence compression modes are set");
        }

        _literalLengthTable = ReadTable(modes >> 6, _literalLengthTable, ZstdFiniteStateEntropyTable.PredefinedLiteralLength, MaximumLiteralLengthAccuracyLog, MaximumLiteralLengthSymbol, section, ref position);
        _offsetTable = ReadTable((modes >> 4) & 3, _offsetTable, ZstdFiniteStateEntropyTable.PredefinedOffset, MaximumOffsetAccuracyLog, MaximumOffsetSymbol, section, ref position);
        _matchLengthTable = ReadTable((modes >> 2) & 3, _matchLengthTable, ZstdFiniteStateEntropyTable.PredefinedMatchLength, MaximumMatchLengthAccuracyLog, MaximumMatchLengthSymbol, section, ref position);
        int literalsUsed = ExecuteSequences(section[position..], count, literals, output);
        output.AddRange(literals.AsSpan(literalsUsed));
    }

    private static FormatException Corrupt(string message) => new("zstd: " + message);

    private static int[] BuildBaselines(int[] extraBits, int first)
    {
        int[] baselines = new int[extraBits.Length];
        int next = first;
        for (int code = 0; code < extraBits.Length; code++)
        {
            baselines[code] = next;
            next += 1 << extraBits[code];
        }

        return baselines;
    }

    private static int ReadCount(ReadOnlySpan<byte> section, out int position)
    {
        if (section.IsEmpty)
        {
            throw Corrupt("the sequences section is missing");
        }

        int first = section[0];
        if (first < 128)
        {
            position = 1;
            return first;
        }

        position = first < 255 ? 2 : 3;
        if (section.Length < position)
        {
            throw Corrupt("the sequence count is truncated");
        }

        return position == 2 ? ((first - 128) << 8) + section[1] : section[1] + (section[2] << 8) + 0x7F00;
    }

    private static ZstdFiniteStateEntropyTable ReadTable(
        int mode,
        ZstdFiniteStateEntropyTable? previous,
        ZstdFiniteStateEntropyTable predefined,
        int maxAccuracyLog,
        int maxSymbol,
        ReadOnlySpan<byte> section,
        ref int position)
    {
        switch (mode)
        {
            case PredefinedMode:
                return predefined;
            case RleMode:
                if (position >= section.Length || section[position] > maxSymbol)
                {
                    throw Corrupt("an RLE sequence symbol is missing or out of range");
                }

                return ZstdFiniteStateEntropyTable.Rle(section[position++]);
            case FseMode:
                ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read(section[position..], maxAccuracyLog, maxSymbol, out int bytesRead);
                position += bytesRead;
                return table;
            default:
                return previous ?? throw Corrupt("repeat mode has no earlier sequence table");
        }
    }

    private int ExecuteSequences(ReadOnlySpan<byte> stream, int count, byte[] literals, List<byte> output)
    {
        ZstdFiniteStateEntropyTable literalLengthTable = _literalLengthTable!;
        ZstdFiniteStateEntropyTable offsetTable = _offsetTable!;
        ZstdFiniteStateEntropyTable matchLengthTable = _matchLengthTable!;
        ZstdBackwardBitReader reader = new(stream);
        int literalLengthState = literalLengthTable.ReadInitialState(reader);
        int offsetState = offsetTable.ReadInitialState(reader);
        int matchLengthState = matchLengthTable.ReadInitialState(reader);
        int literalsUsed = 0;
        for (int sequence = 0; sequence < count; sequence++)
        {
            int offsetCode = offsetTable.Symbol(offsetState);
            int matchLengthCode = matchLengthTable.Symbol(matchLengthState);
            int literalLengthCode = literalLengthTable.Symbol(literalLengthState);
            long offsetValue = (1L << offsetCode) + reader.ReadBits(offsetCode);
            int matchLength = MatchLengthBaselines[matchLengthCode] + (int)reader.ReadBits(MatchLengthExtraBits[matchLengthCode]);
            int literalLength = LiteralLengthBaselines[literalLengthCode] + (int)reader.ReadBits(LiteralLengthExtraBits[literalLengthCode]);
            if (reader.Overflowed)
            {
                throw Corrupt("the sequence bit stream is truncated");
            }

            literalsUsed = CopyLiterals(literals, literalsUsed, literalLength, output);
            CopyMatch(ResolveOffset(offsetValue, literalLength), matchLength, output);
            if (sequence + 1 < count)
            {
                literalLengthState = literalLengthTable.NextState(literalLengthState, reader);
                matchLengthState = matchLengthTable.NextState(matchLengthState, reader);
                offsetState = offsetTable.NextState(offsetState, reader);
            }
        }

        if (reader.RemainingBits != 0)
        {
            throw Corrupt("the sequence bit stream does not end where its sequences do");
        }

        return literalsUsed;
    }

    private static int CopyLiterals(byte[] literals, int used, int length, List<byte> output)
    {
        if (length > literals.Length - used)
        {
            throw Corrupt("a sequence takes more literals than the block holds");
        }

        output.AddRange(literals.AsSpan(used, length));
        return used + length;
    }

    private static void CopyMatch(int offset, int length, List<byte> output)
    {
        if (offset > output.Count)
        {
            throw Corrupt("a match offset reaches before the start of the output");
        }

        for (int copied = 0; copied < length; copied++)
        {
            output.Add(output[output.Count - offset]);
        }
    }

    /// <summary>Turns an offset value into an offset, applying the recent offsets for the values 1 to 3 (RFC 8878 section 3.1.1.5).</summary>
    private int ResolveOffset(long offsetValue, int literalLength)
    {
        if (offsetValue > 3)
        {
            int newOffset = (int)Math.Min(offsetValue - 3, int.MaxValue);
            ShiftRecentOffsets(newOffset, 2);
            return newOffset;
        }

        int index = (int)offsetValue - 1 + (literalLength == 0 ? 1 : 0);
        int offset = new[] { _recentOffset0, _recentOffset1, _recentOffset2, _recentOffset0 - 1 }[index];
        if (offset == 0)
        {
            throw Corrupt("a repeat offset is zero");
        }

        if (index > 0)
        {
            ShiftRecentOffsets(offset, Math.Min(index, 2));
        }

        return offset;
    }

    /// <summary>Makes <paramref name="offset"/> the first recent offset, moving the older ones down; the third is dropped only when <paramref name="from"/> is 2.</summary>
    private void ShiftRecentOffsets(int offset, int from)
    {
        if (from == 2)
        {
            _recentOffset2 = _recentOffset1;
        }

        _recentOffset1 = _recentOffset0;
        _recentOffset0 = offset;
    }
}
