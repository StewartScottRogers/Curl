namespace Curl.Extension.Kafka.Wire.Compression.Zstd;

/// <summary>Checks FSE table decoding against RFC 8878 section 4.1 and the predefined distributions of section 3.1.1.3.2.2.</summary>
[TestClass]
public sealed class ZstdFiniteStateEntropyTableTests
{
    [TestMethod]
    public void Read_TwoSymbolsOfSixteen_ReadsAccuracyLogFiveAndTwoCounts()
    {
        // Bits from the low end: accuracy log field 0 (4 bits), count 16 + 1 = 17 (5 bits), then 17 coded as 31 (5 bits).
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read([0x10, 0x3F], 5, 1, out int bytesRead);

        Assert.AreEqual(2, bytesRead);
        Assert.AreEqual(5, table.AccuracyLog);
        Assert.AreEqual(16, CountStatesOf(table, 0));
        Assert.AreEqual(16, CountStatesOf(table, 1));
    }

    [TestMethod]
    public void Read_ZeroCountFollowedByRunOfOne_SkipsTwoSymbols()
    {
        // Counts 16, 0, then a run field of 1 (one more zero), then 16: symbols 0 and 3 only.
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read([0x10, 0xA3, 0x0F], 5, 3, out int bytesRead);

        Assert.AreEqual(3, bytesRead);
        Assert.AreEqual(16, CountStatesOf(table, 0));
        Assert.AreEqual(0, CountStatesOf(table, 1));
        Assert.AreEqual(0, CountStatesOf(table, 2));
        Assert.AreEqual(16, CountStatesOf(table, 3));
    }

    [TestMethod]
    public void Read_ZeroCountFollowedByRunFieldOfThree_ReadsAnotherRunField()
    {
        // Counts 16, 0, run fields 3 (three zeros, read another) and 0, then 16: symbols 0 and 5.
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read([0x10, 0x63, 0x3E], 5, 5, out int bytesRead);

        Assert.AreEqual(3, bytesRead);
        Assert.AreEqual(16, CountStatesOf(table, 0));
        Assert.AreEqual(16, CountStatesOf(table, 5));
    }

    [TestMethod]
    public void Read_CountInTheShortFormAboveTheLimit_ReadsItWithoutSubtracting()
    {
        // Counts 16, 13, 3: the second is the 5-bit value 14, a short-form value at or above the limit 14 (31 - 17).
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read([0x10, 0xDD, 0x01], 5, 2, out int bytesRead);

        Assert.AreEqual(3, bytesRead);
        Assert.AreEqual(16, CountStatesOf(table, 0));
        Assert.AreEqual(13, CountStatesOf(table, 1));
        Assert.AreEqual(3, CountStatesOf(table, 2));
    }

