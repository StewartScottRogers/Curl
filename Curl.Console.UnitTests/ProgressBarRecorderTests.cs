namespace Curl.Console;

/// <summary>
/// Pins <see cref="ProgressBarRecorder" /> against curl 8.21.0's <c>tool_progress_cb</c> and
/// <c>fly</c> (<c>src/tool_cb_prg.c</c>), and against the frames curl 8.21.0 drew on
/// 2026-09-27 for a body of unknown size at 30 columns (task BL-132 Notes).
/// </summary>
[TestClass]
public sealed class ProgressBarRecorderTests
{
    private readonly ManualTimeProvider clock = new();

    [TestMethod]
    [DataRow(5, 20)]
    [DataRow(20, 20)]
    [DataRow(21, 21)]
    [DataRow(79, 79)]
    [DataRow(400, 400)]
    [DataRow(401, 400)]
    public void Width_TerminalColumns_ClampsTo20Through400(int terminalColumns, int expected)
    {
        int width = ProgressBarRecorder.Width(terminalColumns);

        Assert.AreEqual(expected, width);
    }

    [TestMethod]
    public void ReportTransferStarted_NothingKnown_CallsWithoutDrawing()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);

        bar.ReportTransferStarted();

        Assert.IsTrue(bar.HasBeenCalled);
        Assert.AreEqual(string.Empty, bar.Drawn);
    }

    [TestMethod]
    public void HasBeenCalled_NoReport_IsFalse()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);

        Assert.IsFalse(bar.HasBeenCalled);
    }

    [TestMethod]
    public void ReportTransferStarted_ResumedPastZero_DrawsTheOffsetAsTheWholeTotal()
    {
        ProgressBarRecorder bar = new(clock, 5, 79);

        bar.ReportTransferStarted();

        Assert.AreEqual(Bar(72, "100.0"), bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_Within100MillisecondsBelowTheTotal_WaitsThenDrawsOnceTheyHavePassed()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);
        bar.ReportTransferStarted();

        clock.Advance(50);
        bar.ReportDownloaded(100000, 200000);
        string afterFiftyMilliseconds = bar.Drawn;
        clock.Advance(100);
        bar.ReportDownloaded(100000, 200000);

        Assert.AreEqual(string.Empty, afterFiftyMilliseconds);
        Assert.AreEqual(Bar(36, " 50.0"), bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_PositionUnchanged_DrawsNothing()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);
        bar.ReportDownloaded(100000, 200000);

        clock.Advance(500);
        bar.ReportDownloaded(100000, 200000);

        Assert.AreEqual(Bar(36, " 50.0"), bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_ReachesTheTotalWithin100Milliseconds_DrawsAtOnce()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);
        bar.ReportDownloaded(100000, 200000);

        clock.Advance(10);
        bar.ReportDownloaded(200000, 200000);

        Assert.AreEqual(Bar(36, " 50.0") + Bar(72, "100.0"), bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_OneThird_RoundsThePercentageToOneDecimal()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);

        bar.ReportDownloaded(1, 3);

        Assert.AreEqual(Bar(24, " 33.3"), bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_PastTheTotal_DrawsAFullBar()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);

        bar.ReportDownloaded(20, 10);

        Assert.AreEqual(Bar(72, "100.0"), bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizesPastLongMaxValue_CapsThemAndDrawsAFullBar()
    {
        ProgressBarRecorder bar = new(clock, 1, 79);

        bar.ReportDownloaded(long.MaxValue, long.MaxValue);

        Assert.AreEqual(Bar(72, "100.0"), bar.Drawn);
    }

    [TestMethod]
    public void ReportUploaded_HalfTheUpload_DrawsAHalfBar()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);

        bar.ReportUploaded(5, 10);

        Assert.AreEqual(Bar(36, " 50.0"), bar.Drawn);
    }

    [TestMethod]
    public void ReportUploaded_SizeUnknown_DrawsNothingOnTheFirstCall()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);

        bar.ReportUploaded(5, null);

        Assert.IsTrue(bar.HasBeenCalled);
        Assert.AreEqual(string.Empty, bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizeUnknownEvery250Milliseconds_DrawsCurlsMeasuredFlyFrames()
    {
        ProgressBarRecorder bar = new(clock, 0, 30);
        bar.ReportTransferStarted();

        for (int chunk = 1; chunk <= 4; chunk++)
        {
            clock.Advance(250);
            bar.ReportDownloaded(chunk * 1000, null);
        }

        Assert.AreEqual(
            "\r##O=-                         "
            + "\r###O=-                        "
            + "\r###=O=-                       "
            + "\r## #=O=-                      ",
            bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizeUnknownWithin100Milliseconds_DrawsNothing()
    {
        ProgressBarRecorder bar = new(clock, 0, 30);
        bar.ReportTransferStarted();

        clock.Advance(99);
        bar.ReportDownloaded(1000, null);

        Assert.AreEqual(string.Empty, bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizeUnknownAndPositionUnchanged_DrawsTheFrameWithoutMovingIt()
    {
        ProgressBarRecorder bar = new(clock, 0, 30);
        bar.ReportTransferStarted();

        clock.Advance(100);
        bar.ReportDownloaded(0, null);
        clock.Advance(100);
        bar.ReportDownloaded(0, null);

        Assert.AreEqual(
            "\r##O=-                         "
            + "\r###=-                         ",
            bar.Drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizeUnknownFor31Frames_BouncesTheFlyerOffBothEnds()
    {
        ProgressBarRecorder bar = new(clock, 0, 20);
        bar.ReportTransferStarted();

        for (int frame = 1; frame <= 31; frame++)
        {
            clock.Advance(100);
            bar.ReportDownloaded(frame, null);
        }

        string[] frames = bar.Drawn.Split('\r')[1..];
        Assert.HasCount(31, frames);
        Assert.AreEqual("   ### #      -=O=- ", frames[14]);
        Assert.AreEqual("   # ###     -=O=-  ", frames[15]);
        Assert.AreEqual("-=O=-     # ###     ", frames[28]);
        Assert.AreEqual("-=O=-      ## ##    ", frames[29]);
        Assert.AreEqual(" -=O=-      ####    ", frames[30]);
    }

    [TestMethod]
    public void Finish_SucceededWithNoBytesReported_DrawsTheSizeAsTheWholeTotal()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);
        bar.ReportTransferStarted();

        bar.Finish(succeeded: true, bytesTransferred: 10);

        Assert.AreEqual(Bar(72, "100.0"), bar.Drawn);
    }

    [TestMethod]
    public void Finish_Failed_DrawsNothing()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);
        bar.ReportTransferStarted();

        bar.Finish(succeeded: false, bytesTransferred: 10);

        Assert.AreEqual(string.Empty, bar.Drawn);
    }

    [TestMethod]
    public void Finish_SucceededAfterByteReports_DrawsNothingMore()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);
        bar.ReportDownloaded(5, 10);

        clock.Advance(500);
        bar.Finish(succeeded: true, bytesTransferred: 10);

        Assert.AreEqual(Bar(36, " 50.0"), bar.Drawn);
    }

    private static string Bar(int hashes, string percent) =>
        "\r" + new string('#', hashes).PadRight(72) + " " + percent + "%";
}
