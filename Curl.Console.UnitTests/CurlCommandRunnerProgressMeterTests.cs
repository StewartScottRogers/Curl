using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

namespace Curl.Console;

/// <summary>
/// Pins the progress meter the runner writes to standard error, against curl 8.21.0
/// measured on Windows on 2026-09-26 with a ten-byte <c>file://</c> source holding
/// <c>0123456789</c> and standard error redirected to a file (task BL-102).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerProgressMeterTests
{
    private const string SourceUrl = "file:///source.txt";

    private const string UploadUrl = "http://h/up/";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string Meter =
        "  % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current" + NewLine
        + "                                 Dload  Upload  Total   Spent   Left   Speed" + NewLine
        + "\r  0      0   0      0   0      0      0      0                              0" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly FileProtocolHandler fileHandler =
        new(new InMemoryFileSystem { ReadContent = Encoding.ASCII.GetBytes("0123456789") });

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_OutputFile_WritesTheMeterHeaderLinesAndZeroStatusLine()
    {
        int exitCode = await RunAsync(["-o", "o1", SourceUrl]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAt5_WritesTheResumingLineBeforeTheMeter()
    {
        outputFiles.ExistingContent["o2"] = Encoding.ASCII.GetBytes("01234");

        int exitCode = await RunAsync(["-C", "5", "-o", "o2", SourceUrl]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("** Resuming transfer from byte position 5" + NewLine + Meter, StandardErrorText);
        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(outputFiles.Written["o2"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtDash_NamesTheOutputFileSizeInTheResumingLine()
    {
        outputFiles.ExistingContent["o2"] = Encoding.ASCII.GetBytes("0123");

        await RunAsync(["-C", "-", "-o", "o2", SourceUrl]);

        Assert.AreEqual("** Resuming transfer from byte position 4" + NewLine + Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtDashWithUpload_WritesResumingFromMinusOneBeforeTheMeter()
    {
        outputFiles.ExistingContent["f.txt"] = Encoding.ASCII.GetBytes("abc");

        int exitCode = await RunAsync(["-C", "-", "-T", "f.txt", UploadUrl], handler: RecordingProtocolHandler.WritingPath("http"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("** Resuming transfer from byte position -1" + NewLine + Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtDashWithUploadAndExistingOutputFile_StillWritesResumingFromMinusOne()
    {
        outputFiles.ExistingContent["f.txt"] = Encoding.ASCII.GetBytes("abc");
        outputFiles.ExistingContent["o3"] = Encoding.ASCII.GetBytes("xyz");

        int exitCode = await RunAsync(
            ["-C", "-", "-T", "f.txt", "-o", "o3", UploadUrl],
            handler: RecordingProtocolHandler.WritingPath("http"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("** Resuming transfer from byte position -1" + NewLine + Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAt0WithUpload_WritesTheMeterWithoutTheResumingLine()
    {
        outputFiles.ExistingContent["f.txt"] = Encoding.ASCII.GetBytes("abc");

        await RunAsync(["-C", "0", "-T", "f.txt", UploadUrl], handler: RecordingProtocolHandler.WritingPath("http"));

        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAt0_WritesTheMeterWithoutTheResumingLine()
    {
        await RunAsync(["-C", "0", "-o", "o1", SourceUrl]);

        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--silent")]
    [DataRow("--no-progress-meter")]
    public async Task RunAsync_ContinueAt5UnderOptionThatHidesTheMeter_WritesNeitherResumingLineNorMeter(string option)
    {
        outputFiles.ExistingContent["o2"] = Encoding.ASCII.GetBytes("01234");

        int exitCode = await RunAsync([option, "-C", "5", "-o", "o2", SourceUrl]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressMeterAfterNoProgressMeter_WritesTheMeter()
    {
        await RunAsync(["--no-progress-meter", "--progress-meter", "-o", "o1", SourceUrl]);

        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBar_WritesNoMeterLines()
    {
        outputFiles.ExistingContent["o2"] = Encoding.ASCII.GetBytes("01234");

        await RunAsync(["-#", "-C", "5", "-o", "o2", SourceUrl]);

        StringAssert.DoesNotMatch(StandardErrorText, new System.Text.RegularExpressions.Regex("% Total|Dload|Resuming"));
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputNotATerminal_WritesTheMeterAfterTheBody()
    {
        int exitCode = await RunAsync([SourceUrl]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputIsATerminal_WritesNoMeterForABodyOnStandardOutput()
    {
        await RunAsync([SourceUrl], standardOutputIsTerminal: true);

        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputIsATerminalWithOutputFile_WritesTheMeter()
    {
        await RunAsync(["-o", "o1", SourceUrl], standardOutputIsTerminal: true);

        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TwoUrls_WritesOneMeterForEach()
    {
        await RunAsync([SourceUrl, SourceUrl, "-o", "o1", "-o", "o2"]);

        Assert.AreEqual(Meter + Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransfer_WritesOnlyItsErrorLine()
    {
        RecordingProtocolHandler missingSource =
            RecordingProtocolHandler.Failing("file", CurlExitCode.FileCouldntReadFile, "Could not open file /source.txt");

        int exitCode = await RunAsync(["-o", "o1", SourceUrl], handler: missingSource);

        Assert.AreEqual(37, exitCode);
        Assert.AreEqual("curl: (37) Could not open file /source.txt" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RunnerThatDoesNotWriteTheMeter_WritesNothing()
    {
        await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([fileHandler])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(["-o", "o1", SourceUrl]);

        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    private Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        bool standardOutputIsTerminal = false,
        IProtocolHandler? handler = null) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler ?? fileHandler])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: true,
                standardOutputIsTerminal: standardOutputIsTerminal)
            .RunAsync(arguments);
}
