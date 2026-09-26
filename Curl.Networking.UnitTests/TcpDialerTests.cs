using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpDialer" />. The loopback test opens real sockets and is the one
/// <c>Integration</c> test in this project; the argument check needs none.
/// </summary>
[TestClass]
public sealed class TcpDialerTests
{
    [TestMethod]
    public async Task DialAsync_WithNullEndPoint_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new TcpDialer().DialAsync(null!, CancellationToken.None));

        Assert.AreEqual("endPoint", exception.ParamName);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DialAsync_OverLoopback_CarriesBytesBothWaysThenFailsOnceTheListenerStops()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endPoint = (IPEndPoint)listener.LocalEndpoint;
        var dialer = new TcpDialer();

        await using (var connection = await dialer.DialAsync(endPoint, cancellation.Token))
        {
            using var accepted = await listener.AcceptTcpClientAsync(cancellation.Token);
            var serverStream = accepted.GetStream();

            await connection.WriteAsync(new byte[] { 1, 2, 3 }, cancellation.Token);
            await connection.FlushAsync(cancellation.Token);
            var received = new byte[3];
            await serverStream.ReadExactlyAsync(received, cancellation.Token);
            await serverStream.WriteAsync(new byte[] { 9 }, cancellation.Token);
            var reply = new byte[1];
            var replyLength = await connection.ReadAsync(reply, cancellation.Token);

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, received);
            Assert.AreEqual(1, replyLength);
            Assert.AreEqual(9, reply[0]);
            Assert.IsFalse(connection.IsSecure);
            Assert.AreEqual(endPoint, connection.RemoteEndPoint);
        }

        listener.Stop();

        await Assert.ThrowsAsync<SocketException>(
            async () => await dialer.DialAsync(endPoint, cancellation.Token));
    }
}
