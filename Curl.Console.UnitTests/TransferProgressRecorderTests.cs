namespace Curl.Console;

/// <summary>
/// Pins what <see cref="TransferProgressRecorder" /> records: whether the transfer started (task
/// BL-130), and the status lines curl 8.21.0's <c>progress_calc</c> and <c>progress_meter</c>
/// draw from the handler's byte reports on a manual clock (task BL-131).
/// </summary>
[TestClass]
public sealed class TransferProgressRecorderTests
{
    private const string Zero = ProgressMeterLines.ZeroStatusLine;

    /// <summary>Ten of ten bytes received in 40 ms: 250 bytes per second, no time columns.</summary>
    private const string TenOfTenIn40Milliseconds =
        "\r100     10 100     10   0      0    250      0                              0";

    /// <summary>A time column with no time in it.</summary>
    private const string NoTime = "       ";

    private readonly ManualTimeProvider clock = new();

    [TestMethod]
    public void HasTransferStarted_NothingReported_IsFalse()
    {
        Assert.IsFalse(new TransferProgressRecorder(clock).HasTransferStarted);
    }

    [TestMethod]
    public void HasTransferStarted_AfterReportTransferStarted_IsTrue()
    {
        TransferProgressRecorder recorder = new(clock);

        recorder.ReportTransferStarted();

        Assert.IsTrue(recorder.HasTransferStarted);
    }

    [TestMethod]
    public void HasTransferStarted_AfterOnlyByteReports_IsFalse()
    {
        TransferProgressRecorder recorder = new(clock);

        recorder.ReportDownloaded(10, 20);
        recorder.ReportUploaded(5, null);

        Assert.IsFalse(recorder.HasTransferStarted);
    }

    [TestMethod]
    public void StatusLines_NothingReported_IsTheZeroStatusLine()
    {
        Assert.AreEqual(Zero, new TransferProgressRecorder(clock).StatusLines);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Finish_NoBytesReported_DrawsNothingMore(bool succeeded)
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(5000);

        recorder.Finish(succeeded);

        Assert.AreEqual(Zero, recorder.StatusLines);
    }

    [TestMethod]
    public void ReportDownloaded_WithinASecondOfTheLastSample_DrawsNothing()
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(999);

        recorder.ReportDownloaded(10, 10);

        Assert.AreEqual(Zero, recorder.StatusLines);
    }

    [TestMethod]
    public void Finish_SucceededAfterTenOfTenBytes_DrawsTheDoneLineThreeTimes()
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(40);
        recorder.ReportDownloaded(10, 10);

        recorder.Finish(succeeded: true);

        Assert.AreEqual(Zero + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds, recorder.StatusLines);
    }

    [TestMethod]
    public void FinishRedirectHop_WithNoBytesReported_DrawsTheZeroLineTwiceMore()
    {
        TransferProgressRecorder recorder = new(clock);
        recorder.ReportTransferStarted();

        recorder.FinishRedirectHop();

        Assert.AreEqual(Zero + Zero + Zero, recorder.StatusLines);
    }

    [TestMethod]
    public void ReportTransferStarted_Repeated_EndsTheHopAndStartsTheNextFromZero()
    {
        TransferProgressRecorder recorder = new(clock);
        recorder.ReportTransferStarted();
        clock.Advance(40);
        recorder.ReportDownloaded(10, 10);

        recorder.ReportTransferStarted();
        clock.Advance(40);
        recorder.ReportDownloaded(10, 10);
        recorder.Finish(succeeded: true);

        Assert.AreEqual(
            Zero + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + Environment.NewLine
                + Zero + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds,
            recorder.StatusLines);
        Assert.IsTrue(recorder.HasTransferStarted);
    }

    [TestMethod]
    public void Finish_FailedWithinASecondOfTheLastSample_DrawsNothingMore()
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(40);
        recorder.ReportDownloaded(10, 10);

        recorder.Finish(succeeded: false);

        Assert.AreEqual(Zero, recorder.StatusLines);
    }

    [TestMethod]
    public void Finish_FailedASecondAfterTheLastSample_DrawsOneLine()
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(40);
        recorder.ReportDownloaded(10, 20);
        clock.Advance(1960);

        recorder.Finish(succeeded: false);

        Assert.AreEqual(
            Zero + "\r 50     20  50     10   0      0      5      0   00:04   00:02   00:02      5",
            recorder.StatusLines);
    }

    [TestMethod]
    public void ReportDownloaded_EachSecond_DrawsALineWithTheSpeedOverTheLastFiveSeconds()
    {
        TransferProgressRecorder recorder = new(clock);
        for (int second = 1; second <= 7; second++)
        {
            clock.Advance(1000);
            recorder.ReportDownloaded(second * second * 1000L, null);
        }

        string[] lines = recorder.StatusLines.Split('\r');

        Assert.HasCount(9, lines);
        Assert.AreEqual("100   1000   0   1000   0      0   1000      0 " + NoTime + "   00:01 " + NoTime + "   1000", lines[2]);
        Assert.AreEqual("100  49000   0  49000   0      0   7000      0 " + NoTime + "   00:07 " + NoTime + "   9000", lines[8]);
    }

    [TestMethod]
    public void Finish_SucceededWithACurrentSpeed_KeepsItForEveryDoneLine()
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(1000);
        recorder.ReportUploaded(4000, 8000);
        clock.Advance(500);
        recorder.ReportUploaded(8000, 8000);

        recorder.Finish(succeeded: true);

        string doneLine = "\r100   8000   0      0 100   8000      0   5333   00:01   00:01 " + NoTime + "   4000";
        Assert.AreEqual(
            Zero + "\r 50   8000   0      0  50   4000      0   4000   00:02   00:01   00:01   4000" + doneLine + doneLine + doneLine,
            recorder.StatusLines);
    }

    [TestMethod]
    public void ReportDownloaded_MoreThanTimesAMillionFitsInALong_CapsTheTotalAndComputesTheCurrentSpeedWithoutOverflow()
    {
        TransferProgressRecorder recorder = new(clock);
        recorder.ReportUploaded(1, null);
        clock.Advance(1000);
        recorder.ReportDownloaded(long.MaxValue / 2, long.MaxValue);

        string line = recorder.StatusLines.Split('\r')[2];

        Assert.AreEqual(" 50  7.99E  50  3.99E   0      1  3.99E      1   00:02   00:01   00:01  4.00E", line);
    }

    [TestMethod]
    public void Reports_WithAProgressBar_PassEachReportOnToTheBar()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);
        TransferProgressRecorder recorder = new(clock, bar);

        recorder.ReportTransferStarted();
        clock.Advance(100);
        recorder.ReportUploaded(5, 10);
        clock.Advance(100);
        recorder.ReportDownloaded(10, 10);

        Assert.IsTrue(recorder.HasTransferStarted);
        Assert.AreEqual(
            "\r" + new string('#', 36).PadRight(72) + "  50.0%" + "\r" + new string('#', 54).PadRight(72) + "  75.0%",
            bar.Drawn);
    }
}
