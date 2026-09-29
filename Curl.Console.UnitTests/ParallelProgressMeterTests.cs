using System.Text;

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

    [TestMethod]
    public void DrawFinal_TwoTransfersAsMeasured_WritesTheHeaderAMidRunLineAndTheFinalLine()
    {
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

        Assert.AreEqual(
            Header + NewLine + StartLine + IdleLine + FirstEndedLine + SecondIdleLine + FinalLine + NewLine,
            written.ToString());
    }

    [TestMethod]
    public void DrawIfDue_WithinHalfASecondOfTheLastDraw_DrawsNothing()
    {
        using ParallelProgressMeter meter = NewMeter();
        meter.DrawIfDue();
        int afterFirst = written.Length;

        clock.Advance(500);
        meter.DrawIfDue();

        Assert.AreEqual(afterFirst, written.Length);
        clock.Advance(1);
        meter.DrawIfDue();
        Assert.IsGreaterThan(afterFirst, written.Length);
    }

    [TestMethod]
    public void DrawIfDue_KnownSizeHalfReceived_EstimatesTotalAndLeftTime()
    {
        using ParallelProgressMeter meter = NewMeter();
        ParallelTransferProgress transfer = meter.AddTransfer();
        transfer.MarkLive();

        clock.Advance(1000);
        transfer.ReportDownloaded(5, 10);

        StringAssert.EndsWith(
            written.ToString(),
            "\r 50 --      5     0     1     1  00:00:02 00:00:01 00:00:01     5      ");
    }

    [TestMethod]
    public void DrawIfDue_UploadOfKnownSize_ShowsItsPercentAndTheHigherSpeed()
    {
        using ParallelProgressMeter meter = NewMeter();
        ParallelTransferProgress transfer = meter.AddTransfer();
        transfer.MarkLive();

        clock.Advance(1000);
        transfer.ReportUploaded(50, 100);

        StringAssert.EndsWith(
            written.ToString(),
            "\r--   50     0    50     1     1           00:00:01             50      ");
    }

    [TestMethod]
    public void DrawIfDue_TenSamplesTaken_MeasuresTheSpeedOverTheLastTen()
    {
        using ParallelProgressMeter meter = NewMeter();
        ParallelTransferProgress transfer = meter.AddTransfer();
        for (int draw = 0; draw < 10; draw++)
        {
            meter.DrawIfDue();
            clock.Advance(600);
        }

        transfer.ReportDownloaded(6000, null);

        // 6000 bytes over the 5.4 s since the second sample, not the 6 s since the start.
        StringAssert.EndsWith(written.ToString(), " 1111      ");
    }

    [TestMethod]
    public void DrawFinal_NoTransfer_WritesTheHeaderAndAFinalZeroLine()
    {
        using ParallelProgressMeter meter = NewMeter();

        meter.DrawFinal();

        Assert.AreEqual(
            Header + NewLine + "\r--  --      0     0     0     0                                 0     " + NewLine,
            written.ToString());
    }

    [TestMethod]
    public void Dispose_AfterADraw_StopsTheIdleDraw()
    {
        ParallelProgressMeter meter = NewMeter();
        meter.DrawIfDue();
        int afterFirst = written.Length;

        meter.Dispose();
        clock.Advance(1000);

        Assert.AreEqual(afterFirst, written.Length);
    }

    [TestMethod]
    public void ReportTransferStartedAndDone_DrawNothing()
    {
        using ParallelProgressMeter meter = NewMeter();
        ParallelTransferProgress transfer = meter.AddTransfer();

        transfer.ReportTransferStarted();
        transfer.ReportTransferDone();

        Assert.AreEqual(0, written.Length);
    }

    private ParallelProgressMeter NewMeter() => new(clock, new WriteGate(), text => written.Append(text));
}
