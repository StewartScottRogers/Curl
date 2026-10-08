using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_Version_PrintsTheVersionLinesAndTransfersNothing(bool runsOnWindows)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");
        Diagnostics.Arrange("handler behaviour", "file writes the path it was given");

        int exitCode = await RunAsync(
            new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([file])), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows),
            ["-V", "file:///C:/Windows/win.ini"],
            runsOnWindows);

        string expected = string.Concat(
            CurlVersionText.Lines(runsOnWindows, OperatingSystem.IsMacOS()).Select(line => line + Environment.NewLine));
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert(
            "stdout equals the version lines for this system, each with the platform newline",
            true,
            expected == Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(expected, Encoding.UTF8.GetString(standardOutput.ToArray()));
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
        Diagnostics.Assert("handler call count", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_VersionOnWindows_WritesTheMingwLineFirst()
    {
        int exitCode = await RunAsync(
            new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([])), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows: true),
            ["--version"],
            runsOnWindows: true);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string expectedStart = "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel" + Environment.NewLine + "Release-Date: 2026-06-24";
        string actual = Encoding.UTF8.GetString(standardOutput.ToArray());
        Diagnostics.Diff(
            "stdout start",
            Normalized(expectedStart),
            Normalized(actual)[..Math.Min(Normalized(actual).Length, Normalized(expectedStart).Length)]);
        StringAssert.StartsWith(
            Encoding.UTF8.GetString(standardOutput.ToArray()),
            "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel" + Environment.NewLine + "Release-Date: 2026-06-24");
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(CurlCommandRunner runner, IReadOnlyList<string> arguments, bool runsOnWindows)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("runs on Windows", runsOnWindows);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await runner.RunAsync(arguments);
        }

        // The version text names the operating system, so only OS-independent facts are printed.
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout written", standardOutput.Length > 0);
        Diagnostics.Act("stderr length", standardError.Length);
        return exitCode;
    }
}
