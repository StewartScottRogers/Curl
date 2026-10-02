using System.Net;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="PoolingConnector.ConnectMultiplexedSessionAsync" />: the session a handler
/// builds over a QUIC connection is pooled under a key of its own and shared between
/// transfers to the origin up to its stream limit, as curl's official build multiplexes
/// <c>-Z</c> transfers over HTTP/3 (measured, BL-735 Notes).
/// </summary>
[TestClass]
public sealed class PoolingConnectorQuicSessionTests
{
    private readonly QuicOpeningConnector _inner = new();
    private readonly ManualTimeProvider _time = new();

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_ForThreeTransfers_CarriesThemAllOnOneQuicConnection()
    {
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();

        var first = await ConnectAsync(pool, Target());
        var second = await ConnectAsync(pool, Target() with { Events = events });
        var third = await ConnectAsync(pool, Target() with { Events = events });

        Assert.AreEqual(1, _inner.QuicConnectCount);
        Assert.IsFalse(first.IsReused);
        Assert.AreEqual(0L, first.ConnectionNumber);
        Assert.AreEqual("h3", first.ApplicationProtocol);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 5555), first.LocalEndPoint);
        Assert.IsNotNull(first.Timings);
        Assert.IsTrue(second.IsReused);
        Assert.IsTrue(third.IsReused);
        Assert.AreEqual(0L, third.ConnectionNumber);
        Assert.IsNull(third.Timings);
        CollectionAssert.AreEqual(new[] { "Multiplexed connection found", "Multiplexed connection found" }, events.Info);
        Assert.HasCount(2, events.Reused);
        Assert.AreSame(_inner.Sessions[0], first.Connection!.Session);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WhenTheStreamLimitIsReached_ReportsTheSkipAndOpensAnotherConnection()
    {
        _inner.StreamLimit = 2;
        await using var pool = CreatePool();
        await ConnectAsync(pool, Target());
        await ConnectAsync(pool, Target());
        var events = new RecordingTransferEvents();

        var third = await ConnectAsync(pool, Target() with { Events = events });

        Assert.AreEqual(2, _inner.QuicConnectCount);
        Assert.IsFalse(third.IsReused);
        Assert.AreEqual(1L, third.ConnectionNumber);
        CollectionAssert.AreEqual(new[] { "MAX_CONCURRENT_STREAMS reached, skip (2)" }, events.Info);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_NeverSharesATcpConnectionToTheSameOrigin()
    {
        await using var pool = CreatePool();
        var tcp = await pool.ConnectAsync(Target(), CancellationToken.None);
        tcp.Connection!.MarkReusable();
        await tcp.Connection.DisposeAsync();

        var quic = await ConnectAsync(pool, Target());

        Assert.IsFalse(quic.IsReused);
        Assert.AreEqual(1, _inner.QuicConnectCount);
        Assert.AreEqual(1L, quic.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_AfterTheLastTransferLeftItIntact_ReusesTheIdleSession()
    {
        await using var pool = CreatePool();
        var first = await ConnectAsync(pool, Target());
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();

        var second = await ConnectAsync(pool, Target());

        Assert.IsTrue(second.IsReused);
        Assert.AreEqual(1, _inner.QuicConnectCount);
        Assert.IsFalse(_inner.Sessions[0].IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfThePool_ClosesAnIdleSession()
    {
        var pool = CreatePool();
        var first = await ConnectAsync(pool, Target());
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();

        await pool.DisposeAsync();

        Assert.IsTrue(_inner.Sessions[0].IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WhenTheQuicConnectFails_GivesTheFailureUnnumbered()
    {
        _inner.Failure = MultiplexedConnectResult.Failed(CurlExitCode.QuicConnectError, "QUIC connect failed");
        await using var pool = CreatePool();

        var failed = await ConnectAsync(pool, Target());
        var tcp = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsNull(failed.Connection);
        Assert.AreEqual(CurlExitCode.QuicConnectError, failed.ExitCode);
        Assert.AreEqual("QUIC connect failed", failed.ErrorMessage);
        Assert.AreEqual(0L, failed.ConnectionNumber);
        Assert.AreEqual(0L, tcp.ConnectionNumber, "the TCP connect the race falls back to is still #0");
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WaitingForMultiplexing_SharesTheConnectionOnceItsHandshakeEnds()
    {
        await using var pool = CreatePool(waits: true);
        var gate = _inner.GateNext();
        var opening = ConnectAsync(pool, Target());
        var waiting = ConnectAsync(pool, Target());
        Assert.IsFalse(waiting.IsCompleted);

        gate.SetResult();
        await opening;
        var second = await waiting;

        Assert.IsTrue(second.IsReused);
        Assert.AreEqual(1, _inner.QuicConnectCount);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WaitingForAQuicConnectThatThrows_OpensItsOwnAndTheOpenerSeesTheException()
    {
        await using var pool = CreatePool(waits: true);
        var gate = _inner.GateNext();
        _inner.Exception = new InvalidOperationException("boom");
        var opening = ConnectAsync(pool, Target());
        var waiting = ConnectAsync(pool, Target());

        gate.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => opening);
        Assert.IsFalse((await waiting).IsReused);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_ForATargetThatIsNeverPooled_NeverShares()
    {
        await using var pool = CreatePool();
        var unpooled = Target() with { PoolScheme = null };

        await ConnectAsync(pool, unpooled);
        var second = await ConnectAsync(pool, unpooled);

        Assert.IsFalse(second.IsReused);
        Assert.AreEqual(2, _inner.QuicConnectCount);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_ForASessionThatIsNoConnectionSession_DoesNotShareIt()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectMultiplexedSessionAsync(Target(), _ => new ScriptedConnection([]), CancellationToken.None);

        var second = await pool.ConnectMultiplexedSessionAsync(Target(), _ => new ScriptedConnection([]), CancellationToken.None);

        Assert.IsNull(first.Connection!.Session);
        Assert.IsFalse(second.IsReused);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WhenCancelled_Throws()
    {
        await using var pool = CreatePool();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await pool.ConnectMultiplexedSessionAsync(Target(), _inner.OpenSession, new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WithoutATargetOrSessionBuilder_Throws()
    {
        await using var pool = CreatePool();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await pool.ConnectMultiplexedSessionAsync(null!, _inner.OpenSession, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await pool.ConnectMultiplexedSessionAsync(Target(), null!, CancellationToken.None));
    }

    private static ConnectTarget Target() => new("origin.example", 443, true) { PoolScheme = "https" };

    private Task<ConnectResult> ConnectAsync(PoolingConnector pool, ConnectTarget target) =>
        pool.ConnectMultiplexedSessionAsync(target, _inner.OpenSession, CancellationToken.None).AsTask();

    private PoolingConnector CreatePool(bool waits = false) => new(_inner, _time) { WaitsForMultiplexing = waits };

    /// <summary>
    /// Opens a <see cref="ScriptedConnection" /> for a TCP connect and a <see cref="QuicStub" />
    /// for a QUIC one, optionally holding the next QUIC connect until its gate opens, failing it
    /// or throwing; its <see cref="OpenSession" /> builds a <see cref="StreamLimitedSession" />.
    /// </summary>
    private sealed class QuicOpeningConnector : IConnector
    {
        private TaskCompletionSource? gate;

        public int QuicConnectCount { get; private set; }

        public int? StreamLimit { get; set; } = 100;

        public MultiplexedConnectResult? Failure { get; set; }

        public Exception? Exception { get; set; }

        public List<StreamLimitedSession> Sessions { get; } = [];

        public TaskCompletionSource GateNext()
        {
            gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return gate;
        }

        public IConnection OpenSession(IMultiplexedConnection quic)
        {
            var session = new StreamLimitedSession(StreamLimit);
            Sessions.Add(session);
            return session;
        }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(new ScriptedConnection([])));

        public async ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            var held = gate;
            var exception = Exception;
            var failure = Failure;
            gate = null;
            Exception = null;
            Failure = null;
            QuicConnectCount++;
            if (held is not null)
            {
                await held.Task;
            }

            if (exception is not null)
            {
                throw exception;
            }

            return failure ?? MultiplexedConnectResult.Connected(new QuicStub(), new ConnectTimings(1, null, 1, 1));
        }
    }

    /// <summary>A QUIC connection that only names its ALPN protocol and local endpoint.</summary>
    private sealed class QuicStub : IMultiplexedConnection
    {
        public EndPoint? RemoteEndPoint => null;

        public EndPoint? LocalEndPoint => new IPEndPoint(IPAddress.Loopback, 5555);

        public string ApplicationProtocol => "h3";

        public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A session over a QUIC connection that carries up to a given number of transfers at once.</summary>
    private sealed class StreamLimitedSession(int? limit) : IConnection, IConnectionSession
    {
        public bool IsDisposed { get; private set; }

        public int? ConcurrentTransferLimit => limit;

        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask ShutDownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
