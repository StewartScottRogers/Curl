using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-171 (ADR-0866): under <c>-Z</c>, once a host's first connection has shown it carries one
/// transfer at a time (HTTP/1.1), curl 8.22.0 opens the host's waiting transfers side by side; a
/// <see cref="PoolingConnector" /> that waits for multiplexing stops waiting for a connection
/// being opened to that key, so the second waiter no longer runs after the first has ended.
/// </summary>
[TestClass]
public sealed class Cgr171Tests
{
    private readonly GatedConnector _inner = new();
    private readonly ManualTimeProvider _time = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_AfterTheKeysFirstConnectionHeldAnHttp1Session_OpensItsOwnBesideAConnectionBeingOpened()
    {
        await using var pool = new PoolingConnector(_inner, _time) { WaitsForMultiplexing = true };
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new OneTransferSession());
        first.Connection.TryHoldSession(new OneTransferSession());
        await first.Connection.DisposeAsync();

        var gate = _inner.GateNext();
        var opening = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();
        var opened = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();
        var openedBesideTheOneBeingOpened = opened.IsCompleted;
        gate.SetResult();
        var second = await opening;
        await second.Connection!.DisposeAsync();
        var third = await opened;

        Diagnostics.Arrange("first connection", "held an HTTP/1 session and closed");
        Diagnostics.Act("third opened beside the second", openedBesideTheOneBeingOpened);
        Diagnostics.Assert("third opened beside the second", true, openedBesideTheOneBeingOpened);
        Assert.IsTrue(openedBesideTheOneBeingOpened);
        Assert.IsFalse(third.IsReused);
        Assert.AreEqual(2L, second.ConnectionNumber);
        Assert.AreEqual(1L, third.ConnectionNumber);
        Assert.AreEqual(3, _inner.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_ForAnotherKey_StillWaitsForAConnectionBeingOpened()
    {
        await using var pool = new PoolingConnector(_inner, _time) { WaitsForMultiplexing = true };
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.TryHoldSession(new OneTransferSession());
        await first.Connection.DisposeAsync();

        var gate = _inner.GateNext();
        var opening = pool.ConnectAsync(Target(8080), CancellationToken.None).AsTask();
        var waiting = pool.ConnectAsync(Target(8080), CancellationToken.None).AsTask();
        gate.SetResult();
        await opening;
        var waitedBeforeTheOpenedOneEnded = !waiting.IsCompleted;

        Diagnostics.Arrange("first connection", "HTTP/1 on port 80");
        Diagnostics.Act("waiter on port 8080 still waiting", waitedBeforeTheOpenedOneEnded);
        Diagnostics.Assert("waiter on port 8080 still waiting", true, waitedBeforeTheOpenedOneEnded);
        Assert.IsTrue(waitedBeforeTheOpenedOneEnded);
        await (await opening).Connection!.DisposeAsync();
        await waiting;
    }

    private static ConnectTarget Target(int port = 80) =>
        new("origin.example", port, false) { PoolScheme = "http" };

    /// <summary>An HTTP/1.x session: one transfer at a time.</summary>
    private sealed class OneTransferSession : IConnectionSession
    {
        public int? ConcurrentTransferLimit => null;

        public ValueTask ShutDownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    /// <summary>Opens a <see cref="ScriptedConnection" /> for every call, holding the next one until its gate opens.</summary>
    private sealed class GatedConnector : IConnector
    {
        private TaskCompletionSource? gate;

        public int Opened { get; private set; }

        public TaskCompletionSource GateNext()
        {
            gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return gate;
        }

        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            var held = gate;
            gate = null;
            if (held is not null)
            {
                await held.Task;
            }

            Opened++;
            return ConnectResult.Connected(new ScriptedConnection([]), new ConnectTimings(1, null, 1, null));
        }
    }
}
