using System.Net;
using System.Net.Sockets;
using Curl.Conformance.Differential.Protocols.Proxy;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Proxy;

[TestClass]
public sealed class ProxyTunnelTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [TestMethod]
    public async Task RelayAsync_TargetEndsFirst_ClosesTheClientAndReturns()
    {
        using var cts = new CancellationTokenSource(Limit);
        var (client, connection) = await ConnectLoopbackPairAsync(cts.Token);
        var (target, tunnelRemote) = await ConnectLoopbackPairAsync(cts.Token);
        using var clientStream = client;
        using var connectionStream = connection;
        using var targetStream = target;

        var relay = ProxyTunnel.RelayAsync(connectionStream, new FixedConnector(tunnelRemote), "example.test", 80, cts.Token);
        await target.WriteAsync(new byte[] { 1, 2, 3 }, cts.Token);
        var buffer = new byte[8];
        Assert.AreEqual(3, await ReadAtLeastAsync(client, buffer, 3, cts.Token));
        target.Dispose();

        await relay;
        Assert.AreEqual(0, await client.ReadAsync(buffer, cts.Token));
    }

    [TestMethod]
    public async Task RelayAsync_ClientEndsFirst_ClosesTheTargetAndReturns()
    {
        using var cts = new CancellationTokenSource(Limit);
        var (client, connection) = await ConnectLoopbackPairAsync(cts.Token);
        var (target, tunnelRemote) = await ConnectLoopbackPairAsync(cts.Token);
        using var targetStream = target;
        using var connectionStream = connection;

        var relay = ProxyTunnel.RelayAsync(connectionStream, new FixedConnector(tunnelRemote), "localhost.test", 80, cts.Token);
        await client.WriteAsync(new byte[] { 4, 5 }, cts.Token);
        var buffer = new byte[8];
        Assert.AreEqual(2, await ReadAtLeastAsync(target, buffer, 2, cts.Token));
        client.Dispose();

        await relay;
        Assert.AreEqual(0, await target.ReadAsync(buffer, cts.Token));
    }

    private static async Task<int> ReadAtLeastAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            Assert.AreNotEqual(0, read);
            total += read;
        }

        return total;
    }

    private static async Task<(NetworkStream Left, NetworkStream Right)> ConnectLoopbackPairAsync(CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var left = new TcpClient();
        var accepting = listener.AcceptTcpClientAsync(cancellationToken).AsTask();
        await left.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, cancellationToken);
        var right = await accepting;
        return (left.GetStream(), right.GetStream());
    }

    private sealed class FixedConnector(Stream stream) : IProxyTunnelConnector
    {
        public Task<Stream?> ConnectAsync(string host, int port, CancellationToken cancellationToken) => Task.FromResult<Stream?>(stream);
    }
}
