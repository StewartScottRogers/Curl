using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins what the runner prints for a real curl option Curl does not implement yet (ADR-0137): the
/// standard-error bytes <c>curl: option --&lt;name&gt;: the installed libcurl version does not support this</c>
/// and the try-help line, exit 2, no transfer, and the same with <c>-s</c> first, as the Windows system
/// curl 8.21.0 prints for <c>--http3</c> and <c>-s --http3</c> (measured 2026-09-28). The option is the
/// first name in <see cref="CurlOptionAliasTable"/> without a <see cref="CommandLineOptionTable"/> row, so
/// the test follows the set as it shrinks.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerUnimplementedOptionTests
{
    private const string Url = "file:///dir/x";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem fileSystem = new();

    private static string? FirstUnimplementedName =>
        CurlOptionAliasTable.Aliases
            .Select(alias => alias.Name)
            .FirstOrDefault(name => !CommandLineOptionTable.Rows.Any(row => row.LongName == name));

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer(bool silent)
    {
        string? name = FirstUnimplementedName;
        if (name is null)
        {
            Assert.Inconclusive("Every curl 8.21.0 option has a row: nothing is unimplemented.");
        }

        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");
        string[] silentOption = silent ? ["-s"] : [];

        int exitCode = await RunAsync([.. silentOption, $"--{name}", Url], file);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            $"curl: option --{name}: the installed libcurl version does not support this" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
        Assert.IsEmpty(file.Contexts);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers) =>
        new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher(handlers)), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows: false)
            .RunAsync(arguments);
}
