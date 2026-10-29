using Curl.Engine;
namespace Curl.Console;

/// <summary>
/// Pins CGR-217: the progress meter refreshes once a second while no data arrives, as curl's
/// progress callback does, and stops once the transfer is finished.
/// </summary>
[TestClass]
public sealed class Cgr217Tests
{
    [TestMethod]
    public void Ticker_NoDataForASecond_DrawsAnotherMeterRow()
    {
        ManualTimerTimeProvider clock = new();
        List<string> rows = [];
        TransferProgressRecorder recorder = new(clock, writeLive: rows.Add);
        recorder.ReportTransferStarted();
        int before = rows.Count;

        clock.Advance(1000);

        Assert.IsGreaterThan(before, rows.Count);
    }

    [TestMethod]
    public void Ticker_AfterFinish_DrawsNothing()
    {
        ManualTimerTimeProvider clock = new();
        List<string> rows = [];
        TransferProgressRecorder recorder = new(clock, writeLive: rows.Add);
        recorder.ReportTransferStarted();
        recorder.Finish(succeeded: false);
        int before = rows.Count;

        clock.Advance(1000);
        recorder.ReportTimePassed();

        Assert.AreEqual(before, rows.Count);
    }

    [TestMethod]
    public void ReportTimePassed_BeforeTheTransferStarts_DrawsNothing()
    {
        ManualTimerTimeProvider clock = new();
        List<string> rows = [];
        TransferProgressRecorder recorder = new(clock, writeLive: rows.Add);
        int before = rows.Count;

        recorder.ReportTimePassed();

        Assert.AreEqual(before, rows.Count);
    }

    [TestMethod]
    public void Ticker_BarShown_DrawsTheBarAgain()
    {
        ManualTimerTimeProvider clock = new();
        List<string> rows = [];
        ProgressBarRecorder bar = new(clock, 0, 80);
        TransferProgressRecorder recorder = new(clock, bar, rows.Add);
        recorder.ReportTransferStarted();
        int before = bar.Drawn.Length;

        clock.Advance(1000);

        Assert.IsGreaterThan(before, bar.Drawn.Length);
    }
}
