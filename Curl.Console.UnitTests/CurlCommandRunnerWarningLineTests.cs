using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_FlagLikeOutputThenUnknownOption_PrintsTheWarningBeforeTheRefusal()
    {
        int exitCode = await RunAsync(["-o", "-s", "--bogus"]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: The filename argument '-s' looks like a flag.\n"
            + "curl: option --bogus: is unknown\n"
            + "curl: try 'curl --help' or 'curl --manual' for more information\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "Warning: The filename argument '-s' looks like a flag." + NewLine
            + "curl: option --bogus: is unknown" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
        Diagnostics.Assert("stdout length", 0L, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_FlagLikeOutputOnAnAcceptedCommandLine_PrintsTheWarningBeforeTheTransfer()
    {
        RecordingProtocolHandler file = new("file", async context =>
        {
            Diagnostics.Diff("stderr when the handler runs", FlagLikeFileNameWarning + "\n", Normalized(StandardErrorText));
            Assert.AreEqual(FlagLikeFileNameWarning + NewLine, StandardErrorText);
            await context.Output.WriteAsync(new byte[] { 1 });

            return TransferResult.Success(1);
        });
        Diagnostics.Arrange("handler behaviour", "file checks stderr already holds the warning, then writes one byte 0x01");

        int exitCode = await RunAsync(["-o", "-x", "file:///Windows/win.ini"], file);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", FlagLikeFileNameWarning + "\n", Normalized(StandardErrorText));
        Assert.AreEqual(FlagLikeFileNameWarning + NewLine, StandardErrorText);
        Diagnostics.Assert("handler call count", 1, file.Contexts.Count);
        Assert.HasCount(1, file.Contexts);
        Diagnostics.Bytes("file -x", fileSystem.Written["-x"].ToArray());
        Diagnostics.Assert("bytes written to -x", 1, fileSystem.Written["-x"].ToArray().Length);
        Assert.HasCount(1, fileSystem.Written["-x"].ToArray());
    }

    [TestMethod]
    public async Task RunAsync_FlagLikeOutputAndAFailedTransfer_PrintsTheWarningBeforeTheErrorLine()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.Failing(
            "file", CurlExitCode.FileCouldntReadFile, "Could not open file /nx");
        Diagnostics.Arrange("handler behaviour", "file fails with exit 37, Could not open file /nx");

        int exitCode = await RunAsync(["-o", "-x", "file:///nx"], file);

        Diagnostics.Assert("exit code", 37, exitCode);
        Assert.AreEqual(37, exitCode);
        Diagnostics.Diff(
            "stderr",
            FlagLikeFileNameWarning + "\n" + "curl: (37) Could not open file /nx\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            FlagLikeFileNameWarning + NewLine + "curl: (37) Could not open file /nx" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("-sS")]
    public async Task RunAsync_SilentBeforeFlagLikeOutput_PrintsNoWarning(string silent)
    {
        int exitCode = await RunAsync(
            [silent, "-o", "-x", "file:///Windows/win.ini"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Normalized(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentBeforeFlagLikeOutputThenUnknownOption_PrintsOnlyTheRefusal()
    {
        int exitCode = await RunAsync(["-s", "-o", "-x", "--bogus"]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: option --bogus: is unknown\n"
            + "curl: try 'curl --help' or 'curl --manual' for more information\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "curl: option --bogus: is unknown" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentAfterFlagLikeOutput_StillPrintsTheWarning()
    {
        int exitCode = await RunAsync(
            ["-o", "-x", "-s", "file:///Windows/win.ini"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", FlagLikeFileNameWarning + "\n", Normalized(StandardErrorText));
        Assert.AreEqual(FlagLikeFileNameWarning + NewLine, StandardErrorText);
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange(
            "handler schemes",
            string.Join(", ", handlers.Select(handler => string.Join('/', handler.SupportedSchemes))));

        CurlCommandRunner runner = new(_ => new TransferDispatch(new ProtocolDispatcher(handlers)), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows: false);
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await runner.RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Act("stderr", Normalized(StandardErrorText));
        return exitCode;
    }
}
