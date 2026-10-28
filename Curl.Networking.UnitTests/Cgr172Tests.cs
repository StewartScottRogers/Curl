using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-172 (ADR-0937): under <c>-Z</c>, a transfer the host queue frees while the host's first
/// plain TCP connection is still busy opens a connection of its own, as curl 8.22.0 does, when the
/// option group speaks HTTP/1.x over cleartext (<see cref="PoolingConnector.PlainTcpMayMultiplex" />
/// off); with HTTP/2 over cleartext it still waits for that connection's session.
/// </summary>
[TestClass]
public sealed class Cgr172Tests
{
    private readonly CountingConnector _inner = new();
    private readonly ManualTimeProvider _time = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_PlainTcpThatCannotMultiplexIsBusy_OpensItsOwn()
    {
        await using var pool = new PoolingConnector(_inner, _time) { WaitsForMultiplexing = true, PlainTcpMayMultiplex = false };
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);

        var second = await pool.ConnectAsync(Target(), CancellationToken.None);
        Diagnostics.Arrange("first connection", "plain TCP, in use, no session yet; HTTP/1.x group");
        Diagnostics.Act("connections opened while the first is in use", _inner.Opened);

        Diagnostics.Assert("connections opened while the first is in use", 2, _inner.Opened);
        Assert.AreEqual(2, _inner.Opened);
        Assert.IsFalse(second.IsReused);
        Assert.AreEqual(1L, second.ConnectionNumber);
        await first.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task ConnectAsync_PlainTcpThatMayMultiplexIsBusy_WaitsForItsSession()
    {
        await using var pool = new PoolingConnector(_inner, _time) { WaitsForMultiplexing = true };
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);

        var second = pool.ConnectAsync(Target(), CancellationToken.None).AsTask();
        bool waited = !second.IsCompleted;
        Diagnostics.Arrange("first connection", "plain TCP, in use, no session yet; HTTP/2 over cleartext");
        Diagnostics.Act("second waited", waited);
        int openedWhileWaiting = _inner.Opened;
        await first.Connection!.DisposeAsync();
        await second;

        Diagnostics.Assert("second waited", true, waited);
        Assert.IsTrue(waited);
        Assert.AreEqual(1, openedWhileWaiting);
    }

    private static ConnectTarget Target() =>
        new("origin.example", 80, false) { PoolScheme = "http" };

    /// <summary>Opens a <see cref="ScriptedConnection" /> for every call and counts them.</summary>
    private sealed class CountingConnector : IConnector
    {
        public int Opened { get; private set; }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Opened++;
            return ValueTask.FromResult(ConnectResult.Connected(new ScriptedConnection([]), new ConnectTimings(1, null, 1, null)));
        }
    }
}
