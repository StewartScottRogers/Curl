using System.Text;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins curl 8.22.0 when a proxy refuses the CONNECT of an <c>ftp -p</c> tunnel (BL-6083):
/// <c>-f</c> ends exit 22, without it the connector's exit 7 stands. Recorded with
/// <c>Record-CurlExchange.ps1 -CurlArgs -sS,-v,-f,-p,-x,http://127.0.0.1:P,ftp://example.test/x</c>
/// and a <c>403</c> or <c>407</c> reply, and through a loopback proxy that opens the control
/// tunnel and refuses the data one.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerProxyConnectFailTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://example.test/x";

    private const string Refused = "CONNECT tunnel failed, response 403";

    private const string Login =
        "220 hi\r\n331 pw\r\n230 ok\r\n257 \"/\"\r\n500 no\r\n227 Entering Passive Mode (127,0,0,1,7,208)\r\n";

    [TestMethod]
    [DataRow(HttpFailMode.Fail, 403, false, CurlExitCode.HttpReturnedError, "The requested URL returned error: 403")]
    [DataRow(HttpFailMode.Fail, 407, false, CurlExitCode.HttpReturnedError, "The requested URL returned error: 407")]
    [DataRow(HttpFailMode.Fail, 407, true, CurlExitCode.HttpReturnedError, "HTTP response code said error")]
    [DataRow(HttpFailMode.Fail, 302, false, CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 302")]
    [DataRow(HttpFailMode.FailWithBody, 403, false, CurlExitCode.CouldntConnect, Refused)]
    [DataRow(HttpFailMode.None, 403, false, CurlExitCode.CouldntConnect, Refused)]
    public async Task ExecuteAsync_ProxyRefusesTheControlConnect_EndsAsCurlDoesAndClosesTheConnection(
        HttpFailMode fail, int status, bool authenticationProblem, CurlExitCode expectedExitCode, string expectedMessage)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        var connector = new QueuedConnector(RefusedConnect(status, authenticationProblem));
        diagnostics.Arrange("fail mode", fail);
        diagnostics.Arrange("CONNECT status", status);

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(ContextOf(new HttpRequestOptions { Fail = fail }, events));
        diagnostics.ActResult(result);

        diagnostics.Assert("result", TransferResult.Failure(expectedExitCode, expectedMessage), result);
        Assert.AreEqual(TransferResult.Failure(expectedExitCode, expectedMessage), result);
        Assert.AreEqual("closing connection #0", events.Info[^1]);
        Assert.AreEqual(fail == HttpFailMode.Fail, connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoHttpOptionsAndTheProxyRefusesTheControlConnect_KeepsTheConnectorsExitCode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connector = new QueuedConnector(RefusedConnect(403, authenticationProblem: false));

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(ContextOf(null, new RecordingTransferEvents()));
        diagnostics.ActResult(result);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.CouldntConnect, Refused), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntConnect, Refused), result);
        Assert.IsFalse(connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    [TestMethod]
    [DataRow(HttpFailMode.Fail, CurlExitCode.HttpReturnedError, "The requested URL returned error: 403")]
    [DataRow(HttpFailMode.None, CurlExitCode.CouldntConnect, Refused)]
    public async Task ExecuteAsync_ProxyRefusesThePasvDataConnect_EndsAsCurlDoes(
        HttpFailMode fail, CurlExitCode expectedExitCode, string expectedMessage)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Login));
        var connector = new QueuedConnector(ConnectResult.Connected(control), RefusedConnect(403, authenticationProblem: false));
        var context = ContextOf(new HttpRequestOptions { Fail = fail }, new RecordingTransferEvents(), disableEpsv: false);
        diagnostics.ArrangeFtp(Url, Login);
        diagnostics.Arrange("fail mode", fail);

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(context);
        diagnostics.ActResult(result);

        diagnostics.Assert("result", TransferResult.Failure(expectedExitCode, expectedMessage), result with { Report = null });
        Assert.AreEqual(TransferResult.Failure(expectedExitCode, expectedMessage), result with { Report = null });
        Assert.AreEqual(fail == HttpFailMode.Fail, connector.Targets[1].FailsOnHttpErrorStatus);
    }

    private static ConnectResult RefusedConnect(int status, bool authenticationProblem) =>
        ConnectResult.Failed(CurlExitCode.CouldntConnect, $"CONNECT tunnel failed, response {status}", null, 0, status)
            .WithProxyAuthenticationProblem(authenticationProblem);

    private static TransferContext ContextOf(HttpRequestOptions? http, ITransferEvents events, bool disableEpsv = false) =>
        new()
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            Http = http,
            FtpDisableEpsv = disableEpsv,
        };
}
