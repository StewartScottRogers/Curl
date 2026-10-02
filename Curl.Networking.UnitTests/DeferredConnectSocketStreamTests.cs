using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DeferredConnectSocketStream" /> (BL-1158): a stream that reads and writes its socket as it is,
/// over a loopback socket pair on every platform.
/// </summary>
[TestClass]
public sealed class DeferredConnectSocketStreamTests
{
    [TestMethod]
    public async Task WriteAsync_WithMemory_SendsEveryByteToThePeer()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (client, server) = await ConnectedPairAsync(cancellation.Token);
        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);

        await stream.WriteAsync("GET"u8.ToArray().AsMemory(), cancellation.Token);

        CollectionAssert.AreEqual("GET"u8.ToArray(), await ReceiveAsync(accepted, 3, cancellation.Token));
    }

    [TestMethod]
    public async Task WriteAsync_WithAnArrayRange_SendsOnlyThatRange()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (client, server) = await ConnectedPairAsync(cancellation.Token);
        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);

        await stream.WriteAsync("xGETx"u8.ToArray(), 1, 3, cancellation.Token);

        CollectionAssert.AreEqual("GET"u8.ToArray(), await ReceiveAsync(accepted, 3, cancellation.Token));
    }

    [TestMethod]
    public async Task Write_WithAnArrayRange_SendsOnlyThatRange()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (client, server) = await ConnectedPairAsync(cancellation.Token);
        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);

        stream.Write("xGETx"u8.ToArray(), 1, 3);

        CollectionAssert.AreEqual("GET"u8.ToArray(), await ReceiveAsync(accepted, 3, cancellation.Token));
    }

    [TestMethod]
    public async Task ReadAsync_WithMemory_ReceivesWhatThePeerSent()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (client, server) = await ConnectedPairAsync(cancellation.Token);
        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);
        await accepted.SendAsync("OK!"u8.ToArray(), cancellation.Token);
        var received = new byte[3];

        await stream.ReadExactlyAsync(received.AsMemory(), cancellation.Token);

        CollectionAssert.AreEqual("OK!"u8.ToArray(), received);
    }

    [TestMethod]
    public async Task ReadAsync_WithAnArrayRange_FillsOnlyThatRange()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (client, server) = await ConnectedPairAsync(cancellation.Token);
        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);
        await accepted.SendAsync("O"u8.ToArray(), cancellation.Token);
        var received = new byte[3];

        var count = await stream.ReadAsync(received, 1, 1, cancellation.Token);

        Assert.AreEqual(1, count);
        CollectionAssert.AreEqual(new byte[] { 0, (byte)'O', 0 }, received);
    }

    [TestMethod]
    public async Task Read_WithAnArrayRange_FillsOnlyThatRange()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (client, server) = await ConnectedPairAsync(cancellation.Token);
        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);
        await accepted.SendAsync("O"u8.ToArray(), cancellation.Token);
        var received = new byte[3];

        var count = stream.Read(received, 1, 1);

        Assert.AreEqual(1, count);
        CollectionAssert.AreEqual(new byte[] { 0, (byte)'O', 0 }, received);
    }

    [TestMethod]
    public async Task Flush_AndFlushAsync_ReturnWithoutSending()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await using var stream = new DeferredConnectSocketStream(socket);

        stream.Flush();
        await stream.FlushAsync(CancellationToken.None);

        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public void Capabilities_AreReadAndWriteWithoutSeeking()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var stream = new DeferredConnectSocketStream(socket);

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public void LengthPositionSeekAndSetLength_ThrowNotSupported()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var stream = new DeferredConnectSocketStream(socket);

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void Dispose_DisposesTheSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var stream = new DeferredConnectSocketStream(socket);

        stream.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => socket.Available);
    }

    private static async Task<(Socket Client, Socket Server)> ConnectedPairAsync(CancellationToken cancellationToken)
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen();
        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(listener.LocalEndPoint!, cancellationToken);
        var server = await listener.AcceptAsync(cancellationToken);
        return (client, server);
    }

    private static async Task<byte[]> ReceiveAsync(Socket socket, int count, CancellationToken cancellationToken)
    {
        var received = new byte[count];
        await using var stream = new NetworkStream(socket);
        await stream.ReadExactlyAsync(received, cancellationToken);
        return received;
    }
}
