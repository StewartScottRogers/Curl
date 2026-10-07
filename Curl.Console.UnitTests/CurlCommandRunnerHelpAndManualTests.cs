using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-h</c> / <c>--help [subject]</c> and <c>-M</c> / <c>--manual</c> end to end against curl
/// 8.21.0: <see cref="CurlHelpText" />'s, <see cref="CurlOptionManualSection" />'s or
/// <see cref="CurlManual" />'s lines on standard output, each followed by the platform newline, no
/// transfer, and exit 0.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHelpAndManualTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_Help_PrintsTheUsagePageAndTransfersNothing()
    {
        int exitCode = await RunAsync(["-h"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", Lf(Joined(CurlHelpText.Lines(null, CurlHelpText.DefaultColumns))), Lf(StandardOutputText()));
        Assert.AreEqual(Joined(CurlHelpText.Lines(null, CurlHelpText.DefaultColumns)), StandardOutputText());
        Diagnostics.Assert("stderr length", 0, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
        Diagnostics.Assert("transfers", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    [DataRow("all", 79)]
    [DataRow("http", 79)]
    [DataRow("all", 40)]
    [DataRow("HTTP", 200)]
    public async Task RunAsync_HelpWithASubject_PrintsThatPageAtTheTerminalWidth(string subject, int columns)
    {
        int exitCode = await RunAsync(["--help", subject], columns);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", Lf(Joined(CurlHelpText.Lines(subject, columns))), Lf(StandardOutputText()));
        Assert.AreEqual(Joined(CurlHelpText.Lines(subject, columns)), StandardOutputText());
        Diagnostics.Assert("stderr length", 0, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_HelpWithAnOption_PrintsThatOptionsManualSection()
    {
        int exitCode = await RunAsync(["--help", "-v"]);

        Assert.IsTrue(CurlOptionManualSection.TryGetLines("-v", out IReadOnlyList<string> lines));
        Diagnostics.Assert("manual section lines found", true, lines.Count > 0);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.IsNotEmpty(lines);
        Diagnostics.Diff("stdout", Lf(Joined(lines)), Lf(StandardOutputText()));
        Assert.AreEqual(Joined(lines), StandardOutputText());
        Diagnostics.Assert("stderr length", 0, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_HelpWithAnUnknownOption_WritesTheIncorrectOptionNameLineAndExitsZero()
    {
        int exitCode = await RunAsync(["--help", "--bogus"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("stdout length", 0, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(
            "Incorrect option name to show help for, see curl -h" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.Assert("stderr", "Incorrect option name to show help for, see curl -h\n", Lf(Encoding.UTF8.GetString(standardError.ToArray())));
    }

    [TestMethod]
    [DataRow("-M")]
    [DataRow("--manual")]
    public async Task RunAsync_Manual_PrintsTheManualAndTransfersNothing(string option)
    {
        int exitCode = await RunAsync([option, "file:///dir/x"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", Lf(Joined(CurlManual.Lines())), Lf(StandardOutputText()));
        Assert.AreEqual(Joined(CurlManual.Lines()), StandardOutputText());
        Diagnostics.Assert("stderr length", 0, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
        Diagnostics.Assert("transfers", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_VersionBeforeManual_PrintsTheVersionOnly()
    {
        int exitCode = await RunAsync(["-V", "-M"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", Lf(Joined(CurlVersionText.Lines(false, OperatingSystem.IsMacOS()))), Lf(StandardOutputText()));
        Assert.AreEqual(Joined(CurlVersionText.Lines(false, OperatingSystem.IsMacOS())), StandardOutputText());
        Diagnostics.Assert("stderr length", 0, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, int columns = TerminalColumns.Default)
    {
        Diagnostics.Arrange("command line", "curl " + string.Join(" ", arguments));
        Diagnostics.Arrange("terminal columns", columns);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([file])),
                    fileSystem,
                    fileSystem,
                    standardOutput,
                    standardError,
                    standardInput,
                    runsOnWindows: false,
                    terminalColumns: columns)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }

    private static string Lf(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace(Environment.NewLine, "\n", StringComparison.Ordinal);

    private string StandardOutputText() => Encoding.UTF8.GetString(standardOutput.ToArray());

    private static string Joined(IEnumerable<string> lines) =>
        string.Concat(lines.Select(line => line + Environment.NewLine));
}
