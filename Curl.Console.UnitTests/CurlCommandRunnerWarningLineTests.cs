using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins where the parser's warning lines go against curl 8.21.0, measured on Windows on
/// 2026-09-26: on standard error, before anything else, on accepted and refused command
/// lines alike, and not at all when <c>-s</c> came before the option that raised them.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerWarningLineTests
{
    private const string FlagLikeFileNameWarning = "Warning: The filename argument '-x' looks like a flag.";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem fileSystem = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_FlagLikeOutputThenUnknownOption_PrintsTheWarningBeforeTheRefusal()
    {
        int exitCode = await RunAsync(["-o", "-s", "--bogus"]);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "Warning: The filename argument '-s' looks like a flag." + NewLine
            + "curl: option --bogus: is unknown" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_FlagLikeOutputOnAnAcceptedCommandLine_PrintsTheWarningBeforeTheTransfer()
    {
        RecordingProtocolHandler file = new("file", async context =>
        {
            Assert.AreEqual(FlagLikeFileNameWarning + NewLine, StandardErrorText);
            await context.Output.WriteAsync(new byte[] { 1 });

            return TransferResult.Success(1);
        });

        int exitCode = await RunAsync(["-o", "-x", "file:///C:/Windows/win.ini"], file);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FlagLikeFileNameWarning + NewLine, StandardErrorText);
        Assert.HasCount(1, file.Contexts);
        Assert.HasCount(1, fileSystem.Written["-x"].ToArray());
    }

    [TestMethod]
    public async Task RunAsync_FlagLikeOutputAndAFailedTransfer_PrintsTheWarningBeforeTheErrorLine()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.Failing(
            "file", CurlExitCode.FileCouldntReadFile, "Could not open file Z:/nx");

        int exitCode = await RunAsync(["-o", "-x", "file:///Z:/nx"], file);

        Assert.AreEqual(37, exitCode);
        Assert.AreEqual(
            FlagLikeFileNameWarning + NewLine + "curl: (37) Could not open file Z:/nx" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("-sS")]
    public async Task RunAsync_SilentBeforeFlagLikeOutput_PrintsNoWarning(string silent)
    {
        int exitCode = await RunAsync(
            [silent, "-o", "-x", "file:///C:/Windows/win.ini"], RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentBeforeFlagLikeOutputThenUnknownOption_PrintsOnlyTheRefusal()
    {
        int exitCode = await RunAsync(["-s", "-o", "-x", "--bogus"]);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: option --bogus: is unknown" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentAfterFlagLikeOutput_StillPrintsTheWarning()
    {
        int exitCode = await RunAsync(
            ["-o", "-x", "-s", "file:///C:/Windows/win.ini"], RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FlagLikeFileNameWarning + NewLine, StandardErrorText);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers) =>
        new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher(handlers)), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows: false)
            .RunAsync(arguments);
}
