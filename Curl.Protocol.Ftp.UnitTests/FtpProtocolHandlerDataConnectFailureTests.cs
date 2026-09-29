using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

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
    private const string Url = "ftp://127.0.0.1:47707/f.txt";

    private const string Login =
        "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n500 no\r\n";

    [TestMethod]
    public async Task ExecuteAsync_NoFtpSkipPasvIpDataConnectionRefused_NamesTheControlHostAndViaTheDataAddressInExit7AndVerbose()
    {
        // curl -v --no-ftp-skip-pasv-ip ftp://127.0.0.1:47707/f.txt, PASV naming 127.0.0.2 port 1:
        // * Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2196 ms: Could not connect to server
        // curl: (7) Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2196 ms: Could not connect to server
        const string dialed = "Failed to connect to 127.0.0.2:1 after 2196 ms: Could not connect to server";
        const string expected = "Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2196 ms: Could not connect to server";
        var events = new RecordingTransferEvents();

        TransferResult result = await RunPasvAsync(
            Url,
            "227 Entering Passive Mode (127,0,0,2,0,1)",
            skipPasvIp: false,
            ConnectResult.Refused(dialed),
            events,
            reports => reports.ReportInfo(dialed));

        Assert.AreEqual(new TransferResult(CurlExitCode.CouldntConnect, 0, expected) { IsConnectionRefused = true }, result with { Report = null });
        Assert.AreEqual(expected, events.Info[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFtpSkipPasvIpDataConnectionTimedOut_NamesTheControlHostAndViaTheDataAddressInExit28()
    {
        // curl -sS --no-ftp-skip-pasv-ip ftp://127.0.0.1:47708/f.txt, PASV naming 10.255.255.1 port 1:
        // curl: (28) Failed to connect to 127.0.0.1:47708 via 10.255.255.1:1 after 21175 ms: Could not connect to server
        TransferResult result = await RunPasvAsync(
            "ftp://127.0.0.1:47708/f.txt",
            "227 Entering Passive Mode (10,255,255,1,0,1)",
            skipPasvIp: false,
            ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Failed to connect to 10.255.255.1:1 after 21175 ms: Could not connect to server"),
            new RecordingTransferEvents(),
            reports: null);

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Failed to connect to 127.0.0.1:47708 via 10.255.255.1:1 after 21175 ms: Could not connect to server"),
            result with { Report = null });
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpSkipPasvIpDataConnectionRefused_NamesTheUrlHostAndViaTheControlAddress()
    {
        // curl -sS ftp://localhost:47709/f.txt, PASV naming 127.0.0.2 port 1, so the data
        // connection goes to localhost: curl: (7) Failed to connect to localhost:47709 via
        // 127.0.0.1:1 after 2263 ms: Could not connect to server.
        TransferResult result = await RunPasvAsync(
            "ftp://localhost:47709/f.txt",
            "227 Entering Passive Mode (127,0,0,2,0,1)",
            skipPasvIp: true,
            ConnectResult.Refused("Failed to connect to localhost:1 after 2263 ms: Could not connect to server"),
            new RecordingTransferEvents(),
            reports: null,
            new IPEndPoint(IPAddress.Loopback, 47709));

        Assert.AreEqual("Failed to connect to localhost:47709 via 127.0.0.1:1 after 2263 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataConnectionFailsWithAnotherMessage_KeepsTheConnectorsMessage()
    {
        // A name that cannot be resolved, or a proxy's failure, is not the direct dial's
        // message and is passed on unchanged.
        const string message = "Could not resolve host: 127.0.0.2";

        TransferResult result = await RunPasvAsync(
            Url,
            "227 Entering Passive Mode (127,0,0,2,0,1)",
            skipPasvIp: false,
            ConnectResult.Failed(CurlExitCode.CouldntResolveHost, message),
            new RecordingTransferEvents(),
            reports: null);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntResolveHost, message), result with { Report = null });
    }

    [TestMethod]
    public async Task ExecuteAsync_DataConnect_PassesEveryEventTheConnectorReportsToTheTransfersEvents()
    {
        var events = new CallRecordingTransferEvents();

        await RunPasvAsync(
            Url,
            "227 Entering Passive Mode (127,0,0,2,0,1)",
            skipPasvIp: false,
            ConnectResult.Refused("refused"),
            events,
            ReportEveryEvent);

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

    private static void ReportEveryEvent(ITransferEvents reports)
    {
        reports.ReportInfo("Trying 127.0.0.2:1...");
        reports.ReportConnectionOpened(null!);
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
        string url,
        string pasvReply,
        bool skipPasvIp,
        ConnectResult dataResult,
        ITransferEvents events,
        Action<ITransferEvents>? reports,
        EndPoint? controlPeer = null)
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Login + pasvReply + "\r\n")) { RemoteEndPoint = controlPeer };
        var connector = new QueuedConnector(ConnectResult.Connected(control), dataResult) { DataConnectReports = reports };
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            FtpSkipPasvIp = skipPasvIp,
            Events = events,
        };

        return await new FtpProtocolHandler(connector).ExecuteAsync(context);
    }
}
