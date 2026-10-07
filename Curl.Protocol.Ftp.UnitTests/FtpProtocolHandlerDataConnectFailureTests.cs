using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins curl 8.21.0's message for a passive data connection that cannot be reached, which
/// names the control connection's host and port and then the data address after <c>via</c>
/// (BL-904). Recorded with <c>Record-CurlExchange.ps1 -Ftp -FtpReply 'EPSV=500 no'
/// 'PASV=227 Entering Passive Mode (…)'</c>.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerDataConnectFailureTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:47707/f.txt";

    private const string Login =
        "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n500 no\r\n";

    [TestMethod]
    public async Task ExecuteAsync_NoFtpSkipPasvIpDataConnectionRefused_NamesTheControlHostAndViaTheDataAddressInExit7AndVerbose()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -v --no-ftp-skip-pasv-ip ftp://127.0.0.1:47707/f.txt, PASV naming 127.0.0.2 port 1:
        // * Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2196 ms: Could not connect to server
        // curl: (7) Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2196 ms: Could not connect to server
        const string dialed = "Failed to connect to 127.0.0.2:1 after 2196 ms: Could not connect to server";
        const string expected = "Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2196 ms: Could not connect to server";
        var events = new RecordingTransferEvents();

        TransferResult result = await RunPasvAsync(
            diagnostics,
            Url,
            "227 Entering Passive Mode (127,0,0,2,0,1)",
            skipPasvIp: false,
            ConnectResult.Refused(dialed),
            events,
            reports => reports.ReportInfo(dialed));

        diagnostics.Assert("result", new TransferResult(CurlExitCode.CouldntConnect, 0, expected) { IsConnectionRefused = true }, result with { Report = null });
        Assert.AreEqual(new TransferResult(CurlExitCode.CouldntConnect, 0, expected) { IsConnectionRefused = true }, result with { Report = null });
        diagnostics.Assert("last -v line", expected, events.Info[^1]);
        Assert.AreEqual(expected, events.Info[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFtpSkipPasvIpDataConnectionTimedOut_NamesTheControlHostAndViaTheDataAddressInExit28()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -sS --no-ftp-skip-pasv-ip ftp://127.0.0.1:47708/f.txt, PASV naming 10.255.255.1 port 1:
        // curl: (28) Failed to connect to 127.0.0.1:47708 via 10.255.255.1:1 after 21175 ms: Could not connect to server
        TransferResult result = await RunPasvAsync(
            diagnostics,
            "ftp://127.0.0.1:47708/f.txt",
            "227 Entering Passive Mode (10,255,255,1,0,1)",
            skipPasvIp: false,
            ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Failed to connect to 10.255.255.1:1 after 21175 ms: Could not connect to server"),
            new RecordingTransferEvents(),
            reports: null);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.OperationTimedOut, "Failed to connect to 127.0.0.1:47708 via 10.255.255.1:1 after 21175 ms: Could not connect to server"), result with { Report = null });
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Failed to connect to 127.0.0.1:47708 via 10.255.255.1:1 after 21175 ms: Could not connect to server"),
            result with { Report = null });
    }

    [TestMethod]
    public async Task ExecuteAsync_WithADataConnector_DialsTheDataConnectionThroughItAndNamesItsTimeoutViaTheDataAddress()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -v --disable-epsv --no-ftp-skip-pasv-ip --connect-timeout 1 ftp://127.0.0.1:47911/f.txt,
        // PASV naming 10.255.255.1 port 1025, measured 2026-09-30 (BL-797): the data connect ran
        // until Windows gave up, 21 s, then
        // curl: (28) Failed to connect to 127.0.0.1:47911 via 10.255.255.1:1025 after 21125 ms: Could not connect to server
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n227 Entering Passive Mode (10,255,255,1,4,1)\r\n"));
        var controlConnector = new QueuedConnector(ConnectResult.Connected(control));
        var dataConnector = new QueuedConnector(
            ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Failed to connect to 10.255.255.1:1025 after 21125 ms: Could not connect to server"));
        var handler = new FtpProtocolHandler(
            controlConnector,
            dataConnector,
            new QueuedListener(),
            new QueuedTlsProvider(),
            new NamedDnsResolver(new Dictionary<string, IPAddress[]>()),
            new NamedNetworkInterfaceLookup(new Dictionary<string, IPAddress[]>()));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ftp://127.0.0.1:47911/f.txt"),
            Output = new MemoryStream(),
            FtpDisableEpsv = true,
            FtpSkipPasvIp = false,
        };

        diagnostics.Arrange("url", context.Url);
        diagnostics.Arrange("FtpDisableEpsv", true);

        TransferResult result = await handler.ExecuteAsync(context);
        diagnostics.ActResult(result);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.OperationTimedOut, "Failed to connect to 127.0.0.1:47911 via 10.255.255.1:1025 after 21125 ms: Could not connect to server"), result with { Report = null });
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Failed to connect to 127.0.0.1:47911 via 10.255.255.1:1025 after 21125 ms: Could not connect to server"),
            result with { Report = null });
        diagnostics.Assert("control connects", 1, controlConnector.Targets.Count);
        Assert.HasCount(1, controlConnector.Targets);
        Assert.AreEqual(new ConnectTarget("10.255.255.1", 1025, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, dataConnector.Targets.Single() with { DiagnosticLog = NoDiagnosticLog.Instance });
    }

    [TestMethod]
    public void Constructor_WithANullDataConnector_ThrowsArgumentNullException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        diagnostics.Arrange("dataConnector", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new FtpProtocolHandler(
            new QueuedConnector(),
            null!,
            new QueuedListener(),
            new QueuedTlsProvider(),
            new NamedDnsResolver(new Dictionary<string, IPAddress[]>()),
            new NamedNetworkInterfaceLookup(new Dictionary<string, IPAddress[]>())));

        diagnostics.Assert("parameter name", "dataConnector", exception.ParamName);
        diagnostics.Act("exception", exception.GetType().Name);

        Assert.AreEqual("dataConnector", exception.ParamName);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpSkipPasvIpDataConnectionRefused_NamesTheUrlHostAndViaTheControlAddress()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -sS ftp://localhost:47709/f.txt, PASV naming 127.0.0.2 port 1, so the data
        // connection goes to localhost: curl: (7) Failed to connect to localhost:47709 via
        // 127.0.0.1:1 after 2263 ms: Could not connect to server.
        TransferResult result = await RunPasvAsync(
            diagnostics,
            "ftp://localhost:47709/f.txt",
            "227 Entering Passive Mode (127,0,0,2,0,1)",
            skipPasvIp: true,
            ConnectResult.Refused("Failed to connect to localhost:1 after 2263 ms: Could not connect to server"),
            new RecordingTransferEvents(),
            reports: null,
            new IPEndPoint(IPAddress.Loopback, 47709));

        diagnostics.Assert("error", "Failed to connect to localhost:47709 via 127.0.0.1:1 after 2263 ms: Could not connect to server", result.ErrorMessage);
        Assert.AreEqual("Failed to connect to localhost:47709 via 127.0.0.1:1 after 2263 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataConnectionFailsWithAnotherMessage_KeepsTheConnectorsMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // A name that cannot be resolved, or a proxy's failure, is not the direct dial's
        // message and is passed on unchanged.
        const string message = "Could not resolve host: 127.0.0.2";

        TransferResult result = await RunPasvAsync(
            diagnostics,
            Url,
            "227 Entering Passive Mode (127,0,0,2,0,1)",
            skipPasvIp: false,
            ConnectResult.Failed(CurlExitCode.CouldntResolveHost, message),
            new RecordingTransferEvents(),
            reports: null);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.CouldntResolveHost, message), result with { Report = null });
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntResolveHost, message), result with { Report = null });
    }

    [TestMethod]
    public async Task ExecuteAsync_DataConnect_PassesEveryEventTheConnectorReportsToTheTransfersEvents()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var events = new CallRecordingTransferEvents();

        await RunPasvAsync(
            diagnostics,
            Url,
            "227 Entering Passive Mode (127,0,0,2,0,1)",
            skipPasvIp: false,
            ConnectResult.Refused("refused"),
            events,
            ReportEveryEvent);

        diagnostics.Assert("event calls reported", 11, events.Calls.SkipWhile(call => call != "ReportInfo: Trying 127.0.0.2:1...").Count());
        CollectionAssert.AreEqual(
            new[]
            {
                "ReportInfo: Trying 127.0.0.2:1...",
                "ReportConnectionOpened",
                "ReportConnectionReused",
                "ReportTlsHandshake",
                "ReportTlsData",
                "ReportTlsMessage",
                "ReportTlsTrust",
                "ReportRequestHeader",
                "ReportResponseHeader",
                "ReportDataSent",
                "ReportDataReceived",
            },
            events.Calls.SkipWhile(call => call != "ReportInfo: Trying 127.0.0.2:1...").ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_DataConnectionOpened_IsReportedAsTheSecondConnectionToTheUrlsHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl 8.21.0 -v --no-ftp-skip-pasv-ip ftp://localhost:47943/dir/file.txt (measured 2026-09-30, BL-944):
        // * Established 2nd connection to localhost (127.0.0.1 port 53199) from 127.0.0.1 port 53203
        var events = new RecordingTransferEvents();
        var dialed = new ConnectionOpenedEvent
        {
            HostName = "127.0.0.2",
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.2"), 1),
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 53203),
            ConnectionNumber = 1,
        };

        await RunPasvAsync(
            diagnostics,
            "ftp://localhost:47707/f.txt",
            "227 Entering Passive Mode (127,0,0,2,0,1)",
            skipPasvIp: false,
            ConnectResult.Refused("refused"),
            events,
            reports => reports.ReportConnectionOpened(dialed));

        diagnostics.Assert("connection opened", dialed with { HostName = "localhost", IsSecondConnection = true }, events.ConnectionsOpened.Single());
        Assert.AreEqual(dialed with { HostName = "localhost", IsSecondConnection = true }, events.ConnectionsOpened.Single());
    }

    private static void ReportEveryEvent(ITransferEvents reports)
    {
        reports.ReportInfo("Trying 127.0.0.2:1...");
        reports.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = "127.0.0.2",
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.2"), 1),
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 1),
            ConnectionNumber = 1,
        });
        reports.ReportConnectionReused(null!);
        reports.ReportTlsHandshake(null!);
        reports.ReportTlsData([], sent: true);
        reports.ReportTlsMessage(null!);
        reports.ReportTlsTrust(null!);
        reports.ReportRequestHeader([]);
        reports.ReportResponseHeader([]);
        reports.ReportDataSent([]);
        reports.ReportDataReceived([]);
    }

    private static async Task<TransferResult> RunPasvAsync(
        TestDiagnostics diagnostics,
        string url,
        string pasvReply,
        bool skipPasvIp,
        ConnectResult dataResult,
        ITransferEvents events,
        Action<ITransferEvents>? reports,
        EndPoint? controlPeer = null)
    {
        diagnostics.ArrangeFtp(url, Login + pasvReply);
        diagnostics.Arrange("skip PASV IP", skipPasvIp);
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Login + pasvReply + "\r\n")) { RemoteEndPoint = controlPeer };
        var connector = new QueuedConnector(ConnectResult.Connected(control), dataResult) { DataConnectReports = reports };
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            FtpSkipPasvIp = skipPasvIp,
            Events = events,
        };

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(context);
        diagnostics.ActResult(result);
        return result;
    }
}
