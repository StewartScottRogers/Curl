using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="ConnectionCache.DisposeAsync" /> closes the run's idle connections and
/// every connection handed back after it, as curl closes its connection cache once the run ends
/// (ADR-0285, BL-754).
/// </summary>
[TestClass]
public sealed class ConnectionCacheTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task DisposeAsync_WithAnIdleAndALeasedConnection_ClosesBoth()
    {
        Diagnostics.Arrange("idle host", "idle.example");
        Diagnostics.Arrange("in-use host", "in-use.example");
        var cache = new ConnectionCache(new ManualTimeProvider());
        var inner = new FakeConnector();
        var connector = new PoolingConnector(inner, cache, configuration: null);
        var idle = await connector.ConnectAsync(Target("idle.example"), CancellationToken.None);
        idle.Connection!.MarkReusable();
        await idle.Connection.DisposeAsync();
        var inUse = await connector.ConnectAsync(Target("in-use.example"), CancellationToken.None);

        using (Diagnostics.Phase("dispose cache"))
        {
            await cache.DisposeAsync();
            inUse.Connection!.MarkReusable();
            await inUse.Connection.DisposeAsync();
        }

        Diagnostics.Act("opened connection count", inner.Opened.Count);
        Diagnostics.Act("idle connection disposed", inner.Opened[0].IsDisposed);
        Diagnostics.Act("in-use connection disposed", inner.Opened[1].IsDisposed);
        Diagnostics.Assert("idle connection disposed", true, inner.Opened[0].IsDisposed);
        Diagnostics.Assert("in-use connection disposed", true, inner.Opened[1].IsDisposed);

        Assert.IsTrue(inner.Opened[0].IsDisposed);
        Assert.IsTrue(inner.Opened[1].IsDisposed);
    }

    [TestMethod]
    public async Task CloseIdleAsync_WithAnIdleAndALeasedConnection_ClosesTheIdleOneAndKeepsTheLeasedOneForReuse()
    {
        var cache = new ConnectionCache(new ManualTimeProvider());
        var inner = new FakeConnector();
        var connector = new PoolingConnector(inner, cache, configuration: null);
        var idle = await connector.ConnectAsync(Target("idle.example"), CancellationToken.None);
        idle.Connection!.MarkReusable();
        await idle.Connection.DisposeAsync();
        var inUse = await connector.ConnectAsync(Target("in-use.example"), CancellationToken.None);

        await cache.CloseIdleAsync();
        inUse.Connection!.MarkReusable();
        await inUse.Connection.DisposeAsync();
        var again = await connector.ConnectAsync(Target("in-use.example"), CancellationToken.None);
        var idleAgain = await connector.ConnectAsync(Target("idle.example"), CancellationToken.None);

        Assert.IsTrue(inner.Opened[0].IsDisposed);
        Assert.IsFalse(inner.Opened[1].IsDisposed);
        Assert.IsTrue(again.IsReused);
        Assert.IsFalse(idleAgain.IsReused);
        Assert.HasCount(3, inner.Opened);
    }

    [TestMethod]
    public async Task CloseIdleBeyondAsync_ThreeIdleAndOneLeasedKeepingTwo_ClosesTheTwoOldestIdleAndKeepsTheLeasedOne()
    {
        var cache = new ConnectionCache(new ManualTimeProvider());
        var inner = new FakeConnector();
        var connector = new PoolingConnector(inner, cache, configuration: null);
        foreach (string host in new[] { "oldest.example", "older.example", "newest.example" })
        {
            var opened = await connector.ConnectAsync(Target(host), CancellationToken.None);
            opened.Connection!.MarkReusable();
            await opened.Connection.DisposeAsync();
        }

        var inUse = await connector.ConnectAsync(Target("in-use.example"), CancellationToken.None);

        await cache.CloseIdleBeyondAsync(2);
        var newestAgain = await connector.ConnectAsync(Target("newest.example"), CancellationToken.None);

        Assert.IsTrue(inner.Opened[0].IsDisposed);
        Assert.IsTrue(inner.Opened[1].IsDisposed);
        Assert.IsFalse(inner.Opened[2].IsDisposed);
        Assert.IsFalse(inner.Opened[3].IsDisposed);
        Assert.IsTrue(newestAgain.IsReused);
        Assert.IsNotNull(inUse.Connection);
    }

    [TestMethod]
    [DataRow(5L, false)]
    [DataRow(1L, false)]
    [DataRow(0L, true)]
    [DataRow(-3L, true)]
    public async Task CloseIdleBeyondAsync_OneIdleConnection_ClosesItOnlyWhenNoneIsKept(long keep, bool expectedClosed)
    {
        var cache = new ConnectionCache(new ManualTimeProvider());
        var inner = new FakeConnector();
        var connector = new PoolingConnector(inner, cache, configuration: null);
        var idle = await connector.ConnectAsync(Target("idle.example"), CancellationToken.None);
        idle.Connection!.MarkReusable();
        await idle.Connection.DisposeAsync();

        await cache.CloseIdleBeyondAsync(keep);

        Assert.AreEqual(expectedClosed, inner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task CloseIdleOwnedByAsync_OwnedIdleOtherIdleAndOwnedLeased_ClosesOnlyTheOwnedIdleOne()
    {
        var cache = new ConnectionCache(new ManualTimeProvider());
        var inner = new FakeConnector();
        var connector = new PoolingConnector(inner, cache, configuration: null);
        object owner = new();
        var owned = await ConnectOwnedAsync(connector, cache, owner, "owned.example");
        var other = await connector.ConnectAsync(Target("other.example"), CancellationToken.None);
        var leased = await ConnectOwnedAsync(connector, cache, owner, "leased.example");
        foreach (var idle in new[] { owned, other })
        {
            idle.Connection!.MarkReusable();
            await idle.Connection.DisposeAsync();
        }

        await cache.CloseIdleOwnedByAsync(owner);
        var otherAgain = await connector.ConnectAsync(Target("other.example"), CancellationToken.None);
        var ownedAgain = await connector.ConnectAsync(Target("owned.example"), CancellationToken.None);

        Assert.IsTrue(inner.Opened[0].IsDisposed);
        Assert.IsFalse(inner.Opened[1].IsDisposed);
        Assert.IsFalse(inner.Opened[2].IsDisposed);
        Assert.IsTrue(otherAgain.IsReused);
        Assert.IsFalse(ownedAgain.IsReused);
        Assert.IsNotNull(leased.Connection);
    }

    [TestMethod]
    public async Task OpeningOwner_SetInsideAnAsyncMethod_IsUndoneWhenItReturns()
    {
        var cache = new ConnectionCache(new ManualTimeProvider());

        await ConnectOwnedAsync(new PoolingConnector(new FakeConnector(), cache, configuration: null), cache, new object(), "owned.example");

        Assert.IsNull(cache.OpeningOwner);
    }

    [TestMethod]
    public async Task CloseIdleOwnedByAsync_NullOwner_Throws()
    {
        var cache = new ConnectionCache(new ManualTimeProvider());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await cache.CloseIdleOwnedByAsync(null!));
    }

    private static async Task<ConnectResult> ConnectOwnedAsync(PoolingConnector connector, ConnectionCache cache, object owner, string host)
    {
        cache.OpeningOwner = owner;
        return await connector.ConnectAsync(Target(host), CancellationToken.None);
    }

    private static ConnectTarget Target(string host) => new(host, 80, false) { PoolScheme = "http" };
}
