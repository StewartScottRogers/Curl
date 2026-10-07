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

    private static ConnectTarget Target(string host) => new(host, 80, false) { PoolScheme = "http" };
}
