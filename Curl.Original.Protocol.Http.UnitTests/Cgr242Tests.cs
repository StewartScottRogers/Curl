using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// CGR-242: a connect that failed after the proxy answered the CONNECT is past connect, so the
/// transfer is reported started and the progress meter is drawn before the error, as curl 8.22.0 does.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_ConnectRefusedByProxyWith403_ReportsTheTransferStarted()
    {
        RecordingTransferProgress progress = new();
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 403", null, 0, 403));
        TransferContext context = ProgressContext("http://example.com/", progress, new HttpRequestOptions());

        TransferResult result = await Handler(connector).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "started" }, progress.Reports.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFailsBeforeAnyProxyAnswer_ReportsNothing()
    {
        RecordingTransferProgress progress = new();
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));
        TransferContext context = ProgressContext("http://example.com/", progress, new HttpRequestOptions());

        await Handler(connector).ExecuteAsync(context);

        Assert.IsEmpty(progress.Reports);
    }
}
