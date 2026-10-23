using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The multi handle's <c>CURLMOPT_MAXCONNECTS</c>: once a transfer ends, the multi handle's oldest idle connections
/// beyond the cap are closed, 4 for each transfer in the handle when the cap is 0 (BL-2468, ADR-0605).
/// </summary>
public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    public async Task PerformAsync_MaxConnectsOneAndTwoTransfersEnded_LeavesOnlyTheNewestIdleConnection()
    {
        await using ManagedCurlEngine engine = new();
        IMultiTransfer multi = engine.CreateMulti();
        List<QueuedConnection> connections = await OpenIdleConnectionsAsync(engine, multi, 2);
        multi.Options = new MultiOptions { MaxConnects = 1 };
        ControlledEasyTransfer first = new(), second = new();
        multi.Add(first);
        multi.Add(second);
        await multi.PerformAsync(CancellationToken.None);

        first.Finish(CurlCode.Ok);
        second.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);
        await multi.DisposeAsync();

        Assert.IsTrue(connections[0].IsDisposed);
        Assert.IsFalse(connections[1].IsDisposed);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(2, 0)]
    public async Task PerformAsync_MaxConnectsZero_KeepsFourIdleConnectionsForEachTransferInTheHandle(int transfers, int expectedClosed)
    {
        await using ManagedCurlEngine engine = new();
        IMultiTransfer multi = engine.CreateMulti();
        List<QueuedConnection> connections = await OpenIdleConnectionsAsync(engine, multi, 5);
        ControlledEasyTransfer[] added = [.. Enumerable.Range(0, transfers).Select(_ => new ControlledEasyTransfer())];
        foreach (ControlledEasyTransfer transfer in added)
        {
            multi.Add(transfer);
        }

        await multi.PerformAsync(CancellationToken.None);

        added[0].Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);
        await multi.DisposeAsync();

        Assert.AreEqual(expectedClosed, connections.Count(connection => connection.IsDisposed));
        Assert.AreEqual(expectedClosed == 1, connections[0].IsDisposed);
    }

    [TestMethod]
    public async Task PerformAsync_TransferStillRunning_ClosesNoIdleConnection()
    {
        await using ManagedCurlEngine engine = new();
        IMultiTransfer multi = engine.CreateMulti();
        List<QueuedConnection> connections = await OpenIdleConnectionsAsync(engine, multi, 1);
        multi.Options = new MultiOptions { MaxConnects = 1 };
        multi.Add(new ControlledEasyTransfer());

        await multi.PerformAsync(CancellationToken.None);
        await multi.DisposeAsync();

        Assert.IsFalse(connections[0].IsDisposed);
    }

    /// <summary>Opens <paramref name="count" /> connections through the multi handle's pool, one host each, and hands each back idle, oldest first.</summary>
    private static async Task<List<QueuedConnection>> OpenIdleConnectionsAsync(ManagedCurlEngine engine, IMultiTransfer multi, int count)
    {
        List<QueuedConnection> opened = [];
        Assert.IsTrue(engine.Shared.MultiCaches.TryGetValue((ManagedMultiTransfer)multi, out ManagedEngineSharedState? caches));
        PoolingConnector pool = new(new NewConnectionConnector(opened), caches.Connections, configuration: null);
        for (int index = 0; index < count; index++)
        {
            ConnectResult result = await pool.ConnectAsync(new ConnectTarget($"h{index}.example", 80, false) { PoolScheme = "http" }, CancellationToken.None);
            result.Connection!.MarkReusable();
            await result.Connection.DisposeAsync();
        }

        return opened;
    }

    /// <summary>Gives a new <see cref="QueuedConnection" /> for every connect and keeps each one.</summary>
    private sealed class NewConnectionConnector(List<QueuedConnection> opened) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            QueuedConnection connection = new();
            opened.Add(connection);
            return ValueTask.FromResult(ConnectResult.Connected(connection));
        }
    }
}
