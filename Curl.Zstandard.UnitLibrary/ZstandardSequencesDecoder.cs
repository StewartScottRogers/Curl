namespace Curl.Zstandard;

/// <summary>
/// Decodes and executes the sequences section of each compressed block in a frame (RFC
/// 8878 sections 3.1.1.3.2, 3.1.1.4 and 3.1.1.5), keeping what later blocks of the frame
/// reuse: the last FSE table of each code, for <c>Repeat_Mode</c>, and the three repeat
/// offsets.
/// </summary>
internal sealed class ZstandardSequencesDecoder
{
    private const int PredefinedMode = 0;

    private const int RleMode = 1;

    private const int FseCompressedMode = 2;

    /// <summary>The low two bits of <c>Symbol_Compression_Modes</c> are reserved and must be 0.</summary>
    private const int ReservedModeBits = 3;

    /// <summary>A first byte of 255 starts a 3-byte <c>Number_of_Sequences</c>; 128 to 254 a 2-byte one.</summary>
    private const int LongCountMarker = 255;

    /// <summary>A 3-byte <c>Number_of_Sequences</c> is its last two bytes plus this.</summary>
    private const int LongCountBase = 0x7F00;

    /// <summary>The three codes, in the order the section describes their tables.</summary>
    private static readonly ZstandardSequenceField[] Fields =
        [ZstandardSequenceField.LiteralsLength, ZstandardSequenceField.Offset, ZstandardSequenceField.MatchLength];

    private readonly ZstandardFseTable?[] previousTables = new ZstandardFseTable?[3];

    private readonly long[] repeatOffsets = new long[3];

    /// <summary>Creates a decoder ready for the first block of a frame.</summary>
    public ZstandardSequencesDecoder() => StartFrame();

    /// <summary>Forgets the previous tables and restores the repeat offsets to 1, 4 and 8, as a new frame starts.</summary>
    public void StartFrame()
    {
        Array.Clear(previousTables);
        repeatOffsets[0] = 1;
        repeatOffsets[1] = 4;
        repeatOffsets[2] = 8;
    }

    /// <summary>
    /// Decodes <paramref name="section" />, the rest of the block after its literals section,
    /// and executes its sequences over <paramref name="literals" /> into
    /// <paramref name="output" />, whose length is the most the block may regenerate, copying
    /// matches from <paramref name="history" /> as far as they reach back; sets
    /// <paramref name="outputLength" /> to the bytes written. <see langword="false" /> when
    /// the section is corrupt.
    /// </summary>
    public bool Decode(ReadOnlySpan<byte> section, ReadOnlySpan<byte> literals, Span<byte> output, ZstandardHistory history, out int outputLength)
    {
        var writer = new ZstandardSequenceWriter(literals, output, history);
        var decoded = TryReadSequenceCount(ref section, out var count)
            && (count == 0 ? section.IsEmpty : DecodeSequences(section, count, ref writer))
            && writer.CopyRemainingLiterals();
        outputLength = writer.Written;
        return decoded;
    }

    /// <summary>RFC 8878 section 3.1.1.3.2.1: <c>Number_of_Sequences</c>, in one, two or three bytes.</summary>
    private static bool TryReadSequenceCount(ref ReadOnlySpan<byte> section, out int count)
    {
        count = 0;
        if (section.IsEmpty)
        {
            return false;
        }

        var first = section[0];
        var length = first < 128 ? 1 : first < LongCountMarker ? 2 : 3;
        if (section.Length < length)
        {
            return false;
        }

        count = length switch
        {
            1 => first,
            2 => ((first - 128) << 8) + section[1],
            _ => section[1] + (section[2] << 8) + LongCountBase,
        };
        section = section[length..];
        return true;
    }

    /// <summary>Reads <c>Symbol_Compression_Modes</c> and the three tables, then executes the <paramref name="count" /> sequences of the bitstream.</summary>
    private bool DecodeSequences(ReadOnlySpan<byte> section, int count, ref ZstandardSequenceWriter writer)
    {
        if (section.IsEmpty || (section[0] & ReservedModeBits) != 0)
        {
            return false;
        }

        var modes = section[0];
        section = section[1..];
        var tables = new ZstandardFseTable[Fields.Length];
        for (var index = 0; index < Fields.Length; index++)
        {
            var table = ReadTable((modes >> (6 - (2 * index))) & 3, Fields[index], previousTables[index], ref section);
            if (table is null)
            {
                return false;
            }

            tables[index] = previousTables[index] = table;
        }

        return ZstandardBackwardBitReader.TryCreate(section, out var bits) && ExecuteSequences(ref bits, count, tables, ref writer);
    }

