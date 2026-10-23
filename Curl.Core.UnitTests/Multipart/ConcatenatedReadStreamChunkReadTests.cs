using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins that a read of <see cref="ConcatenatedReadStream" /> fills across segment boundaries, as
/// libcurl 8.22.0's <c>mime_read</c> does, so a chunked upload makes one chunk per read and none is empty
/// before a part's data (BL-3214, BL-3210 case 4).
/// </summary>
[TestClass]
public sealed class ConcatenatedReadStreamChunkReadTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Read_HeadSegmentThenDataSegment_ReturnsBothInOneRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1,2,3], [4,5]");
        using ConcatenatedReadStream stream = new([Segment(1, 2, 3), Segment(4, 5)]);
        byte[] buffer = new byte[16];

        int first = stream.Read(buffer, 0, buffer.Length);
        int second = stream.Read(buffer, 0, buffer.Length);

        diagnostics.Assert("first read", 5, first);
        Assert.AreEqual(5, first);
        diagnostics.Assert("second read", 0, second);
        Assert.AreEqual(0, second);
    }

    [TestMethod]
    public void Read_EmptySegmentsBetweenData_NeverReturnsAnEmptyReadBeforeTheEnd()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[], [1], [], [2], []");
        using ConcatenatedReadStream stream = new([Segment(), Segment(1), Segment(), Segment(2), Segment()]);
        byte[] buffer = new byte[16];

        int first = stream.Read(buffer, 0, buffer.Length);

        diagnostics.Assert("first read", 2, first);
        Assert.AreEqual(2, first);
        Assert.AreEqual(0, stream.Read(buffer, 0, buffer.Length));
    }

    [TestMethod]
    public async Task ReadAsync_SmallBuffer_FillsEachReadAcrossTheBoundary()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("segments", "[1,2,3], [4,5]");
        using ConcatenatedReadStream stream = new([Segment(1, 2, 3), Segment(4, 5)]);
        byte[] buffer = new byte[4];

        int first = await stream.ReadAsync(buffer, 0, 4, TestContext.CancellationToken);
        int second = await stream.ReadAsync(buffer, 0, 4, TestContext.CancellationToken);

        diagnostics.Assert("reads", "4, 1", $"{first}, {second}");
        Assert.AreEqual(4, first);
        Assert.AreEqual(1, second);
    }

    [TestMethod]
    public void Read_SegmentFailsAfterBytes_ReturnsTheBytesThenThrowsOnTheNextRead()
    {
        using ConcatenatedReadStream stream = new([Segment(1, 2), new FailingStream()]);
        byte[] buffer = new byte[16];

        int first = stream.Read(buffer, 0, buffer.Length);

        Assert.AreEqual(2, first);
        Assert.ThrowsExactly<IOException>(() => stream.Read(buffer, 0, buffer.Length));
    }

    [TestMethod]
    public void Read_FirstSegmentFails_Throws()
    {
        using ConcatenatedReadStream stream = new([new FailingStream()]);
        byte[] buffer = new byte[16];

        Assert.ThrowsExactly<IOException>(() => stream.Read(buffer, 0, buffer.Length));
    }

    [TestMethod]
    public async Task ReadAsync_SegmentFailsAfterBytes_ReturnsTheBytesThenThrowsOnTheNextRead()
    {
        using ConcatenatedReadStream stream = new([Segment(1, 2), new FailingStream()]);
        byte[] buffer = new byte[16];

        int first = await stream.ReadAsync(buffer.AsMemory(), TestContext.CancellationToken);

        Assert.AreEqual(2, first);
        await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.ReadExactlyAsync(buffer.AsMemory(), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadAsync_FirstSegmentFails_Throws()
    {
        using ConcatenatedReadStream stream = new([new FailingStream()]);
        byte[] buffer = new byte[16];

        await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.ReadExactlyAsync(buffer.AsMemory(), TestContext.CancellationToken));
    }

    private sealed class FailingStream : MemoryStream
    {
        public override int Read(Span<byte> buffer) => throw new IOException("refused");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("refused");
    }

    private static MemoryStream Segment(params byte[] bytes) => new(bytes, writable: false);
}
