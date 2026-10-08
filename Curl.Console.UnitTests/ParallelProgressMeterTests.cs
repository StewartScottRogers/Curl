using System.Text;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the combined progress meter of a <c>-Z</c> run against curl 8.21.0 (Schannel), measured on
/// 2026-09-28 with <c>curl -Z http://127.0.0.1:18521/a http://127.0.0.1:18521/b</c> against
/// <c>Record-CurlExchange.ps1 -Connections 2 -ResponseDelayMilliseconds 1500</c>, each answer ten bytes
/// (BL-521 Notes): the manual clock replays the measured draws.
/// </summary>
[TestClass]
public sealed class ParallelProgressMeterTests
{
    private const string Header = "DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed";

    private const string StartLine = "\r--  --      0     0     2     1                                 0      ";

    private const string IdleLine = "\r--  --      0     0     2     1           00:00:01              0      ";

    private const string FirstEndedLine = "\r--  --     10     0     2     1           00:00:01              6      ";

    private const string SecondIdleLine = "\r--  --     10     0     2     1           00:00:02              3      ";

    private const string FinalLine = "\r100 --     20     0     2     0  00:00:03 00:00:03              6     ";

    private static readonly string NewLine = Environment.NewLine;

    private readonly ManualTimerTimeProvider clock = new();

    private readonly StringBuilder written = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void DrawFinal_TwoTransfersAsMeasured_WritesTheHeaderAMidRunLineAndTheFinalLine()
    {
        Diagnostics.Arrange("transfers", "two of ten bytes each; the first ends at 1.51 s, the second at 3 s, final draw at 3.07 s");
        using ParallelProgressMeter meter = NewMeter();
        ParallelTransferProgress first = meter.AddTransfer();
        ParallelTransferProgress second = meter.AddTransfer();
        first.MarkLive();

        meter.DrawIfDue();
        clock.Advance(1000);
        clock.Advance(510);
        first.ReportDownloaded(10, 10);
        first.End();
        second.MarkLive();
        clock.Advance(1000);
        clock.Advance(490);
        second.ReportDownloaded(10, 10);
        second.End();
        clock.Advance(70);
        meter.DrawFinal();
        string expected = Header + NewLine + StartLine + IdleLine + FirstEndedLine + SecondIdleLine + FinalLine + NewLine;
        Diagnostics.Act("meter output", Neutral(written.ToString()));

        Diagnostics.Diff("meter output", Neutral(expected), Neutral(written.ToString()));
        Assert.AreEqual(
            expected,
            written.ToString());
    }

    [TestMethod]
    public void DrawIfDue_WithinHalfASecondOfTheLastDraw_DrawsNothing()
    {
        Diagnostics.Arrange("draws", "one at 0 ms, one at 500 ms, one at 501 ms");
        using ParallelProgressMeter meter = NewMeter();
        meter.DrawIfDue();
        int afterFirst = written.Length;

        clock.Advance(500);
        meter.DrawIfDue();
        Diagnostics.Act("characters written by the draw at 500 ms", written.Length - afterFirst);

        Diagnostics.Assert("characters written by the draw at 500 ms", 0, written.Length - afterFirst);
        Assert.AreEqual(afterFirst, written.Length);
        clock.Advance(1);
        meter.DrawIfDue();
        Diagnostics.Act("characters written by the draw at 501 ms", written.Length - afterFirst);
        Assert.IsGreaterThan(afterFirst, written.Length);
    }

    [TestMethod]
    public void DrawIfDue_KnownSizeHalfReceived_EstimatesTotalAndLeftTime()
    {
        Diagnostics.Arrange("transfer", "5 of 10 bytes received after 1 s");
        using ParallelProgressMeter meter = NewMeter();
        ParallelTransferProgress transfer = meter.AddTransfer();
        transfer.MarkLive();

        clock.Advance(1000);
        transfer.ReportDownloaded(5, 10);
        Diagnostics.Act("meter output", Neutral(written.ToString()));

        const string expectedEnd = "\r 50 --      5     0     1     1  00:00:02 00:00:01 00:00:01     5      ";
        Diagnostics.Assert("meter output ends with", Neutral(expectedEnd), Neutral(written.ToString()));
        StringAssert.EndsWith(
            written.ToString(),
            expectedEnd);
    }

