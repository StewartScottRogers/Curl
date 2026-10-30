namespace Curl.Extension.Grpc.Client;

/// <summary>Checks that the connection is an asynchronous, unseekable read-write stream that flushes each write.</summary>
[TestClass]
public sealed class ConnectionStreamTests
{
    [TestMethod]
    public async Task ReadAndWrite_GoToTheConnection()
    {
        var connection = new ScriptedConnection([1, 2]);
        using var stream = new ConnectionStream(connection);
        byte[] buffer = new byte[4];

        int read = await stream.ReadAsync(buffer.AsMemory());
        await stream.WriteAsync(new byte[] { 3 }.AsMemory());
        await stream.FlushAsync();

        Assert.AreEqual(2, read);
        CollectionAssert.AreEqual(new byte[] { 3 }, connection.Sent);
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public void SynchronousAndSeekingMembers_AreNotSupported()
    {
        using var stream = new ConnectionStream(new ScriptedConnection([]));

        _ = Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        _ = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(stream.Flush);
        _ = Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        _ = Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
