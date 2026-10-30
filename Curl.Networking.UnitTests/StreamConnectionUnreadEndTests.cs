using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins the check <see cref="StreamConnection" /> runs before each write over a real loopback
/// socket: a peer's FIN the stream has not read yet is reported, and a socket with nothing
/// waiting is not (AF-0457, BL-4180). The peer sends no byte, only its FIN.
/// </summary>
[TestClass]
public sealed class StreamConnectionUnreadEndTests
{
    [TestMethod]
    public async Task HasUnreadBytesOrEndBeforeWrite_OverASocketWhosePeerSentItsFin_IsTrue()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        using TcpClient peer = await listener.AcceptTcpClientAsync();
        await using var connection = new StreamConnection(client.GetStream(), remoteEndPoint: null);

        peer.Client.Shutdown(SocketShutdown.Send);
        bool finArrived = client.Client.Poll(TimeSpan.FromSeconds(5), SelectMode.SelectRead);

        Assert.IsTrue(finArrived, "The peer's FIN did not arrive.");
        Assert.AreEqual(0, client.Client.Available);
        Assert.IsTrue(connection.HasUnreadBytesOrEndBeforeWrite(client.GetStream()));
    }

    [TestMethod]
    public async Task HasUnreadBytesOrEndBeforeWrite_OverASocketWithNothingWaiting_IsFalse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        using TcpClient peer = await listener.AcceptTcpClientAsync();
        await using var connection = new StreamConnection(client.GetStream(), remoteEndPoint: null);

        Assert.IsFalse(connection.HasUnreadBytesOrEndBeforeWrite(client.GetStream()));
    }
}
