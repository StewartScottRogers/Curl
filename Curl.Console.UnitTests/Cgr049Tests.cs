using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// CGR-049 (BL-3578): a <c>help</c> line in a <c>-K</c> file prints its page on standard output before
/// anything else and the run carries on, as curl 8.21.0 does: with a URL the transfer runs and exits 0,
/// without one <c>curl: (2) no URL specified</c> follows the page, exit 2 (measured 2026-10-28).
/// </summary>
[TestClass]
public sealed class Cgr049Tests
{
    [TestMethod]
    public async Task RunAsync_HelpLineInConfigFileWithUrl_PrintsTheUsagePageThenTransfers()
    {
        (int exitCode, string standardOutput, _) = await RunAsync("help\n", ["-sS", "-K", "k.cfg", "http://127.0.0.1/hp03"]);

        Assert.AreEqual((int)CurlExitCode.Ok, exitCode);
        string usagePage = string.Join(Environment.NewLine, CurlHelpText.Lines(null, TerminalColumns.Default)) + Environment.NewLine;
        Assert.IsTrue(standardOutput.StartsWith(usagePage, StringComparison.Ordinal), standardOutput);
        Assert.IsTrue(standardOutput.Length > usagePage.Length, standardOutput);
    }

    [TestMethod]
    public async Task RunAsync_HelpLineInConfigFileWithoutUrl_PrintsTheUsagePageThenNoUrlSpecified()
    {
        (int exitCode, string standardOutput, string standardError) = await RunAsync("help\n", ["-sS", "-K", "k.cfg"]);

        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(string.Join(Environment.NewLine, CurlHelpText.Lines(null, TerminalColumns.Default)) + Environment.NewLine, standardOutput);
        StringAssert.Contains(standardError, "curl: (2) no URL specified");
    }

    [TestMethod]
    public async Task RunAsync_HelpAllLineInConfigFile_PrintsEveryOption()
    {
        (int exitCode, string standardOutput, _) = await RunAsync("help = all\n", ["-sS", "-K", "k.cfg", "http://127.0.0.1/hp03"]);

        Assert.AreEqual((int)CurlExitCode.Ok, exitCode);
        Assert.IsTrue(
            standardOutput.StartsWith(string.Join(Environment.NewLine, CurlHelpText.Lines("all", TerminalColumns.Default)) + Environment.NewLine, StringComparison.Ordinal),
            standardOutput);
    }

    [TestMethod]
    public async Task RunAsync_HelpOptionLineInConfigFile_PrintsThatOptionsManualSection()
    {
        (int exitCode, string standardOutput, _) = await RunAsync("help = --silent\n", ["-K", "k.cfg"]);

        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        StringAssert.Contains(standardOutput, "--silent");
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(string configFile, string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        InMemoryFileSystem fileSystem = new();
        InMemoryDataFileReader configFiles = new();
        configFiles.Files["k.cfg"] = Encoding.UTF8.GetBytes(configFile);
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http]), []),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                TerminalColumns.Default,
                configFileReader: configFiles)
            .RunAsync(arguments);

        return (exitCode, Encoding.UTF8.GetString(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()));
    }
}
