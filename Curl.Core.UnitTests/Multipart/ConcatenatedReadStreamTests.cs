using System.Text;
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

    [TestMethod]
    public void Read_WithCallbackSegments_FillsAcrossSegmentsAndReadsEachCallbackOncePerRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1,2], callback [3,4], [5], callback [6]");
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(3, 4), Segment(5), Segment(6)], callbackSegments: [1, 3]);
        byte[] buffer = new byte[10];

        int[] reads = [stream.Read(buffer, 0, 10), stream.Read(buffer, 4, 6), stream.Read(buffer, 5, 5), stream.Read(buffer, 6, 4)];

        diagnostics.Act("reads", string.Join(",", reads));
        diagnostics.Assert("reads", "4,1,1,0", string.Join(",", reads));
        CollectionAssert.AreEqual(new[] { 4, 1, 1, 0 }, reads);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 0, 0, 0, 0 }, buffer);
    }

    [TestMethod]
    public async Task ReadAsync_WithCallbackSegments_FillsAcrossSegmentsAndReadsEachCallbackOncePerRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1,2], callback [3,4], [5], callback [6]");
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(3, 4), Segment(5), Segment(6)], callbackSegments: [1, 3]);
        byte[] buffer = new byte[10];

        int[] reads =
        [
            await stream.ReadAsync(buffer.AsMemory(0, 10), TestContext.CancellationToken),
            await stream.ReadAsync(buffer.AsMemory(4, 6), TestContext.CancellationToken),
            await stream.ReadAsync(buffer.AsMemory(5, 5), TestContext.CancellationToken),
            await stream.ReadAsync(buffer.AsMemory(6, 4), TestContext.CancellationToken),
        ];

        diagnostics.Act("reads", string.Join(",", reads));
        diagnostics.Assert("reads", "4,1,1,0", string.Join(",", reads));
        CollectionAssert.AreEqual(new[] { 4, 1, 1, 0 }, reads);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 0, 0, 0, 0 }, buffer);
    }

    [TestMethod]
    public void Read_WithCallbackSegmentsAndAFullBuffer_StopsAtTheBufferEnd()
    {
        using ConcatenatedReadStream stream = new([Segment(1, 2, 3), Segment(4)], callbackSegments: [1]);
        byte[] buffer = new byte[2];

        int first = stream.Read(buffer, 0, 2);

        Assert.AreEqual(2, first);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);
    }

    [TestMethod]
    public void Read_WithFileAndCallbackSegments_ReadsEachFileOncePerReadAndPassesItAtItsEnd()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1,2], file [3,4], [5], file [6], callback [7]");
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(3, 4), Segment(5), Segment(6), Segment(7)], callbackSegments: [4], fileSegments: [1, 3]);
        byte[] buffer = new byte[10];

        int[] reads = [stream.Read(buffer, 0, 10), stream.Read(buffer, 5, 5), stream.Read(buffer, 6, 4), stream.Read(buffer, 7, 3)];

        diagnostics.Act("reads", string.Join(",", reads));
        diagnostics.Assert("reads", "5,1,1,0", string.Join(",", reads));
        CollectionAssert.AreEqual(new[] { 5, 1, 1, 0 }, reads);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 0, 0, 0 }, buffer);
    }

    [TestMethod]
    public async Task ReadAsync_WithFileAndCallbackSegments_ReadsEachFileOncePerReadAndPassesItAtItsEnd()
    {
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(3, 4), Segment(5), Segment(6), Segment(7)], callbackSegments: [4], fileSegments: [1, 3]);
        byte[] buffer = new byte[10];

        int[] reads =
        [
            await stream.ReadAsync(buffer.AsMemory(0, 10), TestContext.CancellationToken),
            await stream.ReadAsync(buffer.AsMemory(5, 5), TestContext.CancellationToken),
            await stream.ReadAsync(buffer.AsMemory(6, 4), TestContext.CancellationToken),
        ];

        CollectionAssert.AreEqual(new[] { 5, 1, 1 }, reads);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 0, 0, 0 }, buffer);
    }

    [TestMethod]
    public void Read_WithAFileThatCannotSeek_StaysOnItUntilItReadsNothing()
    {
        NonSeekableStream file = new();
        file.Write([3]);
        file.Position = 0;
        using ConcatenatedReadStream stream = new([Segment(1), file, Segment(5), Segment(7)], callbackSegments: [3], fileSegments: [1]);
        byte[] buffer = new byte[10];

        int[] reads = [stream.Read(buffer, 0, 10), stream.Read(buffer, 2, 8)];

        CollectionAssert.AreEqual(new[] { 2, 1 }, reads);
        CollectionAssert.AreEqual(new byte[] { 1, 3, 5, 0, 0, 0, 0, 0, 0, 0 }, buffer);
    }

    [TestMethod]
    public void Read_WithAFileLongerThanTheBuffer_ReadsTheRestOfItInTheNextRead()
    {
        using ConcatenatedReadStream stream = new([Segment(1, 2, 3), Segment(4)], callbackSegments: [1], fileSegments: [0]);
        byte[] buffer = new byte[4];

        int[] reads = [stream.Read(buffer, 0, 2), stream.Read(buffer, 2, 2)];

        CollectionAssert.AreEqual(new[] { 2, 1 }, reads);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0 }, buffer);
    }

    [TestMethod]
    public void Read_WithFileSegmentsOnly_ReadsOneSegmentPerRead()
    {
        using ConcatenatedReadStream stream = new([Segment(1, 2), Segment(3)], fileSegments: [1]);
        byte[] buffer = new byte[10];

        int[] reads = [stream.Read(buffer, 0, 10), stream.Read(buffer, 2, 8)];

        CollectionAssert.AreEqual(new[] { 2, 1 }, reads);
    }

    [TestMethod]
    public void Read_WithACallbackSegmentAtItsKnownSize_PassesItInTheSameFill()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1], callback [2,3] of size 2, [4], callback [5]");
        using ConcatenatedReadStream stream = new(
            [Segment(1), Segment(2, 3), Segment(4), Segment(5)], callbackSegments: [1, 3], callbackLengths: new Dictionary<int, long> { [1] = 2 });
        byte[] buffer = new byte[10];

        int[] reads = [stream.Read(buffer, 0, 10), stream.Read(buffer, 4, 6), stream.Read(buffer, 5, 5)];

        diagnostics.Act("reads", string.Join(",", reads));
        diagnostics.Assert("reads", "4,1,0", string.Join(",", reads));
        CollectionAssert.AreEqual(new[] { 4, 1, 0 }, reads);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 0, 0, 0, 0, 0 }, buffer);
    }

    [TestMethod]
    public void Read_WithAnEmptyCallbackSegmentOfKnownSize_DoesNotCountItAsTheFillsRead()
    {
        using ConcatenatedReadStream stream = new(
            [Segment(1), Segment(), Segment(2), Segment(3)], callbackSegments: [1, 3], callbackLengths: new Dictionary<int, long> { [1] = 0 });
        byte[] buffer = new byte[10];

        int first = stream.Read(buffer, 0, 10);

        Assert.AreEqual(3, first);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0, 0, 0, 0, 0, 0, 0 }, buffer);
    }

    [TestMethod]
    public void Position_SetToTheStartAfterACallbackSegmentReachedItsSize_ReadsItAgainAsTheFillsOneRead()
    {
        using ConcatenatedReadStream stream = new(
            [Segment(1), Segment(2), Segment(3)], callbackSegments: [1, 2], callbackLengths: new Dictionary<int, long> { [1] = 1 });
        byte[] buffer = new byte[3];
        _ = stream.Read(buffer, 0, 3);

        stream.Position = 0;
        int again = stream.Read(buffer, 0, 3);

        Assert.AreEqual(2, again);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 0 }, buffer);
    }

    [TestMethod]
    public void Read_WithAnEncodedCallbackSegmentGivenOneByteACall_SendsEachEncodedGroupAsCurlDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "\"[\", base64 callback over \"dummy\" one byte a call, \"]\"");
        using ConcatenatedReadStream stream = new([Segment((byte)'['), EncodedCallback("dummy"), Segment((byte)']')], callbackSegments: [1]);
        byte[] buffer = new byte[100];

        string[] reads = [ReadText(stream, buffer), ReadText(stream, buffer), ReadText(stream, buffer), ReadText(stream, buffer)];

        diagnostics.Act("reads", string.Join("|", reads));
        diagnostics.Assert("reads", "[|ZHVt|bXk=]|", string.Join("|", reads));
        CollectionAssert.AreEqual(new[] { "[", "ZHVt", "bXk=]", string.Empty }, reads);
    }

    [TestMethod]
    public async Task ReadAsync_WithAnEncodedCallbackSegmentGivenOneByteACall_SendsEachEncodedGroupAsCurlDoes()
    {
        using ConcatenatedReadStream stream = new([Segment((byte)'['), EncodedCallback("dummy"), Segment((byte)']')], callbackSegments: [1]);
        byte[] buffer = new byte[100];
        List<string> reads = [];

        for (int i = 0; i < 4; i++)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(), TestContext.CancellationToken);
            reads.Add(Encoding.ASCII.GetString(buffer, 0, read));
        }

        CollectionAssert.AreEqual(new[] { "[", "ZHVt", "bXk=]", string.Empty }, reads);
    }

    private static string ReadText(Stream stream, byte[] buffer) => Encoding.ASCII.GetString(buffer, 0, stream.Read(buffer, 0, buffer.Length));

    /// <summary>A base64 callback segment over <paramref name="text" />, whose source gives one byte a read.</summary>
    private static EncodedReadStream EncodedCallback(string text)
    {
        EncodedReadStream encoded = MultipartPartEncoder.Find("base64")!.EncodeWhileReading(new OneByteReadStream(Encoding.ASCII.GetBytes(text)), null);
        encoded.ReadsSourceOncePerRead = true;
        return encoded;
    }

    private static MemoryStream Segment(params byte[] bytes) => new(bytes, writable: false);

    /// <summary>A stream that gives one byte a read, as lib667's read callback does.</summary>
    private sealed class OneByteReadStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(1, buffer.Length)]);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }

    /// <summary>A readable stream that cannot seek, as a pipe is.</summary>
    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
