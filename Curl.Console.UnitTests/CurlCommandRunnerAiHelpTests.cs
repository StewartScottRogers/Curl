using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>--ai-help [subject]</c> end to end (BL-911, ADR-0223): <see cref="CurlAiHelpText"/>'s Markdown
/// on standard output with its <c>\n</c> line ends on every platform, no transfer and exit 0; for a subject
/// naming no category, the page <c>--help</c> prints for one, with the platform newline, and exit 0.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerAiHelpTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

    [TestMethod]
    [DataRow(new[] { "--ai-help" }, null)]
    [DataRow(new[] { "--ai-help", "tls" }, "tls")]
    [DataRow(new[] { "--ai-help=all", "file:///x" }, "all")]
    public async Task RunAsync_AiHelp_PrintsTheMarkdownAndTransfersNothing(string[] arguments, string? subject)
    {
        int exitCode = await RunAsync(arguments);

        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown(subject, out string markdown));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(markdown, StandardOutputText());
        Assert.AreEqual(0, standardError.Length);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_AiHelpWithAnUnknownCategory_PrintsWhatHelpPrintsForOne()
    {
        int exitCode = await RunAsync(["--ai-help", "bogus"]);
        string aiHelpOutput = StandardOutputText();
        standardOutput.SetLength(0);
        int helpExitCode = await RunAsync(["--help", "bogus"]);

        Assert.AreEqual(helpExitCode, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(StandardOutputText(), aiHelpOutput);
        Assert.AreEqual(0, standardError.Length);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([file])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                standardInput,
                runsOnWindows: false,
                terminalColumns: TerminalColumns.Default)
            .RunAsync(arguments);

    private string StandardOutputText() => Encoding.UTF8.GetString(standardOutput.ToArray());
}
