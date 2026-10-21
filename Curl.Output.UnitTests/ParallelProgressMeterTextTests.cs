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
    [DataRow(86400000L, " 33m 10d")]
    [DataRow(2591999999L, "999m 29d")]
    [DataRow(2592000000L, "     82y")]
    [DataRow(3153599999999L, "  99999y")]
    [DataRow(3153600000000L, " >99999y")]
    [DataRow(long.MaxValue, " >99999y")]
    public void Time_Seconds_IsEightColumnsAsTimebufWritesIt(long seconds, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("seconds", seconds);

        string time = ParallelProgressMeterText.Time(seconds);

        diagnostics.Act("time", time);
        diagnostics.Diff("time", expected, time);
        Assert.AreEqual(expected, time);
    }

    /// <summary>
    /// Every row of curl 8.22.0's <c>tests/tunit/tool1622.c</c>: its <c>timecases</c> table and the
    /// 63 values of test1622's <c>timebuf</c> stdout block, 0 then each one doubled plus one
    /// (ADR-0482, BL-2899).
    /// </summary>
    [TestMethod]
    [DataRow(0L, "        ")]
    [DataRow(1L, "00:00:01")]
    [DataRow(3L, "00:00:03")]
    [DataRow(7L, "00:00:07")]
    [DataRow(15L, "00:00:15")]
    [DataRow(31L, "00:00:31")]
    [DataRow(63L, "00:01:03")]
    [DataRow(127L, "00:02:07")]
    [DataRow(255L, "00:04:15")]
    [DataRow(511L, "00:08:31")]
    [DataRow(1023L, "00:17:03")]
    [DataRow(2047L, "00:34:07")]
    [DataRow(4095L, "01:08:15")]
    [DataRow(8191L, "02:16:31")]
    [DataRow(16383L, "04:33:03")]
    [DataRow(32767L, "09:06:07")]
    [DataRow(65535L, "18:12:15")]
    [DataRow(131071L, "36:24:31")]
    [DataRow(262143L, "72:49:03")]
    [DataRow(524287L, "  6d 01h")]
    [DataRow(1048575L, " 12d 03h")]
    [DataRow(2097151L, " 24d 06h")]
    [DataRow(4194303L, " 48d 13h")]
    [DataRow(8388607L, " 97d 02h")]
    [DataRow(16777215L, "194d 04h")]
    [DataRow(33554431L, "388d 08h")]
    [DataRow(67108863L, "776d 17h")]
    [DataRow(134217727L, " 51m 23d")]
    [DataRow(268435455L, "103m 16d")]
    [DataRow(536870911L, "207m 03d")]
    [DataRow(1073741823L, "414m 07d")]
    [DataRow(2147483647L, "828m 15d")]
    [DataRow(4294967295L, "    136y")]
    [DataRow(8589934591L, "    272y")]
    [DataRow(17179869183L, "    544y")]
    [DataRow(34359738367L, "   1089y")]
    [DataRow(68719476735L, "   2179y")]
    [DataRow(137438953471L, "   4358y")]
    [DataRow(274877906943L, "   8716y")]
    [DataRow(549755813887L, "  17432y")]
    [DataRow(1099511627775L, "  34865y")]
    [DataRow(2199023255551L, "  69730y")]
    [DataRow(4398046511103L, " >99999y")]
    [DataRow(8796093022207L, " >99999y")]
    [DataRow(17592186044415L, " >99999y")]
    [DataRow(35184372088831L, " >99999y")]
    [DataRow(70368744177663L, " >99999y")]
    [DataRow(140737488355327L, " >99999y")]
    [DataRow(281474976710655L, " >99999y")]
    [DataRow(562949953421311L, " >99999y")]
    [DataRow(1125899906842623L, " >99999y")]
    [DataRow(2251799813685247L, " >99999y")]
    [DataRow(4503599627370495L, " >99999y")]
    [DataRow(9007199254740991L, " >99999y")]
    [DataRow(18014398509481983L, " >99999y")]
    [DataRow(36028797018963967L, " >99999y")]
    [DataRow(72057594037927935L, " >99999y")]
    [DataRow(144115188075855871L, " >99999y")]
    [DataRow(288230376151711743L, " >99999y")]
    [DataRow(576460752303423487L, " >99999y")]
    [DataRow(1152921504606846975L, " >99999y")]
    [DataRow(2305843009213693951L, " >99999y")]
    [DataRow(4611686018427387903L, " >99999y")]
    public void Time_EveryTool1622Row_IsAsCurl8220TimebufWritesIt(long seconds, string expected)
    {
        string time = ParallelProgressMeterText.Time(seconds);

        Assert.AreEqual(expected, time);
    }
}
