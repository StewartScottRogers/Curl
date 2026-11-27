using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>Pins the numbering of a connection opened for one still being opened (BL-6058, ADR-1463).</summary>
[TestClass]
public sealed class ConnectionCacheSubTransferNumberingTests
{
    [TestMethod]
    public async Task NumberSubTransferConnection_WithNoConnectionBeingOpened_TakesTheNextNumber()
    {
        await using ConnectionCache cache = new(TimeProvider.System);

        Assert.AreEqual(0, cache.NumberSubTransferConnection());
        Assert.AreEqual(1, cache.NumberNextConnection());
    }

    [TestMethod]
    public async Task NumberSubTransferConnection_WhileAConnectionIsBeingOpened_NumbersThatOneFirstAndOnce()
    {
        await using ConnectionCache cache = new(TimeProvider.System);
        ConnectionNumberReservation opening = cache.ReserveForOpening();

        long first = cache.NumberSubTransferConnection();
        long second = cache.NumberSubTransferConnection();

        Assert.AreEqual(1, first);
        Assert.AreEqual(2, second);
        Assert.AreEqual(0, cache.NumberOpened(opening));
    }

    [TestMethod]
    public async Task NumberOpened_WithNoSubTransferConnection_TakesTheNextNumber()
    {
        await using ConnectionCache cache = new(TimeProvider.System);
        ConnectionNumberReservation opening = cache.ReserveForOpening();
        cache.NumberNextConnection();

        Assert.AreEqual(1, cache.NumberOpened(opening));
    }

    [TestMethod]
    public async Task ConnectAsync_AFailedConnectThatNumberedASubTransferConnection_KeepsTheNumberBeforeIt()
    {
        await using ConnectionCache cache = new(TimeProvider.System);
        long subTransferNumber = -1;
        SubTransferNumberingConnector inner = new(() => subTransferNumber = cache.NumberSubTransferConnection());
        await using PoolingConnector pool = new(inner, cache, configuration: string.Empty);

        ConnectResult failed = await pool.ConnectAsync(new ConnectTarget("host.example", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(0, failed.ConnectionNumber);
        Assert.AreEqual(1, subTransferNumber);
    }

    private sealed class SubTransferNumberingConnector(Action resolve) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            resolve();
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: host.example"));
        }
    }
}
