using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_OutputFile_WritesTheMeterHeaderLinesAndZeroStatusLine()
    {
        int exitCode = await RunAsync(["-o", "o1", SourceUrl]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(Meter), Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAt5_WritesTheResumingLineBeforeTheMeter()
    {
        outputFiles.ExistingContent["o2"] = Encoding.ASCII.GetBytes("01234");
        Diagnostics.Arrange("existing o2 content", "01234");

        int exitCode = await RunAsync(["-C", "5", "-o", "o2", SourceUrl]);

        string expectedStandardError = "** Resuming transfer from byte position 5" + NewLine + Meter;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Diagnostics.Diff("o2 content", "0123456789", Encoding.ASCII.GetString(outputFiles.Written["o2"].ToArray()));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(outputFiles.Written["o2"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtDash_NamesTheOutputFileSizeInTheResumingLine()
    {
        outputFiles.ExistingContent["o2"] = Encoding.ASCII.GetBytes("0123");
        Diagnostics.Arrange("existing o2 content", "0123");

        int exitCode = await RunAsync(["-C", "-", "-o", "o2", SourceUrl]);

        string expectedStandardError = "** Resuming transfer from byte position 4" + NewLine + Meter;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtDashWithUpload_WritesResumingFromMinusOneBeforeTheMeter()
    {
        outputFiles.ExistingContent["f.txt"] = Encoding.ASCII.GetBytes("abc");
        Diagnostics.Arrange("existing f.txt content", "abc");

        int exitCode = await RunAsync(["-C", "-", "-T", "f.txt", UploadUrl], handler: RecordingProtocolHandler.WritingPath("http"));

        string expectedStandardError = "** Resuming transfer from byte position -1" + NewLine + Meter;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtDashWithUploadAndExistingOutputFile_StillWritesResumingFromMinusOne()
    {
        outputFiles.ExistingContent["f.txt"] = Encoding.ASCII.GetBytes("abc");
        outputFiles.ExistingContent["o3"] = Encoding.ASCII.GetBytes("xyz");
        Diagnostics.Arrange("existing f.txt content", "abc");
        Diagnostics.Arrange("existing o3 content", "xyz");

        int exitCode = await RunAsync(
            ["-C", "-", "-T", "f.txt", "-o", "o3", UploadUrl],
            handler: RecordingProtocolHandler.WritingPath("http"));

        string expectedStandardError = "** Resuming transfer from byte position -1" + NewLine + Meter;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAt0WithUpload_WritesTheMeterWithoutTheResumingLine()
    {
        outputFiles.ExistingContent["f.txt"] = Encoding.ASCII.GetBytes("abc");
        Diagnostics.Arrange("existing f.txt content", "abc");

        int exitCode = await RunAsync(["-C", "0", "-T", "f.txt", UploadUrl], handler: RecordingProtocolHandler.WritingPath("http"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(Meter), Lf(StandardErrorText));
        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAt0_WritesTheMeterWithoutTheResumingLine()
    {
        int exitCode = await RunAsync(["-C", "0", "-o", "o1", SourceUrl]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(Meter), Lf(StandardErrorText));
        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--silent")]
    [DataRow("--no-progress-meter")]
    public async Task RunAsync_ContinueAt5UnderOptionThatHidesTheMeter_WritesNeitherResumingLineNorMeter(string option)
    {
        outputFiles.ExistingContent["o2"] = Encoding.ASCII.GetBytes("01234");
        Diagnostics.Arrange("existing o2 content", "01234");

        int exitCode = await RunAsync([option, "-C", "5", "-o", "o2", SourceUrl]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressMeterAfterNoProgressMeter_WritesTheMeter()
    {
        int exitCode = await RunAsync(["--no-progress-meter", "--progress-meter", "-o", "o1", SourceUrl]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(Meter), Lf(StandardErrorText));
        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ProgressBar_WritesNoMeterLines()
    {
        outputFiles.ExistingContent["o2"] = Encoding.ASCII.GetBytes("01234");
        Diagnostics.Arrange("existing o2 content", "01234");

        int exitCode = await RunAsync(["-#", "-C", "5", "-o", "o2", SourceUrl]);

        System.Text.RegularExpressions.Regex forbidden = new("% Total|Dload|Resuming");
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr matches meter or resuming text", false, forbidden.IsMatch(StandardErrorText));
        StringAssert.DoesNotMatch(StandardErrorText, forbidden);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputNotATerminal_WritesTheMeterAfterTheBody()
    {
        int exitCode = await RunAsync([SourceUrl]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "0123456789", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Diagnostics.Diff("stderr", Lf(Meter), Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputIsATerminal_WritesNoMeterForABodyOnStandardOutput()
    {
        int exitCode = await RunAsync([SourceUrl], standardOutputIsTerminal: true);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputIsATerminalWithOutputFile_WritesTheMeter()
    {
        int exitCode = await RunAsync(["-o", "o1", SourceUrl], standardOutputIsTerminal: true);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(Meter), Lf(StandardErrorText));
        Assert.AreEqual(Meter, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TwoUrls_WritesOneMeterForEach()
    {
        int exitCode = await RunAsync([SourceUrl, SourceUrl, "-o", "o1", "-o", "o2"]);

        string expectedStandardError = Meter + Meter;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransfer_WritesOnlyItsErrorLine()
    {
        RecordingProtocolHandler missingSource =
            RecordingProtocolHandler.Failing("file", CurlExitCode.FileCouldntReadFile, "Could not open file /source.txt");
        Diagnostics.Arrange("handler", "fails with exit 37: Could not open file /source.txt");

        int exitCode = await RunAsync(["-o", "o1", SourceUrl], handler: missingSource);

        string expectedStandardError = "curl: (37) Could not open file /source.txt" + NewLine;
        Diagnostics.Assert("exit code", 37, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(37, exitCode);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RunnerThatDoesNotWriteTheMeter_WritesNothing()
    {
        string[] arguments = ["-o", "o1", SourceUrl];
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("writes progress meter", false);
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([fileHandler])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        bool standardOutputIsTerminal = false,
        IProtocolHandler? handler = null)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("standard output is terminal", standardOutputIsTerminal);
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
                    writesProgressMeter: true,
                    standardOutputIsTerminal: standardOutputIsTerminal)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }
}
