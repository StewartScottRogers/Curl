using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="PoolingConnector" /> sharing one connection between transfers whose
/// session multiplexes them, and waiting for a connection that may yet multiplex
/// (<see cref="PoolingConnector.WaitsForMultiplexing" />), as curl 8.21.0 multiplexes
/// <c>-Z</c> transfers over HTTP/2 (measured, BL-717 Notes).
/// </summary>
[TestClass]
public sealed class PoolingConnectorMultiplexingTests
{
    private readonly GatedConnector _inner = new();
    private readonly ManualTimeProvider _time = new();

    [TestMethod]
    public async Task ConnectAsync_WhileASessionHasStreamsToSpare_SharesItsConnection()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        Assert.IsTrue(first.Connection!.TryHoldSession(new LimitedSession(100)));
        var events = new RecordingTransferEvents();
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        var second = await pool.ConnectAsync(Target() with { Events = events, DiagnosticLog = log }, CancellationToken.None);

        Assert.HasCount(1, _inner.Opened);
        Assert.IsTrue(second.IsReused);
        Assert.AreEqual(0L, second.ConnectionNumber);
        Assert.IsNull(second.Timings);
        CollectionAssert.AreEqual(new[] { "Multiplexed connection found" }, events.Info);
        Assert.HasCount(1, events.Reused);
        Assert.IsTrue(first.Connection.IsSharedWithAnotherTransfer);
        Assert.IsTrue(second.Connection!.IsSharedWithAnotherTransfer);
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Verbose, "connect"), "pool http://origin.example:80: sharing multiplexed connection #0");
    }

    [TestMethod]
    public async Task ConnectAsync_ForThreeTransfers_CarriesThemAllOnOneConnection()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(100));

        var second = await pool.ConnectAsync(Target(), CancellationToken.None);
        var third = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.HasCount(1, _inner.Opened);
        Assert.IsTrue(second.IsReused);
        Assert.IsTrue(third.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSessionsStreamsAreAllTaken_ReportsTheSkipAndOpensAnother()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(1));
        var events = new RecordingTransferEvents();

        var second = await pool.ConnectAsync(Target() with { Events = events }, CancellationToken.None);

        Assert.HasCount(2, _inner.Opened);
        Assert.IsFalse(second.IsReused);
        Assert.AreEqual(1L, second.ConnectionNumber);
        CollectionAssert.AreEqual(new[] { "MAX_CONCURRENT_STREAMS reached, skip (1)" }, events.Info);
        Assert.IsFalse(first.Connection.IsSharedWithAnotherTransfer);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSessionTakesNoNewStream_OpensAnotherWithoutTheSkipLine()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(0));
        var events = new RecordingTransferEvents();

        var second = await pool.ConnectAsync(Target() with { Events = events }, CancellationToken.None);

        Assert.IsFalse(second.IsReused);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSessionCarriesOneTransferAtATime_OpensAnother()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(null));

        var second = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsFalse(second.IsReused);
        Assert.HasCount(2, _inner.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_ForAnotherKey_DoesNotShare()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(100));

        var second = await pool.ConnectAsync(Target(port: 8080), CancellationToken.None);

        Assert.IsFalse(second.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSharedConnectionFoundTheServersClose_OpensAnother()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(100));
        _ = await first.Connection.ReadAsync(new byte[1], CancellationToken.None);

        var second = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsFalse(second.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_ForATargetThatIsNeverPooled_NeverShares()
    {
        await using var pool = CreatePool();
        var target = new ConnectTarget("origin.example", 80, UseTls: false);
        var first = await pool.ConnectAsync(target, CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(100));

        var second = await pool.ConnectAsync(target, CancellationToken.None);

        Assert.IsFalse(second.IsReused);
        Assert.IsFalse(first.Connection.IsSharedWithAnotherTransfer);
    }

    [TestMethod]
    public async Task DisposeAsync_OfOneOfTwoLeases_KeepsTheConnectionOpenAndOutOfTheIdlePool()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(100));
        var second = await pool.ConnectAsync(Target(), CancellationToken.None);

        first.Connection.MarkReusable();
        await first.Connection.DisposeAsync();

        Assert.IsFalse(_inner.Opened[0].IsDisposed);
        Assert.IsFalse(second.Connection!.IsSharedWithAnotherTransfer);
        var third = await pool.ConnectAsync(Target(), CancellationToken.None);
        Assert.IsTrue(third.IsReused);
        Assert.HasCount(1, _inner.Opened);
    }

    [TestMethod]
    public async Task IsSharedWithAnotherTransfer_AfterItsLeaseEnded_KeepsWhatItWasWhenItEnded()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(100));
        var second = await pool.ConnectAsync(Target(), CancellationToken.None);

        await first.Connection.DisposeAsync();
        await second.Connection!.DisposeAsync();

        Assert.IsTrue(first.Connection.IsSharedWithAnotherTransfer);
        Assert.IsFalse(second.Connection.IsSharedWithAnotherTransfer);
    }

    [TestMethod]
    public async Task DisposeAsync_OfTheLastLease_ReturnsTheConnectionToThePool()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(1));
        first.Connection.MarkReusable();
        await first.Connection.DisposeAsync();

        var again = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsTrue(again.IsReused);
        Assert.HasCount(1, _inner.Opened);
    }

    [TestMethod]
    public async Task DisposeAsync_OfALeaseNotMarkedReusable_StopsSharingAndClosesAfterTheLast()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new LimitedSession(100));
        var second = await pool.ConnectAsync(Target(), CancellationToken.None);

        await first.Connection.DisposeAsync();
        var third = await pool.ConnectAsync(Target(), CancellationToken.None);
        second.Connection!.MarkReusable();
        await second.Connection.DisposeAsync();

        Assert.IsFalse(third.IsReused);
        Assert.IsTrue(_inner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WaitingForAConnectionBeingOpened_SharesItOnceItHoldsASession()
    {
        await using var pool = CreatePool(waits: true);
        var gate = _inner.GateNext();
        var opening = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();
        var waiting = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();

        gate.SetResult();
        var first = await opening;
        Assert.IsFalse(waiting.IsCompleted);
        first.Connection!.TryHoldSession(new LimitedSession(100));
        var second = await waiting;

        Assert.IsTrue(second.IsReused);
        Assert.HasCount(1, _inner.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_WithoutWaiting_OpensItsOwnBesideAConnectionBeingOpened()
    {
        await using var pool = CreatePool();
        var gate = _inner.GateNext();
        var opening = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();

        var second = await pool.ConnectAsync(Target(), CancellationToken.None);
        gate.SetResult();
        var first = await opening;

        Assert.IsFalse(second.IsReused);
        Assert.AreEqual(0L, second.ConnectionNumber);
        Assert.AreEqual(1L, first.ConnectionNumber);
        Assert.HasCount(2, _inner.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_WaitingForATlsConnectionThatAgreedHttp11_OpensItsOwnOnceItConnects()
    {
        await using var pool = CreatePool(waits: true);
        _inner.ApplicationProtocol = "http/1.1";
        var gate = _inner.GateNext();
        var opening = pool.ConnectAsync(Target(useTls: true), CancellationToken.None).AsTask();
        var waiting = pool.ConnectAsync(Target(useTls: true), CancellationToken.None).AsTask();

        gate.SetResult();
        _ = await opening;
        var second = await waiting;

        Assert.IsFalse(second.IsReused);
        Assert.HasCount(2, _inner.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_ForANewConnectionThatAgreedH2_GivesTheAgreedProtocol()
    {
        await using var pool = CreatePool(waits: false);
        _inner.ApplicationProtocol = "h2";

        var connect = await pool.ConnectAsync(Target(useTls: true), CancellationToken.None);

        Assert.AreEqual("h2", connect.ApplicationProtocol);
    }

    [TestMethod]
    public async Task ConnectAsync_WaitingForATlsConnectionThatAgreedH2_WaitsForItsSession()
    {
        await using var pool = CreatePool(waits: true);
        _inner.ApplicationProtocol = "h2";
        var first = await pool.ConnectAsync(Target(useTls: true), CancellationToken.None);
        var waiting = pool.ConnectAsync(Target(useTls: true), CancellationToken.None).AsTask();

        Assert.IsFalse(waiting.IsCompleted);
        first.Connection!.TryHoldSession(new LimitedSession(100));
        var second = await waiting;

        Assert.IsTrue(second.IsReused);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ConnectAsync_WaitingForAPlainConnectionThroughAProxy_OpensItsOwnOnceItConnects(bool isForwardProxy)
    {
        await using var pool = CreatePool(waits: true);
        var target = isForwardProxy
            ? Target() with { IsForwardProxy = true }
            : Target() with { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) };
        _ = await pool.ConnectAsync(target, CancellationToken.None);

        var second = await pool.ConnectAsync(target, CancellationToken.None);

        Assert.IsFalse(second.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_WaitingForAConnectionThatHoldsNoSession_ReusesItOnceItIsReturned()
    {
        await using var pool = CreatePool(waits: true);
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        var waiting = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();

        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();
        var second = await waiting;

        Assert.IsTrue(second.IsReused);
        Assert.HasCount(1, _inner.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_WaitingForAConnectThatFails_OpensItsOwn()
    {
        await using var pool = CreatePool(waits: true);
        _inner.Failure = ConnectResult.Refused("Failed to connect");
        var gate = _inner.GateNext();
        var opening = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();
        var waiting = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();

        gate.SetResult();
        var failed = await opening;
        var second = await waiting;

        Assert.IsTrue(failed.IsConnectionRefused);
        Assert.IsNotNull(second.Connection);
        Assert.AreEqual(1L, second.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectAsync_WaitingForAConnectThatThrows_OpensItsOwnAndTheOpenerSeesTheException()
    {
        await using var pool = CreatePool(waits: true);
        _inner.Exception = new IOException("dial failed");
        var gate = _inner.GateNext();
        var opening = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();
        var waiting = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();

        gate.SetResult();
        await Assert.ThrowsExactlyAsync<IOException>(() => opening);
        var second = await waiting;

        Assert.IsNotNull(second.Connection);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenCancelledWhileWaiting_Throws()
    {
        await using var pool = CreatePool(waits: true);
        _ = await pool.ConnectAsync(Target(), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = pool.ConnectAsync(Target(), cancellation.Token).AsTask();

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
    }

    private static ConnectTarget Target(int port = 80, bool useTls = false) =>
        new("origin.example", port, useTls) { PoolScheme = useTls ? "https" : "http" };

    private PoolingConnector CreatePool(bool waits = false) => new(_inner, _time) { WaitsForMultiplexing = waits };

    /// <summary>A session that carries up to a given number of transfers at once.</summary>
    private sealed class LimitedSession(int? limit) : IConnectionSession
    {
        public int? ConcurrentTransferLimit => limit;

        public ValueTask ShutDownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Opens a <see cref="ScriptedConnection" /> for every call, optionally holding the next
    /// connect until its gate opens, agreeing a given ALPN protocol, or failing.
    /// </summary>
    private sealed class GatedConnector : IConnector
    {
        private TaskCompletionSource? gate;

        public List<ScriptedConnection> Opened { get; } = [];

        public string? ApplicationProtocol { get; set; }

        public ConnectResult? Failure { get; set; }

        public Exception? Exception { get; set; }

        public TaskCompletionSource GateNext()
        {
            gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return gate;
        }

        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            var held = gate;
            var exception = Exception;
            var failure = Failure;
            gate = null;
            Exception = null;
            Failure = null;
            if (held is not null)
            {
                await held.Task;
            }

            if (exception is not null)
            {
                throw exception;
            }

            if (failure is not null)
            {
                return failure;
            }

            var connection = new ScriptedConnection([]);
            Opened.Add(connection);
            return ConnectResult.Connected(connection, new ConnectTimings(1, null, 1, null), applicationProtocol: ApplicationProtocol);
        }
    }
}
