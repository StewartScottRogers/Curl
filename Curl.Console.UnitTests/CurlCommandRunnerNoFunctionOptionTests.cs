using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins what the runner prints for curl's no-function options, against curl 8.21.0 measured on Windows
/// on 2026-09-28 with <c>Record-CurlExchange.ps1</c> and a loopback 200 (BL-488): the line
/// <c>Warning: --&lt;name&gt; is deprecated and has no function anymore</c> on standard error before the
/// transfer, the body on standard output, exit 0; nothing on standard error when <c>-s</c> came first;
/// and <c>--bogus</c> still refused as unknown with exit 2.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerNoFunctionOptionTests
{
    private const string Url = "file:///dir/x";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem fileSystem = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    [TestMethod]
    [DataRow(new[] { "--metalink" }, "metalink")]
    [DataRow(new[] { "--no-metalink" }, "metalink")]
    [DataRow(new[] { "-2" }, "sslv2")]
    [DataRow(new[] { "-3" }, "sslv3")]
    [DataRow(new[] { "--sslv2" }, "sslv2")]
    [DataRow(new[] { "--sslv3" }, "sslv3")]
    [DataRow(new[] { "--npn" }, "npn")]
    [DataRow(new[] { "--ntlm-wb" }, "ntlm-wb")]
    [DataRow(new[] { "--false-start" }, "false-start")]
    [DataRow(new[] { "--egd-file", "x" }, "egd-file")]
    [DataRow(new[] { "--random-file", "x" }, "random-file")]
    [DataRow(new[] { "--krb4", "x" }, "krb4")]
    public async Task RunAsync_NoFunctionOption_PrintsTheWarningBeforeTheTransferAndExitsZero(string[] option, string longName)
    {
        string warning = $"Warning: --{longName} is deprecated and has no function anymore" + NewLine;
        RecordingProtocolHandler file = new("file", async context =>
        {
            Assert.AreEqual(warning, StandardErrorText);
            await context.Output.WriteAsync("ok"u8.ToArray());

            return TransferResult.Success(2);
        });

        int exitCode = await RunAsync([.. option, Url], file);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(warning, StandardErrorText);
        Assert.AreEqual("ok", StandardOutputText);
        Assert.HasCount(1, file.Contexts);
    }

    [TestMethod]
    [DataRow("--metalink")]
    [DataRow("-2")]
    public async Task RunAsync_SilentThenNoFunctionOption_PrintsNothingOnStandardError(string option)
    {
        int exitCode = await RunAsync(["-s", option, Url], RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NoFunctionOptionThenSilent_PrintsTheWarning()
    {
        int exitCode = await RunAsync(["--metalink", "-s", Url], RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("Warning: --metalink is deprecated and has no function anymore" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_UnknownOption_IsStillRefusedWithExitTwo()
    {
        int exitCode = await RunAsync(["--bogus", Url]);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: option --bogus: is unknown" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers) =>
        new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher(handlers)), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows: false)
            .RunAsync(arguments);
}
