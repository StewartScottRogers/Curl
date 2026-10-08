using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins <see cref="ReceivedDataReportingStream" />: every write is reported as received data
/// before it reaches the output, and nothing but writing and flushing is supported.
/// </summary>
[TestClass]
public sealed class ReceivedDataReportingStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_ReportsTheBytesThenWritesThem()
    {
        using MemoryStream output = new();
        TranscriptTransferEvents events = new();
        using ReceivedDataReportingStream stream = new(output, events);
        Diagnostics.Arrange("write", "\"hello\" through WriteAsync");

        await stream.WriteAsync("hello"u8.ToArray());

        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.ActBytes("output", output.ToArray());
        Diagnostics.Assert("transcript", "<= hello", string.Join(" | ", events.Transcript));
        Diagnostics.AssertBytes("output", "hello"u8.ToArray(), output.ToArray());
        CollectionAssert.AreEqual(new[] { "<= hello" }, events.Transcript);
        CollectionAssert.AreEqual("hello"u8.ToArray(), output.ToArray());
    }

    [TestMethod]
    public void Write_ReportsTheSliceThenWritesIt()
    {
        using MemoryStream output = new();
        TranscriptTransferEvents events = new();
        using ReceivedDataReportingStream stream = new(output, events);
        Diagnostics.Arrange("write", "\"xhey\", offset 1, count 3, then Flush");

        stream.Write("xhey"u8.ToArray(), 1, 3);
        stream.Flush();

        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.ActBytes("output", output.ToArray());
        Diagnostics.Assert("transcript", "<= hey", string.Join(" | ", events.Transcript));
        Diagnostics.AssertBytes("output", "hey"u8.ToArray(), output.ToArray());
        CollectionAssert.AreEqual(new[] { "<= hey" }, events.Transcript);
        CollectionAssert.AreEqual("hey"u8.ToArray(), output.ToArray());
    }

    [TestMethod]
    public async Task FlushAsync_FlushesTheOutput()
    {
        using BufferedStream output = new(new MemoryStream());
        using ReceivedDataReportingStream stream = new(output, new TranscriptTransferEvents());
        output.WriteByte(1);
        Diagnostics.Arrange("output", "a BufferedStream holding one unflushed byte");

        await stream.FlushAsync();

        Diagnostics.Act("underlying stream length", output.UnderlyingStream.Length);
        Diagnostics.Assert("underlying stream length", 1L, output.UnderlyingStream.Length);
        Assert.AreEqual(1, output.UnderlyingStream.Length);
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using ReceivedDataReportingStream stream = new(new MemoryStream(), new TranscriptTransferEvents());
        Diagnostics.Arrange("stream", "over a MemoryStream");

        string capabilities = $"{stream.CanRead}, {stream.CanSeek}, {stream.CanWrite}";

        Diagnostics.Act("CanRead, CanSeek, CanWrite", capabilities);
        Diagnostics.Assert("CanRead, CanSeek, CanWrite", "False, False, True", capabilities);
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Diagnostics.Assert("Length, Position get and set, Read, Seek, SetLength", "each throws NotSupportedException", "each threw NotSupportedException");
    }
}
