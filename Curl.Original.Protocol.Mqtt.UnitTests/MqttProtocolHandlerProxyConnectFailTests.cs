using Curl.Original.Protocol.Mqtt.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Mqtt;

/// <summary>
/// Pins curl 8.22.0 when a proxy refuses the CONNECT of an <c>mqtt -p</c> tunnel (BL-6086):
/// <c>-f</c> ends exit 22, without it the connector's exit 7 stands, and either way the
/// connection is closed. Recorded with
/// <c>Record-CurlExchange.ps1 -CurlArgs -sS,-v,-p,-x,http://127.0.0.1:P,-f,mqtt://example.test/x</c>
/// and a <c>302</c>, <c>403</c> or <c>407</c> reply.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerProxyConnectFailTests
{
    public TestContext TestContext { get; set; } = null!;

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
        var events = new TranscriptTransferEvents();
        var connector = new FakeConnector(RefusedConnect(status, authenticationProblem));
        diagnostics.Arrange("fail mode", fail);
        diagnostics.Arrange("CONNECT status", status);

        TransferResult result = await new MqttProtocolHandler(connector).ExecuteAsync(ContextOf(new HttpRequestOptions { Fail = fail }, events));
        diagnostics.Act("result", result);

        diagnostics.Assert("result", TransferResult.Failure(expectedExitCode, expectedMessage), result);
        Assert.AreEqual(TransferResult.Failure(expectedExitCode, expectedMessage), result);
        CollectionAssert.AreEqual(new[] { "* closing connection #0" }, events.Transcript);
        Assert.AreEqual(fail == HttpFailMode.Fail, connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoHttpOptionsAndTheProxyRefusesTheConnect_KeepsTheConnectorsExitCode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connector = new FakeConnector(RefusedConnect(403, authenticationProblem: false));
        diagnostics.Arrange("HTTP options", null);

        TransferResult result = await new MqttProtocolHandler(connector).ExecuteAsync(ContextOf(null, new TranscriptTransferEvents()));
        diagnostics.Act("result", result);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.CouldntConnect, Refused), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntConnect, Refused), result);
        Assert.IsFalse(connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    private static ConnectResult RefusedConnect(int status, bool authenticationProblem) =>
        ConnectResult.Failed(CurlExitCode.CouldntConnect, $"CONNECT tunnel failed, response {status}", null, 0, status)
            .WithProxyAuthenticationProblem(authenticationProblem);

    private static TransferContext ContextOf(HttpRequestOptions? http, ITransferEvents events) =>
        new()
        {
            Url = CurlUrl.Parse("mqtt://example.test/x"),
            Output = new MemoryStream(),
            Events = events,
            Http = http,
        };
}
