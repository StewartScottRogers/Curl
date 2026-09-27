using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>-V</c> / <c>--version</c> end to end against curl 8.21.0: <see cref="CurlVersionText" />'s
/// lines for the running system on standard output, each followed by the platform newline, nothing on
/// standard error, no transfer, and exit 0.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerVersionTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem fileSystem = new();

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_Version_PrintsTheVersionLinesAndTransfersNothing(bool runsOnWindows)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([file])), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows)
            .RunAsync(["-V", "file:///C:/Windows/win.ini"]);

        string expected = string.Concat(
            CurlVersionText.Lines(runsOnWindows, OperatingSystem.IsMacOS()).Select(line => line + Environment.NewLine));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_VersionOnWindows_WritesTheMingwLineFirst()
    {
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([])), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows: true)
            .RunAsync(["--version"]);

        Assert.AreEqual(0, exitCode);
        StringAssert.StartsWith(
            Encoding.UTF8.GetString(standardOutput.ToArray()),
            "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel" + Environment.NewLine + "Release-Date: 2026-06-24");
    }
}
