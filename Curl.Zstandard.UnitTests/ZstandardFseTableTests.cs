using System.Numerics;
using Curl.Testing;
using static Curl.Zstandard.ZstandardTestLiterals;

namespace Curl.Zstandard;

/// <summary>
/// Pins <see cref="ZstandardFseTable" /> to RFC 8878: the table built from the predefined
/// Literals_Length distribution against Appendix A.1 state for state, and table
/// descriptions read back to the distribution they were written from (BL-859).
/// </summary>
[TestClass]
public sealed class ZstandardFseTableTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>RFC 8878 section 3.1.1.3.2.2.1: <c>literalsLength_defaultDistribution</c>.</summary>
    private static readonly short[] LiteralsLengthDefaultDistribution =
    [
        4, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 2, 1, 1, 1, 1, 1,
        -1, -1, -1, -1,
    ];

    /// <summary>RFC 8878 Appendix A.1, states 0 to 63: (Symbol, Number_Of_Bits, Base).</summary>
    private static readonly (int Symbol, int NumberOfBits, int Base)[] LiteralsLengthCodeTable =
    [
        (0, 4, 0), (0, 4, 16), (1, 5, 32), (3, 5, 0), (4, 5, 0), (6, 5, 0), (7, 5, 0), (9, 5, 0),
        (10, 5, 0), (12, 5, 0), (14, 6, 0), (16, 5, 0), (18, 5, 0), (19, 5, 0), (21, 5, 0), (22, 5, 0),
        (24, 5, 0), (25, 5, 32), (26, 5, 0), (27, 6, 0), (29, 6, 0), (31, 6, 0), (0, 4, 32), (1, 4, 0),
        (2, 5, 0), (4, 5, 32), (5, 5, 0), (7, 5, 32), (8, 5, 0), (10, 5, 32), (11, 5, 0), (13, 6, 0),
        (16, 5, 32), (17, 5, 0), (19, 5, 32), (20, 5, 0), (22, 5, 32), (23, 5, 0), (25, 4, 0), (25, 4, 16),
        (26, 5, 32), (28, 6, 0), (30, 6, 0), (0, 4, 48), (1, 4, 16), (2, 5, 32), (3, 5, 32), (5, 5, 32),
        (6, 5, 32), (8, 5, 32), (9, 5, 32), (11, 5, 32), (12, 5, 32), (15, 6, 0), (17, 5, 32), (18, 5, 32),
        (20, 5, 32), (21, 5, 32), (23, 5, 32), (24, 5, 32), (35, 6, 0), (34, 6, 0), (33, 6, 0), (32, 6, 0),
    ];

    [TestMethod]
    public void Build_LiteralsLengthDefaultDistribution_IsRfc8878AppendixA1()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("distribution", string.Join(", ", LiteralsLengthDefaultDistribution));
        diagnostics.Arrange("accuracy log", 6);

        var table = ZstandardFseTable.Build(LiteralsLengthDefaultDistribution, 6);

        diagnostics.Act("accuracy log", table.AccuracyLog);
        diagnostics.Diff("states 0 to 63 as (symbol, bits, baseline)", Describe(LiteralsLengthCodeTable), Describe(table, 64));
        Assert.AreEqual(6, table.AccuracyLog);
        for (var state = 0; state < 64; state++)
        {
            Assert.AreEqual(LiteralsLengthCodeTable[state], (table.Symbol(state), NumberOfBits(table, state), Baseline(table, state)), $"state {state}");
        }
    }

    [TestMethod]
    [DataRow(new short[] { 26, 2, 1, 2, -1 }, 5, DisplayName = "less-than-1 probability")]
    [DataRow(new short[] { 10, 0, 0, 0, 0, 0, 0, 0, 22 }, 5, DisplayName = "seven zeros: repeat flags 3, 3, 0")]
    [DataRow(new short[] { 10, 0, 0, 0, 0, 22 }, 5, DisplayName = "four zeros: repeat flags 3, 0")]
    [DataRow(new short[] { 30, 0, 2 }, 5, DisplayName = "one zero: repeat flag 0")]
    [DataRow(new short[] { 60, 1, 1, 1, 1 }, 6, DisplayName = "Accuracy_Log 6")]
    public void Read_WrittenDescription_BuildsTheTableOfItsDistribution(short[] counts, int accuracyLog)
    {
        var description = FseTableDescription(counts, accuracyLog);
        var expected = ZstandardFseTable.Build(counts, accuracyLog);
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("counts", string.Join(", ", counts));
        diagnostics.Arrange("accuracy log", accuracyLog);
        diagnostics.Bytes("description, then 0xAA", [.. description, 0xAA]);

        var table = ZstandardFseTable.Read([.. description, 0xAA], 6, 11, out var bytesRead);

        diagnostics.Act("table read", table is not null);
        diagnostics.Act("bytes read", bytesRead);
        diagnostics.Assert("bytes read", description.Length, bytesRead);
        if (table is not null)
        {
            diagnostics.Diff("states as (symbol, bits, baseline)", Describe(expected, 1 << accuracyLog), Describe(table, 1 << table.AccuracyLog));
        }

        Assert.IsNotNull(table);
        Assert.AreEqual(description.Length, bytesRead);
        Assert.AreEqual(accuracyLog, table.AccuracyLog);
        for (var state = 0; state < 1 << accuracyLog; state++)
        {
            Assert.AreEqual(expected.Symbol(state), table.Symbol(state), $"state {state}");
            Assert.AreEqual(NumberOfBits(expected, state), NumberOfBits(table, state), $"state {state}");
            Assert.AreEqual(Baseline(expected, state), Baseline(table, state), $"state {state}");
        }
    }

    /// <summary>
    /// <c>D0 0F</c>: <c>Accuracy_Log</c> 5, then 28 in five bits (its 6-bit field's low five
    /// bits are below the threshold of 30) and 4 in three bits, as libzstd writes counts
    /// {28, 4}.
    /// </summary>
    [TestMethod]
    public void Read_LibzstdDescriptionOfCounts28And4_BuildsThatTable()
    {
        var expected = ZstandardFseTable.Build([28, 4], 5);
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("description", [0xD0, 0x0F]);
        diagnostics.Arrange("counts it describes", "28, 4 at accuracy log 5");
        diagnostics.Bytes("description written from those counts", FseTableDescription([28, 4], 5));

        var table = ZstandardFseTable.Read([0xD0, 0x0F], 6, 11, out var bytesRead);

        diagnostics.Act("table read", table is not null);
        diagnostics.Act("bytes read", bytesRead);
        diagnostics.Assert("bytes read", 2, bytesRead);
        if (table is not null)
        {
            diagnostics.Diff("states as (symbol, bits, baseline)", Describe(expected, 32), Describe(table, 1 << table.AccuracyLog));
        }

        Assert.IsNotNull(table);
        Assert.AreEqual(2, bytesRead);
        CollectionAssert.AreEqual(new byte[] { 0xD0, 0x0F }, FseTableDescription([28, 4], 5));
        for (var state = 0; state < 32; state++)
        {
            Assert.AreEqual(expected.Symbol(state), table.Symbol(state), $"state {state}");
        }
    }

    [TestMethod]
    public void Read_AccuracyLogAboveTheLimit_IsNull()
    {
        var description = FseTableDescription([100, 28], 7);

        var table = ReadWritingDiagnostics("counts 100, 28 at accuracy log 7, limit 6", description, 11, expectTable: false);

        Assert.IsNull(table);
    }

    [TestMethod]
    public void Read_CountsThatStopShortOfTheTotal_IsNull()
    {
        var description = FseTableDescription([10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 22], 5);

        var table = ReadWritingDiagnostics("counts 10, fourteen zeros, 22 at accuracy log 5", description, 11, expectTable: false);

        Assert.IsNull(table);
    }

    [TestMethod]
    public void Read_DescriptionCutShort_IsNull()
    {
        var description = FseTableDescription([.. Enumerable.Repeat((short)-1, 32)], 5);

        var whole = ReadWritingDiagnostics("thirty-two counts of -1 at accuracy log 5, whole", description, 255, expectTable: true);
        var cut = ReadWritingDiagnostics("the same description cut to its first byte", description[..1], 255, expectTable: false);

        Assert.IsNotNull(whole);
        Assert.IsNull(cut);
    }

    /// <summary>Writes a description, reads it with Accuracy_Log limit 6 and <paramref name="maxSymbol" />, and writes whether a table came back against <paramref name="expectTable" />.</summary>
    private ZstandardFseTable? ReadWritingDiagnostics(string name, byte[] description, int maxSymbol, bool expectTable)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("case", name);
        diagnostics.Bytes("description", description);
        diagnostics.Arrange("max symbol", maxSymbol);

        var table = ZstandardFseTable.Read(description, 6, maxSymbol, out var bytesRead);

        diagnostics.Act("table read", table is not null);
        diagnostics.Act("bytes read", bytesRead);
        diagnostics.Assert("table read", expectTable, table is not null);
        return table;
    }

    /// <summary>Lists RFC rows as "state: (symbol, bits, baseline)" lines for a DIFF.</summary>
    private static string Describe((int Symbol, int NumberOfBits, int Base)[] rows) =>
        string.Join("\n", rows.Select((row, state) => $"{state}: {row}"));

    /// <summary>Lists the first <paramref name="states" /> states of <paramref name="table" /> the way the RFC rows are listed.</summary>
    private static string Describe(ZstandardFseTable table, int states) =>
        Describe([.. Enumerable.Range(0, states).Select(state => (table.Symbol(state), NumberOfBits(table, state), Baseline(table, state)))]);

    /// <summary>The state after <paramref name="state" /> when the bits read are all 0: its baseline.</summary>
    private static int Baseline(ZstandardFseTable table, int state)
    {
        Assert.IsTrue(ZstandardBackwardBitReader.TryCreate([0, 0, 0, 1], out var zeros));
        return table.ReadNextState(state, ref zeros);
    }

    /// <summary>How many bits <paramref name="state" /> reads: the span between reading all 0 and all 1 bits.</summary>
    private static int NumberOfBits(ZstandardFseTable table, int state)
    {
        Assert.IsTrue(ZstandardBackwardBitReader.TryCreate([0xFF, 0xFF, 0xFF, 1], out var ones));
        return BitOperations.Log2((uint)(table.ReadNextState(state, ref ones) - Baseline(table, state) + 1));
    }
}
