using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>CGR-220: a refused connect names the ephemeral local port the system gave it, never port 0.</summary>
[TestClass]
public sealed class Cgr220TcpDialerIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task DialAsync_ToAPortNothingListensOn_ThrowsNamingTheEphemeralLocalPort()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        var endPoint = (IPEndPoint)closed.LocalEndpoint;
        closed.Stop();

        var exception = await Assert.ThrowsExactlyAsync<BoundConnectFailedException>(
            async () => await new TcpDialer().DialAsync(endPoint, cancellation.Token));

        Assert.AreNotEqual(0, exception.LocalEndPoint.Port);
        Assert.AreEqual(SocketError.ConnectionRefused, exception.SocketErrorCode);
    }
}
