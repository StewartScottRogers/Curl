using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins the text of a <c>-Z</c> run's combined progress meter against curl 8.21.0 (Schannel),
/// measured on 2026-09-28 (BL-521 Notes): the status lines of two ten-byte HTTP transfers, and the
/// sizes of <c>file://</c> transfers of 300000, 10000000 and 300000000 bytes.
/// </summary>
[TestClass]
public sealed class ParallelProgressMeterTextTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void StatusLine_RunningWithUnknownSizes_WritesTheMeasuredLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ParallelProgressFigures figures = new(null, null, 10, 0, 2, 1, 0, 1, 0, 6);
        diagnostics.Arrange("figures", figures);

        string line = ParallelProgressMeterText.StatusLine(figures, null);

        diagnostics.Act("line", line);
        diagnostics.Diff("line", "\r--  --     10     0     2     1           00:00:01              6      ", line);
        Assert.AreEqual("\r--  --     10     0     2     1           00:00:01              6      ", line);
    }

    [TestMethod]
    public void StatusLine_Final_WritesTheMeasuredLineAndItsLineEnding()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ParallelProgressFigures figures = new(100, null, 20, 0, 2, 0, 3, 3, 0, 6);
        diagnostics.Arrange("figures", figures);

        string line = ParallelProgressMeterText.StatusLine(figures, "\n");

        diagnostics.Act("line", line);
        diagnostics.Diff("line", "\r100 --     20     0     2     0  00:00:03 00:00:03              6     \n", line);
        Assert.AreEqual("\r100 --     20     0     2     0  00:00:03 00:00:03              6     \n", line);
    }

    [TestMethod]
    [DataRow(5L, "\r  5   5 ")]
    [DataRow(1000L, "\r100 100 ")]
    public void StatusLine_KnownPercent_IsRightAlignedInThreeAndCutToThree(long percent, string expectedStart)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("percent", percent);
        diagnostics.Arrange("expected start", expectedStart);
        ParallelProgressFigures figures = new(percent, percent, 0, 0, 0, 0, 0, 0, 0, 0);

        string line = ParallelProgressMeterText.StatusLine(figures, null);

        diagnostics.Act("line", line);
        diagnostics.Assert("line starts with", expectedStart, line.Length >= expectedStart.Length ? line[..expectedStart.Length] : line);
        StringAssert.StartsWith(line, expectedStart);
    }

    [TestMethod]
    [DataRow(0L, "    0")]
    [DataRow(99999L, "99999")]
    [DataRow(100000L, "  97k")]
    [DataRow(300000L, " 292k")]
    [DataRow(10000000L, "9765k")]
    [DataRow(10240000L, " 9.7M")]
    [DataRow(75000000L, "71.5M")]
    [DataRow((100L << 20) - 1, "99.10")]
    [DataRow(300000000L, " 286M")]
    [DataRow(1666000000L, "1588M")]
    [DataRow(10485760000L, " 9.7G")]
    [DataRow(107374182400L, " 100G")]
    [DataRow(10737418240000L, " 9.7T")]
    [DataRow(10995116277760000L, " 9.7P")]
    [DataRow(112589990684262400L, " 100P")]
    [DataRow((100L << 50) - 1, "99.10")]
    [DataRow(long.MaxValue, "8191P")]
    public void Size_Bytes_IsFiveColumnsAsMax5DataWritesIt(long bytes, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("bytes", bytes);

        string size = ParallelProgressMeterText.Size(bytes);

        diagnostics.Act("size", size);
        diagnostics.Diff("size", expected, size);
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [DataRow(0L, "        ")]
    [DataRow(-1L, "        ")]
    [DataRow(3L, "00:00:03")]
    [DataRow(3661L, "01:01:01")]
    [DataRow(359999L, "99:59:59")]
    [DataRow(360000L, "  4d 04h")]
    [DataRow(86399999L, "999d 23h")]
    [DataRow(86400000L, "   1000d")]
    [DataRow(long.MaxValue, "10675199")]
    public void Time_Seconds_IsEightColumnsAsTime2StrWritesIt(long seconds, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("seconds", seconds);

        string time = ParallelProgressMeterText.Time(seconds);

        diagnostics.Act("time", time);
        diagnostics.Diff("time", expected, time);
        Assert.AreEqual(expected, time);
    }
}
