using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine.UnitTests;

/// <summary>
/// CGR-149: <c>curl_multi_cleanup</c> closes the keep-alive connections the multi handle's transfers left in its
/// pool, so none is open after it or after the <c>curl_easy_cleanup</c> that follows, and two multi handles used one
/// after the other leave none open at the end (measured with libcurl-4.dll 8.22.0: 0 open after each step).
/// </summary>
[TestClass]
public sealed class Cgr149Tests
{
    private const string KeepAliveHello = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    [TestMethod]
    public async Task DisposeAsync_MultiWhoseTransferLeftAKeepAliveConnection_ClosesIt()
    {
        ConnectionPerConnectConnector connector = new();
        await using ManagedCurlEngine engine = CreateEngine(connector);
        ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = Hello;
        multi.Add(easy);
        await PerformUntilDoneAsync(multi);
        Assert.AreEqual(CurlMultiCode.Ok, multi.Remove(easy));
        bool closedAfterRemove = connector.Connections[0].IsDisposed;

        await multi.DisposeAsync();
        bool closedAfterMultiCleanup = connector.Connections[0].IsDisposed;
        await easy.DisposeAsync();

        Assert.IsFalse(closedAfterRemove);
        Assert.IsTrue(closedAfterMultiCleanup);
        Assert.HasCount(1, connector.Connections);
    }

    [TestMethod]
    public async Task DisposeAsync_TwoMultisOneAfterTheOther_LeavesNoConnectionOpen()
    {
        ConnectionPerConnectConnector connector = new();
        await using ManagedCurlEngine engine = CreateEngine(connector);

        for (int i = 0; i < 2; i++)
        {
            ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
            await using IEasyTransfer easy = engine.CreateEasy();
            easy.Options = Hello;
            multi.Add(easy);
            await PerformUntilDoneAsync(multi);
            multi.Remove(easy);
            await multi.DisposeAsync();
        }

        Assert.HasCount(2, connector.Connections);
        Assert.IsTrue(connector.Connections.TrueForAll(connection => connection.IsDisposed));
    }

    private static EasyOptions Hello => new() { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:21700/" } };

    private static ManagedCurlEngine CreateEngine(ConnectionPerConnectConnector connector) =>
        new(TimeProvider.System, (_, shared, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            new PoolingConnector(connector, shared.Connections, configuration: null),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Failed to connect to h port 80 after 0 ms: Could not connect to server"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies)));

    private static async Task PerformUntilDoneAsync(ManagedMultiTransfer multi)
    {
        int running;
        do
        {
            (_, running) = await multi.PerformAsync(CancellationToken.None);
            await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
        while (running > 0);
    }

    /// <summary>Opens a new connection answering one keep-alive <c>200 hello</c> for each connect, and keeps them all.</summary>
    private sealed class ConnectionPerConnectConnector : IConnector
    {
        public List<QueuedConnection> Connections { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            QueuedConnection connection = new();
            connection.Enqueue(Encoding.ASCII.GetBytes(KeepAliveHello));
            Connections.Add(connection);
            return ValueTask.FromResult(ConnectResult.Connected(connection));
        }
    }
}
