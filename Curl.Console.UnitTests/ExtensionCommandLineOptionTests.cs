using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins an extension's command-line option end to end through the runner (ADR-1006): parsed into its option
/// group and read back as the group's <see cref="ExtensionOptionValues"/>, and refused as curl refuses an
/// unknown option under <c>CURL_EXTENSIONS=0</c>.
/// </summary>
[TestClass]
public sealed class ExtensionCommandLineOptionTests
{
    private static readonly ExtensionOption CacheDirectory = new("test-cache-dir", ExtensionOptionArgument.Value, "Test cache directory");
    private static readonly ExtensionOption Flag = new("test-flag", ExtensionOptionArgument.None, "Test flag");
    private static readonly ExtensionOption NotGiven = new("test-not-given", ExtensionOptionArgument.None, "Test option not given");

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");
    private ExtensionOptionValues? transferValues;

    [TestMethod]
    public async Task RunAsync_ExtensionOptions_ReachTheTransferAsTheGroupsValues()
    {
        int exitCode = await RunAsync(["--test-cache-dir", "dir", "--test-flag", "file:///x"], _ => null);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
        Assert.IsNotNull(transferValues);
        Assert.AreEqual("dir", transferValues.ValueOf(CacheDirectory));
        Assert.AreEqual(string.Empty, transferValues.ValueOf(Flag));
        Assert.IsFalse(transferValues.IsGiven(NotGiven));
        Assert.HasCount(1, file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_ExtensionOptionWithExtensionsOff_IsRefusedAsAnUnknownOption()
    {
        int exitCode = await RunAsync(["--test-flag", "file:///x"], name => name == "CURL_EXTENSIONS" ? "0" : null);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: option --test-flag: is unknown" + Environment.NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
        Assert.IsNull(transferValues);
        Assert.IsEmpty(file.Contexts);
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
                extensionOptions: [CacheDirectory, Flag, NotGiven])
            .RunAsync(arguments);

    private TransferDispatch CreateTransferDispatch(CommandLineOptions options)
    {
        transferValues = CurlComposition.ExtensionOptionValuesOf(options);
        return new TransferDispatch(new ProtocolDispatcher([file]));
    }
}
