using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

namespace Curl.Console;

/// <summary>
/// Pins where the runner sends each transfer's <c>-D</c> / <c>--dump-header</c> lines, against
/// curl 8.21.0 measured on Windows on 2026-09-26 with a ten-byte <c>file://</c> source.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerDumpHeaderTests
{
    private const string SourceUrl = "file:///C:/source.txt";

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

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_DumpHeaderToStandardOutput_WritesTheHeaderLinesToStandardOutput()
    {
        int exitCode = await RunAsync(["-D", "-", "-o", "body.txt", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(HeaderLines, StandardOutputText);
        Assert.AreEqual("0123456789", WrittenText("body.txt"));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToStandardOutputWithoutOutputFile_WritesHeadersThenBody()
    {
        int exitCode = await RunAsync(["-D", "-", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(HeaderLines + "0123456789", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFile_WritesTheHeaderLinesToTheFile()
    {
        int exitCode = await RunAsync(["-D", "hd.txt", "-o", "body.txt", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(HeaderLines, WrittenText("hd.txt"));
        Assert.AreEqual("0123456789", WrittenText("body.txt"));
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFile_CreatesItWithTheOutputFileMode()
    {
        await RunAsync(["-D", "hd.txt", "-o", "body.txt", SourceUrl], fileHandler);

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

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { FileWriteMode.Truncate, FileWriteMode.Append }, outputFiles.WriteModes);
        Assert.HasCount(2, file.Contexts);
        Assert.IsNotNull(file.Contexts[0].HeaderOutput);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderToFileOnWindows_OpensTheNameUnsanitized()
    {
        await RunAsync(["-D", "h?d.txt", SourceUrl], fileHandler, runsOnWindows: true);

        Assert.AreEqual(HeaderLines, WrittenText("h?d.txt"));
    }

    [TestMethod]
    public async Task RunAsync_WithoutDumpHeader_GivesTheTransferNoHeaderOutput()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync([SourceUrl], file);

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

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderFileCannotBeOpened_StopsBeforeTheRemainingUrls()
    {
        outputFiles.UnwritablePaths.Add("ro.txt");
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync(["-D", "ro.txt", SourceUrl, SourceUrl], file);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(CannotOpenLines, StandardErrorText);
        Assert.IsEmpty(file.Contexts);
    }

    private string WrittenText(string path) => Encoding.ASCII.GetString(outputFiles.Written[path].ToArray());

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler, bool runsOnWindows = false) =>
        new CurlCommandRunner(_ => new ProtocolDispatcher([handler]), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows)
            .RunAsync(arguments);
}
