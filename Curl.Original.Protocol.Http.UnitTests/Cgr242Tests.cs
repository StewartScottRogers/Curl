using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
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
    public async Task ExecuteAsync_FailAndProxyRefusesConnectWith403_IsExit22()
    {
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 403", null, 0, 403));
        TransferContext context = ProgressContext("http://example.com/", new RecordingTransferProgress(), new HttpRequestOptions { Fail = HttpFailMode.Fail });

        TransferResult result = await Handler(connector).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 403", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailWithBodyAndProxyRefusesConnect_StaysExit7()
    {
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 403", null, 0, 403));
        TransferContext context = ProgressContext("http://example.com/", new RecordingTransferProgress(), new HttpRequestOptions { Fail = HttpFailMode.FailWithBody });

        TransferResult result = await Handler(connector).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
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

/// <summary>STD-145: a connect that failed after the TCP connect, or timed out after the name resolved, counts one connection.</summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_ConnectFailedAfterTheTcpConnect_CountsOneConnection()
    {
        ConnectResult failure = ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "certificate refused", new ConnectTimings(1, 2, 3, null));
        TransferResult result = await Handler(new QueueConnector(failure)).ExecuteAsync(ProgressContext("http://example.com/", new RecordingTransferProgress(), new HttpRequestOptions()));

        Assert.AreEqual(1, result.Report!.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTimedOutAfterTheNameResolved_CountsOneConnection()
    {
        ConnectResult failure = ConnectResult.Failed(CurlExitCode.OperationTimedOut, "timed out", new ConnectTimings(1, 2, null, null));
        TransferResult result = await Handler(new QueueConnector(failure)).ExecuteAsync(ProgressContext("http://example.com/", new RecordingTransferProgress(), new HttpRequestOptions()));

        Assert.AreEqual(1, result.Report!.ConnectionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefusedAfterTheNameResolved_CountsNoConnection()
    {
        ConnectResult failure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused", new ConnectTimings(1, 2, null, null));
        TransferResult result = await Handler(new QueueConnector(failure)).ExecuteAsync(ProgressContext("http://example.com/", new RecordingTransferProgress(), new HttpRequestOptions()));

        Assert.AreEqual(0, result.Report!.ConnectionCount);
    }
}