    [TestMethod]
    public void DrawIfDue_UploadOfKnownSize_ShowsItsPercentAndTheHigherSpeed()
    {
        Diagnostics.Arrange("transfer", "50 of 100 bytes uploaded after 1 s");
        using ParallelProgressMeter meter = NewMeter();
        ParallelTransferProgress transfer = meter.AddTransfer();
        transfer.MarkLive();

        clock.Advance(1000);
        transfer.ReportUploaded(50, 100);
        Diagnostics.Act("meter output", Neutral(written.ToString()));

        const string expectedEnd = "\r--   50     0    50     1     1           00:00:01             50      ";
        Diagnostics.Assert("meter output ends with", Neutral(expectedEnd), Neutral(written.ToString()));
        StringAssert.EndsWith(
            written.ToString(),
            expectedEnd);
    }

    [TestMethod]
    public void DrawIfDue_TenSamplesTaken_MeasuresTheSpeedOverTheLastTen()
    {
        Diagnostics.Arrange("samples", "ten draws 600 ms apart, then 6000 bytes of unknown size");
        using ParallelProgressMeter meter = NewMeter();
        ParallelTransferProgress transfer = meter.AddTransfer();
        for (int draw = 0; draw < 10; draw++)
        {
            meter.DrawIfDue();
            clock.Advance(600);
        }

        transfer.ReportDownloaded(6000, null);
        string output = written.ToString();
        string lastLine = Neutral(output[output.LastIndexOf('\r')..]);
        Diagnostics.Act("last meter line", lastLine);

        // 6000 bytes over the 5.4 s since the second sample, not the 6 s since the start.
        Diagnostics.Assert("last meter line ends with", " 1111      ", lastLine);
        StringAssert.EndsWith(written.ToString(), " 1111      ");
    }

    [TestMethod]
    public void DrawFinal_NoTransfer_WritesTheHeaderAndAFinalZeroLine()
    {
        Diagnostics.Arrange("transfers", "none");
        using ParallelProgressMeter meter = NewMeter();

        meter.DrawFinal();
        string expected = Header + NewLine + "\r--  --      0     0     0     0                                 0     " + NewLine;
        Diagnostics.Act("meter output", Neutral(written.ToString()));

        Diagnostics.Diff("meter output", Neutral(expected), Neutral(written.ToString()));
        Assert.AreEqual(
            expected,
            written.ToString());
    }

    [TestMethod]
    public void Dispose_AfterADraw_StopsTheIdleDraw()
    {
        Diagnostics.Arrange("meter", "drawn once, disposed, then 1 s passes");
        ParallelProgressMeter meter = NewMeter();
        meter.DrawIfDue();
        int afterFirst = written.Length;

        meter.Dispose();
        clock.Advance(1000);
        Diagnostics.Act("characters written after disposal", written.Length - afterFirst);

        Diagnostics.Assert("characters written after disposal", 0, written.Length - afterFirst);
        Assert.AreEqual(afterFirst, written.Length);
    }

    [TestMethod]
    public void ReportTransferStartedAndDone_DrawNothing()
    {
        Diagnostics.Arrange("transfer", "one, reported started and done");
        using ParallelProgressMeter meter = NewMeter();
        ParallelTransferProgress transfer = meter.AddTransfer();

        transfer.ReportTransferStarted();
        transfer.ReportTransferDone();
        Diagnostics.Act("characters written", written.Length);

        Diagnostics.Assert("characters written", 0, written.Length);
        Assert.AreEqual(0, written.Length);
    }

    /// <summary>The meter's text with the platform's line ending and carriage returns spelled out, so the diagnostics read the same everywhere.</summary>
    private static string Neutral(string text) => text.Replace(NewLine, "\\n").Replace("\r", "\\r");

    private ParallelProgressMeter NewMeter() => new(clock, new WriteGate(), text => written.Append(text));
}
