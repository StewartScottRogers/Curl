using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the standard-output stream: it passes writes and flushes through, and after
/// standard output fails it absorbs writes until curl's 4096-byte stdio buffer would be
/// full, then throws, as curl 8.21.0 does (measured 2026-09-26).
/// </summary>
[TestClass]
public sealed class StandardOutputFailureDeferringStreamTests
{
    private const int BufferSize = StandardOutputFailureDeferringStream.StdioBufferSize;

    private readonly FailingWriteStream inner = new() { WritesToFail = 0 };

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using StandardOutputFailureDeferringStream stream = new(inner);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using StandardOutputFailureDeferringStream stream = new(inner);

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public async Task Writes_ThatSucceed_ReachTheInnerStreamAndRecordNoFailure()
    {
        using StandardOutputFailureDeferringStream stream = new(inner);

        stream.Write([1, 2, 3], 1, 1);
        await stream.WriteAsync([4, 5, 6], 1, 2, CancellationToken.None);
        await stream.WriteAsync(new byte[] { 7 }.AsMemory());
        stream.Flush();
        await stream.FlushAsync();

        CollectionAssert.AreEqual(new byte[] { 2, 5, 6, 7 }, inner.ToArray());
        Assert.IsFalse(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Write_ThatFailsBelowTheBufferSize_RecordsTheFailureWithoutThrowing()
    {
        inner.WritesToFail = 1;
        using StandardOutputFailureDeferringStream stream = new(inner);

        stream.Write(new byte[BufferSize - 1], 0, BufferSize - 1);

        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Write_ThatFailsAtTheBufferSize_Throws()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());

        Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[BufferSize], 0, BufferSize));

        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Write_AfterAFailure_ThrowsOnceTheWritesSinceFillTheBuffer()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        stream.Write(new byte[BufferSize - 2], 0, BufferSize - 2);
        stream.Write([1], 0, 1);

        Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write([1], 0, 1));
    }

    [TestMethod]
    public async Task WriteAsync_ThatFailsBelowTheBufferSize_RecordsTheFailureWithoutThrowing()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());

        await stream.WriteAsync(new byte[BufferSize - 1].AsMemory());

        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public async Task WriteAsync_AfterAFailure_ThrowsOnceTheWritesSinceFillTheBuffer()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        await stream.WriteAsync(new byte[BufferSize - 1].AsMemory());

        await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(async () => await stream.WriteAsync(new byte[] { 1 }.AsMemory()));
    }

    [TestMethod]
    [DataRow(100, 41, 96)]
    [DataRow(300, 14, 196)]
    [DataRow(1000, 5, 96)]
    [DataRow(30, 137, 16)]
    public void Write_ThatOverflowsTheBufferAfterAFailure_AcceptsTheRoomLeft(int size, int throwingWrite, int bytesAccepted)
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        for (int write = 1; write < throwingWrite; write++)
        {
            stream.Write(new byte[size], 0, size);
        }

        var exception = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[size], 0, size));

        Assert.AreEqual(bytesAccepted, exception.BytesAccepted);
    }

    [TestMethod]
    [DataRow(100, 41, 96)]
    [DataRow(300, 14, 196)]
    [DataRow(1000, 5, 96)]
    [DataRow(30, 137, 16)]
    public async Task WriteAsync_ThatOverflowsTheBufferAfterAFailure_AcceptsTheRoomLeft(int size, int throwingWrite, int bytesAccepted)
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        for (int write = 1; write < throwingWrite; write++)
        {
            await stream.WriteAsync(new byte[size].AsMemory());
        }

        var exception = await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(
            async () => await stream.WriteAsync(new byte[size].AsMemory()));

        Assert.AreEqual(bytesAccepted, exception.BytesAccepted);
    }

    [TestMethod]
    [DataRow(BufferSize)]
    [DataRow(16384)]
    public void Write_ThatFailsAtOrAboveTheBufferSizeWithNothingBefore_AcceptsNothing(int size)
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());

        var exception = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[size], 0, size));

        Assert.AreEqual(0, exception.BytesAccepted);
    }

    [TestMethod]
    [DataRow(BufferSize)]
    [DataRow(16384)]
    public async Task WriteAsync_ThatFailsAtOrAboveTheBufferSizeWithNothingBefore_AcceptsNothing(int size)
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());

        var exception = await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(
            async () => await stream.WriteAsync(new byte[size].AsMemory()));

        Assert.AreEqual(0, exception.BytesAccepted);
    }

    [TestMethod]
    public void Write_AfterTheOverflowingWrite_AcceptsNothing()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        stream.Write(new byte[BufferSize - 4], 0, BufferSize - 4);
        Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[100], 0, 100));

        var exception = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[100], 0, 100));

        Assert.AreEqual(0, exception.BytesAccepted);
    }

    [TestMethod]
    public async Task WriteAsync_AfterTheOverflowingWrite_AcceptsNothing()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        await stream.WriteAsync(new byte[BufferSize - 4].AsMemory());
        await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(async () => await stream.WriteAsync(new byte[100].AsMemory()));

        var exception = await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(
            async () => await stream.WriteAsync(new byte[100].AsMemory()));

        Assert.AreEqual(0, exception.BytesAccepted);
    }

    [TestMethod]
    public void Flush_ThatFails_RecordsTheFailureWithoutThrowing()
    {
        inner.FailsFlush = true;
        using StandardOutputFailureDeferringStream stream = new(inner);

        stream.Flush();

        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public async Task FlushAsync_ThatFails_RecordsTheFailureWithoutThrowing()
    {
        inner.FailsFlush = true;
        using StandardOutputFailureDeferringStream stream = new(inner);

        await stream.FlushAsync();

        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void ClearWriteFailure_AfterAFailure_ForgetsItAndEmptiesTheBuffer()
    {
        FailingWriteStream closed = new() { WritesToFail = 2 };
        using StandardOutputFailureDeferringStream stream = new(closed);
        stream.Write(new byte[BufferSize - 1], 0, BufferSize - 1);

        stream.ClearWriteFailure();

        Assert.IsFalse(stream.HasWriteFailed);
        stream.Write(new byte[BufferSize - 1], 0, BufferSize - 1);
        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Dispose_LeavesTheInnerStreamOpen()
    {
        StandardOutputFailureDeferringStream stream = new(inner);

        stream.Dispose();

        Assert.IsTrue(inner.CanWrite);
    }
}
