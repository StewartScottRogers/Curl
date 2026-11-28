using System.Net;
using System.Text;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins the <c>-v</c> lines curl 8.22.0 writes about an <c>ftp -p</c> data connection through a
/// proxy (BL-6088). Measured through a loopback proxy at <c>localhost</c> that opens the control
/// tunnel, answers <c>EPSV</c> and <c>PASV</c> and opens or refuses the data tunnel:
/// <c>curl -sS -v [-f] -p -x http://localhost:P ftp://example.test/x</c>. The connector writes
/// its own lines (<c>Trying</c>, <c>Establishing HTTP proxy tunnel</c>, the refusal), which the
/// fake stands in for with the refusal alone.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerProxyDataLineTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://example.test/x";

    private const string Login = "220 hi\r\n331 pw\r\n230 ok\r\n257 \"/\"\r\n";

    private const string Pasv = "227 Entering Passive Mode (127,0,0,1,7,208)\r\n";

    private const string Refused = "CONNECT tunnel failed, response 403";

    [TestMethod]
    [DataRow(HttpFailMode.None, Refused)]
    [DataRow(HttpFailMode.Fail, "The requested URL returned error: 403")]
    public async Task ExecuteAsync_ProxyRefusesThePasvDataConnect_NamesTheUrlsHostAndShutsDownTheConnection(
        HttpFailMode fail, string connectorLine)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();

        await RunAsync(diagnostics, events, Login + "500 no\r\n" + Pasv, fail, connectorLine, RefusedConnect());

        AssertLinesFromPassive(
            diagnostics,
            events,
            "Connect data stream passively",
            "Failed EPSV attempt. Disabling EPSV",
            "Skip 127.0.0.1 for data connection, reuse example.test instead",
            "Connecting to example.test port 2000",
            connectorLine,
            "shutting down connection #0");
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyRefusesTheEpsvAndPasvDataConnects_ShutsDownOnlyAfterTheLast()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();

        await RunAsync(diagnostics, events, Login + "229 ok (|||2001|)\r\n" + Pasv, HttpFailMode.None, Refused, RefusedConnect(), RefusedConnect());

        AssertLinesFromPassive(
            diagnostics,
            events,
            "Connect data stream passively",
            "Connecting to example.test port 2001",
            Refused,
            "Failed EPSV attempt. Disabling EPSV",
            "Skip 127.0.0.1 for data connection, reuse example.test instead",
            "Connecting to example.test port 2000",
            "shutting down connection #0");
    }

    [TestMethod]
    public async Task ExecuteAsync_DataConnectionOpenedThroughAProxy_KeepsTheProxysHostName()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        var dialed = new ConnectionOpenedEvent
        {
            HostName = "localhost",
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 3128),
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
            ConnectionNumber = 0,
        };
        var control = Control(Login + "500 no\r\n" + Pasv);
        var connector = new QueuedConnector(ConnectResult.Connected(control), RefusedConnect())
        {
            DataConnectReports = reports => reports.ReportConnectionOpened(dialed),
        };

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(ContextOf(HttpFailMode.None, events));
        diagnostics.ActResult(result);

        var expected = dialed with { IsSecondConnection = true };
        diagnostics.Assert("connection opened", expected, events.ConnectionsOpened.Single());
        Assert.AreEqual(expected, events.ConnectionsOpened.Single());
    }

    private static async Task RunAsync(
        TestDiagnostics diagnostics,
        RecordingTransferEvents events,
        string replies,
        HttpFailMode fail,
        string connectorLine,
        params ConnectResult[] dataResults)
    {
        diagnostics.ArrangeFtp(Url, replies);
        diagnostics.Arrange("fail mode", fail);
        var connector = new QueuedConnector([ConnectResult.Connected(Control(replies)), .. dataResults])
        {
            // Reports the first data connect's refusal, as the real connector does.
            DataConnectReports = reports => reports.ReportInfo(connectorLine),
        };

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(ContextOf(fail, events));
        diagnostics.ActResult(result);
    }

    private static void AssertLinesFromPassive(TestDiagnostics diagnostics, RecordingTransferEvents events, params string[] expected)
    {
        string[] actual = [.. events.Info.SkipWhile(line => line != "Connect data stream passively")];
        diagnostics.Assert("-v lines", string.Join('\n', expected), string.Join('\n', actual));
        CollectionAssert.AreEqual(expected, actual);
    }

    private static ScriptedConnection Control(string replies) =>
        new(Encoding.Latin1.GetBytes(replies)) { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 3128) };

    private static ConnectResult RefusedConnect() =>
        ConnectResult.Failed(CurlExitCode.CouldntConnect, Refused, null, 0, 403);

    private static TransferContext ContextOf(HttpFailMode fail, ITransferEvents events) =>
        new()
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            Http = new HttpRequestOptions { Fail = fail },
            Proxy = new ProxyEndpoint(ProxyKind.Http, "localhost", 3128, null),
            FtpSkipPasvIp = true,
        };
}
