using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using StandardOutputFailureDeferringStream stream = new(inner);
        Diagnostics.Arrange("inner stream", "never fails");
        Diagnostics.Act("can read / seek / write", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");

        Diagnostics.Assert("can read / seek / write", "False / False / True", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using StandardOutputFailureDeferringStream stream = new(inner);
        Diagnostics.Arrange("members", "Length, Position get and set, Read, Seek, SetLength");

        NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Diagnostics.Act("Length throws", exception.GetType().Name);
        Diagnostics.Assert("Length throws", nameof(NotSupportedException), exception.GetType().Name);
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
        Diagnostics.Arrange("writes", "[1,2,3] from 1 count 1, [4,5,6] from 1 count 2 async, [7] async, flush, flush async");

        stream.Write([1, 2, 3], 1, 1);
        await stream.WriteAsync([4, 5, 6], 1, 2, CancellationToken.None);
        await stream.WriteAsync(new byte[] { 7 }.AsMemory());
        stream.Flush();
        await stream.FlushAsync();
        Diagnostics.Bytes("inner stream", inner.ToArray());
        Diagnostics.Act("has write failed", stream.HasWriteFailed);

        Diagnostics.Diff("inner stream", new byte[] { 2, 5, 6, 7 }, inner.ToArray());
        CollectionAssert.AreEqual(new byte[] { 2, 5, 6, 7 }, inner.ToArray());
        Assert.IsFalse(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Write_ThatFailsBelowTheBufferSize_RecordsTheFailureWithoutThrowing()
    {
        inner.WritesToFail = 1;
        using StandardOutputFailureDeferringStream stream = new(inner);
        Diagnostics.Arrange("inner writes to fail / write size", $"1 / {BufferSize - 1}");

        stream.Write(new byte[BufferSize - 1], 0, BufferSize - 1);
        Diagnostics.Act("has write failed", stream.HasWriteFailed);

        Diagnostics.Assert("has write failed", true, stream.HasWriteFailed);
        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Write_ThatFailsAtTheBufferSize_Throws()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        Diagnostics.Arrange("inner stream / write size", $"always fails / {BufferSize}");

        OutputWriteFailedException exception = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[BufferSize], 0, BufferSize));
        ActException(exception);
        Diagnostics.Act("has write failed", stream.HasWriteFailed);

        Diagnostics.Assert("has write failed", true, stream.HasWriteFailed);
        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Write_AfterAFailure_ThrowsOnceTheWritesSinceFillTheBuffer()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        stream.Write(new byte[BufferSize - 2], 0, BufferSize - 2);
        stream.Write([1], 0, 1);
        Diagnostics.Arrange("inner stream / written before", $"always fails / {BufferSize - 1} bytes");

        OutputWriteFailedException exception = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write([1], 0, 1));
        ActException(exception);

        Diagnostics.Assert("exception", nameof(OutputWriteFailedException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task WriteAsync_ThatFailsBelowTheBufferSize_RecordsTheFailureWithoutThrowing()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        Diagnostics.Arrange("inner stream / write size", $"always fails / {BufferSize - 1}");

        await stream.WriteAsync(new byte[BufferSize - 1].AsMemory());
        Diagnostics.Act("has write failed", stream.HasWriteFailed);

        Diagnostics.Assert("has write failed", true, stream.HasWriteFailed);
        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public async Task WriteAsync_AfterAFailure_ThrowsOnceTheWritesSinceFillTheBuffer()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        await stream.WriteAsync(new byte[BufferSize - 1].AsMemory());
        Diagnostics.Arrange("inner stream / written before", $"always fails / {BufferSize - 1} bytes");

        OutputWriteFailedException exception = await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(async () => await stream.WriteAsync(new byte[] { 1 }.AsMemory()));
        ActException(exception);

        Diagnostics.Assert("exception", nameof(OutputWriteFailedException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(100, 41, 96)]
    [DataRow(300, 14, 196)]
    [DataRow(1000, 5, 96)]
    [DataRow(30, 137, 16)]
    public void Write_ThatOverflowsTheBufferAfterAFailure_AcceptsTheRoomLeft(int size, int throwingWrite, int bytesAccepted)
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        Diagnostics.Arrange("write size / throwing write", $"{size} / {throwingWrite}");
        for (int write = 1; write < throwingWrite; write++)
        {
            stream.Write(new byte[size], 0, size);
        }

        var exception = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[size], 0, size));
        ActException(exception);

        Diagnostics.Assert("bytes accepted", bytesAccepted, exception.BytesAccepted);
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
        Diagnostics.Arrange("write size / throwing write", $"{size} / {throwingWrite}");
        for (int write = 1; write < throwingWrite; write++)
        {
            await stream.WriteAsync(new byte[size].AsMemory());
        }

        var exception = await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(
            async () => await stream.WriteAsync(new byte[size].AsMemory()));
        ActException(exception);

        Diagnostics.Assert("bytes accepted", bytesAccepted, exception.BytesAccepted);
        Assert.AreEqual(bytesAccepted, exception.BytesAccepted);
    }

    [TestMethod]
    [DataRow(BufferSize)]
    [DataRow(16384)]
    public void Write_ThatFailsAtOrAboveTheBufferSizeWithNothingBefore_AcceptsNothing(int size)
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        Diagnostics.Arrange("write size", size);

        var exception = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[size], 0, size));
        ActException(exception);

        Diagnostics.Assert("bytes accepted", 0, exception.BytesAccepted);
        Assert.AreEqual(0, exception.BytesAccepted);
    }

    [TestMethod]
    [DataRow(BufferSize)]
    [DataRow(16384)]
    public async Task WriteAsync_ThatFailsAtOrAboveTheBufferSizeWithNothingBefore_AcceptsNothing(int size)
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        Diagnostics.Arrange("write size", size);

        var exception = await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(
            async () => await stream.WriteAsync(new byte[size].AsMemory()));
        ActException(exception);

        Diagnostics.Assert("bytes accepted", 0, exception.BytesAccepted);
        Assert.AreEqual(0, exception.BytesAccepted);
    }

    [TestMethod]
    public void Write_AfterTheOverflowingWrite_AcceptsNothing()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        stream.Write(new byte[BufferSize - 4], 0, BufferSize - 4);
        Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[100], 0, 100));
        Diagnostics.Arrange("before", $"{BufferSize - 4} bytes absorbed, then a 100-byte write overflowed");

        var exception = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write(new byte[100], 0, 100));
        ActException(exception);

        Diagnostics.Assert("bytes accepted", 0, exception.BytesAccepted);
        Assert.AreEqual(0, exception.BytesAccepted);
    }

    [TestMethod]
    public async Task WriteAsync_AfterTheOverflowingWrite_AcceptsNothing()
    {
        using StandardOutputFailureDeferringStream stream = new(new FailingWriteStream());
        await stream.WriteAsync(new byte[BufferSize - 4].AsMemory());
        await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(async () => await stream.WriteAsync(new byte[100].AsMemory()));
        Diagnostics.Arrange("before", $"{BufferSize - 4} bytes absorbed, then a 100-byte write overflowed");

        var exception = await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(
            async () => await stream.WriteAsync(new byte[100].AsMemory()));
        ActException(exception);

        Diagnostics.Assert("bytes accepted", 0, exception.BytesAccepted);
        Assert.AreEqual(0, exception.BytesAccepted);
    }

    [TestMethod]
    public void Flush_ThatFails_RecordsTheFailureWithoutThrowing()
    {
        inner.FailsFlush = true;
        using StandardOutputFailureDeferringStream stream = new(inner);
        Diagnostics.Arrange("inner flush fails", true);

        stream.Flush();
        Diagnostics.Act("has write failed", stream.HasWriteFailed);

        Diagnostics.Assert("has write failed", true, stream.HasWriteFailed);
        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public async Task FlushAsync_ThatFails_RecordsTheFailureWithoutThrowing()
    {
        inner.FailsFlush = true;
        using StandardOutputFailureDeferringStream stream = new(inner);
        Diagnostics.Arrange("inner flush fails", true);

        await stream.FlushAsync();
        Diagnostics.Act("has write failed", stream.HasWriteFailed);

        Diagnostics.Assert("has write failed", true, stream.HasWriteFailed);
        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void ClearWriteFailure_AfterAFailure_ForgetsItAndEmptiesTheBuffer()
    {
        FailingWriteStream closed = new() { WritesToFail = 2 };
        using StandardOutputFailureDeferringStream stream = new(closed);
        stream.Write(new byte[BufferSize - 1], 0, BufferSize - 1);
        Diagnostics.Arrange("inner writes to fail / first write size", $"2 / {BufferSize - 1}");

        stream.ClearWriteFailure();
        Diagnostics.Act("has write failed after clearing", stream.HasWriteFailed);

        Diagnostics.Assert("has write failed after clearing", false, stream.HasWriteFailed);
        Assert.IsFalse(stream.HasWriteFailed);
        stream.Write(new byte[BufferSize - 1], 0, BufferSize - 1);
        Diagnostics.Act("has write failed after the second write", stream.HasWriteFailed);
        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Dispose_LeavesTheInnerStreamOpen()
    {
        StandardOutputFailureDeferringStream stream = new(inner);
        Diagnostics.Arrange("inner stream", "open");

        stream.Dispose();
        Diagnostics.Act("inner can write", inner.CanWrite);

        Diagnostics.Assert("inner can write", true, inner.CanWrite);
        Assert.IsTrue(inner.CanWrite);
    }

    private void ActException(OutputWriteFailedException exception)
    {
        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Act("bytes accepted", exception.BytesAccepted);
    }
}
