using System.Text;
using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

[TestClass]
public sealed class TcpConnectionStreamTests
{
    private const uint ClientNext = ScriptedTcpPeer.ClientInitialSequenceNumber + 1;
    private const uint ServerNext = ScriptedTcpPeer.ServerInitialSequenceNumber + 1;

    [TestMethod]
    public async Task ReadAsyncAndWriteAsync_ArrayOverloads_GoThroughTheConnection()
    {
        ScriptedTcpPeer peer = new();
        TcpConnectionStream stream = new(await peer.ConnectAsync());

        await stream.WriteAsync("GET"u8.ToArray(), 0, 3, CancellationToken.None);
        var request = await peer.ReceiveAsync();
        peer.Send(TcpFlags.Psh | TcpFlags.Ack, ServerNext, ClientNext + 3, "200"u8.ToArray());
        await peer.ReceiveAsync();
        var buffer = new byte[5];
        var count = await stream.ReadAsync(buffer, 1, 4, CancellationToken.None);

        Assert.AreEqual("GET", Encoding.ASCII.GetString(request.Payload));
        Assert.AreEqual(3, count);
        Assert.AreEqual("200", Encoding.ASCII.GetString(buffer, 1, 3));
    }

    [TestMethod]
    public async Task ReadAsyncAndWriteAsync_MemoryOverloads_GoThroughTheConnection()
    {
        ScriptedTcpPeer peer = new();
        TcpConnectionStream stream = new(await peer.ConnectAsync());

        await stream.WriteAsync("ab"u8.ToArray().AsMemory());
        await stream.FlushAsync();
        stream.Flush();
        var written = await peer.ReceiveAsync();
        peer.Send(TcpFlags.Psh | TcpFlags.Ack, ServerNext, ClientNext + 2, "c"u8.ToArray());
        await peer.ReceiveAsync();
        var buffer = new byte[1];
        var count = await stream.ReadAsync(buffer.AsMemory());

        Assert.AreEqual("ab", Encoding.ASCII.GetString(written.Payload));
        Assert.AreEqual(1, count);
        Assert.AreEqual((byte)'c', buffer[0]);
    }

    [TestMethod]
    public async Task DisposeAsync_SendsFin()
    {
        ScriptedTcpPeer peer = new();
        TcpConnectionStream stream = new(await peer.ConnectAsync());

        var closing = stream.DisposeAsync().AsTask();
        var finish = await peer.ReceiveAsync();
        peer.Send(TcpFlags.Ack, ServerNext, ClientNext + 1);
        await closing;

        Assert.AreEqual(TcpFlags.Fin | TcpFlags.Ack, finish.Flags);
    }

    [TestMethod]
    public async Task Dispose_Synchronous_AbortsWithoutFin()
    {
        ScriptedTcpPeer peer = new();
        TcpConnectionStream stream = new(await peer.ConnectAsync());
        var sendsBefore = peer.Channel.SendCount;

        stream.Dispose();

        Assert.AreEqual(sendsBefore, peer.Channel.SendCount);
    }

    [TestMethod]
    public async Task Members_SynchronousAndSeeking_AreRefused()
    {
        ScriptedTcpPeer peer = new();
        TcpConnectionStream stream = new(await peer.ConnectAsync());

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
