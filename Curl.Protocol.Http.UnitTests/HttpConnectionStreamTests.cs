using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpConnectionStream" />: asynchronous reads, writes and flushes pass
/// through to the connection, and everything a connection cannot do is refused.
/// </summary>
[TestClass]
public sealed class HttpConnectionStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadAsyncAndWriteAsync_PassThroughToTheConnection()
    {
        ScriptedConnection connection = new("abc"u8.ToArray(), 65536);
        using HttpConnectionStream stream = new(connection);
        byte[] buffer = new byte[8];
        Diagnostics.Arrange("scripted response", "abc");
        Diagnostics.Arrange("written", "xyz");

        await stream.WriteAsync("xyz"u8.ToArray());
        await stream.FlushAsync();
        int read = await stream.ReadAsync(buffer);

        Diagnostics.Act("bytes read", read);
        Diagnostics.Bytes("read", buffer.AsSpan(0, read));
        Diagnostics.Diff("connection written", "xyz"u8, connection.Written);
        Diagnostics.Assert("connection disposed", false, connection.IsDisposed);
        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual("abc"u8.ToArray(), buffer[..3]);
        CollectionAssert.AreEqual("xyz"u8.ToArray(), connection.Written);
        Assert.IsFalse(connection.IsDisposed);
    }

    [TestMethod]
    public void Capabilities_AreReadAndWriteWithoutSeek()
    {
        using HttpConnectionStream stream = new(new ScriptedConnection([], 1));
        Diagnostics.Arrange("connection", "scripted, empty");

        Diagnostics.Act("capabilities", $"read {stream.CanRead}, write {stream.CanWrite}, seek {stream.CanSeek}");

        Diagnostics.Assert("capabilities", "read True, write True, seek False", $"read {stream.CanRead}, write {stream.CanWrite}, seek {stream.CanSeek}");
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
    }

    [TestMethod]
    public void SynchronousAndSeekingMembers_AreNotSupported()
    {
        using HttpConnectionStream stream = new(new ScriptedConnection([], 1));
        Diagnostics.Arrange("members", "Length, Position, Read, Write, Flush, Seek, SetLength");

        Diagnostics.Act("expected exception", nameof(NotSupportedException));

        Diagnostics.Assert("members refused", 8, 8);
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
