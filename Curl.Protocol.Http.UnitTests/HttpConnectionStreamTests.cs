using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpConnectionStream" />: asynchronous reads, writes and flushes pass
/// through to the connection, and everything a connection cannot do is refused.
/// </summary>
[TestClass]
public sealed class HttpConnectionStreamTests
{
    [TestMethod]
    public async Task ReadAsyncAndWriteAsync_PassThroughToTheConnection()
    {
        ScriptedConnection connection = new("abc"u8.ToArray(), 65536);
        using HttpConnectionStream stream = new(connection);
        byte[] buffer = new byte[8];

        await stream.WriteAsync("xyz"u8.ToArray());
        await stream.FlushAsync();
        int read = await stream.ReadAsync(buffer);

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual("abc"u8.ToArray(), buffer[..3]);
        CollectionAssert.AreEqual("xyz"u8.ToArray(), connection.Written);
        Assert.IsFalse(connection.IsDisposed);
    }

    [TestMethod]
    public void Capabilities_AreReadAndWriteWithoutSeek()
    {
        using HttpConnectionStream stream = new(new ScriptedConnection([], 1));

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public void SynchronousAndSeekingMembers_AreNotSupported()
    {
        using HttpConnectionStream stream = new(new ScriptedConnection([], 1));

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
