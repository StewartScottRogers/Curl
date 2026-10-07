using Curl.Testing;

namespace Curl.Core.Multipart;

[TestClass]
public sealed class ConcatenatedReadStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SynchronousReadsCrossSegmentsAndSkipEmptyOnes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1,2], [], [3]");
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(), Segment(3)]);
        byte[] buffer = new byte[4];

        int first = stream.Read(buffer, 1, 3);
        diagnostics.Act("first read", first);
        diagnostics.Assert("first read", 2, first);
        Assert.AreEqual(2, first);
        int second = stream.Read(buffer, 3, 1);
        diagnostics.Act("second read", second);
        diagnostics.Assert("second read", 1, second);
        Assert.AreEqual(1, second);
        int third = stream.Read(buffer, 0, 4);
        diagnostics.Act("third read", third);
        diagnostics.Assert("third read", 0, third);
        Assert.AreEqual(0, third);
        diagnostics.Bytes("buffer", buffer);
        diagnostics.Diff("buffer", new byte[] { 0, 1, 2, 3 }, buffer);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, buffer);
    }

    [TestMethod]
    public async Task ArrayReadAsyncReadsEverySegment()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1], [2]");
        using ConcatenatedReadStream stream = new([Segment(1), Segment(2)]);
        byte[] buffer = new byte[2];

        int first = await stream.ReadAsync(buffer, 0, 2, TestContext.CancellationToken);
        diagnostics.Act("first read", first);
        diagnostics.Assert("first read", 1, first);
        Assert.AreEqual(1, first);
        int second = await stream.ReadAsync(buffer, 1, 1, TestContext.CancellationToken);
        diagnostics.Act("second read", second);
        diagnostics.Assert("second read", 1, second);
        Assert.AreEqual(1, second);
        int third = await stream.ReadAsync(buffer, 0, 2, TestContext.CancellationToken);
        diagnostics.Act("third read", third);
        diagnostics.Assert("third read", 0, third);
        Assert.AreEqual(0, third);
        diagnostics.Bytes("buffer", buffer);
        diagnostics.Diff("buffer", new byte[] { 1, 2 }, buffer);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);
    }

    [TestMethod]
    public async Task AnEmptyBufferReadsNothingAndLeavesTheSegmentsUnread()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[7]");
        using ConcatenatedReadStream stream = new([Segment(7)]);

        int syncRead = stream.Read([], 0, 0);
        diagnostics.Act("synchronous empty read", syncRead);
        diagnostics.Assert("synchronous empty read", 0, syncRead);
        Assert.AreEqual(0, syncRead);
        int asyncRead = await stream.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken);
        diagnostics.Act("asynchronous empty read", asyncRead);
        diagnostics.Assert("asynchronous empty read", 0, asyncRead);
        Assert.AreEqual(0, asyncRead);
        int next = stream.ReadByte();
        diagnostics.Act("next byte", next);
        diagnostics.Assert("next byte", 7, next);
        Assert.AreEqual(7, next);
    }

    [TestMethod]
    public void ASegmentThatCannotSeekMakesTheStreamReadOnlyAndForwardOnly()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1], non-seekable");
        using ConcatenatedReadStream stream = new([Segment(1), new NonSeekableStream()]);

        diagnostics.Act("CanRead", stream.CanRead);
        diagnostics.Act("CanSeek", stream.CanSeek);
        diagnostics.Act("CanWrite", stream.CanWrite);
        diagnostics.Assert("CanRead", true, stream.CanRead);
        Assert.IsTrue(stream.CanRead);
        diagnostics.Assert("CanSeek", false, stream.CanSeek);
        Assert.IsFalse(stream.CanSeek);
        diagnostics.Assert("CanWrite", false, stream.CanWrite);
        Assert.IsFalse(stream.CanWrite);
        diagnostics.Assert("Length throws", nameof(NotSupportedException), nameof(NotSupportedException));
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1], [2]");
        using ConcatenatedReadStream stream = new([Segment(1), Segment(2)]);

        diagnostics.Act("CanSeek", stream.CanSeek);
        diagnostics.Act("CanWrite", stream.CanWrite);
        diagnostics.Assert("CanSeek", true, stream.CanSeek);
        Assert.IsTrue(stream.CanSeek);
        diagnostics.Assert("CanWrite", false, stream.CanWrite);
        Assert.IsFalse(stream.CanWrite);
        diagnostics.Assert("SetLength and Write throw", nameof(NotSupportedException), nameof(NotSupportedException));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
    }

    [TestMethod]
    public void LengthAndPositionCountFromWhereEachSegmentStartedAt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        MemoryStream second = Segment(9, 2, 3);
        second.Position = 1;
        diagnostics.Arrange("segments", "[1], [9,2,3] starting at position 1");
        using ConcatenatedReadStream stream = new([Segment(1), second]);

        diagnostics.Act("Length", stream.Length);
        diagnostics.Assert("Length", 3L, stream.Length);
        Assert.AreEqual(3L, stream.Length);
        diagnostics.Act("Position", stream.Position);
        diagnostics.Assert("Position", 0L, stream.Position);
        Assert.AreEqual(0L, stream.Position);
        int firstByte = stream.ReadByte();
        diagnostics.Act("first byte", firstByte);
        diagnostics.Assert("first byte", 1, firstByte);
        Assert.AreEqual(1, firstByte);
        int secondByte = stream.ReadByte();
        diagnostics.Act("second byte", secondByte);
        diagnostics.Assert("second byte", 2, secondByte);
        Assert.AreEqual(2, secondByte);
        diagnostics.Act("Position after reads", stream.Position);
        diagnostics.Assert("Position after reads", 2L, stream.Position);
        Assert.AreEqual(2L, stream.Position);
    }

    [TestMethod]
    public void RewindingAfterAWholeReadReadsTheSameBytesAgain()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        MemoryStream second = Segment(9, 2, 3);
        second.Position = 1;
        diagnostics.Arrange("segments", "[1], [], [9,2,3] starting at position 1");
        using ConcatenatedReadStream stream = new([Segment(1), Segment(), second]);
        using MemoryStream first = new();
        stream.CopyTo(first);

        stream.Position = 0;

        using MemoryStream again = new();
        stream.CopyTo(again);
        diagnostics.Act("first pass bytes", first.Length);
        diagnostics.Bytes("first pass", first.ToArray());
        diagnostics.Bytes("second pass", again.ToArray());
        diagnostics.Diff("first pass", new byte[] { 1, 2, 3 }, first.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, first.ToArray());
        diagnostics.Diff("second pass", first.ToArray(), again.ToArray());
        CollectionAssert.AreEqual(first.ToArray(), again.ToArray());
    }

    [TestMethod]
    public void SettingThePositionIntoALaterSegmentReadsFromThere()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1,2], [3,4]; position set to 3 after one byte read");
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(3, 4)]);
        stream.ReadByte();

        stream.Position = 3;

        int fromPosition = stream.ReadByte();
        diagnostics.Act("byte at position 3", fromPosition);
        diagnostics.Assert("byte at position 3", 4, fromPosition);
        Assert.AreEqual(4, fromPosition);
        int atEnd = stream.ReadByte();
        diagnostics.Act("byte at end", atEnd);
        diagnostics.Assert("byte at end", -1, atEnd);
        Assert.AreEqual(-1, atEnd);
    }

    [TestMethod]
    public void SettingThePositionPastTheEndReadsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1]; position set to 5");
        using ConcatenatedReadStream stream = new([Segment(1)]);

        stream.Position = 5;

        int read = stream.ReadByte();
        diagnostics.Act("byte past end", read);
        diagnostics.Assert("byte past end", -1, read);
        Assert.AreEqual(-1, read);
        diagnostics.Act("Position", stream.Position);
        diagnostics.Assert("Position", 1L, stream.Position);
        Assert.AreEqual(1L, stream.Position);
    }

    [TestMethod]
    public void ANegativePositionIsRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("position", -1);
        using ConcatenatedReadStream stream = new([Segment(1)]);

        ArgumentOutOfRangeException thrown = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Position = -1);

        diagnostics.Act("exception", thrown.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), thrown.GetType().Name);
    }

    [TestMethod]
    [DataRow(1L, SeekOrigin.Begin, 1L)]
    [DataRow(1L, SeekOrigin.Current, 2L)]
    [DataRow(-1L, SeekOrigin.End, 2L)]
    public void SeekMovesRelativeToItsOrigin(long offset, SeekOrigin origin, long expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("seek", $"offset {offset} from {origin}, after one byte read of [1,2], [3]");
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(3)]);
        stream.ReadByte();

        long sought = stream.Seek(offset, origin);

        diagnostics.Act("Seek result", sought);
        diagnostics.Assert("Seek result", expected, sought);
        Assert.AreEqual(expected, sought);
        diagnostics.Act("Position", stream.Position);
        diagnostics.Assert("Position", expected, stream.Position);
        Assert.AreEqual(expected, stream.Position);
    }

    [TestMethod]
    public void SeekFromAnUnknownOriginIsRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("origin", (int)(SeekOrigin)3);
        using ConcatenatedReadStream stream = new([Segment(1)]);

        ArgumentOutOfRangeException thrown = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Seek(0, (SeekOrigin)3));

        diagnostics.Act("exception", thrown.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), thrown.GetType().Name);
    }

    [TestMethod]
    public void DisposingDisposesEverySegment()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1], [2]");
        MemoryStream first = Segment(1);
        MemoryStream second = Segment(2);
        ConcatenatedReadStream stream = new([first, second]);

        stream.Dispose();

        diagnostics.Act("first CanRead", first.CanRead);
        diagnostics.Act("second CanRead", second.CanRead);
        diagnostics.Assert("first CanRead", false, first.CanRead);
        Assert.IsFalse(first.CanRead);
        diagnostics.Assert("second CanRead", false, second.CanRead);
        Assert.IsFalse(second.CanRead);
    }

    private static MemoryStream Segment(params byte[] bytes) => new(bytes, writable: false);

    /// <summary>A readable stream that cannot seek, as a pipe is.</summary>
    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
