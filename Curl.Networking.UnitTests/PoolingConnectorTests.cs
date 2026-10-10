using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_WithNullTarget_ThrowsArgumentNullException()
    {
        await using var pool = CreatePool();

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await pool.ConnectAsync(null!, CancellationToken.None));

        Diagnostics.Arrange("target", "null");
        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "target", exception.ParamName);

        Assert.AreEqual("target", exception.ParamName);
    }

    [TestMethod]
    public async Task ConnectAsync_WithCancelledToken_ThrowsWithoutConnecting()
    {
        await using var pool = CreatePool();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await pool.ConnectAsync(Target(), new CancellationToken(canceled: true)));

        Diagnostics.Arrange("token", "already cancelled");
        Diagnostics.Act("inner connect count", _inner.Targets.Count);
        Diagnostics.Assert("inner connect count", 0, _inner.Targets.Count);

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

        Diagnostics.Arrange("inner failure", "CouldntResolveHost, connection number 9");
        Diagnostics.Act("first connection number", first.ConnectionNumber);
        Diagnostics.Act("second connection number", second.ConnectionNumber);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, first.ExitCode);

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

        Diagnostics.Arrange("inner failure", "refused");
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("connection refused", true, result.IsConnectionRefused);

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

        Diagnostics.Arrange("target", "origin.example:80");
        Diagnostics.Act("first connection number", first.ConnectionNumber);
        Diagnostics.Act("second connection number", second.ConnectionNumber);
        Diagnostics.Assert("first reused", false, first.IsReused);

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

        Diagnostics.Arrange("pooled", "origin.example:80 marked reusable");
        Diagnostics.Act("inner connects", _inner.Targets.Count);
        Diagnostics.Assert("inner connects", 1, _inner.Targets.Count);

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

        Diagnostics.Arrange("unix socket path", "/run/app.sock");
        Diagnostics.Act("opened path", opened.UnixSocketPath);
        Diagnostics.Act("reused path", reused.UnixSocketPath);
        Diagnostics.Assert("opened path", "/run/app.sock", opened.UnixSocketPath);

        Assert.AreEqual("/run/app.sock", opened.UnixSocketPath);
        Assert.IsTrue(reused.IsReused);
        Assert.AreEqual("/run/app.sock", reused.UnixSocketPath);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAConnectToDestination_KeepsItOnTheOpenedAndTheReusedResult()
    {
        _inner.MappedHost = "127.0.0.1";
        _inner.MappedPort = 18499;
        await using var pool = CreatePool();

        var opened = await pool.ConnectAsync(Target(), CancellationToken.None);
        opened.Connection!.MarkReusable();
        await opened.Connection.DisposeAsync();
        var reused = await pool.ConnectAsync(Target(), CancellationToken.None);

        Diagnostics.Arrange("mapped destination", "127.0.0.1:18499");
        Diagnostics.Act("opened mapping", $"{opened.MappedHost}:{opened.MappedPort}");
        Diagnostics.Act("reused mapping", $"{reused.MappedHost}:{reused.MappedPort}");
        Diagnostics.Assert("opened mapping", "127.0.0.1:18499", $"{opened.MappedHost}:{opened.MappedPort}");

        Assert.AreEqual(("127.0.0.1", 18499), (opened.MappedHost, opened.MappedPort));
        Assert.IsTrue(reused.IsReused);
        Assert.AreEqual(("127.0.0.1", 18499), (reused.MappedHost, reused.MappedPort));
    }

    [TestMethod]
    public async Task ConnectAsync_ForANewConnection_PassesTheConnectReplyHeaderCountOn()
    {
        _inner.ConnectReplyHeadersStored = 3;
        await using var pool = CreatePool();

        var opened = await pool.ConnectAsync(Target(), CancellationToken.None);

        Diagnostics.Arrange("inner connect reply headers stored", 3);
        Diagnostics.Act("opened connect reply headers stored", opened.ConnectReplyHeadersStored);
        Diagnostics.Assert("opened connect reply headers stored", 3, opened.ConnectReplyHeadersStored);
        Assert.AreEqual(3, opened.ConnectReplyHeadersStored);
    }

    [TestMethod]
    public async Task ConnectAsync_ForANewConnectionThenAReuse_PassesTheConnectReplyHeadBytesOnOnlyOnce()
    {
        _inner.ProxyConnectHeaderBytes = 61;
        await using var pool = CreatePool();

        var opened = await pool.ConnectAsync(Target(), CancellationToken.None);
        opened.Connection!.MarkReusable();
        await opened.Connection.DisposeAsync();
        var reused = await pool.ConnectAsync(Target(), CancellationToken.None);

        Diagnostics.Assert("opened, reused proxy connect header bytes", (61L, 0L), (opened.ProxyConnectHeaderBytes, reused.ProxyConnectHeaderBytes));
        Assert.AreEqual(61L, opened.ProxyConnectHeaderBytes);
        Assert.IsTrue(reused.IsReused);
        Assert.AreEqual(0L, reused.ProxyConnectHeaderBytes);
    }

    [TestMethod]
    public async Task ConnectAsync_ReusingAForwardProxyConnection_ReportsItWithProxy()
    {
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        var forwardProxy = Target(host: "proxy.example", port: 3128) with { IsForwardProxy = true };
        await ReturnToPoolAsync(pool, forwardProxy);

        await pool.ConnectAsync(forwardProxy with { Events = events }, CancellationToken.None);

        Diagnostics.Arrange("forward proxy", "proxy.example:3128");
        Diagnostics.Act("reused events", events.Reused.Count);
        Diagnostics.Assert("reused events", 1, events.Reused.Count);

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

        Diagnostics.Arrange("tunnelling proxy", "proxy.example:3128");
        Diagnostics.Act("reused events", events.Reused.Count);
        Diagnostics.Assert("reused events", 1, events.Reused.Count);

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

        Diagnostics.Arrange("pooled", "Origin.Example HTTP, asked as origin.example http");
        Diagnostics.Act("reused", reused.IsReused);
        Diagnostics.Assert("reused", true, reused.IsReused);

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

        Diagnostics.Arrange("proxy", "proxy.example, asked as PROXY.example, same credential");
        Diagnostics.Act("reused", reused.IsReused);
        Diagnostics.Assert("reused", true, reused.IsReused);

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

        Diagnostics.Arrange("difference", difference);
        Diagnostics.Act("connection number", other.ConnectionNumber);
        Diagnostics.Assert("reused", false, other.IsReused);

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

        Diagnostics.Arrange("alt-svc", "alt.example:443, asked as ALT.example:443");
        Diagnostics.Act("reused", reused.IsReused);
        Diagnostics.Assert("reused", true, reused.IsReused);

        Assert.IsTrue(reused.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_WhileTheConnectionIsStillInUse_OpensANewConnection()
    {
        await using var pool = CreatePool();
        var inUse = await pool.ConnectAsync(Target(), CancellationToken.None);

        var other = await pool.ConnectAsync(Target(), CancellationToken.None);

        Diagnostics.Arrange("first connection", "still in use");
        Diagnostics.Act("second reused", other.IsReused);
        Diagnostics.Assert("second reused", false, other.IsReused);

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

        Diagnostics.Arrange("first connection", "disposed without MarkReusable");
        Diagnostics.Act("first disposed", _inner.Opened[0].IsDisposed);
        Diagnostics.Assert("first disposed", true, _inner.Opened[0].IsDisposed);

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

        Diagnostics.Arrange("target", "origin.example:21, no pool scheme");
        Diagnostics.Act("first disposed", _inner.Opened[0].IsDisposed);
        Diagnostics.Assert("first disposed", true, _inner.Opened[0].IsDisposed);

        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsFalse(second.IsReused);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAConnectionAskedToClearTls_ClosesItThoughMarkedReusable()
    {
        await using var pool = CreatePool();
        var first = await pool.ConnectAsync(Target(), CancellationToken.None);

        var plaintext = await first.Connection!.ClearTlsAsync(sendCloseNotifyFirst: true, CancellationToken.None);
        first.Connection.MarkReusable();
        await first.Connection.DisposeAsync();
        var second = await pool.ConnectAsync(Target(), CancellationToken.None);

        Diagnostics.Arrange("connection", "ClearTlsAsync then MarkReusable");
        Diagnostics.Act("plaintext is null", plaintext is null);
        Diagnostics.Assert("first disposed", true, _inner.Opened[0].IsDisposed);

        Assert.IsNull(plaintext, "The fake underneath is not TLS, so it cannot be cleared.");
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

        Diagnostics.Arrange("connection", "marked reusable, disposed twice");
        Diagnostics.Act("reused", reused.IsReused);
        Diagnostics.Act("other reused", other.IsReused);
        Diagnostics.Assert("reused", true, reused.IsReused);

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

        Diagnostics.Arrange("reused connection", "not marked again");
        Diagnostics.Act("reused", reused.IsReused);
        Diagnostics.Assert("first disposed", true, _inner.Opened[0].IsDisposed);

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

        Diagnostics.Arrange("idle connections", "ports 1 to 6");
        Diagnostics.Act("info", string.Join(" | ", events.Info));
        Diagnostics.Assert("oldest disposed", true, _inner.Opened[0].IsDisposed);

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

        Diagnostics.Arrange("idle connections", PoolingConnector.MaximumIdleConnections);
        Diagnostics.Act("info count", events.Info.Count);
        Diagnostics.Assert("info count", 0, events.Info.Count);

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

        Diagnostics.Arrange("pooled", "read to the close");
        Diagnostics.Act("info", string.Join(" | ", events.Info));
        Diagnostics.Assert("fresh reused", false, fresh.IsReused);

        CollectionAssert.AreEqual(new[] { "Connection 0 seems to be dead", "shutting down connection #0" }, events.Info);
        Assert.IsEmpty(events.Reused);
        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsFalse(fresh.IsReused);
        Assert.AreEqual(1L, fresh.ConnectionNumber);
        Assert.HasCount(2, _inner.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAnIdleConnectionReportsItsPeerClosed_ReportsItDeadAndOpensANewOne()
    {
        // curl 8.21.0 asks Curl_conn_is_alive before reusing a kept connection: a server that
        // answered and then half-closed is found dead without a byte read (BL-2018 Notes).
        await using var pool = CreatePool();
        var events = new RecordingTransferEvents();
        await ReturnToPoolAsync(pool, Target());
        _inner.Opened[0].HasPeerClosed = true;

        var fresh = await pool.ConnectAsync(Target() with { Events = events }, CancellationToken.None);

        Diagnostics.Arrange("pooled", "peer closed while idle");
        Diagnostics.Act("info", string.Join(" | ", events.Info));
        Diagnostics.Assert("fresh reused", false, fresh.IsReused);

        CollectionAssert.AreEqual(new[] { "Connection 0 seems to be dead", "shutting down connection #0" }, events.Info);
        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsEmpty(_inner.Opened[0].Written);
        Assert.IsFalse(fresh.IsReused);
        Assert.AreEqual(1L, fresh.ConnectionNumber);
    }

    [TestMethod]
    public async Task HasPeerClosed_OfALeaseWhoseConnectionReportsItsPeerClosed_IsTrue()
    {
        await using var pool = CreatePool();
        var lease = await pool.ConnectAsync(Target(), CancellationToken.None);
        var before = lease.Connection!.HasPeerClosed;
        _inner.Opened[0].HasPeerClosed = true;

        Diagnostics.Arrange("lease", "underlying connection open, then closed by its peer");
        Diagnostics.Act("has peer closed", lease.Connection.HasPeerClosed);
        Diagnostics.Assert("has peer closed", true, lease.Connection.HasPeerClosed);

        Assert.IsFalse(before);
        Assert.IsTrue(lease.Connection.HasPeerClosed);
        await lease.Connection.DisposeAsync();
    }

    [TestMethod]
    public async Task HasPeerClosed_OfALeaseAfterAReadFoundTheServersClose_IsTrue()
    {
        await using var pool = CreatePool();
        var lease = await pool.ConnectAsync(Target(), CancellationToken.None);
        await lease.Connection!.ReadAsync(new byte[1], CancellationToken.None);

        Diagnostics.Arrange("lease", "a read returned zero");
        Diagnostics.Act("has peer closed", lease.Connection.HasPeerClosed);
        Diagnostics.Assert("has peer closed", true, lease.Connection.HasPeerClosed);

        Assert.IsTrue(lease.Connection.HasPeerClosed);
        await lease.Connection.DisposeAsync();
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

        Diagnostics.Arrange("pooled", "dead connection then live connection");
        Diagnostics.Act("info", string.Join(" | ", events.Info));
        Diagnostics.Assert("reused", true, reused.IsReused);

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

        Diagnostics.Arrange("read failure", "missing close_notify");
        Diagnostics.Act("info", string.Join(" | ", events.Info));
        Diagnostics.Assert("next reused", false, next.IsReused);

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

        Diagnostics.Arrange("read", "empty buffer");
        Diagnostics.Act("info count", events.Info.Count);
        Diagnostics.Assert("reused", true, reused.IsReused);

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

        Diagnostics.Arrange("idle milliseconds", 118_000);
        Diagnostics.Act("reused", reused.IsReused);
        Diagnostics.Assert("reused", true, reused.IsReused);

        Assert.IsTrue(reused.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterMoreThan118SecondsIdle_ClosesTheConnectionAndOpensANewOne()
    {
        await using var pool = CreatePool();
        await ReturnToPoolAsync(pool, Target());
        _time.Advance(118_001);

        var other = await pool.ConnectAsync(Target(), CancellationToken.None);

        Diagnostics.Arrange("idle milliseconds", 118_001);
        Diagnostics.Act("other reused", other.IsReused);
        Diagnostics.Assert("other reused", false, other.IsReused);

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

        Diagnostics.Arrange("idle milliseconds", 118_001);
        Diagnostics.Act("first disposed", _inner.Opened[0].IsDisposed);
        Diagnostics.Assert("first disposed", true, _inner.Opened[0].IsDisposed);

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

        Diagnostics.Arrange("connections", "one idle, one in use at pool disposal");
        Diagnostics.Act("opened count", _inner.Opened.Count);
        Diagnostics.Assert("idle disposed", true, _inner.Opened[0].IsDisposed);

        Assert.IsTrue(_inner.Opened[0].IsDisposed);
        Assert.IsTrue(_inner.Opened[1].IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_ReusingAConnectionThatHoldsASession_HandsTheSameSessionOnWithoutShuttingItDown()
    {
        await using var pool = CreatePool();
        var session = await ReturnWithSessionAsync(pool, Target());

        var reused = await pool.ConnectAsync(Target(), CancellationToken.None);

        Diagnostics.Arrange("pooled", "connection holding a session");
        Diagnostics.Act("shut down count", session.ShutDownCount);
        Diagnostics.Assert("reused", true, reused.IsReused);

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

        Diagnostics.Arrange("session", "held, connection not marked reusable");
        Diagnostics.Act("shut down count", session.ShutDownCount);
        Diagnostics.Assert("shut down count", 1, session.ShutDownCount);

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

        Diagnostics.Arrange("idle connection", "holding a session");
        Diagnostics.Act("shut down count", session.ShutDownCount);
        Diagnostics.Assert("shut down count", 1, session.ShutDownCount);

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

        Diagnostics.Arrange("idle connections", "ports 1 to 6, the first holds a session");
        Diagnostics.Act("shut down count", session.ShutDownCount);
        Diagnostics.Assert("shut down count", 1, session.ShutDownCount);

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

        Diagnostics.Arrange("idle milliseconds", 118_001);
        Diagnostics.Act("shut down count", session.ShutDownCount);
        Diagnostics.Assert("shut down count", 1, session.ShutDownCount);

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
