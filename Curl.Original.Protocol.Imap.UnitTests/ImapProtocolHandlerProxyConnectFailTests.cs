using Curl.Original.Protocol.Imap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins curl 8.22.0 when a proxy refuses the CONNECT of an <c>imap -p</c> tunnel (BL-6085):
/// <c>-f</c> ends exit 22, without it the connector's exit 7 stands, and either way the
/// connection is closed with its <c>-v</c> line. Recorded with
/// <c>Record-CurlExchange.ps1 -CurlArgs -sS,-v,-p,-f,-x,http://127.0.0.1:P,imap://example.test/INBOX</c>
/// and a <c>403</c> or <c>407</c> reply.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerProxyConnectFailTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "imap://example.test/INBOX";

    private const string Refused = "CONNECT tunnel failed, response 403";

    [TestMethod]
    [DataRow(HttpFailMode.Fail, 403, false, CurlExitCode.HttpReturnedError, "The requested URL returned error: 403")]
    [DataRow(HttpFailMode.Fail, 407, false, CurlExitCode.HttpReturnedError, "The requested URL returned error: 407")]
    [DataRow(HttpFailMode.Fail, 407, true, CurlExitCode.HttpReturnedError, "HTTP response code said error")]
    [DataRow(HttpFailMode.Fail, 302, false, CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 302")]
    [DataRow(HttpFailMode.FailWithBody, 403, false, CurlExitCode.CouldntConnect, Refused)]
    [DataRow(HttpFailMode.None, 403, false, CurlExitCode.CouldntConnect, Refused)]
    public async Task ExecuteAsync_ProxyRefusesTheConnect_EndsAsCurlDoesAndClosesTheConnection(
        HttpFailMode fail, int status, bool authenticationProblem, CurlExitCode expectedExitCode, string expectedMessage)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        var connector = new QueuedConnector(RefusedConnect(status, authenticationProblem));
        diagnostics.Arrange("fail mode", fail);
        diagnostics.Arrange("CONNECT status", status);

        TransferResult result = await HandlerOf(connector).ExecuteAsync(ContextOf(new HttpRequestOptions { Fail = fail }, events));
        diagnostics.Act("result", result);

        diagnostics.Assert("result", TransferResult.Failure(expectedExitCode, expectedMessage), result);
        Assert.AreEqual(TransferResult.Failure(expectedExitCode, expectedMessage), result);
        Assert.AreEqual("closing connection #0", events.Info.Single());
        Assert.AreEqual(fail == HttpFailMode.Fail, connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoHttpOptionsAndTheProxyRefusesTheConnect_KeepsTheConnectorsExitCode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connector = new QueuedConnector(RefusedConnect(403, authenticationProblem: false));
        diagnostics.Arrange("URL", Url);

        TransferResult result = await HandlerOf(connector).ExecuteAsync(ContextOf(null, new RecordingTransferEvents()));
        diagnostics.Act("result", result);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.CouldntConnect, Refused), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntConnect, Refused), result);
        Assert.IsFalse(connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    [TestMethod]
    [DataRow(HttpFailMode.Fail, CurlExitCode.HttpReturnedError, "The requested URL returned error: 403")]
    [DataRow(HttpFailMode.None, CurlExitCode.CouldntConnect, Refused)]
    public async Task ConnectOnlyAsync_ProxyRefusesTheConnect_EndsAsATransferDoes(
        HttpFailMode fail, CurlExitCode expectedExitCode, string expectedMessage)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        var connector = new QueuedConnector(RefusedConnect(403, authenticationProblem: false));
        diagnostics.Arrange("fail mode", fail);

        ImapConnectOutcome outcome = await HandlerOf(connector).ConnectOnlyAsync(ContextOf(new HttpRequestOptions { Fail = fail }, events));
        diagnostics.Act("result", outcome.Result);

        diagnostics.Assert("result", TransferResult.Failure(expectedExitCode, expectedMessage), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(expectedExitCode, expectedMessage), outcome.Result);
        Assert.IsNull(outcome.Connection);
        Assert.AreEqual("closing connection #0", events.Info.Single());
        Assert.AreEqual(fail == HttpFailMode.Fail, connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    private static ImapProtocolHandler HandlerOf(IConnector connector) => new(connector, new QueuedTlsProvider());

    private static ConnectResult RefusedConnect(int status, bool authenticationProblem) =>
        ConnectResult.Failed(CurlExitCode.CouldntConnect, $"CONNECT tunnel failed, response {status}", null, 0, status)
            .WithProxyAuthenticationProblem(authenticationProblem);

    private static TransferContext ContextOf(HttpRequestOptions? http, ITransferEvents events) =>
        new()
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            Http = http,
        };
}
