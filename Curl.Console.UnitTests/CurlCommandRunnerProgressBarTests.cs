using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

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

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_ProgressBarOnATenByteFileUrl_WritesOneFullBarThenANewline()
    {
        int exitCode = await RunAsync(["-#", FileUrl, "-o", "out"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarWithAHandlerThatReportsNoBytes_WritesOneFullBarThenANewline()
    {
        int exitCode = await RunAsync(["--progress-bar", FileUrl, "-o", "out"], StartedHandler("file", 10));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarContinueAt5_WritesTwoFullBarsAndNoResumingLine()
    {
        outputFiles.ExistingContent["out"] = Encoding.ASCII.GetBytes("01234");

        int exitCode = await RunAsync(["-#", "-C", "5", FileUrl, "-o", "out"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + FullBar + NewLine, StandardErrorText);
        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(outputFiles.Written["out"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOnAnEmptyFile_WritesOnlyTheNewline()
    {
        int exitCode = await RunAsync(["-#", FileUrl, "-o", "out"], StartedHandler("file", 0));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOn200000Of200000Bytes_WritesTheFullBarAtTheDefaultWidth()
    {
        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "out"], Reporting200000Bytes());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOn200000Of200000BytesAt40Columns_WritesTheFullBarAt40Columns()
    {
        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "out"], Reporting200000Bytes(), terminalColumns: 40);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBarAt40Columns + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarWithBodyOnStandardOutput_WritesTheBar()
    {
        int exitCode = await RunAsync(["-#", HttpUrl], Reporting200000Bytes());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarWithBodyOnATerminal_WritesNothing()
    {
        int exitCode = await RunAsync(["-#", HttpUrl], Reporting200000Bytes(), standardOutputIsTerminal: true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--no-progress-meter")]
    public async Task RunAsync_ProgressBarUnderOptionThatHidesProgress_WritesNoBar(string option)
    {
        int exitCode = await RunAsync([option, "-#", HttpUrl, "-o", "out"], Reporting200000Bytes());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBar_NeverWritesTheMeterHeaderLines()
    {
        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "out"], Reporting200000Bytes());

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(StandardErrorText.Contains("% Total", StringComparison.Ordinal));
        Assert.IsFalse(StandardErrorText.Contains("Dload", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOnAFailureAfterTheStart_WritesTheNewlineAfterTheFailureLine()
    {
        RecordingProtocolHandler notFound = new("http", context =>
        {
            context.Progress.ReportTransferStarted();

            return ValueTask.FromResult(
                TransferResult.Failure(CurlExitCode.HttpReturnedError, "The requested URL returned error: 404"));
        });

        int exitCode = await RunAsync(["-#", "-f", "-w", "[%{exitcode}]", HttpUrl, "-o", "out"], notFound);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual("curl: (22) The requested URL returned error: 404" + NewLine + NewLine, StandardErrorText);
        Assert.AreEqual("[22]", Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarContinueAt5OnAFailedResume_WritesTheBarThenTheFailureLineThenTheNewline()
    {
        RecordingProtocolHandler cannotResume = new("file", context =>
        {
            context.Progress.ReportTransferStarted();

            return ValueTask.FromResult(
                TransferResult.Failure(CurlExitCode.BadDownloadResume, "failed to resume file:// transfer"));
        });

        int exitCode = await RunAsync(["-#", "-C", "5", FileUrl, "-o", "out"], cannotResume);

        Assert.AreEqual(36, exitCode);
        Assert.AreEqual(FullBar + "curl: (36) failed to resume file:// transfer" + NewLine + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOnAFailureBeforeTheStart_WritesOnlyTheFailureLine()
    {
        RecordingProtocolHandler refused =
            RecordingProtocolHandler.Failing("http", CurlExitCode.CouldntConnect, "Failed to connect");

        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "out"], refused);

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("curl: (7) Failed to connect" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarWithWriteOut_WritesTheNewlineBeforeTheWriteOutput()
    {
        int exitCode = await RunAsync(["-#", "-w", "%{stderr}[done]", HttpUrl, "-o", "out"], Reporting200000Bytes());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine + "[done]", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBarOnTwoUrls_DrawsEachOnesBarFromItsOwnReports()
    {
        int exitCode = await RunAsync(["-#", HttpUrl, "-o", "o1", HttpUrl, "-o", "o2"], Reporting200000Bytes());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FullBar + NewLine + FullBar + NewLine, StandardErrorText);
    }

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

    private Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        IProtocolHandler? handler = null,
        int terminalColumns = TerminalColumns.Default,
        bool standardOutputIsTerminal = false) =>
        new CurlCommandRunner(
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
