using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// STD-227: under <c>-f</c> a CONNECT refused with a <c>407</c> curl gives up on (its
/// <c>authproblem</c>) ends with exit 22 and curl 8.22.0's generic text
/// <c>HTTP response code said error</c>, not <c>The requested URL returned error: 407</c>
/// (measured, BL-6024 Notes, ADR-1465).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_FailAndProxyAuthenticationProblem_IsExit22WithTheGenericText()
    {
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 407", null, 0, 407).WithProxyAuthenticationProblem(true));
        TransferContext context = ProgressContext("https://example.com/", new RecordingTransferProgress(), new HttpRequestOptions { Fail = HttpFailMode.Fail });

        TransferResult result = await Handler(connector).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("HTTP response code said error", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(HttpFailMode.Fail, true, DisplayName = "-f")]
    [DataRow(HttpFailMode.FailWithBody, false, DisplayName = "--fail-with-body")]
    [DataRow(HttpFailMode.None, false, DisplayName = "neither")]
    public async Task ExecuteAsync_FailMode_TellsTheConnectorWhetherAnErrorStatusFails(HttpFailMode mode, bool expected)
    {
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 403", null, 0, 403));
        TransferContext context = ProgressContext("https://example.com/", new RecordingTransferProgress(), new HttpRequestOptions { Fail = mode });

        await Handler(connector).ExecuteAsync(context);

        Assert.AreEqual(expected, connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailAndProxyRefuses407WithoutAuthenticationProblem_NamesTheStatus()
    {
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 407", null, 0, 407));
        TransferContext context = ProgressContext("https://example.com/", new RecordingTransferProgress(), new HttpRequestOptions { Fail = HttpFailMode.Fail });

        TransferResult result = await Handler(connector).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 407", result.ErrorMessage);
    }
}
