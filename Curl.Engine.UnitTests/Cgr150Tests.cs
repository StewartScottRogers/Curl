using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine.UnitTests;

/// <summary>
/// CGR-150: under <c>CURLMOPT_MAX_TOTAL_CONNECTIONS</c> a multi handle counts its idle cached connections against the
/// limit, closing the oldest idle one before a transfer opens a new connection, unless one to the transfer's host and
/// port waits to be reused, as libcurl 8.22.0's <c>Curl_cpool_check_limits</c> does.
/// </summary>
[TestClass]
public sealed class Cgr150Tests
{
    private const string Hello = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    [TestMethod]
    public async Task PerformAsync_MaxTotalOneAndASecondHostAfterTheFirstFinished_ClosesTheFirstConnectionBeforeConnecting()
    {
        CountingConnector connector = new();

        await PerformOneAfterAnotherAsync(connector, 1, "http://a.example:21700/a", "http://b.example:21701/b");

        CollectionAssert.AreEqual(new[] { 0, 0 }, connector.OpenAtConnect);
    }

    [TestMethod]
    public async Task PerformAsync_NoLimitAndASecondHostAfterTheFirstFinished_KeepsTheFirstConnectionOpen()
    {
        CountingConnector connector = new();

        await PerformOneAfterAnotherAsync(connector, 0, "http://a.example:21700/a", "http://b.example:21701/b");

        CollectionAssert.AreEqual(new[] { 0, 1 }, connector.OpenAtConnect);
    }

    [TestMethod]
    public async Task PerformAsync_MaxTotalOneAndTheSameHostAgain_ReusesTheIdleConnection()
    {
        CountingConnector connector = new();

        await PerformOneAfterAnotherAsync(connector, 1, "http://a.example:21700/a0", "http://a.example:21700/a1");

        Assert.AreEqual(1, connector.Connects);
    }

    [TestMethod]
    public async Task PerformAsync_MaxTotalOneAndFourHandlesAlternatingTwoHosts_OpensFourConnectionsNeverTwoAtOnce()
    {
        CountingConnector connector = new();
        await using ManagedCurlEngine engine = CreateEngine(connector);
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxTotalConnections = 1 };
        List<IEasyTransfer> transfers = [];
        foreach (string url in new[] { "http://a.example:21700/a0", "http://b.example:21701/b0", "http://a.example:21700/a1", "http://b.example:21701/b1" })
        {
            IEasyTransfer transfer = engine.CreateEasy();
            transfer.Options = new EasyOptions { Network = new() { Url = url } };
            transfers.Add(transfer);
            multi.Add(transfer);
        }

        await PerformUntilDoneAsync(multi);

