using System.Text;

namespace Curl.Extension.ServerSentEvents;

[TestClass]
public sealed class EventStreamWritingStreamTests
{
    [TestMethod]
    public async Task WriteAsync_WritesEachCompletedEventNormalised_Section926()
    {
        MemoryStream output = new();
        EventStreamWritingStream stream = new(new EventStreamParser(), output);

        await stream.WriteAsync(Encoding.UTF8.GetBytes(": comment\nid: 7\nevent: add\ndata: a\ndata:b\nfoo: x\n\ndata: pen"));

        Assert.AreEqual("event: add\nid: 7\ndata: a\ndata: b\n\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_ArrayOverload_WritesTheEventsOfTheGivenRange()
    {
        MemoryStream output = new();
        EventStreamWritingStream stream = new(new EventStreamParser(), output);
        byte[] bytes = Encoding.UTF8.GetBytes("xxdata: one\n\nyy");

        await stream.WriteAsync(bytes, 2, bytes.Length - 4, CancellationToken.None);

        Assert.AreEqual("event: message\ndata: one\n\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public void Write_ArrayAndSpan_WriteTheEventsSynchronously()
    {
        MemoryStream output = new();
        EventStreamWritingStream stream = new(new EventStreamParser(), output);
        byte[] bytes = Encoding.UTF8.GetBytes("data: one\n");

        stream.Write(bytes, 0, bytes.Length);
        stream.Write("\n"u8);
        stream.Flush();

        Assert.AreEqual("event: message\ndata: one\n\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task FlushAsync_FlushesTheOutput()
    {
        FlushCountingStream output = new();

        await new EventStreamWritingStream(new EventStreamParser(), output).FlushAsync();

        Assert.AreEqual(1, output.Flushes);
    }

    [TestMethod]
    public void Stream_IsWriteOnlyAndUnseekable()
    {
        EventStreamWritingStream stream = new(new EventStreamParser(), new MemoryStream());

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

    [TestMethod]
    public void Format_NoEvents_IsEmpty()
    {
        Assert.IsEmpty(EventStreamWritingStream.Format([]));
    }

    private sealed class FlushCountingStream : MemoryStream
    {
        public int Flushes { get; private set; }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Flushes++;
            return Task.CompletedTask;
        }
    }
}
