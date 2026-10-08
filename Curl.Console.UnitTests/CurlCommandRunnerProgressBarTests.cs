using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-#</c>/<c>--progress-bar</c> bar the runner writes to standard error, against
/// curl 8.21.0 measured on Windows on 2026-09-27 with standard error redirected to a file
/// (task BL-132 Notes): a full bar is <c>\r</c>, 72 <c>#</c> and <c> 100.0%</c> at the default
/// 79 columns, 33 <c>#</c> at 40, and the bar ends with one newline, written after the
/// transfer's failure lines and before its <c>-w</c> output.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerProgressBarTests
{
    private const string FileUrl = "file:///ten.bin";

    private const string HttpUrl = "http://127.0.0.1:8765/big.bin";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string FullBar = "\r" + new string('#', 72) + " 100.0%";

    private static readonly string FullBarAt40Columns = "\r" + new string('#', 33) + " 100.0%";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly ManualTimeProvider clock = new();
    private readonly FileProtocolHandler fileHandler =
        new(new InMemoryFileSystem { ReadContent = Encoding.ASCII.GetBytes("0123456789") });

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_ProgressBarOnATenByteFileUrl_WritesOneFullBarThenANewline()
    {
        int exitCode = await RunAsync(["-#", FileUrl, "-o", "out"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(FullBar + NewLine), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarWithAHandlerThatReportsNoBytes_WritesOneFullBarThenANewline()
    {
        int exitCode = await RunAsync(["--progress-bar", FileUrl, "-o", "out"], StartedHandler("file", 10));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(FullBar + NewLine), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarContinueAt5_WritesTwoFullBarsAndNoResumingLine()
    {
        outputFiles.ExistingContent["out"] = Encoding.ASCII.GetBytes("01234");
        Diagnostics.Arrange("existing out", "01234");

        int exitCode = await RunAsync(["-#", "-C", "5", FileUrl, "-o", "out"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(FullBar + FullBar + NewLine), Visible(StandardErrorText));
        Diagnostics.Diff("out", "0123456789", Encoding.ASCII.GetString(outputFiles.Written["out"].ToArray()));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + FullBar + NewLine, StandardErrorText);
        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(outputFiles.Written["out"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOnAnEmptyFile_WritesOnlyTheNewline()
    {
        int exitCode = await RunAsync(["-#", FileUrl, "-o", "out"], StartedHandler("file", 0));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(NewLine), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOn200000Of200000Bytes_WritesTheFullBarAtTheDefaultWidth()
    {
        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "out"], Reporting200000Bytes());

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(FullBar + NewLine), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOn200000Of200000BytesAt40Columns_WritesTheFullBarAt40Columns()
    {
        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "out"], Reporting200000Bytes(), terminalColumns: 40);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(FullBarAt40Columns + NewLine), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBarAt40Columns + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarWithBodyOnStandardOutput_WritesTheBar()
    {
        int exitCode = await RunAsync(["-#", HttpUrl], Reporting200000Bytes());

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(FullBar + NewLine), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarWithBodyOnATerminal_WritesNothing()
    {
        int exitCode = await RunAsync(["-#", HttpUrl], Reporting200000Bytes(), standardOutputIsTerminal: true);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--no-progress-meter")]
    public async Task RunAsync_ProgressBarUnderOptionThatHidesProgress_WritesNoBar(string option)
    {
        int exitCode = await RunAsync([option, "-#", HttpUrl, "-o", "out"], Reporting200000Bytes());

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBar_NeverWritesTheMeterHeaderLines()
    {
        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "out"], Reporting200000Bytes());

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains % Total", false, StandardErrorText.Contains("% Total", StringComparison.Ordinal));
        Diagnostics.Assert("stderr contains Dload", false, StandardErrorText.Contains("Dload", StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(StandardErrorText.Contains("% Total", StringComparison.Ordinal));
        Assert.IsFalse(StandardErrorText.Contains("Dload", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOnAFailureAfterTheStart_WritesTheNewlineAfterTheFailureLine()
    {
        Diagnostics.Arrange("handler", "http reports the transfer started, then fails with 404");
        RecordingProtocolHandler notFound = new("http", context =>
        {
            context.Progress.ReportTransferStarted();

            return ValueTask.FromResult(
                TransferResult.Failure(CurlExitCode.HttpReturnedError, "The requested URL returned error: 404"));
        });

        int exitCode = await RunAsync(["-#", "-f", "-w", "[%{exitcode}]", HttpUrl, "-o", "out"], notFound);

        Diagnostics.Assert("exit code", 22, exitCode);
        Diagnostics.Diff("stderr", Visible("curl: (22) The requested URL returned error: 404" + NewLine + NewLine), Visible(StandardErrorText));
        Diagnostics.Diff("stdout", "[22]", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(22, exitCode);
        Assert.AreEqual("curl: (22) The requested URL returned error: 404" + NewLine + NewLine, StandardErrorText);
        Assert.AreEqual("[22]", Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarContinueAt5OnAFailedResume_WritesTheBarThenTheFailureLineThenTheNewline()
    {
        Diagnostics.Arrange("handler", "file reports the transfer started, then fails with 36");
        RecordingProtocolHandler cannotResume = new("file", context =>
        {
            context.Progress.ReportTransferStarted();

            return ValueTask.FromResult(
                TransferResult.Failure(CurlExitCode.BadDownloadResume, "failed to resume file:// transfer"));
        });

        int exitCode = await RunAsync(["-#", "-C", "5", FileUrl, "-o", "out"], cannotResume);

        Diagnostics.Assert("exit code", 36, exitCode);
        Diagnostics.Diff("stderr", Visible(FullBar + "curl: (36) failed to resume file:// transfer" + NewLine + NewLine), Visible(StandardErrorText));
        Assert.AreEqual(36, exitCode);
        Assert.AreEqual(FullBar + "curl: (36) failed to resume file:// transfer" + NewLine + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOnAFailureBeforeTheStart_WritesOnlyTheFailureLine()
    {
        RecordingProtocolHandler refused =
            RecordingProtocolHandler.Failing("http", CurlExitCode.CouldntConnect, "Failed to connect");

        Diagnostics.Arrange("handler", "http fails with 7 before the transfer starts");

        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "out"], refused);

        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Diff("stderr", Visible("curl: (7) Failed to connect" + NewLine), Visible(StandardErrorText));
        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("curl: (7) Failed to connect" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarWithWriteOut_WritesTheNewlineBeforeTheWriteOutput()
    {
        int exitCode = await RunAsync(["-#", "-w", "%{stderr}[done]", HttpUrl, "-o", "out"], Reporting200000Bytes());

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(FullBar + NewLine + "[done]"), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine + "[done]", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOnTwoUrls_DrawsEachOnesBarFromItsOwnReports()
    {
        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "o1", HttpUrl, "-o", "o2"], Reporting200000Bytes());

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(FullBar + NewLine + FullBar + NewLine), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine + FullBar + NewLine, StandardErrorText);
    }

    private static string Visible(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal);

    private static RecordingProtocolHandler StartedHandler(string scheme, long bytes) =>
        new(scheme, context =>
        {
            context.Progress.ReportTransferStarted();

            return ValueTask.FromResult(TransferResult.Success(bytes));
        });

    private RecordingProtocolHandler Reporting200000Bytes() =>
        new("http", context =>
        {
            context.Progress.ReportTransferStarted();
            clock.Advance(40);
            context.Progress.ReportDownloaded(200000, 200000);

            return ValueTask.FromResult(TransferResult.Success(200000));
        });

    private async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        IProtocolHandler? handler = null,
        int terminalColumns = TerminalColumns.Default,
        bool standardOutputIsTerminal = false)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("terminal columns", terminalColumns);
        Diagnostics.Arrange("standard output is a terminal", standardOutputIsTerminal);
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([handler ?? fileHandler])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false,
                    terminalColumns: terminalColumns,
                    writesProgressMeter: true,
                    standardOutputIsTerminal: standardOutputIsTerminal,
                    timeProvider: clock)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Visible(Encoding.UTF8.GetString(standardOutput.ToArray())));
        Diagnostics.Act("stderr", Visible(StandardErrorText));
        return exitCode;
    }
}
