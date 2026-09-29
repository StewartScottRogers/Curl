using System.Numerics;
using static Curl.Zstandard.ZstandardTestLiterals;

namespace Curl.Zstandard;

/// <summary>
/// Hand-assembles sequences sections for the decoder tests (RFC 8878 sections 3.1.1.3.2 and
/// 4.1): <c>Number_of_Sequences</c>, <c>Symbol_Compression_Modes</c>, the three table
/// descriptions, and the FSE bitstream of the sequences, each code's chain of states found
/// backwards from its last. Its baseline tables are copied from the RFC, not from the
/// decoder; real curl decoding what it builds is what shows it is right.
/// </summary>
internal static class ZstandardTestSequences
{
    private static readonly int[] LiteralsLengthBaselines =
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24, 28, 32, 40, 48, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536];

    private static readonly int[] LiteralsLengthBits =
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 3, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    private static readonly int[] MatchLengthBaselines =
    [
        3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34,
        35, 37, 39, 41, 43, 47, 51, 59, 67, 83, 99, 131, 259, 515, 1027, 2051, 4099, 8195, 16387, 32771, 65539,
    ];

    private static readonly int[] MatchLengthBits =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 2, 2, 3, 3, 4, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
    ];

    /// <summary>The predefined tables, in <c>Predefined_Mode</c>.</summary>
    public static SequenceTable PredefinedLiteralsLength => new(0, ZstandardSequenceField.LiteralsLength.PredefinedTable, []);

    public static SequenceTable PredefinedOffset => new(0, ZstandardSequenceField.Offset.PredefinedTable, []);

    public static SequenceTable PredefinedMatchLength => new(0, ZstandardSequenceField.MatchLength.PredefinedTable, []);

    /// <summary>An <c>RLE_Mode</c> table: every sequence uses <paramref name="code" />.</summary>
    public static SequenceTable RleTable(byte code) => new(1, ZstandardFseTable.Rle(code), [code]);

    /// <summary>An <c>FSE_Compressed_Mode</c> table of <paramref name="counts" />.</summary>
    public static SequenceTable FseTable(short[] counts, int accuracyLog) =>
        new(2, ZstandardFseTable.Build(counts, accuracyLog), FseTableDescription(counts, accuracyLog));

    /// <summary><c>Repeat_Mode</c>: the table <paramref name="previous" /> set, used again.</summary>
    public static SequenceTable Repeated(SequenceTable previous) => new(3, previous.Table, []);

    /// <summary><c>Number_of_Sequences</c> in its shortest form.</summary>
    public static byte[] SequenceCount(int count) => count switch
    {
        < 128 => [(byte)count],
        < 0x7F00 => [(byte)((count >> 8) + 128), (byte)count],
        _ => [255, (byte)(count - 0x7F00), (byte)((count - 0x7F00) >> 8)],
    };

    /// <summary>A whole sequences section: count, modes, table descriptions and bitstream.</summary>
    public static byte[] SequencesSection(Sequence[] sequences, SequenceTable literalsLength, SequenceTable offset, SequenceTable matchLength) =>
        SequencesSection(sequences.Length, literalsLength, offset, matchLength, BackwardStream(SequenceReads(sequences, literalsLength, offset, matchLength)));

    /// <summary>A sequences section around a given <paramref name="bitstream" />.</summary>
    public static byte[] SequencesSection(int count, SequenceTable literalsLength, SequenceTable offset, SequenceTable matchLength, byte[] bitstream) =>
    [
        .. SequenceCount(count),
        (byte)((literalsLength.Mode << 6) | (offset.Mode << 4) | (matchLength.Mode << 2)),
        .. literalsLength.Description,
        .. offset.Description,
        .. matchLength.Description,
        .. bitstream,
    ];

    /// <summary>
    /// Every read the decoder makes of the bitstream, in order: the three initial states,
    /// then per sequence its offset, match length and literals length extra bits and, but
    /// for the last, the literals length, match length and offset state updates.
    /// </summary>
    public static List<(uint Value, int Bits)> SequenceReads(Sequence[] sequences, SequenceTable literalsLength, SequenceTable offset, SequenceTable matchLength)
    {
        var codes = sequences.Select(sequence => (
            LiteralsLength: LengthCode(sequence.LiteralsLength, LiteralsLengthBaselines, LiteralsLengthBits),
            Offset: OffsetCode(sequence.OffsetValue),
            MatchLength: LengthCode(sequence.MatchLength, MatchLengthBaselines, MatchLengthBits))).ToArray();
        var literalsLengthStates = States(literalsLength.Table, [.. codes.Select(code => code.LiteralsLength.Code)]);
        var offsetStates = States(offset.Table, [.. codes.Select(code => code.Offset.Code)]);
        var matchLengthStates = States(matchLength.Table, [.. codes.Select(code => code.MatchLength.Code)]);
        var reads = new List<(uint, int)>
        {
            ((uint)literalsLengthStates[0], literalsLength.Table.AccuracyLog),
            ((uint)offsetStates[0], offset.Table.AccuracyLog),
            ((uint)matchLengthStates[0], matchLength.Table.AccuracyLog),
        };
        for (var index = 0; index < sequences.Length; index++)
        {
            reads.Add((codes[index].Offset.Extra, codes[index].Offset.Bits));
            reads.Add((codes[index].MatchLength.Extra, codes[index].MatchLength.Bits));
            reads.Add((codes[index].LiteralsLength.Extra, codes[index].LiteralsLength.Bits));
            if (index < sequences.Length - 1)
            {
                reads.Add(StateUpdate(literalsLength.Table, literalsLengthStates[index], literalsLengthStates[index + 1]));
                reads.Add(StateUpdate(matchLength.Table, matchLengthStates[index], matchLengthStates[index + 1]));
                reads.Add(StateUpdate(offset.Table, offsetStates[index], offsetStates[index + 1]));
            }
        }

        return reads;
    }

    private static (int Code, uint Extra, int Bits) LengthCode(int value, int[] baselines, int[] bits)
    {
        var code = Array.FindLastIndex(baselines, baseline => baseline <= value);
        return (code, (uint)(value - baselines[code]), bits[code]);
    }

    private static (int Code, uint Extra, int Bits) OffsetCode(long offsetValue)
    {
        var code = BitOperations.Log2((ulong)offsetValue);
        return (code, (uint)(offsetValue - (1L << code)), code);
    }

    /// <summary>A chain of states decoding <paramref name="codes" />, each reached from the one before, found from the last back.</summary>
    private static int[] States(ZstandardFseTable table, int[] codes)
    {
        var size = 1 << table.AccuracyLog;
        var states = new int[codes.Length];
        states[^1] = Enumerable.Range(0, size).First(state => table.Symbol(state) == codes[^1]);
        for (var index = codes.Length - 2; index >= 0; index--)
        {
            var next = states[index + 1];
            states[index] = Enumerable.Range(0, size).Single(state =>
                table.Symbol(state) == codes[index] && Baseline(table, state) <= next && next < Baseline(table, state) + (1 << NumberOfBits(table, state)));
        }

        return states;
    }

    private static (uint Value, int Bits) StateUpdate(ZstandardFseTable table, int state, int next) =>
        ((uint)(next - Baseline(table, state)), NumberOfBits(table, state));

    /// <summary>The state after <paramref name="state" /> when the bits read are all 0: its baseline.</summary>
    private static int Baseline(ZstandardFseTable table, int state)
    {
        ZstandardBackwardBitReader.TryCreate([0, 0, 0, 1], out var zeros);
        return table.ReadNextState(state, ref zeros);
    }

    /// <summary>How many bits <paramref name="state" /> reads: the span between reading all 0 and all 1 bits.</summary>
    private static int NumberOfBits(ZstandardFseTable table, int state)
    {
        ZstandardBackwardBitReader.TryCreate([0xFF, 0xFF, 0xFF, 1], out var ones);
        return BitOperations.Log2((uint)(table.ReadNextState(state, ref ones) - Baseline(table, state) + 1));
    }

    /// <summary>One sequence: its literals length, its <c>Offset_Value</c> (a new offset plus 3, or a repeat offset 1 to 3), and its match length.</summary>
    public readonly record struct Sequence(int LiteralsLength, long OffsetValue, int MatchLength);

    /// <summary>One code's table: its <c>Symbol_Compression_Modes</c> value, the decoding table, and the bytes that describe it.</summary>
    public sealed record SequenceTable(int Mode, ZstandardFseTable Table, byte[] Description);
}
