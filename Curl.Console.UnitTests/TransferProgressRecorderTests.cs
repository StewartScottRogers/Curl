using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void HasTransferStarted_NothingReported_IsFalse()
    {
        Diagnostics.Arrange("reports", "none");
        Diagnostics.Act("transfer started", new TransferProgressRecorder(clock).HasTransferStarted);

        Diagnostics.Assert("transfer started", false, new TransferProgressRecorder(clock).HasTransferStarted);
        Assert.IsFalse(new TransferProgressRecorder(clock).HasTransferStarted);
    }

    [TestMethod]
    public void HasTransferStarted_AfterReportTransferStarted_IsTrue()
    {
        TransferProgressRecorder recorder = new(clock);
        Diagnostics.Arrange("reports", "transfer started");

        recorder.ReportTransferStarted();
        Diagnostics.Act("transfer started", recorder.HasTransferStarted);

        Diagnostics.Assert("transfer started", true, recorder.HasTransferStarted);
        Assert.IsTrue(recorder.HasTransferStarted);
    }

    [TestMethod]
    public void HasTransferStarted_AfterOnlyByteReports_IsFalse()
    {
        TransferProgressRecorder recorder = new(clock);
        Diagnostics.Arrange("reports", "downloaded 10 of 20, uploaded 5 of unknown");

        recorder.ReportDownloaded(10, 20);
        recorder.ReportUploaded(5, null);
        Diagnostics.Act("transfer started", recorder.HasTransferStarted);

        Diagnostics.Assert("transfer started", false, recorder.HasTransferStarted);
        Assert.IsFalse(recorder.HasTransferStarted);
    }

    [TestMethod]
    public void WriteLive_ByteReportsBeforeTransferStarted_WritesNothing()
    {
        List<string> written = [];
        TransferProgressRecorder recorder = new(clock, writeLive: written.Add);
        Diagnostics.Arrange("reports", "after 1000 ms: downloaded 10 of 10, uploaded 5 of unknown; transfer not started");

        clock.Advance(1000);
        recorder.ReportDownloaded(10, 10);
        recorder.ReportUploaded(5, null);
        Diagnostics.Act("live writes", written.Count);

        Diagnostics.Assert("live writes", 0, written.Count);
        Assert.IsEmpty(written);
    }

    [TestMethod]
    public void WriteLive_TransferStarted_WritesTheLinesDrawnSoFarOnce()
    {
        List<string> written = [];
        TransferProgressRecorder recorder = new(clock, writeLive: written.Add);
        Diagnostics.Arrange("reports", "transfer started; after 40 ms: downloaded 10 of 10, uploaded 0 of unknown");

        recorder.ReportTransferStarted();
        clock.Advance(40);
        recorder.ReportDownloaded(10, 10);
        recorder.ReportUploaded(0, null);
        Diagnostics.Act("live writes", Visible(string.Join(" | ", written)));

        Diagnostics.Assert("live writes", Visible(Zero), Visible(string.Join(" | ", written)));
        CollectionAssert.AreEqual(new[] { Zero }, written);
    }

    [TestMethod]
    public void ReportTransferDone_MeterWrittenLiveAfterTransferStarted_HoldsTheEventOutput()
    {
        using MemoryStream standardError = new();
        using HoldableStream eventOutput = new(standardError);
        TransferProgressRecorder recorder = new(clock, writeLive: _ => { }, eventOutput: eventOutput);
        recorder.ReportTransferStarted();
        Diagnostics.Arrange("meter / reports", "written live / transfer started, then done, then one event byte");

        recorder.ReportTransferDone();
        eventOutput.Write([1], 0, 1);
        Diagnostics.Act("standard error length", standardError.Length);

        Diagnostics.Assert("standard error length", 0, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public void ReportTransferDone_BeforeTransferStarted_LeavesTheEventOutputUnheld()
    {
        using MemoryStream standardError = new();
        using HoldableStream eventOutput = new(standardError);
        TransferProgressRecorder recorder = new(clock, writeLive: _ => { }, eventOutput: eventOutput);
        Diagnostics.Arrange("meter / reports", "written live / done without a start, then one event byte");

        recorder.ReportTransferDone();
        eventOutput.Write([1], 0, 1);
        Diagnostics.Act("standard error length", standardError.Length);

        Diagnostics.Assert("standard error length", 1, standardError.Length);
        Assert.AreEqual(1, standardError.Length);
    }

    [TestMethod]
    public void ReportTransferDone_MeterNotWrittenLive_LeavesTheEventOutputUnheld()
    {
        using MemoryStream standardError = new();
        using HoldableStream eventOutput = new(standardError);
        TransferProgressRecorder recorder = new(clock, eventOutput: eventOutput);
        recorder.ReportTransferStarted();
        Diagnostics.Arrange("meter / reports", "not written live / transfer started, then done, then one event byte");

        recorder.ReportTransferDone();
        eventOutput.Write([1], 0, 1);
        Diagnostics.Act("standard error length", standardError.Length);

        Diagnostics.Assert("standard error length", 1, standardError.Length);
        Assert.AreEqual(1, standardError.Length);
    }

    [TestMethod]
    public void ReportTransferDone_NoEventOutput_DrawsNothing()
    {
        List<string> written = [];
        TransferProgressRecorder recorder = new(clock, writeLive: written.Add);
        recorder.ReportTransferStarted();
        Diagnostics.Arrange("event output / reports", "none / transfer started, then done");

        recorder.ReportTransferDone();
        Diagnostics.Act("live writes", Visible(string.Join(" | ", written)));

        Diagnostics.Assert("live writes", Visible(Zero), Visible(string.Join(" | ", written)));
        CollectionAssert.AreEqual(new[] { Zero }, written);
    }

    [TestMethod]
    public void ReportTransferStarted_NextHopAfterDone_ReleasesTheHeldEventOutputBeforeTheHopsDraws()
    {
        using MemoryStream standardError = new();
        using HoldableStream eventOutput = new(standardError);
        TransferProgressRecorder recorder = new(clock, writeLive: text => standardError.Write(System.Text.Encoding.UTF8.GetBytes(text)), eventOutput: eventOutput);
        recorder.ReportTransferStarted();
        recorder.ReportTransferDone();
        eventOutput.Write("* left intact\n"u8.ToArray(), 0, 14);
        Diagnostics.Arrange("reports", "started, done, event \"* left intact\", then started again");

        recorder.ReportTransferStarted();
        string written = System.Text.Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Act("standard error", Visible(written));

        Diagnostics.Assert("standard error starts with", Visible(Zero + "* left intact\n" + Zero), Visible(written));
        StringAssert.StartsWith(
            System.Text.Encoding.UTF8.GetString(standardError.ToArray()),
            Zero + "* left intact\n" + Zero);
    }

    [TestMethod]
    public void TakeUnwrittenStatusLines_AfterLiveWrites_IsOnlyTheLinesDrawnSince()
    {
        List<string> written = [];
        TransferProgressRecorder recorder = new(clock, writeLive: written.Add);
        recorder.ReportTransferStarted();
        clock.Advance(40);
        recorder.ReportDownloaded(10, 10);
        Diagnostics.Arrange("reports", "started; after 40 ms: downloaded 10 of 10; finished, succeeded");

        recorder.Finish(succeeded: true);
        string unwritten = recorder.TakeUnwrittenStatusLines();
        Diagnostics.Act("unwritten status lines", Visible(unwritten));

        Diagnostics.Assert("unwritten status lines", Visible(TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds), Visible(unwritten));
        Assert.AreEqual(TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds, unwritten);
        Assert.AreEqual(string.Empty, recorder.TakeUnwrittenStatusLines());
        ExpectStatusLines(recorder, Zero + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds);
        Assert.AreEqual(Zero + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds, recorder.StatusLines);
    }

    [TestMethod]
    public void StatusLines_NothingReported_IsTheZeroStatusLine()
    {
        TransferProgressRecorder recorder = new(clock);
        Diagnostics.Arrange("reports", "none");
        Diagnostics.Act("status lines", Visible(recorder.StatusLines));

        ExpectStatusLines(recorder, Zero);
        Assert.AreEqual(Zero, new TransferProgressRecorder(clock).StatusLines);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Finish_NoBytesReported_DrawsNothingMore(bool succeeded)
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(5000);
        Diagnostics.Arrange("reports", $"after 5000 ms: finished, succeeded {succeeded}");

        recorder.Finish(succeeded);
        Diagnostics.Act("status lines", Visible(recorder.StatusLines));

        ExpectStatusLines(recorder, Zero);
        Assert.AreEqual(Zero, recorder.StatusLines);
    }

    [TestMethod]
    public void ReportDownloaded_WithinASecondOfTheLastSample_DrawsNothing()
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(999);
        Diagnostics.Arrange("reports", "after 999 ms: downloaded 10 of 10");

        recorder.ReportDownloaded(10, 10);
        Diagnostics.Act("status lines", Visible(recorder.StatusLines));

        ExpectStatusLines(recorder, Zero);
        Assert.AreEqual(Zero, recorder.StatusLines);
    }

    [TestMethod]
    public void Finish_SucceededAfterTenOfTenBytes_DrawsTheDoneLineThreeTimes()
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(40);
        recorder.ReportDownloaded(10, 10);
        Diagnostics.Arrange("reports", "after 40 ms: downloaded 10 of 10; finished, succeeded");

        recorder.Finish(succeeded: true);
        Diagnostics.Act("status lines", Visible(recorder.StatusLines));

        ExpectStatusLines(recorder, Zero + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds);
        Assert.AreEqual(Zero + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds, recorder.StatusLines);
    }

    [TestMethod]
    public void FinishRedirectHop_WithNoBytesReported_DrawsTheZeroLineTwiceMore()
    {
        TransferProgressRecorder recorder = new(clock);
        recorder.ReportTransferStarted();
        Diagnostics.Arrange("reports", "started; redirect hop finished");

        recorder.FinishRedirectHop();
        Diagnostics.Act("status lines", Visible(recorder.StatusLines));

        ExpectStatusLines(recorder, Zero + Zero + Zero);
        Assert.AreEqual(Zero + Zero + Zero, recorder.StatusLines);
    }

    [TestMethod]
    public void ReportTransferStarted_Repeated_EndsTheHopAndStartsTheNextFromZero()
    {
        TransferProgressRecorder recorder = new(clock);
        recorder.ReportTransferStarted();
        clock.Advance(40);
        recorder.ReportDownloaded(10, 10);
        Diagnostics.Arrange("reports", "two hops, each started then 10 of 10 downloaded after 40 ms; finished, succeeded");

        recorder.ReportTransferStarted();
        clock.Advance(40);
        recorder.ReportDownloaded(10, 10);
        recorder.Finish(succeeded: true);
        Diagnostics.Act("status lines / transfer started", $"{VisibleAcrossPlatforms(recorder.StatusLines)} / {recorder.HasTransferStarted}");

        ExpectStatusLines(
            recorder,
            Zero + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + Environment.NewLine
                + Zero + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds + TenOfTenIn40Milliseconds);
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
        Diagnostics.Arrange("reports", "after 40 ms: downloaded 10 of 10; finished, failed");

        recorder.Finish(succeeded: false);
        Diagnostics.Act("status lines", Visible(recorder.StatusLines));

        ExpectStatusLines(recorder, Zero);
        Assert.AreEqual(Zero, recorder.StatusLines);
    }

    [TestMethod]
    public void Finish_FailedASecondAfterTheLastSample_DrawsOneLine()
    {
        TransferProgressRecorder recorder = new(clock);
        clock.Advance(40);
        recorder.ReportDownloaded(10, 20);
        clock.Advance(1960);
        Diagnostics.Arrange("reports", "after 40 ms: downloaded 10 of 20; after 1960 ms more: finished, failed");

        recorder.Finish(succeeded: false);
        Diagnostics.Act("status lines", Visible(recorder.StatusLines));

        ExpectStatusLines(recorder, Zero + "\r 50     20  50     10   0      0      5      0   00:04   00:02   00:02      5");
        Assert.AreEqual(
            Zero + "\r 50     20  50     10   0      0      5      0   00:04   00:02   00:02      5",
            recorder.StatusLines);
    }

    [TestMethod]
    public void ReportDownloaded_EachSecond_DrawsALineWithTheSpeedOverTheLastFiveSeconds()
    {
        TransferProgressRecorder recorder = new(clock);
        Diagnostics.Arrange("reports", "each second for 7 seconds: downloaded second squared thousand bytes of unknown");
        for (int second = 1; second <= 7; second++)
        {
            clock.Advance(1000);
            recorder.ReportDownloaded(second * second * 1000L, null);
        }

        string[] lines = recorder.StatusLines.Split('\r');
        Diagnostics.Act("status lines", Visible(recorder.StatusLines));

        Diagnostics.Assert(
            "line count / line 2 / line 8",
            $"9 / 100   1000   0   1000   0      0   1000      0 {NoTime}   00:01 {NoTime}   1000 / 100  49000   0  49000   0      0   7000      0 {NoTime}   00:07 {NoTime}   9000",
            $"{lines.Length} / {lines[2]} / {lines[8]}");
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
        Diagnostics.Arrange("reports", "after 1000 ms: uploaded 4000 of 8000; after 500 ms more: 8000 of 8000; finished, succeeded");

        recorder.Finish(succeeded: true);
        Diagnostics.Act("status lines", Visible(recorder.StatusLines));

        string doneLine = "\r100   8000   0      0 100   8000      0   5333   00:01   00:01 " + NoTime + "   4000";
        ExpectStatusLines(recorder, Zero + "\r 50   8000   0      0  50   4000      0   4000   00:02   00:01   00:01   4000" + doneLine + doneLine + doneLine);
        Assert.AreEqual(
            Zero + "\r 50   8000   0      0  50   4000      0   4000   00:02   00:01   00:01   4000" + doneLine + doneLine + doneLine,
            recorder.StatusLines);
    }

    [TestMethod]
    public void ReportDownloaded_MoreThanTimesAMillionFitsInALong_CapsTheTotalAndComputesTheCurrentSpeedWithoutOverflow()
    {
        TransferProgressRecorder recorder = new(clock);
        Diagnostics.Arrange("reports", "uploaded 1 of unknown; after 1000 ms: downloaded long.MaxValue / 2 of long.MaxValue");
        recorder.ReportUploaded(1, null);
        clock.Advance(1000);
        recorder.ReportDownloaded(long.MaxValue / 2, long.MaxValue);

        string line = recorder.StatusLines.Split('\r')[2];
        Diagnostics.Act("line 2", line);

        Diagnostics.Assert("line 2", " 50  7.99E  50  3.99E   0      1  3.99E      1   00:02   00:01   00:01  4.00E", line);
        Assert.AreEqual(" 50  7.99E  50  3.99E   0      1  3.99E      1   00:02   00:01   00:01  4.00E", line);
    }

    [TestMethod]
    public void Reports_WithAProgressBar_PassEachReportOnToTheBar()
    {
        ProgressBarRecorder bar = new(clock, 0, 79);
        TransferProgressRecorder recorder = new(clock, bar);
        Diagnostics.Arrange("bar / reports", "79 columns / started; after 100 ms: uploaded 5 of 10; after 100 ms more: downloaded 10 of 10");

        recorder.ReportTransferStarted();
        clock.Advance(100);
        recorder.ReportUploaded(5, 10);
        clock.Advance(100);
        recorder.ReportDownloaded(10, 10);
        Diagnostics.Act("transfer started / bar drawn", $"{recorder.HasTransferStarted} / {Visible(bar.Drawn)}");

        string expectedBar = "\r" + new string('#', 36).PadRight(72) + "  50.0%" + "\r" + new string('#', 54).PadRight(72) + "  75.0%";
        Diagnostics.Assert("transfer started / bar drawn", $"True / {Visible(expectedBar)}", $"{recorder.HasTransferStarted} / {Visible(bar.Drawn)}");
        Assert.IsTrue(recorder.HasTransferStarted);
        Assert.AreEqual(
            "\r" + new string('#', 36).PadRight(72) + "  50.0%" + "\r" + new string('#', 54).PadRight(72) + "  75.0%",
            bar.Drawn);
    }

    [TestMethod]
    public void ReportUploaded_InAParallelRun_PassesTheBytesOnToItsShareOfTheCombinedMeter()
    {
        ParallelTransferProgress share = new ParallelProgressMeter(new ManualTimerTimeProvider(), new WriteGate(), _ => { }).AddTransfer();
        TransferProgressRecorder recorder = new(clock, parallelProgress: share);
        Diagnostics.Arrange("meter / reports", "a parallel run's share / uploaded 5 of 20");

        recorder.ReportUploaded(5, 20);
        Diagnostics.Act("share uploaded / upload total", $"{share.Uploaded} / {share.UploadTotal}");

        Diagnostics.Assert("share uploaded / upload total", "5 / 20", $"{share.Uploaded} / {share.UploadTotal}");
        Assert.AreEqual(5, share.Uploaded);
        Assert.AreEqual(20, share.UploadTotal);
    }

    /// <summary>Shows a carriage return as <c>\r</c> and a line feed as <c>\n</c>.</summary>
    private static string Visible(string? text) =>
        (text ?? string.Empty).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Shows text that holds the platform's new line, as <c>&lt;newline&gt;</c>, so nothing printed depends on the operating system.</summary>
    private static string VisibleAcrossPlatforms(string text) =>
        Visible(text.Replace(Environment.NewLine, "<newline>", StringComparison.Ordinal));

    private void ExpectStatusLines(TransferProgressRecorder recorder, string expected) =>
        Diagnostics.Assert("status lines", VisibleAcrossPlatforms(expected), VisibleAcrossPlatforms(recorder.StatusLines));
}
