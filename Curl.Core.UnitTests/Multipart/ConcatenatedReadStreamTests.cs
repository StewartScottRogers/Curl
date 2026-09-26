namespace Curl.Core.Multipart;

[TestClass]
public sealed class ConcatenatedReadStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SynchronousReadsCrossSegmentsAndSkipEmptyOnes()
    {
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(), Segment(3)]);
        byte[] buffer = new byte[4];

        Assert.AreEqual(2, stream.Read(buffer, 1, 3));
        Assert.AreEqual(1, stream.Read(buffer, 3, 1));
        Assert.AreEqual(0, stream.Read(buffer, 0, 4));
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, buffer);
    }

    [TestMethod]
    public async Task ArrayReadAsyncReadsEverySegment()
    {
        using ConcatenatedReadStream stream = new([Segment(1), Segment(2)]);
        byte[] buffer = new byte[2];

        Assert.AreEqual(1, await stream.ReadAsync(buffer, 0, 2, TestContext.CancellationToken));
        Assert.AreEqual(1, await stream.ReadAsync(buffer, 1, 1, TestContext.CancellationToken));
        Assert.AreEqual(0, await stream.ReadAsync(buffer, 0, 2, TestContext.CancellationToken));
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);
    }

    [TestMethod]
    public async Task AnEmptyBufferReadsNothingAndLeavesTheSegmentsUnread()
    {
        using ConcatenatedReadStream stream = new([Segment(7)]);

        Assert.AreEqual(0, stream.Read([], 0, 0));
        Assert.AreEqual(0, await stream.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken));
        Assert.AreEqual(7, stream.ReadByte());
    }

    [TestMethod]
    public void TheStreamIsReadOnlyAndForwardOnly()
    {
        using ConcatenatedReadStream stream = new([Segment()]);

        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
        stream.Flush();
    }

    [TestMethod]
    public void DisposingDisposesEverySegment()
    {
        MemoryStream first = Segment(1);
        MemoryStream second = Segment(2);
        ConcatenatedReadStream stream = new([first, second]);

        stream.Dispose();

        Assert.IsFalse(first.CanRead);
        Assert.IsFalse(second.CanRead);
    }

    private static MemoryStream Segment(params byte[] bytes) => new(bytes, writable: false);
}
