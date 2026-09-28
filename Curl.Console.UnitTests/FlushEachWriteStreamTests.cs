namespace Curl.Console;

/// <summary>
/// Pins the <c>-N</c> stream: write-only, every write passed through unchanged and followed by a
/// flush of the wrapped stream, and flushes passed through.
/// </summary>
[TestClass]
public sealed class FlushEachWriteStreamTests
{
    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using FlushEachWriteStream stream = new(new MemoryStream());

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using FlushEachWriteStream stream = new(new MemoryStream());

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void Write_FlushesAfterEachWrite()
    {
        WriteAndFlushRecordingStream inner = new();
        using FlushEachWriteStream stream = new(inner);

        stream.Write("_ab_"u8.ToArray(), 1, 2);
        stream.Write("c"u8.ToArray(), 0, 1);

        CollectionAssert.AreEqual(new[] { "write:ab", "flush", "write:c", "flush" }, inner.Events);
    }

    [TestMethod]
    public async Task WriteAsync_FlushesAfterEachWrite()
    {
        WriteAndFlushRecordingStream inner = new();
        await using FlushEachWriteStream stream = new(inner);

        await stream.WriteAsync("ab"u8.ToArray());
        await stream.WriteAsync("c"u8.ToArray());

        CollectionAssert.AreEqual(new[] { "write:ab", "flush", "write:c", "flush" }, inner.Events);
    }

    [TestMethod]
    public async Task Flush_PassesThrough()
    {
        WriteAndFlushRecordingStream inner = new();
        using FlushEachWriteStream stream = new(inner);

        stream.Flush();
        await stream.FlushAsync();

        CollectionAssert.AreEqual(new[] { "flush", "flush" }, inner.Events);
    }

    [TestMethod]
    public void Dispose_LeavesTheInnerStreamOpen()
    {
        using MemoryStream inner = new();
        FlushEachWriteStream stream = new(inner);

        stream.Dispose();

        Assert.IsTrue(inner.CanWrite);
    }
}
