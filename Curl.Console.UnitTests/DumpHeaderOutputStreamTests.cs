using System.Text;

namespace Curl.Console;

[TestClass]
public sealed class DumpHeaderOutputStreamTests
{
    private static readonly byte[] HeaderLine = "Content-Length: 10\r\n"u8.ToArray();

    private static readonly string FailureLine = "curl: Failed writing headers to hd.txt" + Environment.NewLine;

    private readonly MemoryStream standardError = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task WriteAsync_DestinationAccepts_WritesAndFlushesWithoutReportingAFailure()
    {
        FlushCountingStream destination = new();
        DumpHeaderOutputStream stream = new(destination, "hd.txt", standardError);

        await stream.WriteAsync(HeaderLine);

        CollectionAssert.AreEqual(HeaderLine, destination.ToArray());
        Assert.AreEqual(1, destination.Flushes);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public void Write_DestinationAccepts_WritesAndFlushesWithoutReportingAFailure()
    {
        FlushCountingStream destination = new();
        DumpHeaderOutputStream stream = new(destination, "hd.txt", standardError);

        stream.Write(HeaderLine, 0, HeaderLine.Length);

        CollectionAssert.AreEqual(HeaderLine, destination.ToArray());
        Assert.AreEqual(1, destination.Flushes);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task WriteAsync_DestinationFailsTheWrite_PrintsTheFailureLineAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream(), "hd.txt", standardError);

        await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.WriteAsync(HeaderLine));

        Assert.AreEqual(FailureLine, StandardErrorText);
    }

    [TestMethod]
    public async Task WriteAsync_DestinationFailsWithErrorsNotShown_PrintsNothingAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream(), "hd.txt", null);

        await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.WriteAsync(HeaderLine));
    }

    [TestMethod]
    public void Write_DestinationFailsTheFlush_PrintsTheFailureLineAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream { WritesToFail = 0, FailsFlush = true }, "hd.txt", standardError);

        Assert.ThrowsExactly<IOException>(() => stream.Write(HeaderLine, 0, HeaderLine.Length));

        Assert.AreEqual(FailureLine, StandardErrorText);
    }

    [TestMethod]
    public void Write_DestinationFailsWithErrorsNotShown_PrintsNothingAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream(), "hd.txt", null);

        Assert.ThrowsExactly<IOException>(() => stream.Write(HeaderLine, 0, HeaderLine.Length));
    }

    [TestMethod]
    public void Members_ThatDoNotApply_AreWriteOnlyAndUnseekable()
    {
        DumpHeaderOutputStream stream = new(new MemoryStream(), "hd.txt", standardError);

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
