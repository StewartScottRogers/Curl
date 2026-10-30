namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3MultiplexedStreamAdapter" />: asynchronous reads and writes pass
/// through without ending the stream, flushing does nothing, and the rest is not supported.
/// </summary>
[TestClass]
public sealed class Http3MultiplexedStreamAdapterTests
{
    [TestMethod]
    public async Task ReadAsyncAndWriteAsync_PassThroughWithoutEndingTheStream()
    {
        InMemoryMultiplexedStream stream = new InMemoryMultiplexedStream(0).Receive(0x41);
        using Http3MultiplexedStreamAdapter adapter = new(stream);
        byte[] buffer = new byte[4];

        int count = await adapter.ReadAsync(buffer, CancellationToken.None);
        await adapter.WriteAsync(new byte[] { 0x42 }, CancellationToken.None);
        await adapter.FlushAsync(CancellationToken.None);
        adapter.Flush();

        Assert.AreEqual(1, count);
        Assert.AreEqual(0x41, buffer[0]);
        CollectionAssert.AreEqual(new byte[] { 0x42 }, stream.Written.ToArray());
        Assert.IsFalse(stream.IsWriteEnded);
        Assert.IsTrue(adapter.CanRead);
        Assert.IsTrue(adapter.CanWrite);
        Assert.IsFalse(adapter.CanSeek);
    }

    [TestMethod]
    public void SynchronousAndSeekingMembers_AreNotSupported()
    {
        using Http3MultiplexedStreamAdapter adapter = new(new InMemoryMultiplexedStream(0));

        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Position = 1);
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.SetLength(1));
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Write(new byte[1], 0, 1));
    }
}
