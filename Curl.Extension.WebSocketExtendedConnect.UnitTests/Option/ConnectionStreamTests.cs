namespace Curl.Extension.WebSocketExtendedConnect.Option;

[TestClass]
public sealed class ConnectionStreamTests
{
    [TestMethod]
    public async Task ReadAndWrite_PassToTheConnectionAndFlushEachWrite()
    {
        MemoryStream inner = new();
        inner.Write([1, 2, 3]);
        inner.Position = 0;
        StreamConnection connection = new(inner);
        using ConnectionStream stream = new(connection);

        byte[] buffer = new byte[3];
        Assert.AreEqual(3, await stream.ReadAsync(buffer));
        await stream.WriteAsync(new byte[] { 4 });
        await stream.FlushAsync();

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, inner.ToArray());
        Assert.AreEqual(2, connection.Flushes);
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public void SynchronousAndSeekingMembers_AreNotSupported()
    {
        using ConnectionStream stream = new(new StreamConnection(new MemoryStream()));

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(stream.Flush);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
