namespace Curl.Console;

/// <summary>
/// Pins the text-mode stream: write-only, each line feed written as CR LF, every other byte
/// unchanged, and flushes passed through.
/// </summary>
[TestClass]
public sealed class LineFeedToCrLfStreamTests
{
    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using LineFeedToCrLfStream stream = new(new MemoryStream());

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using LineFeedToCrLfStream stream = new(new MemoryStream());

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void Write_WritesEachLineFeedAsCrLfAndLeavesCarriageReturns()
    {
        using MemoryStream inner = new();
        using LineFeedToCrLfStream stream = new(inner);

        stream.Write("_a\nb\r\n_"u8.ToArray(), 1, 5);

        CollectionAssert.AreEqual("a\r\nb\r\r\n"u8.ToArray(), inner.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_WritesEachLineFeedAsCrLf()
    {
        using MemoryStream inner = new();
        using LineFeedToCrLfStream stream = new(inner);

        await stream.WriteAsync("\n1\n"u8.ToArray().AsMemory());

        CollectionAssert.AreEqual("\r\n1\r\n"u8.ToArray(), inner.ToArray());
    }

    [TestMethod]
    public async Task Flush_ReachesTheInnerStream()
    {
        using BufferedStream inner = new(new MemoryStream());
        using LineFeedToCrLfStream stream = new(inner);

        stream.Write([1], 0, 1);
        stream.Flush();
        await stream.WriteAsync(new byte[] { 2 }.AsMemory());
        await stream.FlushAsync();

        Assert.AreEqual(2, inner.Length);
    }
}