    [TestMethod]
    public void Read_AccuracyLogAboveTheMaximum_ThrowsFormatExceptionNamingZstd()
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() =>
            ZstdFiniteStateEntropyTable.Read([0x0F, 0, 0, 0], 9, 35, out _));

        StringAssert.Contains(exception.Message, "zstd");
    }

    [TestMethod]
    public void Read_CountsStopBeforeTheTableIsFull_ThrowsFormatExceptionNamingZstd()
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() =>
            ZstdFiniteStateEntropyTable.Read([0x10, 0x3F], 5, 0, out _));

        StringAssert.Contains(exception.Message, "zstd");
    }

    [TestMethod]
    public void Read_ZeroRunPastTheLastSymbol_ThrowsFormatExceptionNamingZstd()
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() =>
            ZstdFiniteStateEntropyTable.Read([0x10, 0xA3, 0x0F], 5, 2, out _));

        StringAssert.Contains(exception.Message, "zstd");
    }

    [TestMethod]
    public void Read_TruncatedDescription_ThrowsFormatExceptionNamingZstd()
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() =>
            ZstdFiniteStateEntropyTable.Read([0x10], 5, 1, out _));

        StringAssert.Contains(exception.Message, "zstd");
    }

    [TestMethod]
    public void Read_NoBytes_ThrowsFormatExceptionNamingZstd()
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() =>
            ZstdFiniteStateEntropyTable.Read([], 5, 1, out _));

        StringAssert.Contains(exception.Message, "zstd");
    }

    [TestMethod]
    public void FromNormalizedCounts_CountsAboveTheTableSize_ThrowsFormatExceptionNamingZstd()
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() =>
            ZstdFiniteStateEntropyTable.FromNormalizedCounts([20, 20], 5));

        StringAssert.Contains(exception.Message, "zstd");
    }

    [TestMethod]
    public void PredefinedOffset_PlacesSymbolsByTheSpreadStepOf23()
    {
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.PredefinedOffset;

        Assert.AreEqual(5, table.AccuracyLog);
        Assert.AreEqual(0, table.Symbol(0));
        Assert.AreEqual(1, table.Symbol(23));
        Assert.AreEqual(2, table.Symbol(14));
        Assert.AreEqual(3, table.Symbol(5));
        Assert.AreEqual(4, table.Symbol(19));
        Assert.AreEqual(24, table.Symbol(31));
        Assert.AreEqual(28, table.Symbol(27));
    }

    [TestMethod]
    public void PredefinedOffset_SymbolWithCountTwo_HasStatesWithBaselines0And16()
    {
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.PredefinedOffset;
        List<int> states = [];
        for (int state = 0; state < 32; state++)
        {
            if (table.Symbol(state) == 6)
            {
                states.Add(state);
            }
        }

        Assert.HasCount(2, states);
        // Each of the two states reads 4 bits; the reader holds four zero bits under its marker.
        Assert.AreEqual(0, table.NextState(states[0], new ZstdBackwardBitReader([0x10])));
        Assert.AreEqual(16, table.NextState(states[1], new ZstdBackwardBitReader([0x10])));
    }

    [TestMethod]
    public void PredefinedOffset_SymbolWithCountOne_ReadsFiveBitsFromBaselineZero()
    {

        Assert.AreEqual(0x1F, ZstdFiniteStateEntropyTable.PredefinedOffset.NextState(0, new ZstdBackwardBitReader([0x3F])));
    }

    [TestMethod]
    public void PredefinedLiteralLength_StartsWithSymbolZeroReadingFourBitsFromBaselineZero()
    {
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.PredefinedLiteralLength;

        Assert.AreEqual(6, table.AccuracyLog);
        Assert.AreEqual(0, table.Symbol(0));
        Assert.AreEqual(0, table.NextState(0, new ZstdBackwardBitReader([0x10])));
        Assert.AreEqual(32, table.Symbol(63));
        Assert.AreEqual(35, table.Symbol(60));
    }

    [TestMethod]
    public void PredefinedMatchLength_StartsWithSymbolZeroReadingSixBitsFromBaselineZero()
    {
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.PredefinedMatchLength;

        Assert.AreEqual(6, table.AccuracyLog);
        Assert.AreEqual(0, table.Symbol(0));
        Assert.AreEqual(0b101010, table.NextState(0, new ZstdBackwardBitReader([0x6A])));
        Assert.AreEqual(52, table.Symbol(57));
    }

    [TestMethod]
    public void Rle_AlwaysDecodesItsSymbolAndReadsNoBits()
    {
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Rle(7);
        ZstdBackwardBitReader reader = new([0x01]);

        Assert.AreEqual(0, table.AccuracyLog);
        Assert.AreEqual(0, table.ReadInitialState(reader));
        Assert.AreEqual(7, table.Symbol(0));
        Assert.AreEqual(0, table.NextState(0, reader));
        Assert.AreEqual(0, reader.RemainingBits);
    }

    [TestMethod]
    public void ReadInitialState_ReadsAccuracyLogBits()
    {
        ZstdBackwardBitReader reader = new([0x20 | 0x0A]);

        Assert.AreEqual(0x0A, ZstdFiniteStateEntropyTable.PredefinedOffset.ReadInitialState(reader));
    }

    private static int CountStatesOf(ZstdFiniteStateEntropyTable table, byte symbol)
    {
        int count = 0;
        for (int state = 0; state < 1 << table.AccuracyLog; state++)
        {
            if (table.Symbol(state) == symbol)
            {
                count++;
            }
        }

        return count;
    }
}
