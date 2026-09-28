namespace Curl.Console;

[TestClass]
public sealed class DumpHeaderOutputStreamTests
{
    private static readonly byte[] HeaderLine = "Content-Length: 10\r\n"u8.ToArray();

    [TestMethod]
    public async Task WriteAsync_DestinationAccepts_WritesAndFlushesWithoutFailure()
    {
        FlushCountingStream destination = new();
        DumpHeaderOutputStream stream = new(destination);

        await stream.WriteAsync(HeaderLine);

        CollectionAssert.AreEqual(HeaderLine, destination.ToArray());
        Assert.AreEqual(1, destination.Flushes);
        Assert.IsFalse(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Write_DestinationAccepts_WritesAndFlushesWithoutFailure()
    {
        FlushCountingStream destination = new();
        DumpHeaderOutputStream stream = new(destination);

        stream.Write(HeaderLine, 0, HeaderLine.Length);

        CollectionAssert.AreEqual(HeaderLine, destination.ToArray());
        Assert.AreEqual(1, destination.Flushes);
        Assert.IsFalse(stream.HasWriteFailed);
    }

    [TestMethod]
    public async Task WriteAsync_DestinationFailsTheWrite_RecordsTheFailureAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream());

        await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.WriteAsync(HeaderLine));

        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Write_DestinationFailsTheFlush_RecordsTheFailureAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream { WritesToFail = 0, FailsFlush = true });

        Assert.ThrowsExactly<IOException>(() => stream.Write(HeaderLine, 0, HeaderLine.Length));

        Assert.IsTrue(stream.HasWriteFailed);
    }

    [TestMethod]
    public void Members_ThatDoNotApply_AreWriteOnlyAndUnseekable()
    {
        DumpHeaderOutputStream stream = new(new MemoryStream());

        stream.Flush();
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    /// <summary>A memory stream that counts its flushes; <see cref="MemoryStream.FlushAsync(CancellationToken)" /> calls <see cref="Flush" />.</summary>
    private sealed class FlushCountingStream : MemoryStream
    {
        public int Flushes { get; private set; }

        public override void Flush()
        {
            Flushes++;
            base.Flush();
        }
    }
}
