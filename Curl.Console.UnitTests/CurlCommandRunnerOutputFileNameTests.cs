using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

namespace Curl.Console;

/// <summary>
/// Pins that the runner uses an <c>-o</c> name rewritten by
/// <see cref="WindowsOutputFileNameSanitizer" /> when its platform check reports Windows,
/// for the file, the <c>-C -</c> size and the messages that name it, and the name as typed
/// otherwise, against curl 8.21.0 measured on Windows on 2026-09-26.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerOutputFileNameTests
{
    private const string SourceUrl = "file:///source.txt";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly FileProtocolHandler fileHandler =
        new(new InMemoryFileSystem { ReadContent = Encoding.ASCII.GetBytes("0123456789") });

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_OnWindowsOutputFileOpenDenied_WarnsWithTheRewrittenName()
    {
        InMemoryFileSystem files = new() { UnwritableStatus = FileAccessStatus.AccessDenied };
        files.UnwritablePaths.Add("C:/x_y");

        int exitCode = await RunAsync(files, runsOnWindows: true, ["-o", "C:/x?y", SourceUrl]);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "Warning: Failed to open the file C:/x_y: Permission denied" + NewLine
            + "curl: (23) client returned ERROR on write of 10 bytes" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OnWindows_WritesTheRewrittenFile()
    {
        int exitCode = await RunAsync(outputFiles, runsOnWindows: true, ["-o", "a*b", SourceUrl]);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "a_b" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_NotOnWindows_WritesTheNameAsTyped()
    {
        int exitCode = await RunAsync(outputFiles, runsOnWindows: false, ["-o", "C:/x?y", SourceUrl]);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "C:/x?y" }, outputFiles.Written.Keys);
    }

    [TestMethod]
    public async Task RunAsync_OnWindowsContinueAtOutputSize_ResumesFromTheRewrittenFilesSize()
    {
        outputFiles.ExistingContent["a_b"] = Encoding.ASCII.GetBytes("XYZ");

        int exitCode = await RunAsync(outputFiles, runsOnWindows: true, ["-C", "-", "-o", "a?b", SourceUrl]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("XYZ3456789", Encoding.ASCII.GetString(outputFiles.Written["a_b"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_OnWindowsContinueAtToUnopenableOutputFile_NamesTheRewrittenFile()
    {
        outputFiles.UnwritablePaths.Add("sub_");

        int exitCode = await RunAsync(outputFiles, runsOnWindows: true, ["-C", "3", "-o", "sub?", SourceUrl]);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "curl: cannot open 'sub_'" + NewLine
            + "curl: (23) Failed writing received data to disk/application" + NewLine,
            StandardErrorText);
    }

    private Task<int> RunAsync(InMemoryFileSystem files, bool runsOnWindows, IReadOnlyList<string> arguments) =>
        new CurlCommandRunner(
            _ => new TransferDispatch(new ProtocolDispatcher([fileHandler])),
            files,
            files,
            new MemoryStream(),
            standardError,
            new MemoryStream(),
            runsOnWindows)
            .RunAsync(arguments);
}
