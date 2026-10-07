using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class MultiplexedStreamAdapterTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadAsync_StreamWithBytes_PassesThemThroughThenZeroAtFin()
    {
        FakeMultiplexedStream stream = new(0, "abc"u8.ToArray());
        await using MultiplexedStreamAdapter adapter = new(stream);
        byte[] buffer = new byte[8];
        Diagnostics.Arrange("stream bytes", "abc");
        Diagnostics.Arrange("buffer size", buffer.Length);

        int first = await adapter.ReadAsync(buffer.AsMemory(), CancellationToken.None);
        int second = await adapter.ReadAsync(buffer.AsMemory(), CancellationToken.None);

        Diagnostics.Act("first read", first);
        Diagnostics.Act("second read", second);
        Diagnostics.Assert("first read", 3, first);
        Diagnostics.Assert("second read", 0, second);
        Assert.AreEqual(3, first);
        Assert.AreEqual(0, second);
    }

    [TestMethod]
    public async Task WriteAsync_Bytes_WritesThemWithoutEndingTheStream()
    {
        FakeMultiplexedStream stream = new(0, []);
        await using MultiplexedStreamAdapter adapter = new(stream);
        Diagnostics.Arrange("bytes to write", "ab");

        await adapter.WriteAsync("ab"u8.ToArray(), CancellationToken.None);
        await adapter.FlushAsync(CancellationToken.None);
        adapter.Flush();

        Diagnostics.Bytes("written", stream.Written.ToArray());
        Diagnostics.Act("ended by client", stream.IsEndedByClient);
        Diagnostics.Assert("ended by client", false, stream.IsEndedByClient);
        CollectionAssert.AreEqual("ab"u8.ToArray(), stream.Written.ToArray());
        Assert.IsFalse(stream.IsEndedByClient);
    }

    [TestMethod]
    public void Capabilities_AdapterOverAStream_ReadsAndWritesButCannotSeek()
    {
        Diagnostics.Arrange("stream", "empty FakeMultiplexedStream");
        using MultiplexedStreamAdapter adapter = new(new FakeMultiplexedStream(0, []));

        Diagnostics.Act("CanRead", adapter.CanRead);
        Diagnostics.Act("CanWrite", adapter.CanWrite);
        Diagnostics.Act("CanSeek", adapter.CanSeek);
        Diagnostics.Assert("CanRead", true, adapter.CanRead);
        Diagnostics.Assert("CanWrite", true, adapter.CanWrite);
        Diagnostics.Assert("CanSeek", false, adapter.CanSeek);
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
