using System.Net;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_ForThreeTransfers_CarriesThemAllOnOneQuicConnection()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("transfers", 3);
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();

        ConnectResult first, second, third;
        using (diagnostics.Phase("connect"))
        {
            first = await ConnectAsync(pool, Target());
            second = await ConnectAsync(pool, Target() with { Events = events });
            third = await ConnectAsync(pool, Target() with { Events = events });
        }

        diagnostics.Act("quic connect count", _inner.QuicConnectCount);
        diagnostics.Act("connection numbers", $"{first.ConnectionNumber},{second.ConnectionNumber},{third.ConnectionNumber}");
        diagnostics.Assert("quic connect count", 1, _inner.QuicConnectCount);
        diagnostics.Assert("first connection number", 0L, first.ConnectionNumber);
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
        var diagnostics = Diagnostics;
        _inner.StreamLimit = 2;
        diagnostics.Arrange("stream limit", _inner.StreamLimit);
        await using var pool = CreatePool();
        await ConnectAsync(pool, Target());
        await ConnectAsync(pool, Target());
        var events = new RecordingTransferEvents();

        ConnectResult third;
        using (diagnostics.Phase("third connect"))
        {
            third = await ConnectAsync(pool, Target() with { Events = events });
        }

        diagnostics.Act("quic connect count", _inner.QuicConnectCount);
        diagnostics.Act("third connection number", third.ConnectionNumber);
        diagnostics.Assert("quic connect count", 2, _inner.QuicConnectCount);
        Assert.AreEqual(2, _inner.QuicConnectCount);
        Assert.IsFalse(third.IsReused);
        Assert.AreEqual(1L, third.ConnectionNumber);
        CollectionAssert.AreEqual(new[] { "MAX_CONCURRENT_STREAMS reached, skip (2)" }, events.Info);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_NeverSharesATcpConnectionToTheSameOrigin()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("target", "https://origin.example:443");
        await using var pool = CreatePool();
        var tcp = await pool.ConnectAsync(Target(), CancellationToken.None);
        tcp.Connection!.MarkReusable();
        await tcp.Connection.DisposeAsync();

        ConnectResult quic;
        using (diagnostics.Phase("quic connect"))
        {
            quic = await ConnectAsync(pool, Target());
        }

        diagnostics.Act("quic reused", quic.IsReused);
        diagnostics.Act("quic connection number", quic.ConnectionNumber);
        diagnostics.Assert("quic connect count", 1, _inner.QuicConnectCount);
        Assert.IsFalse(quic.IsReused);
        Assert.AreEqual(1, _inner.QuicConnectCount);
        Assert.AreEqual(1L, quic.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_AfterTheLastTransferLeftItIntact_ReusesTheIdleSession()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("target", "https://origin.example:443");
        await using var pool = CreatePool();
        var first = await ConnectAsync(pool, Target());
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();

        ConnectResult second;
        using (diagnostics.Phase("second connect"))
        {
            second = await ConnectAsync(pool, Target());
        }

        diagnostics.Act("second reused", second.IsReused);
        diagnostics.Act("session disposed", _inner.Sessions[0].IsDisposed);
        diagnostics.Assert("quic connect count", 1, _inner.QuicConnectCount);
        Assert.IsTrue(second.IsReused);
        Assert.AreEqual(1, _inner.QuicConnectCount);
        Assert.IsFalse(_inner.Sessions[0].IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfThePool_ClosesAnIdleSession()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("target", "https://origin.example:443");
        var pool = CreatePool();
        var first = await ConnectAsync(pool, Target());
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();

        using (diagnostics.Phase("dispose pool"))
        {
            await pool.DisposeAsync();
        }

        diagnostics.Act("session disposed", _inner.Sessions[0].IsDisposed);
        diagnostics.Assert("session disposed", true, _inner.Sessions[0].IsDisposed);
        Assert.IsTrue(_inner.Sessions[0].IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WhenTheQuicConnectFails_GivesTheFailureUnnumbered()
    {
        var diagnostics = Diagnostics;
        _inner.Failure = MultiplexedConnectResult.Failed(CurlExitCode.QuicConnectError, "QUIC connect failed");
        diagnostics.Arrange("failure", "QUIC connect failed");
        await using var pool = CreatePool();

        ConnectResult failed, tcp;
        using (diagnostics.Phase("connect"))
        {
            failed = await ConnectAsync(pool, Target());
            tcp = await pool.ConnectAsync(Target(), CancellationToken.None);
        }

        diagnostics.Act("failed exit code", failed.ExitCode);
        diagnostics.Act("failed error message", failed.ErrorMessage);
        diagnostics.Act("tcp connection number", tcp.ConnectionNumber);
        diagnostics.Assert("failed error message", "QUIC connect failed", failed.ErrorMessage);
        Assert.IsNull(failed.Connection);
        Assert.AreEqual(CurlExitCode.QuicConnectError, failed.ExitCode);
        Assert.AreEqual("QUIC connect failed", failed.ErrorMessage);
        Assert.AreEqual(0L, failed.ConnectionNumber);
        Assert.AreEqual(0L, tcp.ConnectionNumber, "the TCP connect the race falls back to is still #0");
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WaitingForMultiplexing_SharesTheConnectionOnceItsHandshakeEnds()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("waits for multiplexing", true);
        await using var pool = CreatePool(waits: true);
        var gate = _inner.GateNext();
        var opening = ConnectAsync(pool, Target());
        var waiting = ConnectAsync(pool, Target());
        diagnostics.Act("waiting completed before gate", waiting.IsCompleted);
        Assert.IsFalse(waiting.IsCompleted);

        ConnectResult second;
        using (diagnostics.Phase("open gate"))
        {
            gate.SetResult();
            await opening;
            second = await waiting;
        }

        diagnostics.Act("second reused", second.IsReused);
        diagnostics.Assert("quic connect count", 1, _inner.QuicConnectCount);
        Assert.IsTrue(second.IsReused);
        Assert.AreEqual(1, _inner.QuicConnectCount);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WaitingForAQuicConnectThatThrows_OpensItsOwnAndTheOpenerSeesTheException()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("exception", "boom");
        await using var pool = CreatePool(waits: true);
        var gate = _inner.GateNext();
        _inner.Exception = new InvalidOperationException("boom");
        var opening = ConnectAsync(pool, Target());
        var waiting = ConnectAsync(pool, Target());

        gate.SetResult();

        InvalidOperationException thrown;
        using (diagnostics.Phase("opener"))
        {
            thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => opening);
        }

        var waited = await waiting;
        diagnostics.Act("opener exception", thrown.Message);
        diagnostics.Act("waiter reused", waited.IsReused);
        diagnostics.Assert("opener exception", "boom", thrown.Message);
        Assert.IsFalse(waited.IsReused);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_ForATargetThatIsNeverPooled_NeverShares()
    {
        var diagnostics = Diagnostics;
        await using var pool = CreatePool();
        var unpooled = Target() with { PoolScheme = null };
        diagnostics.Arrange("pool scheme", unpooled.PoolScheme ?? "(none)");

        ConnectResult second;
        using (diagnostics.Phase("connect twice"))
        {
            await ConnectAsync(pool, unpooled);
            second = await ConnectAsync(pool, unpooled);
        }

        diagnostics.Act("second reused", second.IsReused);
        diagnostics.Act("quic connect count", _inner.QuicConnectCount);
        diagnostics.Assert("quic connect count", 2, _inner.QuicConnectCount);
        Assert.IsFalse(second.IsReused);
        Assert.AreEqual(2, _inner.QuicConnectCount);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_ForASessionThatIsNoConnectionSession_DoesNotShareIt()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("session builder", "scripted connection without a session");
        await using var pool = CreatePool();
        var first = await pool.ConnectMultiplexedSessionAsync(Target(), _ => new ScriptedConnection([]), CancellationToken.None);

        ConnectResult second;
        using (diagnostics.Phase("second connect"))
        {
            second = await pool.ConnectMultiplexedSessionAsync(Target(), _ => new ScriptedConnection([]), CancellationToken.None);
        }

        diagnostics.Act("first session is null", first.Connection!.Session is null);
        diagnostics.Act("second reused", second.IsReused);
        diagnostics.Assert("second reused", false, second.IsReused);
        Assert.IsNull(first.Connection!.Session);
        Assert.IsFalse(second.IsReused);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WhenCancelled_Throws()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("cancellation", "already cancelled");
        await using var pool = CreatePool();

        OperationCanceledException thrown;
        using (diagnostics.Phase("cancelled connect"))
        {
            thrown = await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await pool.ConnectMultiplexedSessionAsync(Target(), _inner.OpenSession, new CancellationToken(canceled: true)));
        }

        diagnostics.Act("exception type", thrown.GetType().Name);
        diagnostics.Assert("exception type", nameof(OperationCanceledException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_WithoutATargetOrSessionBuilder_Throws()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("arguments", "null target, then null session builder");
        await using var pool = CreatePool();

        ArgumentNullException noTarget, noBuilder;
        using (diagnostics.Phase("null arguments"))
        {
            noTarget = await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await pool.ConnectMultiplexedSessionAsync(null!, _inner.OpenSession, CancellationToken.None));
            noBuilder = await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await pool.ConnectMultiplexedSessionAsync(Target(), null!, CancellationToken.None));
        }

        diagnostics.Act("null target parameter", noTarget.ParamName);
        diagnostics.Act("null builder parameter", noBuilder.ParamName);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), noTarget.GetType().Name);
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
