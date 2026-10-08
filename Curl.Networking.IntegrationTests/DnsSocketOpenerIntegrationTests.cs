using System.Net;

namespace Curl.Networking;

[TestClass]
public sealed class DnsSocketOpenerIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task ConnectStreamAsync_ToALoopbackListener_ConnectsFromTheLocalAddress()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var server = (IPEndPoint)listener.LocalEndpoint;

            var stream = await new DnsSocketOpener().ConnectStreamAsync(server, IPAddress.Loopback, CancellationToken.None);
            await using (stream)
            {
                using var accepted = await listener.AcceptTcpClientAsync();
                Assert.AreEqual(IPAddress.Loopback, ((IPEndPoint)accepted.Client.RemoteEndPoint!).Address);
            }
        }
        finally
        {
            listener.Stop();
        }
    }
}
