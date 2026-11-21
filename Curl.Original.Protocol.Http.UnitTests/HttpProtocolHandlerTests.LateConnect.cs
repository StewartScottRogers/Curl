using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// A connect that ignores its token and finishes after the connect timeout (BL-5666).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_ConnectorIgnoresItsTokenAndConnectsAfterTheTimeout_FailsWithExit28AndDisposesTheLateConnection()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        LateConnector connector = new();
        ScriptedConnection lateConnection = new([], 1);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://10.255.255.1/"),
            Output = new MemoryStream(),
            TimeProvider = time,
            ConnectTimeout = TimeSpan.FromSeconds(1),
        };
        Diagnostics.Arrange("connect timeout ms", 1000);

        Task<TransferResult> transfer = new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(context).AsTask();
        await connector.Started;
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult result = await transfer;
        connector.Finish(ConnectResult.Connected(lateConnection));
        await lateConnection.Disposed;

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.IsTrue(lateConnection.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeLateConnectionAsync_ConnectEndsWithAConnection_DisposesIt()
    {
        ScriptedConnection connection = new([], 1);
        Diagnostics.Arrange("late connect", "connected");

        await HttpTransferDeadline.DisposeLateConnectionAsync(Task.FromResult(ConnectResult.Connected(connection)));

        Diagnostics.Assert("disposed", true, connection.IsDisposed);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeLateConnectionAsync_ConnectEndsWithoutAConnection_Returns()
    {
        Diagnostics.Arrange("late connect", "failed");

        await HttpTransferDeadline.DisposeLateConnectionAsync(
            Task.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused")));

        Diagnostics.Assert("returned", true, true);
    }

    [TestMethod]
    public async Task DisposeLateConnectionAsync_ConnectFaults_ObservesTheFault()
    {
        Diagnostics.Arrange("late connect", "faulted");

        await HttpTransferDeadline.DisposeLateConnectionAsync(Task.FromException<ConnectResult>(new IOException("late")));

        Diagnostics.Assert("returned", true, true);
    }
}
