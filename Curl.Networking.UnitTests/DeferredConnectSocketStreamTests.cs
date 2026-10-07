using System.Net;
using System.Net.Sockets;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DeferredConnectSocketStream" /> (BL-1158): a stream that reads and writes its socket as it is,
/// over a loopback socket pair on every platform.
/// </summary>
[TestClass]
public sealed class DeferredConnectSocketStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_WithMemory_SendsEveryByteToThePeer()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Socket client;
        Socket server;
        using (Diagnostics.Phase("connect"))
        {
            (client, server) = await ConnectedPairAsync(cancellation.Token);
        }

        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);
        Diagnostics.Arrange("loopback pair", "connected");
        Diagnostics.Arrange("bytes written", "GET");

        byte[] received;
        using (Diagnostics.Phase("io"))
        {
            await stream.WriteAsync("GET"u8.ToArray().AsMemory(), cancellation.Token);
            received = await ReceiveAsync(accepted, 3, cancellation.Token);
        }

        Diagnostics.Bytes("received by the peer", received);
        Diagnostics.Act("received length", received.Length);
        Diagnostics.Diff("received bytes", "GET"u8, received);
        CollectionAssert.AreEqual("GET"u8.ToArray(), received);
    }

    [TestMethod]
    public async Task WriteAsync_WithAnArrayRange_SendsOnlyThatRange()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Socket client;
        Socket server;
        using (Diagnostics.Phase("connect"))
        {
            (client, server) = await ConnectedPairAsync(cancellation.Token);
        }

        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);
        Diagnostics.Arrange("buffer", "xGETx");
        Diagnostics.Arrange("offset", 1);
        Diagnostics.Arrange("count", 3);

        byte[] received;
        using (Diagnostics.Phase("io"))
        {
            await stream.WriteAsync("xGETx"u8.ToArray(), 1, 3, cancellation.Token);
            received = await ReceiveAsync(accepted, 3, cancellation.Token);
        }

        Diagnostics.Bytes("received by the peer", received);
        Diagnostics.Act("received length", received.Length);
        Diagnostics.Diff("received bytes", "GET"u8, received);
        CollectionAssert.AreEqual("GET"u8.ToArray(), received);
    }

    [TestMethod]
    public async Task Write_WithAnArrayRange_SendsOnlyThatRange()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Socket client;
        Socket server;
        using (Diagnostics.Phase("connect"))
        {
            (client, server) = await ConnectedPairAsync(cancellation.Token);
        }

        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);
        Diagnostics.Arrange("buffer", "xGETx");
        Diagnostics.Arrange("offset", 1);
        Diagnostics.Arrange("count", 3);

        byte[] received;
        using (Diagnostics.Phase("io"))
        {
            stream.Write("xGETx"u8.ToArray(), 1, 3);
            received = await ReceiveAsync(accepted, 3, cancellation.Token);
        }

        Diagnostics.Bytes("received by the peer", received);
        Diagnostics.Act("received length", received.Length);
        Diagnostics.Diff("received bytes", "GET"u8, received);
        CollectionAssert.AreEqual("GET"u8.ToArray(), received);
    }

    [TestMethod]
    public async Task ReadAsync_WithMemory_ReceivesWhatThePeerSent()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Socket client;
        Socket server;
        using (Diagnostics.Phase("connect"))
        {
            (client, server) = await ConnectedPairAsync(cancellation.Token);
        }

        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);
        Diagnostics.Arrange("bytes sent by the peer", "OK!");
        var received = new byte[3];

        using (Diagnostics.Phase("io"))
        {
            await accepted.SendAsync("OK!"u8.ToArray(), cancellation.Token);
            await stream.ReadExactlyAsync(received.AsMemory(), cancellation.Token);
        }

        Diagnostics.Bytes("received by the stream", received);
        Diagnostics.Act("received length", received.Length);
        Diagnostics.Diff("received bytes", "OK!"u8, received);
        CollectionAssert.AreEqual("OK!"u8.ToArray(), received);
    }

    [TestMethod]
    public async Task ReadAsync_WithAnArrayRange_FillsOnlyThatRange()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Socket client;
        Socket server;
        using (Diagnostics.Phase("connect"))
        {
            (client, server) = await ConnectedPairAsync(cancellation.Token);
        }

        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);
        Diagnostics.Arrange("bytes sent by the peer", "O");
        Diagnostics.Arrange("offset", 1);
        Diagnostics.Arrange("count", 1);
        var received = new byte[3];

        int count;
        using (Diagnostics.Phase("io"))
        {
            await accepted.SendAsync("O"u8.ToArray(), cancellation.Token);
            count = await stream.ReadAsync(received, 1, 1, cancellation.Token);
        }

        Diagnostics.Bytes("buffer after the read", received);
        Diagnostics.Act("bytes read", count);
        Diagnostics.Assert("bytes read", 1, count);
        Assert.AreEqual(1, count);
        Diagnostics.Diff("buffer after the read", new byte[] { 0, (byte)'O', 0 }, received);
        CollectionAssert.AreEqual(new byte[] { 0, (byte)'O', 0 }, received);
    }

    [TestMethod]
    public async Task Read_WithAnArrayRange_FillsOnlyThatRange()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Socket client;
        Socket server;
        using (Diagnostics.Phase("connect"))
        {
            (client, server) = await ConnectedPairAsync(cancellation.Token);
        }

        using var accepted = server;
        await using var stream = new DeferredConnectSocketStream(client);
        Diagnostics.Arrange("bytes sent by the peer", "O");
        Diagnostics.Arrange("offset", 1);
        Diagnostics.Arrange("count", 1);
        await accepted.SendAsync("O"u8.ToArray(), cancellation.Token);
        var received = new byte[3];

        int count;
        using (Diagnostics.Phase("io"))
        {
            count = stream.Read(received, 1, 1);
        }

        Diagnostics.Bytes("buffer after the read", received);
        Diagnostics.Act("bytes read", count);
        Diagnostics.Assert("bytes read", 1, count);
        Assert.AreEqual(1, count);
        Diagnostics.Diff("buffer after the read", new byte[] { 0, (byte)'O', 0 }, received);
        CollectionAssert.AreEqual(new byte[] { 0, (byte)'O', 0 }, received);
    }

    [TestMethod]
    public async Task Flush_AndFlushAsync_ReturnWithoutSending()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await using var stream = new DeferredConnectSocketStream(socket);
        Diagnostics.Arrange("socket connected", socket.Connected);

        stream.Flush();
        await stream.FlushAsync(CancellationToken.None);

        var canRead = stream.CanRead;
        Diagnostics.Act("CanRead after flushes", canRead);
        Diagnostics.Act("socket connected after flushes", socket.Connected);
        Diagnostics.Assert("CanRead", true, canRead);
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public void Capabilities_AreReadAndWriteWithoutSeeking()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var stream = new DeferredConnectSocketStream(socket);
        Diagnostics.Arrange("socket", "unconnected TCP");

        var canRead = stream.CanRead;
        var canWrite = stream.CanWrite;
        var canSeek = stream.CanSeek;

        Diagnostics.Act("CanRead", canRead);
        Diagnostics.Act("CanWrite", canWrite);
        Diagnostics.Act("CanSeek", canSeek);
        Diagnostics.Assert("CanRead", true, canRead);
        Assert.IsTrue(stream.CanRead);
        Diagnostics.Assert("CanWrite", true, canWrite);
        Assert.IsTrue(stream.CanWrite);
        Diagnostics.Assert("CanSeek", false, canSeek);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public void LengthPositionSeekAndSetLength_ThrowNotSupported()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var stream = new DeferredConnectSocketStream(socket);
        Diagnostics.Arrange("members tried", "Length, Position get/set, Seek, SetLength");

        var length = Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        var positionGet = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        var positionSet = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        var seek = Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        var setLength = Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));

        Diagnostics.Act("Length throws", length.GetType().Name);
        Diagnostics.Act("Position get throws", positionGet.GetType().Name);
        Diagnostics.Act("Position set throws", positionSet.GetType().Name);
        Diagnostics.Act("Seek throws", seek.GetType().Name);
        Diagnostics.Act("SetLength throws", setLength.GetType().Name);
        Diagnostics.Assert("exception type of Length", nameof(NotSupportedException), length.GetType().Name);
        Assert.AreEqual(nameof(NotSupportedException), setLength.GetType().Name);
    }

    [TestMethod]
    public void Dispose_DisposesTheSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var stream = new DeferredConnectSocketStream(socket);
        Diagnostics.Arrange("socket", "unconnected TCP");

        stream.Dispose();

        var exception = Assert.ThrowsExactly<ObjectDisposedException>(() => socket.Available);
        Diagnostics.Act("exception type after dispose", exception.GetType().Name);
        Diagnostics.Assert("exception type", nameof(ObjectDisposedException), exception.GetType().Name);
        Assert.AreEqual(nameof(ObjectDisposedException), exception.GetType().Name);
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
