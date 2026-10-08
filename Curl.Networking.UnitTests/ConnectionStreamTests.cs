using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="ConnectionStream" /> over a <see cref="StreamConnection" /> on an
/// <see cref="InMemoryDuplexStream" /> pair: the asynchronous members carry bytes to the
/// connection and every synchronous or seeking member is refused.
/// </summary>
[TestClass]
public sealed class ConnectionStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Capabilities_ReadAndWriteButNeverSeek()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        Diagnostics.Arrange("transport", "in-memory duplex pair, client side");
        await using var stream = new ConnectionStream(new StreamConnection(client, null));

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
    public async Task ArrayOverloads_WriteToAndReadFromTheConnection()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null));
        var toWrite = new byte[] { 0, 7, 8, 0 };
        Diagnostics.Bytes("buffer to write (offset 1, count 2)", toWrite);
        Diagnostics.Arrange("write offset", 1);
        Diagnostics.Arrange("write count", 2);

        int read;
        var sent = new byte[2];
        var received = new byte[3];
        using (Diagnostics.Phase("io"))
        {
            await stream.WriteAsync(toWrite, 1, 2, CancellationToken.None);
            await stream.FlushAsync(CancellationToken.None);
            await server.ReadExactlyAsync(sent);
            await server.WriteAsync(new byte[] { 9 });
            read = await stream.ReadAsync(received, 1, 2, CancellationToken.None);
        }

        Diagnostics.Bytes("sent to server", sent);
        Diagnostics.Bytes("received buffer", received);
        Diagnostics.Act("read count", read);
        Diagnostics.Diff("sent bytes", new byte[] { 7, 8 }, sent);
        CollectionAssert.AreEqual(new byte[] { 7, 8 }, sent);
        Diagnostics.Assert("read count", 1, read);
        Assert.AreEqual(1, read);
        Diagnostics.Assert("received[1]", 9, received[1]);
        Assert.AreEqual(9, received[1]);
    }

    [TestMethod]
    public async Task MemoryOverloads_WriteToAndReadFromTheConnection()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null));
        Diagnostics.Arrange("byte written by client", 5);
        Diagnostics.Arrange("byte written by server", 6);

        int read;
        var sent = new byte[1];
        var received = new byte[1];
        using (Diagnostics.Phase("io"))
        {
            await stream.WriteAsync(new ReadOnlyMemory<byte>([5]));
            await server.ReadExactlyAsync(sent);
            await server.WriteAsync(new byte[] { 6 });
            read = await stream.ReadAsync(received.AsMemory());
        }

        Diagnostics.Act("sent[0]", sent[0]);
        Diagnostics.Act("read count", read);
        Diagnostics.Act("received[0]", received[0]);
        Diagnostics.Assert("sent[0]", 5, sent[0]);
        Assert.AreEqual(5, sent[0]);
        Diagnostics.Assert("read count", 1, read);
        Assert.AreEqual(1, read);
        Diagnostics.Assert("received[0]", 6, received[0]);
        Assert.AreEqual(6, received[0]);
    }

    [TestMethod]
    public async Task TransportEnded_IsSetOnlyByANonEmptyReadThatReturnsZero()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null));
        Diagnostics.Arrange("byte written by server before it closes", 1);

        await server.WriteAsync(new byte[] { 1 });
        var readOne = await stream.ReadAsync(new byte[1].AsMemory());
        var endedAfterData = stream.TransportEnded;
        await server.DisposeAsync();
        var readNothing = await stream.ReadAsync(Memory<byte>.Empty);
        var endedAfterEmptyRead = stream.TransportEnded;
        var readAtEnd = await stream.ReadAsync(new byte[1].AsMemory());
        var endedAtEnd = stream.TransportEnded;

        Diagnostics.Act("read one", readOne);
        Diagnostics.Act("ended after data", endedAfterData);
        Diagnostics.Act("read of empty memory", readNothing);
        Diagnostics.Act("ended after empty read", endedAfterEmptyRead);
        Diagnostics.Act("read at end", readAtEnd);
        Diagnostics.Act("ended at end", endedAtEnd);
        Diagnostics.Assert("read one", 1, readOne);
        Assert.AreEqual(1, readOne);
        Diagnostics.Assert("ended after data", false, endedAfterData);
        Assert.IsFalse(endedAfterData);
        Diagnostics.Assert("read of empty memory", 0, readNothing);
        Assert.AreEqual(0, readNothing);
        Diagnostics.Assert("ended after empty read", false, endedAfterEmptyRead);
        Assert.IsFalse(endedAfterEmptyRead);
        Diagnostics.Assert("read at end", 0, readAtEnd);
        Assert.AreEqual(0, readAtEnd);
        Diagnostics.Assert("ended at end", true, endedAtEnd);
        Assert.IsTrue(stream.TransportEnded);
    }

    [TestMethod]
    public async Task DisposeAsync_LeavesTheConnectionOpen()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        Diagnostics.Arrange("client disposed before", client.IsDisposed);
        var stream = new ConnectionStream(new StreamConnection(client, null));

        await stream.DisposeAsync();

        var disposed = client.IsDisposed;
        Diagnostics.Act("client disposed after", disposed);
        Diagnostics.Assert("client disposed", false, disposed);
        Assert.IsFalse(client.IsDisposed);
    }

    [TestMethod]
    public async Task SynchronousAndSeekingMembers_ThrowNotSupportedException()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null));
        Diagnostics.Arrange("members tried", "Length, Position get/set, Read, Write, Flush, Seek, SetLength");

        var length = Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        var positionGet = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        var positionSet = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        var read = Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        var write = Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        var flush = Assert.ThrowsExactly<NotSupportedException>(stream.Flush);
        var seek = Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        var setLength = Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));

        Diagnostics.Act("Length throws", length.GetType().Name);
        Diagnostics.Act("Position get throws", positionGet.GetType().Name);
        Diagnostics.Act("Position set throws", positionSet.GetType().Name);
        Diagnostics.Act("Read throws", read.GetType().Name);
        Diagnostics.Act("Write throws", write.GetType().Name);
        Diagnostics.Act("Flush throws", flush.GetType().Name);
        Diagnostics.Act("Seek throws", seek.GetType().Name);
        Diagnostics.Act("SetLength throws", setLength.GetType().Name);
        Diagnostics.Assert("exception type of Length", nameof(NotSupportedException), length.GetType().Name);
        Assert.AreEqual(nameof(NotSupportedException), setLength.GetType().Name);
    }
}
