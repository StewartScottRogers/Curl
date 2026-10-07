using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

[TestClass]
public sealed class FastOpenSocketOptionIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task DialAsync_WithFastOpen_ConnectsAndCarriesBytes()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen();

        var dialed = await new TcpDialer(new TcpSocketOptions(FastOpen: true)).DialAsync((IPEndPoint)listener.LocalEndPoint!, cancellation.Token);
        await using var connection = dialed.Connection;
        await connection.WriteAsync("GET"u8.ToArray(), cancellation.Token);
        using var accepted = await listener.AcceptAsync(cancellation.Token);
        var received = new byte[3];
        await new NetworkStream(accepted).ReadExactlyAsync(received, cancellation.Token);

        CollectionAssert.AreEqual("GET"u8.ToArray(), received);
        Assert.AreEqual(IPAddress.Loopback, dialed.LocalEndPoint.Address);
    }
}
