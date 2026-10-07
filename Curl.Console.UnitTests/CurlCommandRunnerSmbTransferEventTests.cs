using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins an <c>smb://</c> and an <c>smbs://</c> download end to end on every platform, through
/// the production handler set over a scripted connection, against Ubuntu's curl 8.18.0
/// (OpenSSL) recorded on 2026-10-01 with <c>Record-CurlExchange.ps1 -Script</c> running
/// <c>-sS -v -u User:Password smb://172.26.96.1:14450/share/dir/x.txt</c> against replies
/// hand-assembled from <c>lib/smb.c</c> (BL-598 Notes; the Windows build has no SMB, ADR-0200).
/// The connector reports the <c>Trying</c> and <c>Established connection</c> lines, as
/// <c>TcpConnector</c> does; for <c>smbs</c> it also owns the TLS handshake and its lines.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSmbTransferEventTests
{
    private const string InfoEnd = "\r\n";

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    [TestMethod]
    [DataRow("smb", false)]
    [DataRow("smbs", true)]
    public async Task RunAsync_VerboseDownload_WritesTheFileAndTheMeasuredLines(string scheme, bool usesTls)
    {
        var scripted = new ScriptedConnector(Replies());

        int exitCode = await RunAsync(scripted, ["-sS", "-v", "-u", "User:Password", $"{scheme}://172.26.96.1:14450/share/dir/x.txt"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "hello world", StandardOutputText);
        Diagnostics.Diff(
            "stderr",
            ("*   Trying 172.26.96.1:14450..." + InfoEnd
            + "* Established connection to 172.26.96.1 (172.26.96.1 port 14450) from 172.26.99.197 port 54210 " + InfoEnd
            + "{ [11 bytes data]" + InfoEnd
            + "* shutting down connection #0" + InfoEnd).Replace("\r\n", "\n", StringComparison.Ordinal),
            Lf(StandardErrorText));
        Diagnostics.Assert("first target uses TLS", usesTls, scripted.Targets[0].UseTls);
        Assert.AreEqual("hello world", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(
            "*   Trying 172.26.96.1:14450..." + InfoEnd
            + "* Established connection to 172.26.96.1 (172.26.96.1 port 14450) from 172.26.99.197 port 54210 " + InfoEnd
            + "{ [11 bytes data]" + InfoEnd
            + "* shutting down connection #0" + InfoEnd,
            Encoding.ASCII.GetString(standardError.ToArray()));
        Assert.AreEqual(usesTls, scripted.Targets[0].UseTls);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithoutUser_ClosesTheConnectionWithExit67()
    {
        int exitCode = await RunAsync(new ScriptedConnector([]), ["-sS", "-v", "smb://172.26.96.1:14450/share/dir/x.txt"]);

        Diagnostics.Assert("exit code", 67, exitCode);
        Diagnostics.Diff(
            "stderr",
            ("*   Trying 172.26.96.1:14450..." + InfoEnd
            + "* Established connection to 172.26.96.1 (172.26.96.1 port 14450) from 172.26.99.197 port 54210 " + InfoEnd
            + "* closing connection #0" + InfoEnd
            + "curl: (67) Login denied" + Environment.NewLine).Replace("\r\n", "\n", StringComparison.Ordinal),
            Lf(StandardErrorText));
        Assert.AreEqual(67, exitCode);
        Assert.AreEqual(
            "*   Trying 172.26.96.1:14450..." + InfoEnd
            + "* Established connection to 172.26.96.1 (172.26.96.1 port 14450) from 172.26.99.197 port 54210 " + InfoEnd
            + "* closing connection #0" + InfoEnd
            + "curl: (67) Login denied" + Environment.NewLine,
            Encoding.ASCII.GetString(standardError.ToArray()));
    }

    private static byte[][] Replies() =>
    [
        Convert.FromHexString(
            "0000004dff534d427200000000980100000000000000000000000000000000000000000011000003010001000040000000000100"
            + "78563412fde30000000000000000000000000808000123456789abcdef"),
        Convert.FromHexString(
            "00000029ff534d427300000000980100000000000000000000000000000000006400000003ff00000000000000"),
        Convert.FromHexString(
            "0000002cff534d427500000000980100000000000000000000000000070000006400000003ff00000001000300413a00"),
        Convert.FromHexString(
            "00000067ff534d42a200000000980100000000000000000000000000070000006400000022ff0000000001400100000080004074"
            + "947bdc0180004074947bdc0180004074947bdc0180004074947bdc01800000000b000000000000000b0000000000000000000000"
            + "000000"),
        Convert.FromHexString(
            "00000046ff534d422e0000000098010000000000000000000000000007000000640000000cff000000ffff000000000b003b0000"
            + "0000000000000000000b0068656c6c6f20776f726c64"),
        Convert.FromHexString(
            "00000023ff534d4204000000009801000000000000000000000000000700000064000000000000"),
        Convert.FromHexString(
            "00000023ff534d4271000000009801000000000000000000000000000700000064000000000000"),
    ];

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(ScriptedConnector scripted, string[] arguments)
    {
        var files = new InMemoryFileSystem();
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("connector script", "scripted SMB replies behind a connector that reports Trying and Established lines");
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await RunWithAsync(scripted, arguments, files);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }

    private Task<int> RunWithAsync(ScriptedConnector scripted, string[] arguments, InMemoryFileSystem files)
    {
        return new CurlCommandRunner(
                _ => new TransferDispatch(
                    new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
                        new ReportingConnector(scripted, 54210),
                        new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                        new PassThroughTlsProvider(),
                        new LoopbackDnsResolver()))),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(arguments);
    }

    /// <summary>
    /// Reports the <c>Trying</c> and <c>Established connection</c> lines for each connect, from
    /// the measured local address, then connects through <paramref name="inner" />.
    /// </summary>
    private sealed class ReportingConnector(IConnector inner, int localPort) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            target.Events.ReportInfo($"  Trying {target.Host}:{target.Port}...");
            target.Events.ReportConnectionOpened(new ConnectionOpenedEvent
            {
                HostName = target.Host,
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(target.Host), target.Port),
                LocalEndPoint = new IPEndPoint(IPAddress.Parse("172.26.99.197"), localPort),
                ConnectionNumber = 0,
            });
            return inner.ConnectAsync(target, cancellationToken);
        }
    }

    /// <summary>A TLS provider the SMB handler never calls: <c>smbs</c> is TLS from the connector's first byte.</summary>
    private sealed class PassThroughTlsProvider : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(plaintext));
    }
}
