namespace Curl.Output;

/// <summary>
/// Adversarial black-box tests for <see cref="ParallelProgressMeterText"/> (BL-1504): sizes
/// and times at zero, one past each column change, and near <see cref="long.MaxValue"/>.
/// </summary>
[TestClass]
public sealed class ParallelProgressMeterTextAdversarialTests
{
    [TestMethod]
    [DataRow(0L)]
    [DataRow(99_999L)]
    [DataRow(100_000L)]
    [DataRow(10_239_999L)]
    [DataRow(10_240_000L)]
    [DataRow(long.MaxValue - 1)]
    [DataRow(long.MaxValue)]
    public void Size_AnyCountFromZeroToLongMaxValue_IsFiveColumns(long bytes)
    {
        string size = ParallelProgressMeterText.Size(bytes);

        Assert.HasCount(5, size);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    [DataRow(long.MinValue)]
    public void Time_ZeroOrNegativeSeconds_IsEightBlanks(long seconds)
    {
        string time = ParallelProgressMeterText.Time(seconds);

        Assert.AreEqual(new string(' ', 8), time);
    }

    [TestMethod]
    [DataRow(1L, "00:00:01")]
    [DataRow(360_000L, "  4d 04h")]
    [DataRow(86_400_000L, "   1000d")]
    [DataRow(863_999_999_999L, "9999999d")]
    public void Time_AtEachLayoutChange_IsEightColumns(long seconds, string expected)
    {
        string time = ParallelProgressMeterText.Time(seconds);

        Assert.AreEqual(expected, time);
    }

    [TestMethod]
    public void StatusLine_EveryFigureAtItsLimit_ReturnsWithoutThrowing()
    {
        ParallelProgressFigures figures = new(
            DownloadPercent: long.MaxValue,
            UploadPercent: long.MinValue,
            Downloaded: long.MaxValue,
            Uploaded: long.MaxValue,
            Transfers: long.MaxValue,
            Live: long.MaxValue,
            TotalSeconds: 0,
            SpentSeconds: 0,
            LeftSeconds: 0,
            Speed: long.MaxValue);

        string line = ParallelProgressMeterText.StatusLine(figures, "\n");

        Assert.StartsWith("\r922 -92 8191P 8191P ", line);
        Assert.EndsWith("8191P     \n", line);
    }

    [TestMethod]
    public void StatusLine_ZeroElapsedAndNothingTransferred_HasBlankTimesAndZeroSizes()
    {
        ParallelProgressFigures figures = new(null, null, 0, 0, 0, 0, 0, 0, 0, 0);

        string line = ParallelProgressMeterText.StatusLine(figures, null);

        // Two blanks after Live, three eight-column blank times each followed by a blank, then the speed.
        Assert.AreEqual("\r--  --      0     0     0     0" + new string(' ', 2 + 27) + "    0" + new string(' ', 6), line);
    }
}
