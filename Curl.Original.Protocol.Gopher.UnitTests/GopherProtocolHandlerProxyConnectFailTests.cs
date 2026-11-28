using Curl.Original.Protocol.Gopher.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Gopher;

/// <summary>
/// Pins curl 8.22.0 when a proxy refuses the CONNECT of a <c>gopher -p</c> tunnel (BL-6089):
/// <c>-f</c> ends exit 22, without it the connector's exit 7 stands, and both close the
/// connection. Recorded with <c>Record-CurlExchange.ps1 -CurlArgs
/// -sS,-v,-f,-p,-x,http://127.0.0.1:P,gopher://example.test/1</c> and a <c>403</c> or
/// <c>407</c> reply.
/// </summary>
[TestClass]
public sealed class GopherProtocolHandlerProxyConnectFailTests
{
    private const string Url = "gopher://example.test/1";

    [TestMethod]
    [DataRow(HttpFailMode.Fail, 403, false, CurlExitCode.HttpReturnedError, "The requested URL returned error: 403")]
    [DataRow(HttpFailMode.Fail, 407, false, CurlExitCode.HttpReturnedError, "The requested URL returned error: 407")]
    [DataRow(HttpFailMode.Fail, 407, true, CurlExitCode.HttpReturnedError, "HTTP response code said error")]
    [DataRow(HttpFailMode.Fail, 302, false, CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 302")]
    [DataRow(HttpFailMode.FailWithBody, 403, false, CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 403")]
    [DataRow(HttpFailMode.None, 403, false, CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 403")]
    public async Task ExecuteAsync_ProxyRefusesTheConnect_EndsAsCurlDoesAndClosesTheConnection(
        HttpFailMode fail, int status, bool authenticationProblem, CurlExitCode expectedExitCode, string expectedMessage)
    {
        var events = new TranscriptTransferEvents();
        var connector = new FakeConnector(RefusedConnect(status, authenticationProblem));

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(ContextOf(new HttpRequestOptions { Fail = fail }, events));

        Assert.AreEqual(TransferResult.Failure(expectedExitCode, expectedMessage), result);
        Assert.AreEqual("* closing connection #0", events.Transcript.Single());
        Assert.AreEqual(fail == HttpFailMode.Fail, connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoHttpOptionsAndTheProxyRefusesTheConnect_KeepsTheConnectorsExitCode()
    {
        var connector = new FakeConnector(RefusedConnect(403, authenticationProblem: false));

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(ContextOf(null, new TranscriptTransferEvents()));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 403"), result);
        Assert.IsFalse(connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    private static ConnectResult RefusedConnect(int status, bool authenticationProblem) =>
        ConnectResult.Failed(CurlExitCode.CouldntConnect, $"CONNECT tunnel failed, response {status}", null, 0, status)
            .WithProxyAuthenticationProblem(authenticationProblem);

    private static TransferContext ContextOf(HttpRequestOptions? http, ITransferEvents events) => new()
    {
        Url = CurlUrl.Parse(Url),
        Output = new MemoryStream(),
        Events = events,
        Http = http,
    };
}
