using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(5, 20)]
    [DataRow(20, 20)]
    [DataRow(21, 21)]
    [DataRow(79, 79)]
    [DataRow(400, 400)]
    [DataRow(401, 400)]
    public void Width_TerminalColumns_ClampsTo20Through400(int terminalColumns, int expected)
    {
        Diagnostics.Arrange("terminal columns", terminalColumns);

        int width = ProgressBarRecorder.Width(terminalColumns);
        Diagnostics.Act("width", width);

        Diagnostics.Assert("width", expected, width);
        Assert.AreEqual(expected, width);
    }

    [TestMethod]
    public void ReportTransferStarted_NothingKnown_CallsWithoutDrawing()
    {
        ProgressBarRecorder bar = NewBar(0, 79);

        bar.ReportTransferStarted();
        string drawn = ActDrawn(bar);

        Diagnostics.Assert("has been called", true, bar.HasBeenCalled);
        AssertDrawn(string.Empty, drawn);
        Assert.IsTrue(bar.HasBeenCalled);
        Assert.AreEqual(string.Empty, bar.Drawn);
    }

    [TestMethod]
    public void HasBeenCalled_NoReport_IsFalse()
    {
        ProgressBarRecorder bar = NewBar(0, 79);

        bool called = bar.HasBeenCalled;
        Diagnostics.Act("has been called", called);

        Diagnostics.Assert("has been called", false, called);
        Assert.IsFalse(called);
    }

    [TestMethod]
    public void ReportTransferStarted_ResumedPastZero_DrawsTheOffsetAsTheWholeTotal()
    {
        ProgressBarRecorder bar = NewBar(5, 79);

        bar.ReportTransferStarted();
        string drawn = ActDrawn(bar);

        AssertDrawn(Bar(72, "100.0"), drawn);
        Assert.AreEqual(Bar(72, "100.0"), drawn);
    }

    [TestMethod]
    public void ReportDownloaded_Within100MillisecondsBelowTheTotal_WaitsThenDrawsOnceTheyHavePassed()
    {
        Diagnostics.Arrange("reports", "100000 of 200000 bytes at 50 ms and again at 150 ms");
        ProgressBarRecorder bar = NewBar(0, 79);
        bar.ReportTransferStarted();

        clock.Advance(50);
        bar.ReportDownloaded(100000, 200000);
        string afterFiftyMilliseconds = bar.Drawn;
        clock.Advance(100);
        bar.ReportDownloaded(100000, 200000);
        Diagnostics.Act("drawn after 50 ms", Neutral(afterFiftyMilliseconds));
        string drawn = ActDrawn(bar);

        Diagnostics.Assert("drawn after 50 ms", string.Empty, Neutral(afterFiftyMilliseconds));
        AssertDrawn(Bar(36, " 50.0"), drawn);
        Assert.AreEqual(string.Empty, afterFiftyMilliseconds);
        Assert.AreEqual(Bar(36, " 50.0"), drawn);
    }

    [TestMethod]
    public void ReportDownloaded_PositionUnchanged_DrawsNothing()
    {
        Diagnostics.Arrange("reports", "100000 of 200000 bytes at 0 ms and again at 500 ms");
        ProgressBarRecorder bar = NewBar(0, 79);
        bar.ReportDownloaded(100000, 200000);

        clock.Advance(500);
        bar.ReportDownloaded(100000, 200000);
        string drawn = ActDrawn(bar);

        AssertDrawn(Bar(36, " 50.0"), drawn);
        Assert.AreEqual(Bar(36, " 50.0"), drawn);
    }

    [TestMethod]
    public void ReportDownloaded_ReachesTheTotalWithin100Milliseconds_DrawsAtOnce()
    {
        Diagnostics.Arrange("reports", "100000 of 200000 bytes at 0 ms, 200000 of 200000 at 10 ms");
        ProgressBarRecorder bar = NewBar(0, 79);
        bar.ReportDownloaded(100000, 200000);

        clock.Advance(10);
        bar.ReportDownloaded(200000, 200000);
        string drawn = ActDrawn(bar);

        AssertDrawn(Bar(36, " 50.0") + Bar(72, "100.0"), drawn);
        Assert.AreEqual(Bar(36, " 50.0") + Bar(72, "100.0"), drawn);
    }

    [TestMethod]
    public void ReportDownloaded_OneThird_RoundsThePercentageToOneDecimal()
    {
        Diagnostics.Arrange("report", "1 of 3 bytes");
        ProgressBarRecorder bar = NewBar(0, 79);

        bar.ReportDownloaded(1, 3);
        string drawn = ActDrawn(bar);

        AssertDrawn(Bar(24, " 33.3"), drawn);
        Assert.AreEqual(Bar(24, " 33.3"), drawn);
    }

    [TestMethod]
    public void ReportDownloaded_PastTheTotal_DrawsAFullBar()
    {
        Diagnostics.Arrange("report", "20 of 10 bytes");
        ProgressBarRecorder bar = NewBar(0, 79);

        bar.ReportDownloaded(20, 10);
        string drawn = ActDrawn(bar);

        AssertDrawn(Bar(72, "100.0"), drawn);
        Assert.AreEqual(Bar(72, "100.0"), drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizesPastLongMaxValue_CapsThemAndDrawsAFullBar()
    {
        Diagnostics.Arrange("report", "long.MaxValue of long.MaxValue bytes");
        ProgressBarRecorder bar = NewBar(1, 79);

        bar.ReportDownloaded(long.MaxValue, long.MaxValue);
        string drawn = ActDrawn(bar);

        AssertDrawn(Bar(72, "100.0"), drawn);
        Assert.AreEqual(Bar(72, "100.0"), drawn);
    }

    [TestMethod]
    public void ReportUploaded_HalfTheUpload_DrawsAHalfBar()
    {
        Diagnostics.Arrange("report", "5 of 10 bytes uploaded");
        ProgressBarRecorder bar = NewBar(0, 79);

        bar.ReportUploaded(5, 10);
        string drawn = ActDrawn(bar);

        AssertDrawn(Bar(36, " 50.0"), drawn);
        Assert.AreEqual(Bar(36, " 50.0"), drawn);
    }

    [TestMethod]
    public void ReportUploaded_SizeUnknown_DrawsNothingOnTheFirstCall()
    {
        Diagnostics.Arrange("report", "5 bytes uploaded of unknown size");
        ProgressBarRecorder bar = NewBar(0, 79);

        bar.ReportUploaded(5, null);
        string drawn = ActDrawn(bar);

        Diagnostics.Assert("has been called", true, bar.HasBeenCalled);
        AssertDrawn(string.Empty, drawn);
        Assert.IsTrue(bar.HasBeenCalled);
        Assert.AreEqual(string.Empty, drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizeUnknownEvery250Milliseconds_DrawsCurlsMeasuredFlyFrames()
    {
        Diagnostics.Arrange("reports", "1000, 2000, 3000, 4000 bytes of unknown size, 250 ms apart");
        ProgressBarRecorder bar = NewBar(0, 30);
        bar.ReportTransferStarted();

        for (int chunk = 1; chunk <= 4; chunk++)
        {
            clock.Advance(250);
            bar.ReportDownloaded(chunk * 1000, null);
        }

        string drawn = ActDrawn(bar);
        const string expected =
            "\r##O=-                         "
            + "\r###O=-                        "
            + "\r###=O=-                       "
            + "\r## #=O=-                      ";
        AssertDrawn(expected, drawn);
        Assert.AreEqual(
            expected,
            drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizeUnknownWithin100Milliseconds_DrawsNothing()
    {
        Diagnostics.Arrange("report", "1000 bytes of unknown size at 99 ms");
        ProgressBarRecorder bar = NewBar(0, 30);
        bar.ReportTransferStarted();

        clock.Advance(99);
        bar.ReportDownloaded(1000, null);
        string drawn = ActDrawn(bar);

        AssertDrawn(string.Empty, drawn);
        Assert.AreEqual(string.Empty, drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizeUnknownAndPositionUnchanged_DrawsTheFrameWithoutMovingIt()
    {
        Diagnostics.Arrange("reports", "0 bytes of unknown size at 100 ms and 200 ms");
        ProgressBarRecorder bar = NewBar(0, 30);
        bar.ReportTransferStarted();

        clock.Advance(100);
        bar.ReportDownloaded(0, null);
        clock.Advance(100);
        bar.ReportDownloaded(0, null);
        string drawn = ActDrawn(bar);

        const string expected =
            "\r##O=-                         "
            + "\r###=-                         ";
        AssertDrawn(expected, drawn);
        Assert.AreEqual(
            expected,
            drawn);
    }

    [TestMethod]
    public void ReportDownloaded_SizeUnknownFor31Frames_BouncesTheFlyerOffBothEnds()
    {
        Diagnostics.Arrange("reports", "31 reports of unknown size, 100 ms apart");
        ProgressBarRecorder bar = NewBar(0, 20);
        bar.ReportTransferStarted();

        for (int frame = 1; frame <= 31; frame++)
        {
            clock.Advance(100);
            bar.ReportDownloaded(frame, null);
        }

        string[] frames = bar.Drawn.Split('\r')[1..];
        Diagnostics.Act("frame count", frames.Length);
        Diagnostics.Act("frames 14, 15, 28, 29, 30", $"[{frames[14]}] [{frames[15]}] [{frames[28]}] [{frames[29]}] [{frames[30]}]");
        Diagnostics.Assert("frame count", 31, frames.Length);
        Diagnostics.Assert("frame 14", "   ### #      -=O=- ", frames[14]);
        Diagnostics.Assert("frame 30", " -=O=-      ####    ", frames[30]);
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
        Diagnostics.Arrange("finish", "succeeded, 10 bytes, none reported");
        ProgressBarRecorder bar = NewBar(0, 79);
        bar.ReportTransferStarted();

        bar.Finish(succeeded: true, bytesTransferred: 10);
        string drawn = ActDrawn(bar);

        AssertDrawn(Bar(72, "100.0"), drawn);
        Assert.AreEqual(Bar(72, "100.0"), drawn);
    }

    [TestMethod]
    public void Finish_Failed_DrawsNothing()
    {
        Diagnostics.Arrange("finish", "failed, 10 bytes");
        ProgressBarRecorder bar = NewBar(0, 79);
        bar.ReportTransferStarted();

        bar.Finish(succeeded: false, bytesTransferred: 10);
        string drawn = ActDrawn(bar);

        AssertDrawn(string.Empty, drawn);
        Assert.AreEqual(string.Empty, drawn);
    }

    [TestMethod]
    public void Finish_SucceededAfterByteReports_DrawsNothingMore()
    {
        Diagnostics.Arrange("reports", "5 of 10 bytes, then finish succeeded with 10 bytes at 500 ms");
        ProgressBarRecorder bar = NewBar(0, 79);
        bar.ReportDownloaded(5, 10);

        clock.Advance(500);
        bar.Finish(succeeded: true, bytesTransferred: 10);
        string drawn = ActDrawn(bar);

        AssertDrawn(Bar(36, " 50.0"), drawn);
        Assert.AreEqual(Bar(36, " 50.0"), drawn);
    }

    private static string Bar(int hashes, string percent) =>
        "\r" + new string('#', hashes).PadRight(72) + " " + percent + "%";

    /// <summary>The bar's text with carriage returns spelled out, so the diagnostics stay on one line.</summary>
    private static string Neutral(string text) => text.Replace("\r", "\\r");

    private ProgressBarRecorder NewBar(long resumeOffset, int terminalColumns)
    {
        Diagnostics.Arrange("resume offset", resumeOffset);
        Diagnostics.Arrange("terminal columns", terminalColumns);
        return new ProgressBarRecorder(clock, resumeOffset, terminalColumns);
    }

    private string ActDrawn(ProgressBarRecorder bar)
    {
        Diagnostics.Act("drawn", Neutral(bar.Drawn));
        return bar.Drawn;
    }

    private void AssertDrawn(string expected, string actual) => Diagnostics.Diff("drawn", Neutral(expected), Neutral(actual));
}
