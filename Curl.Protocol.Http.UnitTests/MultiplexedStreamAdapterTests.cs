using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class MultiplexedStreamAdapterTests
{
    [TestMethod]
    public async Task ReadAsync_StreamWithBytes_PassesThemThroughThenZeroAtFin()
    {
        FakeMultiplexedStream stream = new(0, "abc"u8.ToArray());
        await using MultiplexedStreamAdapter adapter = new(stream);
        byte[] buffer = new byte[8];

        int first = await adapter.ReadAsync(buffer.AsMemory(), CancellationToken.None);
        int second = await adapter.ReadAsync(buffer.AsMemory(), CancellationToken.None);

        Assert.AreEqual(3, first);
        Assert.AreEqual(0, second);
    }

    [TestMethod]
    public async Task WriteAsync_Bytes_WritesThemWithoutEndingTheStream()
    {
        FakeMultiplexedStream stream = new(0, []);
        await using MultiplexedStreamAdapter adapter = new(stream);

        await adapter.WriteAsync("ab"u8.ToArray(), CancellationToken.None);
        await adapter.FlushAsync(CancellationToken.None);
        adapter.Flush();

        CollectionAssert.AreEqual("ab"u8.ToArray(), stream.Written.ToArray());
        Assert.IsFalse(stream.IsEndedByClient);
    }

    [TestMethod]
    public void Capabilities_AdapterOverAStream_ReadsAndWritesButCannotSeek()
    {
        using MultiplexedStreamAdapter adapter = new(new FakeMultiplexedStream(0, []));

        Assert.IsTrue(adapter.CanRead);
        Assert.IsTrue(adapter.CanWrite);
        Assert.IsFalse(adapter.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => adapter.Write(new byte[1], 0, 1));
    }
}