        Assert.AreEqual(4, connector.Connects);
        CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, connector.OpenAtConnect);
        foreach (IEasyTransfer transfer in transfers)
        {
            await transfer.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task PerformAsync_MaxTotalSet_AsksToMakeRoomWithTheLimitHostAndPort()
    {
        List<(long Limit, string Host, int Port)> asked = [];
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = RecordingMulti(engine, asked);
        multi.Options = new MultiOptions { MaxTotalConnections = 3 };
        await using IEasyTransfer transfer = engine.CreateEasy();
        transfer.Options = new EasyOptions { Network = new() { Url = "unsupported://h.example:9/" } };

        multi.Add(transfer);
        await PerformUntilDoneAsync(multi);

        Assert.AreEqual((3L, "h.example", 9), asked.Single());
    }

    [TestMethod]
    public async Task PerformAsync_MaxTotalSetAndAUrlThatDoesNotParse_AsksToMakeRoomWithNoHost()
    {
        List<(long Limit, string Host, int Port)> asked = [];
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = RecordingMulti(engine, asked);
        multi.Options = new MultiOptions { MaxTotalConnections = 1 };
        await using IEasyTransfer transfer = engine.CreateEasy();
        transfer.Options = new EasyOptions { Network = new() { Url = "not a url" } };

        multi.Add(transfer);
        await PerformUntilDoneAsync(multi);

        Assert.AreEqual((1L, string.Empty, 0), asked.Single());
    }

    [TestMethod]
    public async Task PerformAsync_NoLimit_NeverAsksToMakeRoom()
    {
        List<(long Limit, string Host, int Port)> asked = [];
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = RecordingMulti(engine, asked);
        await using IEasyTransfer transfer = engine.CreateEasy();
        transfer.Options = new EasyOptions { Network = new() { Url = "unsupported://h.example:9/" } };

        multi.Add(transfer);
        await PerformUntilDoneAsync(multi);

        Assert.IsEmpty(asked);
    }

    [TestMethod]
    public async Task CloseIdleToMakeRoomAsync_IdleConnectionToAnotherHost_ClosesIt()
    {
        ConnectionCache cache = new(TimeProvider.System);
        CountingConnector connector = new();
        PoolingConnector pool = new(connector, cache, configuration: null);
        await ConnectAndReturnAsync(pool, "a.example", 80);

        await cache.CloseIdleToMakeRoomAsync(1, "b.example", 80);

        Assert.AreEqual(0, connector.Open);
    }

    [TestMethod]
    public async Task CloseIdleToMakeRoomAsync_IdleConnectionToTheSameHostInAnotherCase_KeepsItForReuse()
    {
        ConnectionCache cache = new(TimeProvider.System);
        CountingConnector connector = new();
        PoolingConnector pool = new(connector, cache, configuration: null);
        await ConnectAndReturnAsync(pool, "a.example", 80);

        await cache.CloseIdleToMakeRoomAsync(1, "A.EXAMPLE", 80);

        Assert.AreEqual(1, connector.Open);
    }

    [TestMethod]
    public async Task CloseIdleToMakeRoomAsync_IdleConnectionToTheSameHostOnAnotherPort_ClosesIt()
    {
        ConnectionCache cache = new(TimeProvider.System);
        CountingConnector connector = new();
        PoolingConnector pool = new(connector, cache, configuration: null);
        await ConnectAndReturnAsync(pool, "a.example", 80);

        await cache.CloseIdleToMakeRoomAsync(1, "a.example", 81);

        Assert.AreEqual(0, connector.Open);
    }

    [TestMethod]
    public async Task CloseIdleToMakeRoomAsync_LimitLeavingRoom_KeepsTheIdleConnection()
    {
        ConnectionCache cache = new(TimeProvider.System);
        CountingConnector connector = new();
        PoolingConnector pool = new(connector, cache, configuration: null);
        await ConnectAndReturnAsync(pool, "a.example", 80);

        await cache.CloseIdleToMakeRoomAsync(2, "b.example", 80);

        Assert.AreEqual(1, connector.Open);
    }

    private static async Task ConnectAndReturnAsync(PoolingConnector pool, string host, int port)
    {
        ConnectResult result = await pool.ConnectAsync(new ConnectTarget(host, port, false) { PoolScheme = "http" }, CancellationToken.None);
        result.Connection!.MarkReusable();
        await result.Connection.DisposeAsync();
    }

    private static ManagedMultiTransfer RecordingMulti(ManagedCurlEngine engine, List<(long Limit, string Host, int Port)> asked) =>
        new(engine.Shared.MultiOwners, TimeProvider.System, SocketWaitPoller.Instance, () => ValueTask.CompletedTask, _ => ValueTask.CompletedTask, () => { })
        {
            CloseIdleConnectionsToMakeRoom = (limit, host, port) =>
            {
                asked.Add((limit, host, port));
                return ValueTask.CompletedTask;
            },
        };

    private static async Task PerformOneAfterAnotherAsync(CountingConnector connector, long maxTotalConnections, string firstUrl, string secondUrl)
    {
        await using ManagedCurlEngine engine = CreateEngine(connector);
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxTotalConnections = maxTotalConnections };
        await using IEasyTransfer first = engine.CreateEasy();
        await using IEasyTransfer second = engine.CreateEasy();
        first.Options = new EasyOptions { Network = new() { Url = firstUrl } };
        second.Options = new EasyOptions { Network = new() { Url = secondUrl } };

        multi.Add(first);
        await PerformUntilDoneAsync(multi);
        multi.Add(second);
        await PerformUntilDoneAsync(multi);

        Assert.AreEqual(200L, second.Info.ResponseCode);
    }

    private static ManagedCurlEngine CreateEngine(CountingConnector connector) =>
        new(TimeProvider.System, (_, shared, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            new PoolingConnector(connector, shared.Connections, configuration: null),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Failed to connect to h port 80 after 0 ms: Could not connect to server"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies)));

    private static async Task PerformUntilDoneAsync(IMultiTransfer multi)
    {
        int running;
        do
        {
            (_, running) = await multi.PerformAsync(CancellationToken.None);
            await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
        while (running > 0);
    }

    /// <summary>
    /// Gives every connect a connection of its own that answers each request with <see cref="Hello" /> and keeps the
    /// connection open, and records how many of its connections were still open at each connect.
    /// </summary>
    private sealed class CountingConnector : IConnector
    {
        private int open;

        public ConcurrentQueue<int> OpenAtConnectQueue { get; } = new();

        public int[] OpenAtConnect => [.. OpenAtConnectQueue];

        public int Connects => OpenAtConnectQueue.Count;

        public int Open => Volatile.Read(ref open);

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            OpenAtConnectQueue.Enqueue(Open);
            Interlocked.Increment(ref open);
            return ValueTask.FromResult(ConnectResult.Connected(new AnsweringConnection(() => Interlocked.Decrement(ref open))));
        }
    }

    private sealed class AnsweringConnection(Action closed) : IConnection
    {
        private readonly Queue<byte> pending = new();
        private readonly StringBuilder request = new();
        private bool disposed;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int count = Math.Min(pending.Count, buffer.Length);
            for (int index = 0; index < count; index++)
            {
                buffer.Span[index] = pending.Dequeue();
            }

            return ValueTask.FromResult(count);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            request.Append(Encoding.Latin1.GetString(buffer.Span));
            if (request.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                request.Clear();
                foreach (byte b in Encoding.Latin1.GetBytes(Hello))
                {
                    pending.Enqueue(b);
                }
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                disposed = true;
                closed();
            }

            return ValueTask.CompletedTask;
        }
    }
}