    /// <summary>
    /// RFC 8878 section 3.1.1.3.2.1.1: the table of one code in <paramref name="mode" />, read
    /// from the start of <paramref name="section" /> and moved past; <see langword="null" />
    /// when it is corrupt, or repeats a table the frame has not had.
    /// </summary>
    private static ZstandardFseTable? ReadTable(int mode, ZstandardSequenceField field, ZstandardFseTable? previous, ref ReadOnlySpan<byte> section)
    {
        switch (mode)
        {
            case PredefinedMode:
                return field.PredefinedTable;
            case RleMode:
                return ReadRleTable(field, ref section);
            case FseCompressedMode:
                var table = ZstandardFseTable.Read(section, field.MaxAccuracyLog, field.MaxSymbol, out var bytesRead);
                section = section[Math.Min(bytesRead, section.Length)..];
                return table;
            default:
                return previous;
        }
    }

    /// <summary><c>RLE_Mode</c>: one byte, the code every sequence of the block uses.</summary>
    private static ZstandardFseTable? ReadRleTable(ZstandardSequenceField field, ref ReadOnlySpan<byte> section)
    {
        if (section.IsEmpty || section[0] > field.MaxSymbol)
        {
            return null;
        }

        var table = ZstandardFseTable.Rle(section[0]);
        section = section[1..];
        return table;
    }

    /// <summary>
    /// RFC 8878 section 3.1.1.3.2.2: reads the three initial states, then for each sequence
    /// its offset, match length and literals length, executes it, and updates the states
    /// (literals length, match length, offset) unless it was the last; the bitstream must end
    /// exactly there.
    /// </summary>
    private bool ExecuteSequences(ref ZstandardBackwardBitReader bits, int count, ZstandardFseTable[] tables, ref ZstandardSequenceWriter writer)
    {
        var (literalsLengthTable, offsetTable, matchLengthTable) = (tables[0], tables[1], tables[2]);
        var literalsLengthState = literalsLengthTable.ReadInitialState(ref bits);
        var offsetState = offsetTable.ReadInitialState(ref bits);
        var matchLengthState = matchLengthTable.ReadInitialState(ref bits);
        for (var index = 1; ; index++)
        {
            var offsetValue = ZstandardSequenceValues.ReadOffsetValue(offsetTable.Symbol(offsetState), ref bits);
            var matchLength = ZstandardSequenceValues.ReadMatchLength(matchLengthTable.Symbol(matchLengthState), ref bits);
            var literalsLength = ZstandardSequenceValues.ReadLiteralsLength(literalsLengthTable.Symbol(literalsLengthState), ref bits);
            if (!writer.CopyLiterals(literalsLength) || !writer.CopyMatch(ResolveOffset(offsetValue, literalsLength), matchLength))
            {
                return false;
            }

            if (index == count)
            {
                return bits.BitsRemaining == 0;
            }

            literalsLengthState = literalsLengthTable.ReadNextState(literalsLengthState, ref bits);
            matchLengthState = matchLengthTable.ReadNextState(matchLengthState, ref bits);
            offsetState = offsetTable.ReadNextState(offsetState, ref bits);
        }
    }

    /// <summary>
    /// RFC 8878 section 3.1.1.5: an <c>Offset_Value</c> above 3 is a new offset 3 less; 1 to 3
    /// name a repeat offset, shifted by one when the literals length is 0, where the third
    /// means <c>Repeated_Offset1 - 1</c>. Whatever is used moves to the front.
    /// </summary>
    private long ResolveOffset(long offsetValue, int literalsLength)
    {
        if (offsetValue > 3)
        {
            MoveToFront(offsetValue - 3, 2);
            return offsetValue - 3;
        }

        var repeat = (int)offsetValue - 1 + (literalsLength == 0 ? 1 : 0);
        if (repeat == 0)
        {
            return repeatOffsets[0];
        }

        var offset = repeat == 3 ? repeatOffsets[0] - 1 : repeatOffsets[repeat];
        MoveToFront(offset, Math.Min(repeat, 2));
        return offset;
    }

    /// <summary>Puts <paramref name="offset" /> first, shifting the <paramref name="shifted" /> repeat offsets before it down one.</summary>
    private void MoveToFront(long offset, int shifted)
    {
        repeatOffsets[2] = shifted == 2 ? repeatOffsets[1] : repeatOffsets[2];
        repeatOffsets[1] = repeatOffsets[0];
        repeatOffsets[0] = offset;
    }
}
