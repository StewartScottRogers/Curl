using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="PoolingConnector" /> and <see cref="PooledConnection" /> through a fake
/// inner connector: reuse for the same key, no reuse across keys, after close or while in use,
/// the five-connection limit, the 118-second idle limit, and the connection numbers and reuse
/// flag <c>%{num_connects}</c> is built from (ADR-0050).
/// </summary>
[TestClass]
public sealed class PoolingConnectorTests
{
    private readonly FakeConnector _inner = new();
    private readonly ManualTimeProvider _time = new();

    [TestMethod]
    public async Task ConnectAsync_WithNullTarget_ThrowsArgumentNullException()
    {
        await using var pool = CreatePool();

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await pool.ConnectAsync(null!, CancellationToken.None));

        Assert.AreEqual("target", exception.ParamName);
    }

    [TestMethod]
    public async Task ConnectAsync_WithCancelledToken_ThrowsWithoutConnecting()
    {
        await using var pool = CreatePool();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await pool.ConnectAsync(Target(), new CancellationToken(canceled: true)));

        Assert.IsEmpty(_inner.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheInnerConnectorFails_ReturnsItsFailureWithThePoolsNumber()
    {
        await using var pool = CreatePool();
        var timings = new ConnectTimings(1, 2, null, null);
        _inner.Failure = ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: x", timings, connectionNumber: 9);

        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        var second = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, first.ExitCode);
        Assert.AreEqual("Could not resolve host: x", first.ErrorMessage);
        Assert.AreSame(timings, first.Timings);
        Assert.IsFalse(first.IsConnectionRefused);
        Assert.IsNull(first.Connection);
        Assert.AreEqual(0L, first.ConnectionNumber);
        Assert.AreEqual(1L, second.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheInnerConnectorIsRefused_KeepsItRefused()
    {
        await using var pool = CreatePool();
        _inner.Failure = ConnectResult.Refused("Failed to connect");

        var result = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(0L, result.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectAsync_ForANewConnection_PassesTheInnerResultOnAndNumbersItFromZero()
    {
        await using var pool = CreatePool();
        var target = Target();

        var first = await pool.ConnectAsync(target, CancellationToken.None);
        var second = await pool.ConnectAsync(target, CancellationToken.None);

        Assert.IsInstanceOfType<PooledConnection>(first.Connection);
        Assert.IsFalse(first.IsReused);
        Assert.AreEqual(0L, first.ConnectionNumber);
        Assert.AreEqual(1L, second.ConnectionNumber);
        Assert.AreEqual(new ConnectTimings(1, null, 1, null), first.Timings);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50001), first.LocalEndPoint);
        Assert.AreEqual(200, first.ProxyConnectResponseCode);
        CollectionAssert.AreEqual(new byte[] { 1 }, first.PeerCertificates[0].ToArray());
        CollectionAssert.AreEqual(new[] { target, target }, _inner.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterAConnectionMarkedReusableIsDisposed_ReusesItWithoutConnecting()
    {
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        await ReturnToPoolAsync(pool, Target());

        var reused = await pool.ConnectAsync(Target() with { Events = events }, CancellationToken.None);

        Assert.HasCount(1, _inner.Targets);
        Assert.IsFalse(_inner.Opened[0].IsDisposed);
        Assert.IsTrue(reused.IsReused);
        Assert.AreEqual(0L, reused.ConnectionNumber);
        Assert.IsNull(reused.Timings);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50001), reused.LocalEndPoint);
        Assert.AreEqual(0, reused.ProxyConnectResponseCode);
        CollectionAssert.AreEqual(new byte[] { 1 }, reused.PeerCertificates[0].ToArray());
        Assert.HasCount(1, events.Reused);
        Assert.AreEqual(
            new ConnectionReusedEvent
            {
                Scheme = "http",
                IsProxy = false,
                HostName = "origin.example",
                Port = 80,
                ConnectionNumber = 0,
            },
            events.Reused[0]);
    }

    [TestMethod]
    public async Task ConnectAsync_OverAUnixSocket_KeepsItsPathOnTheOpenedAndTheReusedResult()
    {
        _inner.UnixSocketPath = "/run/app.sock";
        await using var pool = CreatePool();

        var opened = await pool.ConnectAsync(Target(), CancellationToken.None);
        opened.Connection!.MarkReusable();
        await opened.Connection.DisposeAsync();
        var reused = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.AreEqual("/run/app.sock", opened.UnixSocketPath);
        Assert.IsTrue(reused.IsReused);
        Assert.AreEqual("/run/app.sock", reused.UnixSocketPath);
    }

    [TestMethod]
    public async Task ConnectAsync_ReusingAForwardProxyConnection_ReportsItWithProxy()
    {
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        var forwardProxy = Target(host: "proxy.example", port: 3128) with { IsForwardProxy = true };
        await ReturnToPoolAsync(pool, forwardProxy);

        await pool.ConnectAsync(forwardProxy with { Events = events }, CancellationToken.None);

        Assert.AreEqual(
            new ConnectionReusedEvent
            {
                Scheme = "http",
                IsProxy = true,
                HostName = "proxy.example",
                Port = 3128,
                ConnectionNumber = 0,
            },
            events.Reused.Single());
    }

    [TestMethod]
    public async Task ConnectAsync_ReusingATunnelledConnection_ReportsItWithTheTunnellingProxy()
    {
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        var tunnelled = Target(proxy: Proxy("proxy.example", "user", "secret"));
        await ReturnToPoolAsync(pool, tunnelled);

        await pool.ConnectAsync(tunnelled with { Events = events }, CancellationToken.None);

        Assert.AreEqual(
            new ConnectionReusedEvent
            {
                Scheme = "http",
                IsProxy = true,
                HostName = "proxy.example",
                Port = 3128,
                ConnectionNumber = 0,
            },
            events.Reused.Single());
    }

    [TestMethod]
    public async Task ConnectAsync_ForTheSameHostInAnotherCase_ReusesTheConnection()
    {
        await using var pool = CreatePool();
        await ReturnToPoolAsync(pool, Target(host: "Origin.Example", scheme: "HTTP"));

        var reused = await pool.ConnectAsync(Target(host: "origin.example", scheme: "http"), CancellationToken.None);

        Assert.IsTrue(reused.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughTheSameProxyWithAnEqualCredential_ReusesTheConnection()
    {
        await using var pool = CreatePool();
        await ReturnToPoolAsync(pool, Target(proxy: Proxy("proxy.example", "user", "secret")));

        var reused = await pool.ConnectAsync(
            Target(proxy: Proxy("PROXY.example", "user", "secret")),
            CancellationToken.None);

        Assert.IsTrue(reused.IsReused);
    }

    [TestMethod]
    [DataRow("scheme")]
    [DataRow("host")]
    [DataRow("address")]
    [DataRow("port")]
    [DataRow("tls")]
    [DataRow("direct")]
    [DataRow("forward proxy")]
    [DataRow("proxy kind")]
    [DataRow("proxy host")]
    [DataRow("proxy port")]
    [DataRow("proxy user")]
    [DataRow("proxy password")]
    [DataRow("proxy domain")]
    [DataRow("proxy credential")]
    [DataRow("alt-svc")]
    public async Task ConnectAsync_ForADifferentKey_OpensANewConnection(string difference)
    {
        await using var pool = CreatePool();
        var pooled = Target(proxy: Proxy("proxy.example", "user", "secret"));
        await ReturnToPoolAsync(pool, pooled);

        var other = await pool.ConnectAsync(Differing(pooled, difference), CancellationToken.None);

        Assert.IsFalse(other.IsReused);
        Assert.AreEqual(1L, other.ConnectionNumber);
        Assert.HasCount(2, _inner.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_ToTheSameAltSvcAlternativeInAnotherCase_ReusesTheConnection()
    {
        await using var pool = CreatePool();
        await ReturnToPoolAsync(pool, Target() with { AltSvcRoute = new AltSvcRoute("h1", new AltSvcAlternative("h1", "alt.example", 443)) });

        var reused = await pool.ConnectAsync(
            Target() with { AltSvcRoute = new AltSvcRoute("h1", new AltSvcAlternative("h1", "ALT.example", 443)) },
            CancellationToken.None);

        Assert.IsTrue(reused.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_WhileTheConnectionIsStillInUse_OpensANewConnection()
    {
        await using var pool = CreatePool();
        var inUse = await pool.ConnectAsync(Target(), CancellationToken.None);

        var other = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsFalse(other.IsReused);
        Assert.AreNotSame(inUse.Connection, other.Connection);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAConnectionNotMarkedReusable_ClosesItAndTheNextConnectOpensANewOne()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        await first.Connection!.DisposeAsync();

        var second = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsFalse(second.IsReused);
        Assert.HasCount(2, _inner.Targets);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAMarkedConnectionWithoutPoolScheme_ClosesItAndNeverPoolsIt()
    {
        await using var pool = CreatePool();
        var target = new ConnectTarget("origin.example", 21, UseTls: false);
        await ReturnToPoolAsync(pool, target);

        var second = await pool.ConnectAsync(target, CancellationToken.None);

        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsFalse(second.IsReused);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAPooledConnectionTwice_ReturnsItOnlyOnce()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();
        await first.Connection.DisposeAsync();

        var reused = await pool.ConnectAsync(Target(), CancellationToken.None);
        var other = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsTrue(reused.IsReused);
        Assert.IsFalse(other.IsReused);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAReusedConnectionNotMarkedAgain_ClosesIt()
    {
        await using var pool = CreatePool();
        await ReturnToPoolAsync(pool, Target());
        var reused = await pool.ConnectAsync(Target(), CancellationToken.None);

        await reused.Connection!.DisposeAsync();

        Assert.IsTrue(_inner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfTheSixthIdleConnection_ClosesTheOldestAndReportsItAsCurlDoes()
    {
        await using var pool = CreatePool();
        for (var port = 1; port <= 5; port++)
        {
            await ReturnToPoolAsync(pool, Target(port: port));
        }

        var events = new RecordingTransferEvents();
        await ReturnToPoolAsync(pool, Target(port: 6) with { Events = events });

        CollectionAssert.AreEqual(
            new[] { "Connection pool is full, closing the oldest of 6/5", "shutting down connection #0" },
            events.Info);
        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsFalse(_inner.Opened[1].IsDisposed);
        Assert.IsFalse((await pool.ConnectAsync(Target(port: 1), CancellationToken.None)).IsReused);
        Assert.IsTrue((await pool.ConnectAsync(Target(port: 2), CancellationToken.None)).IsReused);
    }

    [TestMethod]
    public async Task DisposeAsync_OfTheFifthIdleConnection_KeepsEveryOneAndReportsNothing()
    {
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        for (var port = 1; port <= PoolingConnector.MaximumIdleConnections; port++)
        {
            await ReturnToPoolAsync(pool, Target(port: port) with { Events = events });
        }

        Assert.IsEmpty(events.Info);
        Assert.IsFalse(_inner.Opened.Exists(connection => connection.IsDisposed));
    }

    [TestMethod]
    public async Task ConnectAsync_AfterAReadFoundTheServersClose_ReportsTheConnectionDeadAndOpensANewOne()
    {
        // curl 8.21.0 -s -v http://127.0.0.1:P/a http://127.0.0.1:P/b, each answered
        // HTTP/1.0 200 with Connection: keep-alive and no length, then closed (BL-477 Notes).
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        await ReturnToPoolAsync(pool, Target(), readsToTheClose: true);

        var fresh = await pool.ConnectAsync(Target() with { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Connection 0 seems to be dead", "shutting down connection #0" }, events.Info);
        Assert.IsEmpty(events.Reused);
        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsFalse(fresh.IsReused);
        Assert.AreEqual(1L, fresh.ConnectionNumber);
        Assert.HasCount(2, _inner.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenADeadConnectionIsPooledBeforeALiveOne_ShutsTheDeadOneAndReusesTheLiveOne()
    {
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        var dead = await pool.ConnectAsync(Target(), CancellationToken.None);
        var live = await pool.ConnectAsync(Target(), CancellationToken.None);
        await dead.Connection!.ReadAsync(new byte[1], CancellationToken.None);
        dead.Connection.MarkReusable();
        await dead.Connection.DisposeAsync();
        live.Connection!.MarkReusable();
        await live.Connection.DisposeAsync();

        var reused = await pool.ConnectAsync(Target() with { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Connection 0 seems to be dead", "shutting down connection #0" }, events.Info);
        Assert.IsTrue(reused.IsReused);
        Assert.AreEqual(1L, reused.ConnectionNumber);
        Assert.HasCount(2, _inner.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterAReadFoundCloseNotifyMissing_ShutsThatConnectionAndOpensANewOne()
    {
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        _inner.ReadException = new MissingCloseNotifyException("schannel: server closed abruptly (missing close_notify)");
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        await Assert.ThrowsExactlyAsync<MissingCloseNotifyException>(
            () => first.Connection!.ReadAsync(new byte[1], CancellationToken.None).AsTask());
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();

        var next = await pool.ConnectAsync(Target() with { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Connection 0 seems to be dead", "shutting down connection #0" }, events.Info);
        Assert.IsFalse(next.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterAnEmptyReadReturnedZero_ReusesTheConnection()
    {
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        await first.Connection!.ReadAsync(Memory<byte>.Empty, CancellationToken.None);
        first.Connection.MarkReusable();
        await first.Connection.DisposeAsync();

        var reused = await pool.ConnectAsync(Target() with { Events = events }, CancellationToken.None);

        Assert.IsTrue(reused.IsReused);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterExactly118SecondsIdle_ReusesTheConnection()
    {
        await using var pool = CreatePool();
        await ReturnToPoolAsync(pool, Target());
        _time.Advance(118_000);

        var reused = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsTrue(reused.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterMoreThan118SecondsIdle_ClosesTheConnectionAndOpensANewOne()
    {
        await using var pool = CreatePool();
        await ReturnToPoolAsync(pool, Target());
        _time.Advance(118_001);

        var other = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsFalse(other.IsReused);
        Assert.IsTrue(_inner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAConnectionAfterAnotherExpired_ClosesTheExpiredOne()
    {
        await using var pool = CreatePool();
        await ReturnToPoolAsync(pool, Target(port: 1));
        _time.Advance(118_001);

        await ReturnToPoolAsync(pool, Target(port: 2));

        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsFalse(_inner.Opened[1].IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfThePool_ClosesEveryIdleConnectionAndEveryOneReturnedLater()
    {
        var pool = CreatePool();
        await ReturnToPoolAsync(pool, Target(port: 1));
        var inUse = await pool.ConnectAsync(Target(port: 2), CancellationToken.None);

        await pool.DisposeAsync();
        inUse.Connection!.MarkReusable();
        await inUse.Connection.DisposeAsync();

        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsTrue(_inner.Opened[1].IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_ReusingAConnectionThatHoldsASession_HandsTheSameSessionOnWithoutShuttingItDown()
    {
        await using var pool = CreatePool();
        var session = await ReturnWithSessionAsync(pool, Target());

        var reused = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsTrue(reused.IsReused);
        Assert.AreSame(session, reused.Connection!.Session);
        Assert.AreEqual(0, session.ShutDownCount);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAConnectionNotMarkedReusable_ShutsItsSessionDownBeforeClosingIt()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);
        var session = new RecordingConnectionSession(_inner.Opened[0]);
        _ = first.Connection!.TryHoldSession(session);

        await first.Connection.DisposeAsync();

        Assert.AreEqual(1, session.ShutDownCount);
        Assert.IsTrue(session.WasConnectionOpenAtShutDown);
        Assert.IsTrue(_inner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfThePool_ShutsDownTheSessionOfEveryIdleConnectionBeforeClosingIt()
    {
        var pool = CreatePool();
        var session = await ReturnWithSessionAsync(pool, Target());

        await pool.DisposeAsync();

        Assert.AreEqual(1, session.ShutDownCount);
        Assert.IsTrue(session.WasConnectionOpenAtShutDown);
        Assert.IsTrue(_inner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfTheSixthIdleConnection_ShutsDownTheEvictedOnesSession()
    {
        await using var pool = CreatePool();
        var session = await ReturnWithSessionAsync(pool, Target(port: 1));
        for (var port = 2; port <= 6; port++)
        {
            await ReturnToPoolAsync(pool, Target(port: port));
        }

        Assert.AreEqual(1, session.ShutDownCount);
        Assert.IsTrue(_inner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterAConnectionHoldingASessionExpired_ShutsItsSessionDown()
    {
        await using var pool = CreatePool();
        var session = await ReturnWithSessionAsync(pool, Target());
        _time.Advance(118_001);

        _ = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.AreEqual(1, session.ShutDownCount);
    }

    private static ConnectTarget Target(
        string host = "origin.example",
        int port = 80,
        bool useTls = false,
        string scheme = "http",
        ProxyEndpoint? proxy = null) =>
        new(host, port, useTls) { PoolScheme = scheme, Proxy = proxy };

    private static ProxyEndpoint Proxy(string host, string user, string password, int port = 3128, string domain = "") =>
        new(ProxyKind.Http, host, port, new NetworkCredential(user, password, domain));

    private static ConnectTarget Differing(ConnectTarget pooled, string difference) =>
        difference switch
        {
            "scheme" => pooled with { PoolScheme = "https" },
            "host" => new ConnectTarget("other.example", pooled.Port, pooled.UseTls) { PoolScheme = pooled.PoolScheme, Proxy = pooled.Proxy },
            "address" => new ConnectTarget("127.0.0.1", pooled.Port, pooled.UseTls) { PoolScheme = pooled.PoolScheme, Proxy = pooled.Proxy },
            "port" => new ConnectTarget(pooled.Host, 8080, pooled.UseTls) { PoolScheme = pooled.PoolScheme, Proxy = pooled.Proxy },
            "tls" => pooled with { UseTls = true },
            "direct" => pooled with { Proxy = null },
            "forward proxy" => pooled with { IsForwardProxy = true },
            "proxy kind" => pooled with { Proxy = pooled.Proxy! with { Kind = ProxyKind.Socks5 } },
            "proxy host" => pooled with { Proxy = Proxy("other-proxy.example", "user", "secret") },
            "proxy port" => pooled with { Proxy = Proxy("proxy.example", "user", "secret", port: 8888) },
            "proxy user" => pooled with { Proxy = Proxy("proxy.example", "other", "secret") },
            "proxy password" => pooled with { Proxy = Proxy("proxy.example", "user", "other") },
            "proxy domain" => pooled with { Proxy = Proxy("proxy.example", "user", "secret", domain: "CORP") },
            "alt-svc" => pooled with { AltSvcRoute = new AltSvcRoute("h1", new AltSvcAlternative("h1", "alt.example", 443)) },
            _ => pooled with { Proxy = pooled.Proxy! with { Credential = null } },
        };

    private static async Task ReturnToPoolAsync(PoolingConnector pool, ConnectTarget target, bool readsToTheClose = false)
    {
        var result = await pool.ConnectAsync(target, CancellationToken.None);
        if (readsToTheClose)
        {
            await result.Connection!.ReadAsync(new byte[1], CancellationToken.None);
        }

        result.Connection!.MarkReusable();
        await result.Connection.DisposeAsync();
    }

    private async Task<RecordingConnectionSession> ReturnWithSessionAsync(PoolingConnector pool, ConnectTarget target)
    {
        var result = await pool.ConnectAsync(target, CancellationToken.None);
        var session = new RecordingConnectionSession(_inner.Opened[^1]);
        Assert.IsTrue(result.Connection!.TryHoldSession(session));
        result.Connection.MarkReusable();
        await result.Connection.DisposeAsync();
        return session;
    }

    private PoolingConnector CreatePool() => new(_inner, _time);
}
