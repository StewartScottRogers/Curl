using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins that the target an HTTP transfer hands the connector carries the transfer's diagnostic
/// log, so the connector's <c>dns</c>, <c>connect</c>, <c>proxy</c> and <c>tls</c> lines reach
/// <c>--log-level</c> (BL-5935, ADR-0222).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_DirectRequest_HandsTheConnectorTheTransfersDiagnosticLog()
    {
        await AssertConnectTargetCarriesTheDiagnosticLogAsync(options: null, expectsForwardProxy: false);
    }

    [TestMethod]
    public async Task ExecuteAsync_ForwardProxyRequest_HandsTheConnectorTheTransfersDiagnosticLog()
    {
        HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18183, null) };

        await AssertConnectTargetCarriesTheDiagnosticLogAsync(options, expectsForwardProxy: true);
    }

    private async Task AssertConnectTargetCarriesTheDiagnosticLogAsync(HttpRequestOptions? options, bool expectsForwardProxy)
    {
        RecordingDiagnosticLog log = new();
        QueueConnector connector = QueueConnector.For(Connection(Head + "hello", 65536));
        Diagnostics.Arrange("url, forward proxy", $"{LogUrl}, {expectsForwardProxy}");

        TransferResult result = await Handler(connector).ExecuteAsync(LogContext(log, options));

        WriteResult(result);
        ConnectTarget target = connector.Targets.Single();
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("forward proxy target", expectsForwardProxy, target.IsForwardProxy);
        Assert.AreEqual(expectsForwardProxy, target.IsForwardProxy);
        Diagnostics.Assert("target carries the transfer's log", true, ReferenceEquals(log, target.DiagnosticLog));
        Assert.AreSame(log, target.DiagnosticLog);
    }
}
