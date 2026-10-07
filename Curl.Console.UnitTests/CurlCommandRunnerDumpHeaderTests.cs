using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins where the runner sends each transfer's <c>-D</c> / <c>--dump-header</c> lines, against
/// curl 8.21.0 measured on Windows on 2026-09-26 with a ten-byte <c>file://</c> source.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerDumpHeaderTests
{
    private const string SourceUrl = "file:///source.txt";

    private const string HeaderLines = "Content-Length: 10\r\nAccept-ranges: bytes\r\n\r\n";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string CannotOpenLines =
        "curl: Failed to open ro.txt" + NewLine
        + "curl: (23) Failed writing received data to disk/application" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly FileProtocolHandler fileHandler =
        new(new InMemoryFileSystem { ReadContent = Encoding.ASCII.GetBytes("0123456789") });

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_DumpHeaderToStandardOutput_WritesTheHeaderLinesToStandardOutput()
    {
        int exitCode = await RunAsync(["-D", "-", "-o", "body.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", Lf(HeaderLines), Lf(StandardOutputText));
        Diagnostics.Diff("body.txt", "0123456789", WrittenText("body.txt"));
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(HeaderLines, StandardOutputText);
        Assert.AreEqual("0123456789", WrittenText("body.txt"));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToStandardOutputWithoutOutputFile_WritesHeadersThenBody()
    {
        int exitCode = await RunAsync(["-D", "-", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", Lf(HeaderLines + "0123456789"), Lf(StandardOutputText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(HeaderLines + "0123456789", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFile_WritesTheHeaderLinesToTheFile()
    {
        int exitCode = await RunAsync(["-D", "hd.txt", "-o", "body.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("hd.txt", Lf(HeaderLines), Lf(WrittenText("hd.txt")));
        Diagnostics.Diff("body.txt", "0123456789", WrittenText("body.txt"));
        Diagnostics.Assert("standard output length", 0L, standardOutput.Length);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(HeaderLines, WrittenText("hd.txt"));
        Assert.AreEqual("0123456789", WrittenText("body.txt"));
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFile_CreatesItWithTheOutputFileMode()
    {
        int exitCode = await RunAsync(["-D", "hd.txt", "-o", "body.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "create modes",
            string.Join(",", DeferredOutputFileStream.CreateMode, DeferredOutputFileStream.CreateMode),
            string.Join(",", outputFiles.CreateModes));
        CollectionAssert.AreEqual(
            new[] { DeferredOutputFileStream.CreateMode, DeferredOutputFileStream.CreateMode },
            outputFiles.CreateModes);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFileForTwoUrls_TruncatesForTheFirstAndAppendsTheSecond()
    {
        outputFiles.ExistingContent["hd.txt"] = Encoding.ASCII.GetBytes("stale");
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync(["-D", "hd.txt", SourceUrl, SourceUrl], file);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "write modes",
            string.Join(",", FileWriteMode.Truncate, FileWriteMode.Append),
            string.Join(",", outputFiles.WriteModes));
        Diagnostics.Assert("transfer contexts", 2, file.Contexts.Count);
        Diagnostics.Assert("first transfer has header output", true, file.Contexts[0].HeaderOutput is not null);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { FileWriteMode.Truncate, FileWriteMode.Append }, outputFiles.WriteModes);
        Assert.HasCount(2, file.Contexts);
        Assert.IsNotNull(file.Contexts[0].HeaderOutput);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFileOnWindows_OpensTheNameUnsanitized()
    {
        int exitCode = await RunAsync(["-D", "h?d.txt", SourceUrl], fileHandler, runsOnWindows: true);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("h?d.txt", Lf(HeaderLines), Lf(WrittenText("h?d.txt")));
        Assert.AreEqual(HeaderLines, WrittenText("h?d.txt"));
    }

    [TestMethod]
    public async Task RunAsync_WithoutDumpHeader_GivesTheTransferNoHeaderOutput()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync([SourceUrl], file);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("header output is null", true, file.Contexts[0].HeaderOutput is null);
        Assert.IsNull(file.Contexts[0].HeaderOutput);
    }

    [TestMethod]
    [DataRow(new[] { "-D", "ro.txt", "-o", "body.txt", SourceUrl })]
    [DataRow(new[] { "-sS", "-D", "ro.txt", "-o", "body.txt", SourceUrl })]
    public async Task RunAsync_DumpHeaderFileCannotBeOpened_PrintsFailedToOpenAndExits23(string[] arguments)
    {
        outputFiles.UnwritablePaths.Add("ro.txt");
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync(arguments, file);

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff("stderr", Lf(CannotOpenLines), Lf(StandardErrorText));
        Diagnostics.Assert("transfer contexts", 0, file.Contexts.Count);
        Diagnostics.Assert("body.txt written", false, outputFiles.Written.ContainsKey("body.txt"));
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(CannotOpenLines, StandardErrorText);
        Assert.IsEmpty(file.Contexts);
        Assert.IsFalse(outputFiles.Written.ContainsKey("body.txt"));
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderFileCannotBeOpenedUnderSilent_PrintsNothing()
    {
        outputFiles.UnwritablePaths.Add("ro.txt");

        int exitCode = await RunAsync(["-s", "-D", "ro.txt", "-o", "body.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderFileCannotBeOpened_StopsBeforeTheRemainingUrls()
    {
        outputFiles.UnwritablePaths.Add("ro.txt");
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync(["-D", "ro.txt", SourceUrl, SourceUrl], file);

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff("stderr", Lf(CannotOpenLines), Lf(StandardErrorText));
        Diagnostics.Assert("transfer contexts", 0, file.Contexts.Count);
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(CannotOpenLines, StandardErrorText);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFailingStandardOutput_PrintsFailedWritingHeadersThenTheWriteError()
    {
        FailingWriteStream closedStandardOutput = new();

        int exitCode = await RunAsync(["-sS", "-D", "-", "-o", "body.txt", SourceUrl], fileHandler, standardOutput: closedStandardOutput);

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: Failed writing headers to -\ncurl: (23) client returned ERROR on write of 20 bytes\n",
            Lf(StandardErrorText));
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(
            "curl: Failed writing headers to -" + NewLine
            + "curl: (23) client returned ERROR on write of 20 bytes" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFileWhoseWriteFails_PrintsFailedWritingHeadersToTheFileAsGiven()
    {
        outputFiles.WriteFailingPaths.Add("CONIN$");

        int exitCode = await RunAsync(["-sS", "-D", "CONIN$", "-o", "body.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: Failed writing headers to CONIN$\ncurl: (23) client returned ERROR on write of 20 bytes\n",
            Lf(StandardErrorText));
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(
            "curl: Failed writing headers to CONIN$" + NewLine
            + "curl: (23) client returned ERROR on write of 20 bytes" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToStandardOutputWhoseFlushFails_PrintsFailedWritingHeaders()
    {
        FailingWriteStream closedStandardOutput = new() { WritesToFail = 0, FailsFlush = true };

        int exitCode = await RunAsync(["-D", "-", "-o", "body.txt", SourceUrl], fileHandler, standardOutput: closedStandardOutput);

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Assert(
            "stderr starts with the failed-writing line",
            true,
            Lf(StandardErrorText).StartsWith("curl: Failed writing headers to -\n", StringComparison.Ordinal));
        Assert.AreEqual(23, exitCode);
        Assert.StartsWith("curl: Failed writing headers to -" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFailingStandardOutputUnderVerbose_PrintsFailedWritingHeadersBeforeTheVerboseLine()
    {
        FailingWriteStream closedStandardOutput = new();

        int exitCode = await RunAsync(["-v", "-D", "-", "-o", "body.txt", SourceUrl], fileHandler, standardOutput: closedStandardOutput);

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: Failed writing headers to -\n* client returned ERROR on write of 20 bytes\n* closing connection #0\ncurl: (23) client returned ERROR on write of 20 bytes\n",
            Lf(StandardErrorText));
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(
            "curl: Failed writing headers to -" + NewLine
            + "* client returned ERROR on write of 20 bytes" + "\n"
            + "* closing connection #0" + "\n"
            + "curl: (23) client returned ERROR on write of 20 bytes" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderWriteFailsUnderSilent_PrintsNothingAndExits23()
    {
        FailingWriteStream closedStandardOutput = new();

        int exitCode = await RunAsync(["-s", "-D", "-", "-o", "body.txt", SourceUrl], fileHandler, standardOutput: closedStandardOutput);

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private string WrittenText(string path) => Encoding.ASCII.GetString(outputFiles.Written[path].ToArray());

    private async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        IProtocolHandler handler,
        bool runsOnWindows = false,
        Stream? standardOutput = null)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput ?? this.standardOutput, standardError, new MemoryStream(), runsOnWindows)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", this.standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }
}
