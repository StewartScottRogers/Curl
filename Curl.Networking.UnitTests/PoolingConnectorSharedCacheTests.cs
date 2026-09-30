using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="PoolingConnector" />s over one <see cref="ConnectionCache" />, as the
/// <c>-:</c>/<c>--next</c> option groups of one run have, reuse each other's connections only
/// when their configurations are equal, number connections across the run, and leave the cache
/// for its owner to close (ADR-0285, BL-754).
/// </summary>
[TestClass]
public sealed class PoolingConnectorSharedCacheTests
{
    private readonly ManualTimeProvider _time = new();

    [TestMethod]
    public void Constructor_WithNullInnerConnector_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new PoolingConnector(null!, new ConnectionCache(_time), configuration: null));

        Assert.AreEqual("innerConnector", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new PoolingConnector(new FakeConnector(), null!, configuration: null));

        Assert.AreEqual("cache", exception.ParamName);
    }

    [TestMethod]
    public async Task ConnectAsync_InALaterGroupWithAnEqualConfiguration_ReusesTheEarlierGroupsConnection()
    {
        var cache = new ConnectionCache(_time);
        var firstInner = new FakeConnector();
        var laterInner = new FakeConnector();
        var first = new PoolingConnector(firstInner, cache, "settings");
        var later = new PoolingConnector(laterInner, cache, string.Concat("sett", "ings"));
        await ReturnToCacheAsync(first);
        await first.DisposeAsync();

        var reused = await later.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsTrue(reused.IsReused);
        Assert.AreEqual(0L, reused.ConnectionNumber);
        Assert.IsEmpty(laterInner.Targets);
        Assert.IsFalse(firstInner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_InALaterGroupWithADifferentConfiguration_OpensItsOwnWithTheNextNumber()
    {
        var cache = new ConnectionCache(_time);
        var firstInner = new FakeConnector();
        var laterInner = new FakeConnector();
        var first = new PoolingConnector(firstInner, cache, "verifies the peer");
        var later = new PoolingConnector(laterInner, cache, "insecure");
        await ReturnToCacheAsync(first);

        var opened = await later.ConnectAsync(Target(), CancellationToken.None);

        Assert.IsFalse(opened.IsReused);
        Assert.AreEqual(1L, opened.ConnectionNumber);
        Assert.HasCount(1, laterInner.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAConfigurationAgainstAPoolWithout_OpensItsOwn()
    {
        var cache = new ConnectionCache(_time);
        var laterInner = new FakeConnector();
        await ReturnToCacheAsync(new PoolingConnector(new FakeConnector(), cache, configuration: null));

        var opened = await new PoolingConnector(laterInner, cache, "settings").ConnectAsync(Target(), CancellationToken.None);

        Assert.IsFalse(opened.IsReused);
        Assert.HasCount(1, laterInner.Targets);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAConnectorOverAGivenCache_LeavesItsIdleConnectionsOpen()
    {
        var cache = new ConnectionCache(_time);
        var inner = new FakeConnector();
        var connector = new PoolingConnector(inner, cache, configuration: null);
        await ReturnToCacheAsync(connector);

        await connector.DisposeAsync();

        Assert.IsFalse(inner.Opened[0].IsDisposed);
    }

    private static async Task ReturnToCacheAsync(PoolingConnector connector)
    {
        var result = await connector.ConnectAsync(Target(), CancellationToken.None);
        result.Connection!.MarkReusable();
        await result.Connection.DisposeAsync();
    }

    private static ConnectTarget Target() =>
        new("origin.example", 80, false) { PoolScheme = "http" };
}
