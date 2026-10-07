using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-v</c>, <c>-i</c> and <c>--trace-ascii</c> for a WebSocket transfer end to end,
/// through the production handler set over a scripted connection, against curl 8.21.0 (mingw,
/// Schannel) recorded on 2026-09-28 with <c>Record-CurlExchange.ps1</c>, curl running
/// <c>ws://127.0.0.1:47932/p</c> against a <c>101</c> head followed by a text frame
/// <c>hello</c> and a close frame with status 1000, all in one read (BL-584 Notes). The random
/// <c>Sec-WebSocket-Key</c> is replaced by the one curl sent in the trace recording after it
/// is checked to be the base64 of 16 bytes; the connector reports the <c>Trying</c> and
/// <c>Established connection</c> lines with the ports curl used, as <c>TcpConnector</c> does.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerWsTransferEventTests
{
    private const string InfoEnd = "\r\n";

    private const string HeaderEnd = "\r\r\n";

    private const string MeasuredKey = "6HaZGji9W4cjqwD6VmsxSg==";

    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n";

    private const string HelloAndClose = "\x81\x05hello\x88\x02\x03\xe8";

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_Verbose_WritesTheMeasuredLines()
    {
        int exitCode = await RunAsync(["-sv"], Head101 + HelloAndClose, 59509);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "*   Trying 127.0.0.1:47932..." + InfoEnd
            + "* Established connection to 127.0.0.1 (127.0.0.1 port 47932) from 127.0.0.1 port 59509 " + InfoEnd
            + "* using HTTP/1.x" + InfoEnd
            + "> GET /p HTTP/1.1" + HeaderEnd
            + "> Host: 127.0.0.1:47932" + HeaderEnd
            + "> User-Agent: curl/8.21.0" + HeaderEnd
            + "> Accept: */*" + HeaderEnd
            + "> Upgrade: websocket" + HeaderEnd
            + "> Sec-WebSocket-Version: 13" + HeaderEnd
            + "> Sec-WebSocket-Key: " + MeasuredKey + HeaderEnd
            + "> Connection: Upgrade" + HeaderEnd
            + "> " + HeaderEnd
            + "* Request completely sent off" + InfoEnd
            + "< HTTP/1.1 101 Switching Protocols" + HeaderEnd
            + "< Upgrade: websocket" + HeaderEnd
            + "< Connection: Upgrade" + HeaderEnd
            + "< Sec-WebSocket-Accept: x" + HeaderEnd
            + "< " + HeaderEnd
            + "* Received 101, Switching to WebSocket" + InfoEnd
            + "* [WS] Received 101, switch to WebSocket" + InfoEnd
            + "{ [11 bytes data]" + InfoEnd
            + "* shutting down connection #0" + InfoEnd,
            WithMeasuredKey(Encoding.ASCII.GetString(standardError.ToArray())));
        Assert.AreEqual("hello\x03\xe8", Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    /// <summary>
    /// curl 8.21.0 under <c>-sS --trace-config ws -v</c> against a <c>101</c> head followed by a
    /// text frame <c>hi</c> and an empty close frame in one read, measured 2026-10-02 (BL-1164 Notes).
    /// </summary>
    [TestMethod]
    [DataRow("ws")]
    [DataRow("protocol")]
    public async Task RunAsync_VerboseWithTheWsTraceComponent_WritesTheMeasuredWsLines(string component)
    {
        int exitCode = await RunAsync(["-sSv", "--trace-config", component], Head101 + "\x81\x02hi\x88\x00", 59511);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        StringAssert.EndsWith(
            WithMeasuredKey(Encoding.ASCII.GetString(standardError.ToArray())),
            "< " + HeaderEnd
            + "* Received 101, Switching to WebSocket" + InfoEnd
            + "* [WS] WS, using chunk size 65535" + InfoEnd
            + "* [WS] Received 101, switch to WebSocket" + InfoEnd
            + "{ [6 bytes data]" + InfoEnd
            + "* [WS] decoded decoded [TEXT payload=0/2]" + InfoEnd
            + "* [WS] passed 2 bytes payload, 0 remain" + InfoEnd
            + "* [WS] decoded passing [TEXT payload=2/2]" + InfoEnd
            + "* [WS] decoded decoded [CLOSE payload=0/0]" + InfoEnd
            + "* [WS] websocket established, callback mode" + InfoEnd
            + "{ [0 bytes data]" + InfoEnd
            + "* shutting down connection #0" + InfoEnd);
        Assert.AreEqual("hi", Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    [DataRow("-sv")]
    [DataRow("-sv", "--trace-config", "smtp")]
    [DataRow("-sv", "--trace-config", "ws,-ws")]
    [DataRow("-s", "--trace-config", "ws")]
    public async Task RunAsync_WithoutTheWsTraceComponentOrVerbose_WritesNoWsTraceLines(params string[] arguments)
    {
        int exitCode = await RunAsync(arguments, Head101 + "\x81\x02hi\x88\x00", 59512);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string standardErrorText = Encoding.ASCII.GetString(standardError.ToArray());
        Assert.DoesNotContain("[WS] WS, using chunk size", standardErrorText);
        Assert.DoesNotContain("[WS] decoded", standardErrorText);
        Assert.DoesNotContain("[WS] websocket established", standardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_Include_WritesOnlyThePayloads()
    {
        int exitCode = await RunAsync(["-si"], Head101 + HelloAndClose, 59510);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, Encoding.ASCII.GetString(standardError.ToArray()));
        Assert.AreEqual("hello\x03\xe8", Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_Refused_WritesTheRefusalBeforeTheBlankLine()
    {
        int exitCode = await RunAsync(["-sv"], "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n", 64833);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        StringAssert.EndsWith(
            WithMeasuredKey(Encoding.ASCII.GetString(standardError.ToArray())),
            "* Request completely sent off" + InfoEnd
            + "< HTTP/1.1 404 Not Found" + HeaderEnd
            + "< Content-Length: 0" + HeaderEnd
            + "* Refused WebSocket upgrade: 404" + InfoEnd
            + "< " + HeaderEnd
            + "* closing connection #0" + InfoEnd);
    }

    [TestMethod]
    public async Task RunAsync_NoFrameAfterThe101_WritesTheEmptyReadAndEmptyReply()
    {
        int exitCode = await RunAsync(["-sv"], Head101, 64834);

        Diagnostics.Assert("exit code", 52, exitCode);
        Assert.AreEqual(52, exitCode);
        StringAssert.EndsWith(
            WithMeasuredKey(Encoding.ASCII.GetString(standardError.ToArray())),
            "* [WS] Received 101, switch to WebSocket" + InfoEnd
            + "{ [0 bytes data]" + InfoEnd
            + "* Empty reply from server" + InfoEnd
            + "* shutting down connection #0" + InfoEnd);
    }

    [TestMethod]
    public async Task RunAsync_TraceAscii_WritesTheMeasuredDumpAndThePayloadsOnStandardOutput()
    {
        int exitCode = await RunAsync(["--trace-ascii", "-"], Head101 + HelloAndClose, 61657);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "*   Trying 127.0.0.1:47932...\n"
            + "* Established connection to 127.0.0.1 (127.0.0.1 port 47932) from 127.0.0.1 port 61657 \n"
            + "* using HTTP/1.x\n"
            + "=> Send header, 193 bytes (0xc1)\n"
            + "0000: GET /p HTTP/1.1\n"
            + "0011: Host: 127.0.0.1:47932\n"
            + "0028: User-Agent: curl/8.21.0\n"
            + "0041: Accept: */*\n"
            + "004e: Upgrade: websocket\n"
            + "0062: Sec-WebSocket-Version: 13\n"
            + "007d: Sec-WebSocket-Key: " + MeasuredKey + "\n"
            + "00aa: Connection: Upgrade\n"
            + "00bf: \n"
            + "* Request completely sent off\n"
            + "<= Recv header, 34 bytes (0x22)\n0000: HTTP/1.1 101 Switching Protocols\n"
            + "<= Recv header, 20 bytes (0x14)\n0000: Upgrade: websocket\n"
            + "<= Recv header, 21 bytes (0x15)\n0000: Connection: Upgrade\n"
            + "<= Recv header, 25 bytes (0x19)\n0000: Sec-WebSocket-Accept: x\n"
            + "<= Recv header, 2 bytes (0x2)\n0000: \n"
            + "* Received 101, Switching to WebSocket\n"
            + "* [WS] Received 101, switch to WebSocket\n"
            + "<= Recv data, 11 bytes (0xb)\n"
            + "0000: ..hello....\n"
            + "hello\x03\xe8"
            + "<= Recv data, 0 bytes (0x0)\n"
            + "* shutting down connection #0\n",
            WithMeasuredKey(Encoding.Latin1.GetString(standardOutput.ToArray())));
    }

    /// <summary>
    /// Replaces the random <c>Sec-WebSocket-Key</c> with <see cref="MeasuredKey" />, after
    /// checking it is the base64 of 16 bytes.
    /// </summary>
    private static string WithMeasuredKey(string text)
    {
        Match key = Regex.Match(text, "Sec-WebSocket-Key: ([A-Za-z0-9+/]{22}==)");
        Assert.IsTrue(key.Success, text);
        Assert.HasCount(16, Convert.FromBase64String(key.Groups[1].Value));
        return text.Replace(key.Groups[1].Value, MeasuredKey, StringComparison.Ordinal);
    }

    private async Task<int> RunAsync(IReadOnlyList<string> options, string reply, int localPort)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', options.Append("ws://127.0.0.1:47932/p")));
        Diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));
        Diagnostics.Arrange("local port", localPort);
        var connector = new ReportingConnector(new ScriptedConnector([Encoding.Latin1.GetBytes(reply)]), localPort);
        var files = new InMemoryFileSystem();

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await RunCommandAsync(options, connector, files);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }

    private Task<int> RunCommandAsync(IReadOnlyList<string> options, ReportingConnector connector, InMemoryFileSystem files)
    {
        return new CurlCommandRunner(
                parsed => new TransferDispatch(
                    new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
                        connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesWs: CurlComposition.TracesWs(parsed)))),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync([.. options, "ws://127.0.0.1:47932/p"]);
    }

    /// <summary>
    /// Reports the <c>Trying</c> and <c>Established connection</c> lines for each connect, as
    /// <c>TcpConnector</c> does, then connects through <paramref name="inner" />.
    /// </summary>
    private sealed class ReportingConnector(IConnector inner, int localPort) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            target.Events.ReportInfo($"  Trying {target.Host}:{target.Port}...");
            target.Events.ReportConnectionOpened(new ConnectionOpenedEvent
            {
                HostName = target.Host,
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, target.Port),
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, localPort),
                ConnectionNumber = 0,
            });
            return inner.ConnectAsync(target, cancellationToken);
        }
    }
}
