using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_WithNullInnerConnector_ThrowsArgumentNullException()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("inner connector", "null");

        ArgumentNullException exception;
        using (diagnostics.Phase("construct"))
        {
            exception = Assert.ThrowsExactly<ArgumentNullException>(
                () => new PoolingConnector(null!, new ConnectionCache(_time), configuration: null));
        }

        diagnostics.Act("parameter name", exception.ParamName);
        diagnostics.Assert("parameter name", "innerConnector", exception.ParamName);
        Assert.AreEqual("innerConnector", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("cache", "null");

        ArgumentNullException exception;
        using (diagnostics.Phase("construct"))
        {
            exception = Assert.ThrowsExactly<ArgumentNullException>(
                () => new PoolingConnector(new FakeConnector(), null!, configuration: null));
        }

        diagnostics.Act("parameter name", exception.ParamName);
        diagnostics.Assert("parameter name", "cache", exception.ParamName);
        Assert.AreEqual("cache", exception.ParamName);
    }

    [TestMethod]
    public async Task ConnectAsync_InALaterGroupWithAnEqualConfiguration_ReusesTheEarlierGroupsConnection()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("configurations", "settings and an equal copy of it");
        var cache = new ConnectionCache(_time);
        var firstInner = new FakeConnector();
        var laterInner = new FakeConnector();
        var first = new PoolingConnector(firstInner, cache, "settings");
        var later = new PoolingConnector(laterInner, cache, string.Concat("sett", "ings"));
        await ReturnToCacheAsync(first);
        await first.DisposeAsync();

        ConnectResult reused;
        using (diagnostics.Phase("later connect"))
        {
            reused = await later.ConnectAsync(Target(), CancellationToken.None);
        }

        diagnostics.Act("reused", reused.IsReused);
        diagnostics.Act("connection number", reused.ConnectionNumber);
        diagnostics.Act("later inner targets", laterInner.Targets.Count);
        diagnostics.Assert("connection number", 0L, reused.ConnectionNumber);
        Assert.IsTrue(reused.IsReused);
        Assert.AreEqual(0L, reused.ConnectionNumber);
        Assert.IsEmpty(laterInner.Targets);
        Assert.IsFalse(firstInner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_InALaterGroupWithADifferentConfiguration_OpensItsOwnWithTheNextNumber()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("configurations", "verifies the peer versus insecure");
        var cache = new ConnectionCache(_time);
        var firstInner = new FakeConnector();
        var laterInner = new FakeConnector();
        var first = new PoolingConnector(firstInner, cache, "verifies the peer");
        var later = new PoolingConnector(laterInner, cache, "insecure");
        await ReturnToCacheAsync(first);

        ConnectResult opened;
        using (diagnostics.Phase("later connect"))
        {
            opened = await later.ConnectAsync(Target(), CancellationToken.None);
        }

        diagnostics.Act("reused", opened.IsReused);
        diagnostics.Act("connection number", opened.ConnectionNumber);
        diagnostics.Act("later inner targets", laterInner.Targets.Count);
        diagnostics.Assert("connection number", 1L, opened.ConnectionNumber);
        Assert.IsFalse(opened.IsReused);
        Assert.AreEqual(1L, opened.ConnectionNumber);
        Assert.HasCount(1, laterInner.Targets);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAConfigurationAgainstAPoolWithout_OpensItsOwn()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("configurations", "none pooled, settings requested");
        var cache = new ConnectionCache(_time);
        var laterInner = new FakeConnector();
        await ReturnToCacheAsync(new PoolingConnector(new FakeConnector(), cache, configuration: null));

        ConnectResult opened;
        using (diagnostics.Phase("later connect"))
        {
            opened = await new PoolingConnector(laterInner, cache, "settings").ConnectAsync(Target(), CancellationToken.None);
        }

        diagnostics.Act("reused", opened.IsReused);
        diagnostics.Act("later inner targets", laterInner.Targets.Count);
        diagnostics.Assert("later inner targets", 1, laterInner.Targets.Count);
        Assert.IsFalse(opened.IsReused);
        Assert.HasCount(1, laterInner.Targets);
    }

    [TestMethod]
    public async Task Over_OpensThroughTheGivenConnectorNumberedInTheSamePool()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("waits for multiplexing", true);
        var inner = new FakeConnector();
        var otherInner = new FakeConnector();
        await using var pool = new PoolingConnector(inner, _time) { WaitsForMultiplexing = true };
        var other = pool.Over(otherInner);

        ConnectResult first, second;
        using (diagnostics.Phase("connect through both"))
        {
            first = await pool.ConnectAsync(new ConnectTarget("origin.example", 21, false), CancellationToken.None);
            second = await other.ConnectAsync(new ConnectTarget("192.0.2.1", 1025, false), CancellationToken.None);
        }

        diagnostics.Act("first connection number", first.ConnectionNumber);
        diagnostics.Act("second connection number", second.ConnectionNumber);
        diagnostics.Assert("second connection number", 1L, second.ConnectionNumber);
        Assert.AreEqual(0L, first.ConnectionNumber);
        Assert.AreEqual(1L, second.ConnectionNumber);
        Assert.HasCount(1, inner.Targets);
        Assert.HasCount(1, otherInner.Targets);
        Assert.IsTrue(other.WaitsForMultiplexing);
    }

    [TestMethod]
    public async Task Over_ReusesAConnectionItsOwnerPooledAndLeavesTheCacheOpenWhenDisposed()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("target", "http://origin.example:80");
        var inner = new FakeConnector();
        await using var pool = new PoolingConnector(inner, _time);
        var other = pool.Over(new FakeConnector());
        await ReturnToCacheAsync(pool);

        ConnectResult reused;
        using (diagnostics.Phase("connect over"))
        {
            reused = await other.ConnectAsync(Target(), CancellationToken.None);
            await other.DisposeAsync();
        }

        diagnostics.Act("reused", reused.IsReused);
        diagnostics.Act("pooled connection disposed", inner.Opened[0].IsDisposed);
        diagnostics.Assert("reused", true, reused.IsReused);
        Assert.IsTrue(reused.IsReused);
        Assert.IsFalse(inner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAConnectorOverAGivenCache_LeavesItsIdleConnectionsOpen()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("target", "http://origin.example:80");
        var cache = new ConnectionCache(_time);
        var inner = new FakeConnector();
        var connector = new PoolingConnector(inner, cache, configuration: null);
        await ReturnToCacheAsync(connector);

        using (diagnostics.Phase("dispose connector"))
        {
            await connector.DisposeAsync();
        }

        diagnostics.Act("idle connection disposed", inner.Opened[0].IsDisposed);
        diagnostics.Assert("idle connection disposed", false, inner.Opened[0].IsDisposed);
        Assert.IsFalse(inner.Opened[0].IsDisposed);
    }

    [TestMethod]
    public async Task NumberingDatagrams_WithNullConnector_ThrowsArgumentNullException()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("datagram connector", "null");
        await using var pool = new PoolingConnector(new FakeConnector(), _time);

        ArgumentNullException exception;
        using (diagnostics.Phase("number datagrams"))
        {
            exception = Assert.ThrowsExactly<ArgumentNullException>(() => pool.NumberingDatagrams(null!));
        }

        diagnostics.Act("parameter name", exception.ParamName);
        diagnostics.Assert("parameter name", "datagramConnector", exception.ParamName);
        Assert.AreEqual("datagramConnector", exception.ParamName);
    }

    [TestMethod]
    public async Task NumberingDatagrams_AfterATcpConnection_NumbersEachOpenNextInThePool()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("datagram endpoint", "tftp.example:69");
        await using var pool = new PoolingConnector(new FakeConnector(), _time);
        var channel = new UnusedChannel();
        var datagrams = pool.NumberingDatagrams(new FixedDatagramConnector(DatagramOpenResult.Opened(channel)));
        await ReturnToCacheAsync(pool);

        DatagramOpenResult first, second;
        using (diagnostics.Phase("open twice"))
        {
            first = await datagrams.OpenAsync("tftp.example", 69, CancellationToken.None);
            second = await datagrams.OpenAsync("tftp.example", 69, CancellationToken.None);
        }

        diagnostics.Act("first connection number", first.ConnectionNumber);
        diagnostics.Act("second connection number", second.ConnectionNumber);
        diagnostics.Assert("second connection number", 2L, second.ConnectionNumber);
        Assert.AreSame(channel, first.Channel);
        Assert.AreEqual(1L, first.ConnectionNumber);
        Assert.AreEqual(2L, second.ConnectionNumber);
    }

    [TestMethod]
    public async Task NumberingDatagrams_WhenTheOpenFails_NumbersTheFailureAndTheNextTcpConnectionAfterIt()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("failure", "Could not resolve host: tftp.example");
        await using var pool = new PoolingConnector(new FakeConnector(), _time);
        var datagrams = pool.NumberingDatagrams(new FixedDatagramConnector(DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: tftp.example")));

        DatagramOpenResult failed;
        ConnectResult connected;
        using (diagnostics.Phase("open then connect"))
        {
            failed = await datagrams.OpenAsync("tftp.example", 69, CancellationToken.None);
            connected = await pool.ConnectAsync(Target(), CancellationToken.None);
        }

        diagnostics.Act("failed exit code", failed.ExitCode);
        diagnostics.Act("failed connection number", failed.ConnectionNumber);
        diagnostics.Act("connected connection number", connected.ConnectionNumber);
        diagnostics.Assert("failed error message", "Could not resolve host: tftp.example", failed.ErrorMessage);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, failed.ExitCode);
        Assert.AreEqual("Could not resolve host: tftp.example", failed.ErrorMessage);
        Assert.AreEqual(0L, failed.ConnectionNumber);
        Assert.AreEqual(1L, connected.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectionNumbers_AfterATcpConnection_GivesTheNextNumberAndTheNextConnectionTheOneAfter()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("next target", "http://other.example:80");
        await using var pool = new PoolingConnector(new FakeConnector(), _time);
        await ReturnToCacheAsync(pool);

        long fileTransferNumber;
        ConnectResult connected;
        using (diagnostics.Phase("number then connect"))
        {
            fileTransferNumber = pool.ConnectionNumbers.NumberNextConnection();
            connected = await pool.ConnectAsync(new ConnectTarget("other.example", 80, false) { PoolScheme = "http" }, CancellationToken.None);
        }

        diagnostics.Act("file transfer number", fileTransferNumber);
        diagnostics.Act("connected connection number", connected.ConnectionNumber);
        diagnostics.Assert("connected connection number", 2L, connected.ConnectionNumber);
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
