using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public async Task RunAsync_Help_PrintsTheUsagePageAndTransfersNothing()
    {
        int exitCode = await RunAsync(["-h"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Joined(CurlHelpText.Lines(null, CurlHelpText.DefaultColumns)), StandardOutputText());
        Assert.AreEqual(0, standardError.Length);
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

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Joined(CurlHelpText.Lines(subject, columns)), StandardOutputText());
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_HelpWithAnOption_PrintsThatOptionsManualSection()
    {
        int exitCode = await RunAsync(["--help", "-v"]);

        Assert.IsTrue(CurlOptionManualSection.TryGetLines("-v", out IReadOnlyList<string> lines));
        Assert.AreEqual(0, exitCode);
        Assert.IsNotEmpty(lines);
        Assert.AreEqual(Joined(lines), StandardOutputText());
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_HelpWithAnUnknownOption_WritesTheIncorrectOptionNameLineAndExitsZero()
    {
        int exitCode = await RunAsync(["--help", "--bogus"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(
            "Incorrect option name to show help for, see curl -h" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    [DataRow("-M")]
    [DataRow("--manual")]
    public async Task RunAsync_Manual_PrintsTheManualAndTransfersNothing(string option)
    {
        int exitCode = await RunAsync([option, "file:///dir/x"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Joined(CurlManual.Lines()), StandardOutputText());
        Assert.AreEqual(0, standardError.Length);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_VersionBeforeManual_PrintsTheVersionOnly()
    {
        int exitCode = await RunAsync(["-V", "-M"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Joined(CurlVersionText.Lines(false, OperatingSystem.IsMacOS())), StandardOutputText());
        Assert.AreEqual(0, standardError.Length);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, int columns = TerminalColumns.Default) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([file])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                standardInput,
                runsOnWindows: false,
                terminalColumns: columns)
            .RunAsync(arguments);

    private string StandardOutputText() => Encoding.UTF8.GetString(standardOutput.ToArray());

    private static string Joined(IEnumerable<string> lines) =>
        string.Concat(lines.Select(line => line + Environment.NewLine));
}
