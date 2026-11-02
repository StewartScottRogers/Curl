using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Engine;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>--extended-help</c>, that the bare <c>--extensions</c> and the former <c>--extensions-help</c> are unknown, and <c>--ai-help extensions</c> end to end (ADR-1008): <see cref="ExtensionListing"/>'s
/// lines and Markdown on standard output, with extensions on and with <c>CURL_EXTENSIONS=0</c>, no transfer and exit 0.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerExtensionsTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

    [TestMethod]
    [DataRow(null)]
    [DataRow("0")]
    public async Task RunAsync_ExtendedHelp_PrintsTheListingAndTransfersNothing(string? switchValue)
    {
        Func<string, string?> environment = name => name == "CURL_EXTENSIONS" ? switchValue : null;

        int exitCode = await RunAsync(["--extended-help", "file:///x"], environment);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Concat(ExtensionListing.Lines(environment).Select(line => line + Environment.NewLine)), StandardOutputText());
        Assert.AreEqual(0, standardError.Length);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_ExtendedHelpWithExtensionsOff_SaysTheyAreOff()
    {
        int exitCode = await RunAsync(["--extended-help"], name => name == "CURL_EXTENSIONS" ? "0" : null);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(ExtensionListing.OffLine + Environment.NewLine, StandardOutputText());
    }

    [TestMethod]
    [DataRow("--extensions")]
    [DataRow("--extensions-help")]
    public async Task RunAsync_FormerExtensionsOptionName_FailsAsAnUnknownOption(string formerName)
    {
        int exitCode = await RunAsync([formerName, "file:///x"], _ => null);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
        StringAssert.Contains(Encoding.UTF8.GetString(standardError.ToArray()), formerName);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    [DataRow("extensions", null)]
    [DataRow("EXTENSIONS", "0")]
    public async Task RunAsync_AiHelpExtensions_PrintsTheExtensionsMarkdown(string subject, string? switchValue)
    {
        Func<string, string?> environment = name => name == "CURL_EXTENSIONS" ? switchValue : null;

        int exitCode = await RunAsync(["--ai-help", subject], environment);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(ExtensionListing.Markdown(environment), StandardOutputText());
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_AiHelpIndex_ListsTheExtensionsSubject()
    {
        await RunAsync(["--ai-help"], _ => null);

        StringAssert.Contains(StandardOutputText(), "Run `curl --ai-help extensions`");
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, Func<string, string?> environment) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([file])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                terminalColumns: TerminalColumns.Default,
                readEnvironmentVariable: environment)
            .RunAsync(arguments);

    private string StandardOutputText() => Encoding.UTF8.GetString(standardOutput.ToArray());
}
