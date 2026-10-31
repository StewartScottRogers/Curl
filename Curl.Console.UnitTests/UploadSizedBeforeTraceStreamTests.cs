namespace Curl.Console;

/// <summary>
/// Pins the stream a <c>-T</c> file truncated by the run's trace file is uploaded through: it reports
/// the size the file had before the trace opened, reads synchronously and passes bytes and seeks
/// through unchanged (AF-0513, ADR-1174).
/// </summary>
[TestClass]
public sealed class UploadSizedBeforeTraceStreamTests
{
    [TestMethod]
    public void Capabilities_OverASeekableFile_AreReadOnlyAndSeekable()
    {
        using UploadSizedBeforeTraceStream stream = new(new MemoryStream([1, 2, 3]), 9);

        bool[] capabilities = [stream.CanRead, stream.CanSeek, stream.CanWrite];

        CollectionAssert.AreEqual(new[] { true, true, false }, capabilities);
    }

    [TestMethod]
    public void Length_WhateverTheFileHoldsNow_IsTheSizeBeforeTheTrace()
    {
        using UploadSizedBeforeTraceStream stream = new(new MemoryStream([]), 9);

        Assert.AreEqual(9L, stream.Length);
    }

    [TestMethod]
    public async Task ReadAsync_PassesTheBytesThroughCompletedAtOnce()
    {
        using UploadSizedBeforeTraceStream stream = new(new MemoryStream([1, 2, 3]), 9);
        byte[] buffer = new byte[4];

        ValueTask<int> read = stream.ReadAsync(buffer);

        Assert.IsTrue(read.IsCompleted);
        Assert.AreEqual(3, await read);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0 }, buffer);
    }

    [TestMethod]
    public void ReadSeekAndPosition_PassThroughToTheFile()
    {
        using UploadSizedBeforeTraceStream stream = new(new MemoryStream([1, 2, 3]), 9);
        byte[] buffer = new byte[2];

        stream.Position = 1;
        int read = stream.Read(buffer, 0, 2);
        long sought = stream.Seek(0, SeekOrigin.Begin);
        stream.Flush();

        Assert.AreEqual(2, read);
        CollectionAssert.AreEqual(new byte[] { 2, 3 }, buffer);
        Assert.AreEqual(0L, sought);
        Assert.AreEqual(0L, stream.Position);
    }

    [TestMethod]
    public void WriteAndSetLength_AreRefused()
    {
        using UploadSizedBeforeTraceStream stream = new(new MemoryStream([1]), 9);

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void Dispose_DisposesTheFile()
    {
        MemoryStream file = new([1]);
        UploadSizedBeforeTraceStream stream = new(file, 9);

        stream.Dispose();

        Assert.IsFalse(file.CanRead);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheFile()
    {
        MemoryStream file = new([1]);
        UploadSizedBeforeTraceStream stream = new(file, 9);

        await stream.DisposeAsync();

        Assert.IsFalse(file.CanRead);
    }
}
