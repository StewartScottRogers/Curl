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
/// Pins a trailing argument expander end to end through the runner (ADR-1051): its companion option and the
/// positional arguments it claims taken from its <c>--next</c> group before the parse, the rest of the command line
/// parsed as before, and nothing taken under <c>CURL_EXTENSIONS=0</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTrailingArgumentExpansionTests
{
    private static readonly ExtensionOption Spec = new("x-spec", ExtensionOptionArgument.Value, "Test spec");

    private static readonly ExtensionOption Operation = new("x-op", ExtensionOptionArgument.Value, "Test operation");

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
    private readonly List<CommandLineOptions> groups = [];
    private readonly SpecExpander expander = new();

    [TestMethod]
    public async Task RunAsync_ExpandersCompanionAndPositionals_AreHandedToTheExpanderAndTheRestParsedAsBefore()
    {
        int exitCode = await RunAsync(["-s", "--x-spec", "s", "--x-op", "o", "a=1", "b=2", "http://h/"], _ => null);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
        ClaimedArguments claimed = expander.Claimed.Single();
        Assert.AreEqual("s", claimed.Value);
        CollectionAssert.AreEqual(new[] { new CompanionOptionValue(Operation, "o") }, claimed.CompanionValues.ToArray());
        CollectionAssert.AreEqual(new[] { "a=1", "b=2" }, claimed.PositionalArguments.ToArray());
        Assert.HasCount(1, groups);
        CollectionAssert.AreEqual(new[] { "http://h/" }, groups[0].Urls.ToArray());
        CollectionAssert.AreEqual(new[] { "X-Spec: s o a=1 b=2" }, groups[0].Headers.ToArray());
        Assert.HasCount(1, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_PositionalsInAnotherNextGroup_StayUrls()
    {
        int exitCode = await RunAsync(["-s", "--x-spec", "s", "http://h/", "--next", "h2?a=1"], _ => null);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(expander.Claimed.Single().PositionalArguments);
        Assert.HasCount(2, groups);
        CollectionAssert.AreEqual(new[] { "http://h/" }, groups[0].Urls.ToArray());
        CollectionAssert.AreEqual(new[] { "h2?a=1" }, groups[1].Urls.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_WithoutTheExpandersOption_PositionalsStayUrls()
    {
        int exitCode = await RunAsync(["-s", "h2?a=1", "http://h/"], _ => null);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(expander.Claimed);
        Assert.HasCount(1, groups);
        CollectionAssert.AreEqual(new[] { "h2?a=1", "http://h/" }, groups[0].Urls.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_ExpandersOptionWithExtensionsOff_IsRefusedAsAnUnknownOption()
    {
        int exitCode = await RunAsync(["--x-spec", "s", "http://h/"], name => name == "CURL_EXTENSIONS" ? "0" : null);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: option --x-spec: is unknown" + Environment.NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.IsEmpty(expander.Claimed);
        Assert.IsEmpty(groups);
    }

    [TestMethod]
    public async Task RunAsync_CompanionWithExtensionsOff_IsRefusedAsAnUnknownOption()
    {
        int exitCode = await RunAsync(["--x-op", "o", "http://h/"], name => name == "CURL_EXTENSIONS" ? "0" : null);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: option --x-op: is unknown" + Environment.NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.IsEmpty(groups);
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
                readEnvironmentVariable: environment,
                extensionOptions: [Spec, Operation],
                argumentExpanders: [expander])
            .RunAsync(arguments);

    private TransferDispatch CreateTransferDispatch(CommandLineOptions options)
    {
        groups.Add(options);
        return new TransferDispatch(new ProtocolDispatcher([http]));
    }

    /// <summary>Records what it takes and expands to one header holding the value, companions and positionals.</summary>
    private sealed class SpecExpander : ITrailingArgumentExpander
    {
        public List<ClaimedArguments> Claimed { get; } = [];

        public ExtensionOption Option => Spec;

        public IReadOnlyList<ExtensionOption> CompanionOptions => [Operation];

        public bool ClaimsPositionalArgument(string argument) => argument.Contains('=', StringComparison.Ordinal);

        public IReadOnlyList<string> ArgumentsFor(ClaimedArguments claimed, ArgumentExpansionContext context)
        {
            Claimed.Add(claimed);
            string[] parts = [claimed.Value, .. claimed.CompanionValues.Select(companion => companion.Value), .. claimed.PositionalArguments];
            return ["-H", "X-Spec: " + string.Join(' ', parts)];
        }
    }
}
