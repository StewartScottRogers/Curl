using System.Text;
using Curl.Testing;

namespace Curl.Console;

[TestClass]
public sealed class DumpHeaderOutputStreamTests
{
    private static readonly byte[] HeaderLine = "Content-Length: 10\r\n"u8.ToArray();

    private static readonly string FailureLine = "curl: Failed writing headers to hd.txt" + Environment.NewLine;

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task WriteAsync_DestinationAccepts_WritesAndFlushesWithoutReportingAFailure()
    {
        FlushCountingStream destination = new();
        DumpHeaderOutputStream stream = new(destination, "hd.txt", standardError);
        Diagnostics.Bytes("header line", HeaderLine);
        Diagnostics.Arrange("header line", Encoding.ASCII.GetString(HeaderLine));

        await stream.WriteAsync(HeaderLine);
        Diagnostics.Act("destination flushes", destination.Flushes);
        Diagnostics.Act("standard error", Unix(StandardErrorText));

        Diagnostics.Diff("destination bytes", HeaderLine, destination.ToArray());
        Diagnostics.Assert("destination flushes", 1, destination.Flushes);
        Diagnostics.Assert("standard error", string.Empty, Unix(StandardErrorText));
        CollectionAssert.AreEqual(HeaderLine, destination.ToArray());
        Assert.AreEqual(1, destination.Flushes);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public void Write_DestinationAccepts_WritesAndFlushesWithoutReportingAFailure()
    {
        FlushCountingStream destination = new();
        DumpHeaderOutputStream stream = new(destination, "hd.txt", standardError);
        Diagnostics.Bytes("header line", HeaderLine);
        Diagnostics.Arrange("header line", Encoding.ASCII.GetString(HeaderLine));

        stream.Write(HeaderLine, 0, HeaderLine.Length);
        Diagnostics.Act("destination flushes", destination.Flushes);
        Diagnostics.Act("standard error", Unix(StandardErrorText));

        Diagnostics.Diff("destination bytes", HeaderLine, destination.ToArray());
        Diagnostics.Assert("destination flushes", 1, destination.Flushes);
        Diagnostics.Assert("standard error", string.Empty, Unix(StandardErrorText));
        CollectionAssert.AreEqual(HeaderLine, destination.ToArray());
        Assert.AreEqual(1, destination.Flushes);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task WriteAsync_DestinationFailsTheWrite_PrintsTheFailureLineAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream(), "hd.txt", standardError);
        Diagnostics.Arrange("destination", "fails the write");

        await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.WriteAsync(HeaderLine));
        Diagnostics.Act("standard error", Unix(StandardErrorText));

        Diagnostics.Assert("standard error", Unix(FailureLine), Unix(StandardErrorText));
        Assert.AreEqual(FailureLine, StandardErrorText);
    }

    [TestMethod]
    public async Task WriteAsync_DestinationFailsWithErrorsNotShown_PrintsNothingAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream(), "hd.txt", null);
        Diagnostics.Arrange("destination", "fails the write, no standard error");

        IOException thrown = await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.WriteAsync(HeaderLine));
        Diagnostics.Act("thrown", thrown.GetType().Name);

        Diagnostics.Assert("thrown", nameof(IOException), thrown.GetType().Name);
    }

    [TestMethod]
    public void Write_DestinationFailsTheFlush_PrintsTheFailureLineAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream { WritesToFail = 0, FailsFlush = true }, "hd.txt", standardError);
        Diagnostics.Arrange("destination", "accepts the write, fails the flush");

        Assert.ThrowsExactly<IOException>(() => stream.Write(HeaderLine, 0, HeaderLine.Length));
        Diagnostics.Act("standard error", Unix(StandardErrorText));

        Diagnostics.Assert("standard error", Unix(FailureLine), Unix(StandardErrorText));
        Assert.AreEqual(FailureLine, StandardErrorText);
    }

    [TestMethod]
    public void Write_DestinationFailsWithErrorsNotShown_PrintsNothingAndRethrows()
    {
        DumpHeaderOutputStream stream = new(new FailingWriteStream(), "hd.txt", null);
        Diagnostics.Arrange("destination", "fails the write, no standard error");

        IOException thrown = Assert.ThrowsExactly<IOException>(() => stream.Write(HeaderLine, 0, HeaderLine.Length));
        Diagnostics.Act("thrown", thrown.GetType().Name);

        Diagnostics.Assert("thrown", nameof(IOException), thrown.GetType().Name);
    }

    [TestMethod]
    public void Members_ThatDoNotApply_AreWriteOnlyAndUnseekable()
    {
        DumpHeaderOutputStream stream = new(new MemoryStream(), "hd.txt", standardError);
        Diagnostics.Arrange("members tried", "Flush, Length, Position get and set, Read, Seek, SetLength");

        stream.Flush();
        Diagnostics.Act("capabilities", $"read {stream.CanRead}, seek {stream.CanSeek}, write {stream.CanWrite}");

        Diagnostics.Assert("capabilities", "read False, seek False, write True", $"read {stream.CanRead}, seek {stream.CanSeek}, write {stream.CanWrite}");
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

    private static string Unix(string text) => text.Replace("\r", string.Empty, StringComparison.Ordinal);

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
