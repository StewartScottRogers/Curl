using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Arguments;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins an extension's argument expander end to end through the runner (ADR-1040): its option and value replaced by
/// the expander's arguments before the parse, a failure ending the run before any transfer, and nothing expanded
/// under <c>CURL_EXTENSIONS=0</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerArgumentExpansionTests
{
    private static readonly ExtensionOption RequestFile = new("test-request-file", ExtensionOptionArgument.Value, "Test requests file");

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly InMemoryDataFileReader dataFiles = new();
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
    private readonly List<CommandLineOptions> groups = [];

    [TestMethod]
    public async Task RunAsync_ExpandersOption_IsParsedAsTheExpandersArguments()
    {
        dataFiles.Files["requests.http"] = Encoding.UTF8.GetBytes("-X POST -H A:%20b --data-binary x http://h/ --next http://h/2");

        int exitCode = await RunAsync(["-s", "--test-request-file", "requests.http"], _ => null);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
        Assert.HasCount(2, groups);
        CollectionAssert.AreEqual(new[] { "http://h/" }, groups[0].Urls.ToArray());
        Assert.AreEqual("POST", groups[0].RequestMethod);
        CollectionAssert.AreEqual(new[] { "A: b" }, groups[0].Headers.ToArray());
        Assert.AreEqual("x", Encoding.UTF8.GetString(groups[0].PostData!.Value.Span));
        CollectionAssert.AreEqual(new[] { "http://h/2" }, groups[1].Urls.ToArray());
        Assert.IsNull(groups[1].RequestMethod);
        Assert.IsEmpty(groups[1].Headers);
        Assert.IsNull(groups[1].PostData);
        Assert.HasCount(2, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_ExpandersFailure_EndsTheRunWithItsMessageAndExitCodeBeforeAnyTransfer()
    {
        int exitCode = await RunAsync(["--test-request-file", "missing.http", "http://h/"], _ => null);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("curl: cannot read missing.http" + Environment.NewLine, Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
        Assert.IsEmpty(groups);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_ExpandersOptionWithExtensionsOff_IsRefusedAsAnUnknownOption()
    {
        dataFiles.Files["requests.http"] = Encoding.UTF8.GetBytes("http://h/");

        int exitCode = await RunAsync(["--test-request-file", "requests.http"], name => name == "CURL_EXTENSIONS" ? "0" : null);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: option --test-request-file: is unknown" + Environment.NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.IsEmpty(groups);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_WithoutAnExpandersOption_ParsesTheCommandLineUnchanged()
    {
        int exitCode = await RunAsync(["-s", "-H", "A: b", "http://h/"], _ => null);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, groups);
        CollectionAssert.AreEqual(new[] { "http://h/" }, groups[0].Urls.ToArray());
        CollectionAssert.AreEqual(new[] { "A: b" }, groups[0].Headers.ToArray());
        Assert.IsEmpty(dataFiles.PathsRead);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, Func<string, string?> environment) =>
        new CurlCommandRunner(
                CreateTransferDispatch,
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                terminalColumns: TerminalColumns.Default,
                configFileReader: dataFiles,
                readEnvironmentVariable: environment,
                extensionOptions: [RequestFile],
                argumentExpanders: [new SpaceSeparatedFileExpander()])
            .RunAsync(arguments);

    private TransferDispatch CreateTransferDispatch(CommandLineOptions options)
    {
        groups.Add(options);
        return new TransferDispatch(new ProtocolDispatcher([http]));
    }

    /// <summary>Expands a file of space-separated, percent-encoded arguments, failing with exit 26 when it cannot be read.</summary>
    private sealed class SpaceSeparatedFileExpander : IArgumentExpander
    {
        public ExtensionOption Option => RequestFile;

        public IReadOnlyList<string> ArgumentsFor(string value, ArgumentExpansionContext context)
        {
            byte[] contents = context.ReadFile(value) ?? throw new ArgumentExpansionException("cannot read " + value, CurlExitCode.ReadError);
            return [.. Encoding.UTF8.GetString(contents).Split(' ').Select(Uri.UnescapeDataString)];
        }
    }
}
