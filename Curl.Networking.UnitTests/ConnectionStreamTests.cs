using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="ConnectionStream" /> over a <see cref="StreamConnection" /> on an
/// <see cref="InMemoryDuplexStream" /> pair: the asynchronous members carry bytes to the
/// connection and every synchronous or seeking member is refused.
/// </summary>
[TestClass]
public sealed class ConnectionStreamTests
{
    [TestMethod]
    public async Task Capabilities_ReadAndWriteButNeverSeek()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null));

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public async Task ArrayOverloads_WriteToAndReadFromTheConnection()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null));

        await stream.WriteAsync(new byte[] { 0, 7, 8, 0 }, 1, 2, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
        var sent = new byte[2];
        await server.ReadExactlyAsync(sent);
        await server.WriteAsync(new byte[] { 9 });
        var received = new byte[3];
        var read = await stream.ReadAsync(received, 1, 2, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 7, 8 }, sent);
        Assert.AreEqual(1, read);
        Assert.AreEqual(9, received[1]);
    }

    [TestMethod]
    public async Task MemoryOverloads_WriteToAndReadFromTheConnection()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null));

        await stream.WriteAsync(new ReadOnlyMemory<byte>([5]));
        var sent = new byte[1];
        await server.ReadExactlyAsync(sent);
        await server.WriteAsync(new byte[] { 6 });
        var received = new byte[1];
        var read = await stream.ReadAsync(received.AsMemory());

        Assert.AreEqual(5, sent[0]);
        Assert.AreEqual(1, read);
        Assert.AreEqual(6, received[0]);
    }

    [TestMethod]
    public async Task TransportEnded_IsSetOnlyByANonEmptyReadThatReturnsZero()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null));

        await server.WriteAsync(new byte[] { 1 });
        var readOne = await stream.ReadAsync(new byte[1].AsMemory());
        var endedAfterData = stream.TransportEnded;
        await server.DisposeAsync();
        var readNothing = await stream.ReadAsync(Memory<byte>.Empty);
        var endedAfterEmptyRead = stream.TransportEnded;
        var readAtEnd = await stream.ReadAsync(new byte[1].AsMemory());

        Assert.AreEqual(1, readOne);
        Assert.IsFalse(endedAfterData);
        Assert.AreEqual(0, readNothing);
        Assert.IsFalse(endedAfterEmptyRead);
        Assert.AreEqual(0, readAtEnd);
        Assert.IsTrue(stream.TransportEnded);
    }

    [TestMethod]
    public async Task DisposeAsync_LeavesTheConnectionOpen()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        var stream = new ConnectionStream(new StreamConnection(client, null));

        await stream.DisposeAsync();

        Assert.IsFalse(client.IsDisposed);
    }

    [TestMethod]
    public async Task SynchronousAndSeekingMembers_ThrowNotSupportedException()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null));

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(stream.Flush);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
