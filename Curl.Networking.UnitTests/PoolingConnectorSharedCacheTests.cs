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
    public async Task Over_OpensThroughTheGivenConnectorNumberedInTheSamePool()
    {
        var inner = new FakeConnector();
        var otherInner = new FakeConnector();
        await using var pool = new PoolingConnector(inner, _time) { WaitsForMultiplexing = true };
        var other = pool.Over(otherInner);

        var first = await pool.ConnectAsync(new ConnectTarget("origin.example", 21, false), CancellationToken.None);
        var second = await other.ConnectAsync(new ConnectTarget("192.0.2.1", 1025, false), CancellationToken.None);

        Assert.AreEqual(0L, first.ConnectionNumber);
        Assert.AreEqual(1L, second.ConnectionNumber);
        Assert.HasCount(1, inner.Targets);
        Assert.HasCount(1, otherInner.Targets);
        Assert.IsTrue(other.WaitsForMultiplexing);
    }

    [TestMethod]
    public async Task Over_ReusesAConnectionItsOwnerPooledAndLeavesTheCacheOpenWhenDisposed()
    {
        var inner = new FakeConnector();
        await using var pool = new PoolingConnector(inner, _time);
        var other = pool.Over(new FakeConnector());
        await ReturnToCacheAsync(pool);

        var reused = await other.ConnectAsync(Target(), CancellationToken.None);
        await other.DisposeAsync();

        Assert.IsTrue(reused.IsReused);
        Assert.IsFalse(inner.Opened[0].IsDisposed);
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

    [TestMethod]
    public async Task NumberingDatagrams_WithNullConnector_ThrowsArgumentNullException()
    {
        await using var pool = new PoolingConnector(new FakeConnector(), _time);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => pool.NumberingDatagrams(null!));

        Assert.AreEqual("datagramConnector", exception.ParamName);
    }

    [TestMethod]
    public async Task NumberingDatagrams_AfterATcpConnection_NumbersEachOpenNextInThePool()
    {
        await using var pool = new PoolingConnector(new FakeConnector(), _time);
        var channel = new UnusedChannel();
        var datagrams = pool.NumberingDatagrams(new FixedDatagramConnector(DatagramOpenResult.Opened(channel)));
        await ReturnToCacheAsync(pool);

        var first = await datagrams.OpenAsync("tftp.example", 69, CancellationToken.None);
        var second = await datagrams.OpenAsync("tftp.example", 69, CancellationToken.None);

        Assert.AreSame(channel, first.Channel);
        Assert.AreEqual(1L, first.ConnectionNumber);
        Assert.AreEqual(2L, second.ConnectionNumber);
    }

    [TestMethod]
    public async Task NumberingDatagrams_WhenTheOpenFails_NumbersTheFailureAndTheNextTcpConnectionAfterIt()
    {
        await using var pool = new PoolingConnector(new FakeConnector(), _time);
        var datagrams = pool.NumberingDatagrams(new FixedDatagramConnector(DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: tftp.example")));

        var failed = await datagrams.OpenAsync("tftp.example", 69, CancellationToken.None);
        var connected = await pool.ConnectAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, failed.ExitCode);
        Assert.AreEqual("Could not resolve host: tftp.example", failed.ErrorMessage);
        Assert.AreEqual(0L, failed.ConnectionNumber);
        Assert.AreEqual(1L, connected.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectionNumbers_AfterATcpConnection_GivesTheNextNumberAndTheNextConnectionTheOneAfter()
    {
        await using var pool = new PoolingConnector(new FakeConnector(), _time);
        await ReturnToCacheAsync(pool);

        long fileTransferNumber = pool.ConnectionNumbers.NumberNextConnection();
        var connected = await pool.ConnectAsync(new ConnectTarget("other.example", 80, false) { PoolScheme = "http" }, CancellationToken.None);

        Assert.AreEqual(1L, fileTransferNumber);
        Assert.AreEqual(2L, connected.ConnectionNumber);
    }

    private static async Task ReturnToCacheAsync(PoolingConnector connector)
    {
        var result = await connector.ConnectAsync(Target(), CancellationToken.None);
        result.Connection!.MarkReusable();
        await result.Connection.DisposeAsync();
    }

    private static ConnectTarget Target() =>
        new("origin.example", 80, false) { PoolScheme = "http" };

    private sealed class FixedDatagramConnector(DatagramOpenResult result) : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }

    private sealed class UnusedChannel : IDatagramChannel
    {
        public System.Net.EndPoint ServerEndPoint { get; } = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 69);

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, System.Net.EndPoint destination, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
