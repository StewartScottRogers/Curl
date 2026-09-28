using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the runner accepts the connection and TLS switches curl 8.21.0 takes with no
/// output of their own, measured on Windows on 2026-09-28 against a loopback 200 and an
/// <c>https</c> 200 under <c>-k</c>: with <c>-s</c> each exits 0 and writes nothing to
/// standard error, and the transfer is still dispatched.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerConnectionSwitchTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    [TestMethod]
    [DataRow("--tcp-nodelay", "http")]
    [DataRow("--no-tcp-nodelay", "http")]
    [DataRow("--no-alpn", "https")]
    [DataRow("--ca-native", "https")]
    [DataRow("--no-ca-native", "https")]
    [DataRow("--no-keepalive", "http")]
    [DataRow("--no-sessionid", "https")]
    [DataRow("--no-styled-output", "http")]
    [DataRow("--ssl-allow-beast", "https")]
    [DataRow("--ssl-revoke-best-effort", "https")]
    public async Task RunAsync_Switch_ExitsZeroAndWritesNothingToStandardError(string argument, string scheme)
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath(scheme);

        int exitCode = await RunAsync(["-s", "-k", argument, scheme + "://127.0.0.1/"], handler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.HasCount(1, handler.Contexts);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler) =>
        new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows: false, TerminalColumns.Default)
            .RunAsync(arguments);
}
