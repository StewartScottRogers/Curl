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
    public void ASegmentThatCannotSeekMakesTheStreamReadOnlyAndForwardOnly()
    {
        using ConcatenatedReadStream stream = new([Segment(1), new NonSeekableStream()]);

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
    public void SeekableSegmentsMakeASeekableReadOnlyStream()
    {
        using ConcatenatedReadStream stream = new([Segment(1), Segment(2)]);

        Assert.IsTrue(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
    }

    [TestMethod]
    public void LengthAndPositionCountFromWhereEachSegmentStartedAt()
    {
        MemoryStream second = Segment(9, 2, 3);
        second.Position = 1;
        using ConcatenatedReadStream stream = new([Segment(1), second]);

        Assert.AreEqual(3L, stream.Length);
        Assert.AreEqual(0L, stream.Position);
        Assert.AreEqual(1, stream.ReadByte());
        Assert.AreEqual(2, stream.ReadByte());
        Assert.AreEqual(2L, stream.Position);
    }

    [TestMethod]
    public void RewindingAfterAWholeReadReadsTheSameBytesAgain()
    {
        MemoryStream second = Segment(9, 2, 3);
        second.Position = 1;
        using ConcatenatedReadStream stream = new([Segment(1), Segment(), second]);
        using MemoryStream first = new();
        stream.CopyTo(first);

        stream.Position = 0;

        using MemoryStream again = new();
        stream.CopyTo(again);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, first.ToArray());
        CollectionAssert.AreEqual(first.ToArray(), again.ToArray());
    }

    [TestMethod]
    public void SettingThePositionIntoALaterSegmentReadsFromThere()
    {
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(3, 4)]);
        stream.ReadByte();

        stream.Position = 3;

        Assert.AreEqual(4, stream.ReadByte());
        Assert.AreEqual(-1, stream.ReadByte());
    }

    [TestMethod]
    public void SettingThePositionPastTheEndReadsNothing()
    {
        using ConcatenatedReadStream stream = new([Segment(1)]);

        stream.Position = 5;

        Assert.AreEqual(-1, stream.ReadByte());
        Assert.AreEqual(1L, stream.Position);
    }

    [TestMethod]
    public void ANegativePositionIsRefused()
    {
        using ConcatenatedReadStream stream = new([Segment(1)]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Position = -1);
    }

    [TestMethod]
    [DataRow(1L, SeekOrigin.Begin, 1L)]
    [DataRow(1L, SeekOrigin.Current, 2L)]
    [DataRow(-1L, SeekOrigin.End, 2L)]
    public void SeekMovesRelativeToItsOrigin(long offset, SeekOrigin origin, long expected)
    {
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(3)]);
        stream.ReadByte();

        Assert.AreEqual(expected, stream.Seek(offset, origin));
        Assert.AreEqual(expected, stream.Position);
    }

    [TestMethod]
    public void SeekFromAnUnknownOriginIsRefused()
    {
        using ConcatenatedReadStream stream = new([Segment(1)]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Seek(0, (SeekOrigin)3));
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

    /// <summary>A readable stream that cannot seek, as a pipe is.</summary>
    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
