using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins curl 8.21.0's fallback to <c>PASV</c> when the data connection to the port a
/// <c>229</c> reply names cannot be made, port 0 included, and its exit 8 over IPv6 (BL-1250).
/// Recorded with <c>Record-CurlExchange.ps1 -Ftp -FtpReply 'EPSV=229 Entering Extended
/// Passive Mode (|||40000|)'</c>, nothing listening on 40000, measured 2026-10-02.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerEpsvDialFallbackTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:18922/f.txt";

    private const string LoggedIn =
        "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Pasv = "227 Entering Passive Mode (127,0,0,1,156,65)\r\n";

    private const string Retrieved = "200 Type set\r\n213 5\r\n150 Opening\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string Dialed = "Failed to connect to 127.0.0.1:40000 after 2214 ms: Could not connect to server";

    private const string Shown = "Failed to connect to 127.0.0.1:18922 via 127.0.0.1:40000 after 2214 ms: Could not connect to server";

    [TestMethod]
    public async Task ExecuteAsync_EpsvDataDialRefused_WritesCurlsTwoLinesSendsPasvAndCompletesOverThe227sPort()
    {
        // * Connecting to 127.0.0.1 port 40000 / *   Trying 127.0.0.1:40000... /
        // * Failed to connect to 127.0.0.1:18922 via 127.0.0.1:40000 after 2214 ms: Could not connect to server /
        // * Failed EPSV attempt. Disabling EPSV / > PASV, exit 0.
        var diagnostics = TestDiagnostics.For(TestContext);
        const string replies = "229 Entering Extended Passive Mode (|||40000|)\r\n" + Pasv + Retrieved;
        diagnostics.ArrangeFtp(Url, replies);

        Run run = await RunAsync(replies);

        diagnostics.ActResult(run.Result);
        diagnostics.Act("connect targets", run.Connector.Targets.Count);
        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 40000, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 40001, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[2]);
        string[] expected =
        [
            "* Connecting to 127.0.0.1 port 40000",
            "* " + Shown,
            "* Failed EPSV attempt. Disabling EPSV",
            "> PASV\r\n",
        ];
        string[] actualLines = run.Events.Transcript.SkipWhile(line => !line.StartsWith("* Connecting", StringComparison.Ordinal)).Take(4).ToArray();
        diagnostics.Diff("transcript", FtpDiagnostics.Escape(string.Join("\n", expected)), FtpDiagnostics.Escape(string.Join("\n", actualLines)));
        CollectionAssert.AreEqual(expected, actualLines);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(run.Output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvPortZero_FailsTheDialWithoutConnectingAndFallsBackToPasv()
    {
        // * Connecting to 127.0.0.1 port 0 / ... /
        // * Failed to connect to 127.0.0.1:18921 via 127.0.0.1:0 after 81 ms: Could not connect to server /
        // * Failed EPSV attempt. Disabling EPSV / > PASV, exit 0 (measured 2026-10-02).
        var diagnostics = TestDiagnostics.For(TestContext);
        const string replies = "229 Entering Extended Passive Mode (|||0|)\r\n" + Pasv + Retrieved;
        diagnostics.ArrangeFtp(Url, replies);
        diagnostics.Arrange("data results", "none");

        Run run = await RunAsync(replies, dataResults: []);

        diagnostics.ActResult(run.Result);
        diagnostics.Act("connect targets", run.Connector.Targets.Count);
        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.HasCount(2, run.Connector.Targets);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 40001, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        string[] expected =
        [
            "* Connecting to 127.0.0.1 port 0",
            "* Failed to connect to 127.0.0.1:18922 via 127.0.0.1:0 after 0 ms: Could not connect to server",
            "* Failed EPSV attempt. Disabling EPSV",
            "> PASV\r\n",
        ];
        string[] actualLines = run.Events.Transcript.SkipWhile(line => !line.StartsWith("* Connecting", StringComparison.Ordinal)).Take(4).ToArray();
        diagnostics.Diff("transcript", FtpDiagnostics.Escape(string.Join("\n", expected)), FtpDiagnostics.Escape(string.Join("\n", actualLines)));
        CollectionAssert.AreEqual(expected, actualLines);
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvDataDialRefused_LogsAWarningForTheFallbackToPasv()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);
        const string replies = "229 Entering Extended Passive Mode (|||40000|)\r\n" + Pasv + Retrieved;
        diagnostics.ArrangeFtp(Url, replies);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Warning);

        Run run = await RunAsync(replies, log: log);

        diagnostics.ActResult(run.Result);
        diagnostics.Act("warnings logged", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Warning), "EPSV data connection failed; falling back to PASV");
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvDataDialRefusedOverIPv6_FailsWithExit8AndTheDialsMessageWithoutPasvOrQuit()
    {
        // curl -g -v ftp://[::1]:18932/f.txt, 229 naming 40000, measured 2026-10-02:
        // * Failed to connect to ::1:18932 via ::1:40000 after 2768 ms: Could not connect to server
        // * Failed EPSV attempt, exiting
        // curl: (8) Failed to connect to ::1:18932 via ::1:40000 after 2768 ms: Could not connect to server
        const string dialed = "Failed to connect to ::1:40000 after 2768 ms: Could not connect to server";
        const string shown = "Failed to connect to ::1:18932 via ::1:40000 after 2768 ms: Could not connect to server";

        var diagnostics = TestDiagnostics.For(TestContext);
        const string replies = "229 Entering Extended Passive Mode (|||40000|)\r\n" + Pasv + Retrieved;
        diagnostics.ArrangeFtp("ftp://[::1]:18932/f.txt", replies);
        diagnostics.Arrange("dialed", dialed);

        Run run = await RunAsync(
            replies,
            url: "ftp://[::1]:18932/f.txt",
            controlPeer: new IPEndPoint(IPAddress.IPv6Loopback, 18932),
            dataResults: [ConnectResult.Refused(dialed)],
            dialed: dialed);

        diagnostics.ActResult(run.Result);
        diagnostics.Act("connect targets", run.Connector.Targets.Count);
        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.WeirdServerReply, shown), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, shown), run.Result);
        Assert.HasCount(2, run.Connector.Targets);
        Assert.AreEqual("* Failed EPSV attempt, exiting", run.Events.Transcript[^1]);
        Assert.AreEqual("* " + shown, run.Events.Transcript[^2]);
        Assert.DoesNotContain("> PASV\r\n", run.Events.Transcript);
    }

    private static async Task<Run> RunAsync(
        string replies,
        string url = Url,
        IPEndPoint? controlPeer = null,
        ConnectResult[]? dataResults = null,
        string dialed = Dialed,
        IDiagnosticLog? log = null)
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + replies))
        {
            RemoteEndPoint = controlPeer ?? new IPEndPoint(IPAddress.Loopback, 18922),
        };
        ConnectResult[] results =
        [
            ConnectResult.Connected(control),
            .. dataResults ?? [ConnectResult.Refused(dialed)],
            ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes("hello"))),
        ];
        var connector = new QueuedConnector(results) { DataConnectReports = reports => reports.ReportInfo(dialed) };
        var events = new RecordingTransferEvents();
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Events = events,
            DiagnosticLog = log ?? NoDiagnosticLog.Instance,
        };

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(context);
        return new Run(result with { Report = null }, connector, events, output);
    }

    /// <summary>One transfer: its result, the connects it asked for, its events and its output.</summary>
    private sealed record Run(TransferResult Result, QueuedConnector Connector, RecordingTransferEvents Events, MemoryStream Output);
}
