using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="ConnectionCache.DisposeAsync" /> closes the run's idle connections and
/// every connection handed back after it, as curl closes its connection cache once the run ends
/// (ADR-0285, BL-754).
/// </summary>
[TestClass]
public sealed class ConnectionCacheTests
{
    [TestMethod]
    public async Task DisposeAsync_WithAnIdleAndALeasedConnection_ClosesBoth()
    {
        var cache = new ConnectionCache(new ManualTimeProvider());
        var inner = new FakeConnector();
        var connector = new PoolingConnector(inner, cache, configuration: null);
        var idle = await connector.ConnectAsync(Target("idle.example"), CancellationToken.None);
        idle.Connection!.MarkReusable();
        await idle.Connection.DisposeAsync();
        var inUse = await connector.ConnectAsync(Target("in-use.example"), CancellationToken.None);

        await cache.DisposeAsync();
        inUse.Connection!.MarkReusable();
        await inUse.Connection.DisposeAsync();

        Assert.IsTrue(inner.Opened[0].IsDisposed);
        Assert.IsTrue(inner.Opened[1].IsDisposed);
    }

    private static ConnectTarget Target(string host) => new(host, 80, false) { PoolScheme = "http" };
}
