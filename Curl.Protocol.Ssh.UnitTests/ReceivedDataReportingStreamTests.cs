using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins <see cref="ReceivedDataReportingStream" />: every write is reported as received data
/// before it reaches the output, and nothing but writing and flushing is supported.
/// </summary>
[TestClass]
public sealed class ReceivedDataReportingStreamTests
{
    [TestMethod]
    public async Task WriteAsync_ReportsTheBytesThenWritesThem()
    {
        using MemoryStream output = new();
        TranscriptTransferEvents events = new();
        using ReceivedDataReportingStream stream = new(output, events);

        await stream.WriteAsync("hello"u8.ToArray());

        CollectionAssert.AreEqual(new[] { "<= hello" }, events.Transcript);
        CollectionAssert.AreEqual("hello"u8.ToArray(), output.ToArray());
    }

    [TestMethod]
    public void Write_ReportsTheSliceThenWritesIt()
    {
        using MemoryStream output = new();
        TranscriptTransferEvents events = new();
        using ReceivedDataReportingStream stream = new(output, events);

        stream.Write("xhey"u8.ToArray(), 1, 3);
        stream.Flush();

        CollectionAssert.AreEqual(new[] { "<= hey" }, events.Transcript);
        CollectionAssert.AreEqual("hey"u8.ToArray(), output.ToArray());
    }

    [TestMethod]
    public async Task FlushAsync_FlushesTheOutput()
    {
        using BufferedStream output = new(new MemoryStream());
        using ReceivedDataReportingStream stream = new(output, new TranscriptTransferEvents());
        output.WriteByte(1);

        await stream.FlushAsync();

        Assert.AreEqual(1, output.UnderlyingStream.Length);
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using ReceivedDataReportingStream stream = new(new MemoryStream(), new TranscriptTransferEvents());

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
}
