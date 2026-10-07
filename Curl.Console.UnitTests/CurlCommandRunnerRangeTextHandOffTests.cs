using System.Text;
using System.Text.RegularExpressions;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <c>-r</c> text naming no range (<c>5-2</c>) reaches every handler, as curl 8.21.0
/// hands it on (BL-1322, BL-1396): only <c>Curl_range</c> in FTP and file and SFTP's
/// <c>Curl_ssh_range</c> parse it. Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) against
/// a 12-byte file: <c>-sv -r 5-2 file://...</c> writes only <c>* shutting down connection #0</c>
/// and exits 33; with the meter, the meter comes first and <c>curl: (33) Requested range was not
/// delivered by the server</c> last; <c>-I</c> exits 0; a missing file exits 37 with no
/// <c>shutting down</c> line; FTP exits 0 after <c>EPSV</c>; <c>dict://</c> connects and exits 7.
/// The transfers run through the production handler set over fake connectors.
/// </summary>
[TestClass]
public sealed partial class CurlCommandRunnerRangeTextHandOffTests
{
    private const string ShuttingDownLine = "* shutting down connection #0";

    private static readonly string NotDeliveredLine = "curl: (33) " + ByteRangeParser.NotDeliveredMessage;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_SilentVerboseFileRangeThatNamesNoRange_ShutsDownTheConnectionAndExits33()
    {
        string fileUrl = CreateTemporaryFileUrl();

        int exitCode = await RunAsync(new RefusingConnector(), "-sv", "-r", "5-2", fileUrl);

        Diagnostics.Assert("exit code", 33, exitCode);
        Diagnostics.Diff("stderr", ShuttingDownLine + "\n", Lf(StandardErrorText));
        Diagnostics.Assert("stdout length", 0L, standardOutput.Length);
        Assert.AreEqual(33, exitCode);
        Assert.AreEqual(ShuttingDownLine + "\n", StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_VerboseFileRangeThatNamesNoRange_WritesTheMeterThenTheShutdownThenTheFailure()
    {
        string fileUrl = CreateTemporaryFileUrl();

        int exitCode = await RunAsync(new RefusingConnector(), "-v", "-r", "5-2", fileUrl, "-o", Path.Combine(Path.GetTempPath(), "curl-bl1322-" + Guid.NewGuid().ToString("N")));

        Diagnostics.Assert("exit code", 33, exitCode);
        Assert.AreEqual(33, exitCode);
        int meter = StandardErrorText.IndexOf("  % Total    % Received % Xferd", StringComparison.Ordinal);
        int shutdown = StandardErrorText.IndexOf(ShuttingDownLine, StringComparison.Ordinal);
        int failure = StandardErrorText.IndexOf(NotDeliveredLine, StringComparison.Ordinal);
        Diagnostics.Assert("meter header found", true, meter >= 0);
        Diagnostics.Assert("shutdown line after meter", true, shutdown > meter);
        Diagnostics.Assert("failure line after shutdown", true, failure > shutdown);
        Assert.IsGreaterThanOrEqualTo(0, meter);
        Assert.IsGreaterThan(meter, shutdown);
        Assert.IsGreaterThan(shutdown, failure);
    }

    [TestMethod]
    public async Task RunAsync_HeadFileRangeThatNamesNoRange_WritesTheHeaderBlockAndExits0()
    {
        string fileUrl = CreateTemporaryFileUrl();

        int exitCode = await RunAsync(new RefusingConnector(), "-sv", "-I", "-r", "5-2", fileUrl);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stdout contains Content-Length: 12", true, StandardOutputText.Contains("Content-Length: 12", StringComparison.Ordinal));
        Diagnostics.Assert("stderr contains shutdown line", true, StandardErrorText.Contains(ShuttingDownLine, StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardOutputText, "Content-Length: 12");
        StringAssert.Contains(StandardErrorText, ShuttingDownLine);
    }

    [TestMethod]
    public async Task RunAsync_MissingFileRangeThatNamesNoRange_Exits37WithoutAShutdownLine()
    {
        string missingUrl = new Uri(Path.Combine(Path.GetTempPath(), "curl-bl1322-" + Guid.NewGuid().ToString("N"), "nonexist.txt")).AbsoluteUri;

        int exitCode = await RunAsync(new RefusingConnector(), "-v", "-r", "5-2", missingUrl);

        Diagnostics.Assert("exit code", (int)CurlExitCode.FileCouldntReadFile, exitCode);
        Diagnostics.Assert("stderr contains shutting down", false, StandardErrorText.Contains("shutting down", StringComparison.Ordinal));
        Assert.AreEqual((int)CurlExitCode.FileCouldntReadFile, exitCode);
        Assert.DoesNotContain("shutting down", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FtpRangeThatNamesNoRange_EndsAfterEpsvWithExit0()
    {
        ScriptedConnector connector = new(
        [
            Encoding.ASCII.GetBytes("220 Recorder ready\r\n"),
            Encoding.ASCII.GetBytes("331 Password required\r\n"),
            Encoding.ASCII.GetBytes("230 Logged in\r\n"),
            Encoding.ASCII.GetBytes("257 \"/\" is current directory\r\n"),
            Encoding.ASCII.GetBytes("229 Entering Extended Passive Mode (|||40001|)\r\n"),
        ]);
        Diagnostics.Arrange("scripted replies", "220, 331, 230, 257, 229 (EPSV port 40001)");

        int exitCode = await RunAsync(connector, "-sv", "-r", "5-2", "ftp://127.0.0.1/f.txt");

        string written = Encoding.ASCII.GetString(connector.Written);
        Diagnostics.Act("commands sent", Lf(written));
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains curl: (33)", false, StandardErrorText.Contains("curl: (33)", StringComparison.Ordinal));
        Diagnostics.Assert("EPSV sent", true, written.Contains("EPSV\r\n", StringComparison.Ordinal));
        Diagnostics.Assert("RETR sent", false, written.Contains("RETR", StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.DoesNotContain("curl: (33)", StandardErrorText);
        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "EPSV\r\n");
        Assert.DoesNotContain("RETR", Encoding.ASCII.GetString(connector.Written));
    }

    [TestMethod]
    public async Task RunAsync_DictRangeThatNamesNoRange_GoesOnToConnect()
    {
        RefusingConnector connector = new();

        int exitCode = await RunAsync(connector, "-s", "-r", "5-2", "dict://127.0.0.1:1/x");

        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntConnect, exitCode);
        Diagnostics.Assert("connect attempts", 1, connector.Targets.Count);
        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task RunAsync_WsUpgradeRangeThatNamesNoRange_SendsTheRangeTextAsTyped()
    {
        ScriptedConnector connector = new(
        [
            Encoding.Latin1.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n"
                + "\x88\x02\x03\xe8"),
        ]);
        Diagnostics.Arrange("scripted reply", "101 Switching Protocols, then a close frame (1000)");

        int exitCode = await RunAsync(connector, "-s", "-r", "5-2", "ws://127.0.0.1:47901/");

        Diagnostics.Bytes("request bytes", connector.Written);
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("request has Range: bytes=5-2", true, Encoding.Latin1.GetString(connector.Written).Contains("\r\nRange: bytes=5-2\r\n", StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode, StandardErrorText);
        StringAssert.Contains(Encoding.Latin1.GetString(connector.Written), "\r\nRange: bytes=5-2\r\n");
    }

    [TestMethod]
    [DataRow("sftp")]
    [DataRow("scp")]
    public async Task RunAsync_SshRangeThatNamesNoRange_GoesOnToConnect(string scheme)
    {
        RefusingConnector connector = new();

        int exitCode = await RunAsync(connector, "-s", "-k", "-r", "5-2", scheme + "://127.0.0.1:1/f");

        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntConnect, exitCode);
        Diagnostics.Assert("connect attempts", 1, connector.Targets.Count);
        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        Assert.HasCount(1, connector.Targets);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Replaces every temporary path or file URL this class makes, which differ by OS, with a fixed token.</summary>
    private static string WithoutTemporaryPaths(string text) =>
        TemporaryPathPattern().Replace(
            text.Replace(Path.GetTempPath(), "<temp>/", StringComparison.Ordinal)
                .Replace(new Uri(Path.GetTempPath()).AbsoluteUri, "<temp>/", StringComparison.Ordinal),
            "<temporary path>");

    [GeneratedRegex(@"\S*curl-bl1322-[0-9a-f]{32}\S*")]
    private static partial Regex TemporaryPathPattern();

    private string CreateTemporaryFileUrl()
    {
        string directory = Path.Combine(Path.GetTempPath(), "curl-bl1322-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "f.txt");
        File.WriteAllText(path, "0123456789ab");
        Diagnostics.Arrange("temporary file", "f.txt, 12 bytes: 0123456789ab");
        return new Uri(path).AbsoluteUri;
    }

    private async Task<int> RunAsync(IConnector connector, params string[] arguments)
    {
        PhysicalFileSystem files = new();
        TransferDispatch dispatch = new(
            new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver())));
        Diagnostics.Arrange("arguments", WithoutTemporaryPaths(string.Join(' ', arguments)));
        Diagnostics.Arrange("connector", connector.GetType().Name);
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => dispatch,
                    files,
                    files,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false,
                    writesProgressMeter: true,
                    timeProvider: new ManualTimeProvider())
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", WithoutTemporaryPaths(Lf(StandardOutputText)));
        Diagnostics.Act("stderr", WithoutTemporaryPaths(Lf(StandardErrorText)));
        return exitCode;
    }

    /// <summary>A connector that refuses every connect with exit 7, recording each target.</summary>
    private sealed class RefusingConnector : IConnector
    {
        public List<ConnectTarget> Targets { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));
        }
    }
}
