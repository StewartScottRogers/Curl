using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the text-mode stream: write-only, each line feed written as CR LF, every other byte
/// unchanged, flushes passed through, and the inner stream disposed only when owned.
/// </summary>
[TestClass]
public sealed class LineFeedToCrLfStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using LineFeedToCrLfStream stream = new(new MemoryStream());
        Diagnostics.Arrange("inner stream", "empty MemoryStream");

        bool canRead = stream.CanRead;
        bool canSeek = stream.CanSeek;
        bool canWrite = stream.CanWrite;
        Diagnostics.Act("CanRead, CanSeek, CanWrite", $"{canRead}, {canSeek}, {canWrite}");

        Diagnostics.Assert("CanRead", false, canRead);
        Assert.IsFalse(stream.CanRead);
        Diagnostics.Assert("CanSeek", false, canSeek);
        Assert.IsFalse(stream.CanSeek);
        Diagnostics.Assert("CanWrite", true, canWrite);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using LineFeedToCrLfStream stream = new(new MemoryStream());
        Diagnostics.Arrange("inner stream", "empty MemoryStream");

        NotSupportedException length = Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Diagnostics.Act("Length", length.GetType().Name);
        Diagnostics.Assert("Length", nameof(NotSupportedException), length.GetType().Name);
        NotSupportedException position = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Diagnostics.Act("Position", position.GetType().Name);
        Diagnostics.Assert("Position", nameof(NotSupportedException), position.GetType().Name);
        NotSupportedException setPosition = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Diagnostics.Act("Position set", setPosition.GetType().Name);
        Diagnostics.Assert("Position set", nameof(NotSupportedException), setPosition.GetType().Name);
        NotSupportedException read = Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Diagnostics.Act("Read", read.GetType().Name);
        Diagnostics.Assert("Read", nameof(NotSupportedException), read.GetType().Name);
        NotSupportedException seek = Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Diagnostics.Act("Seek", seek.GetType().Name);
        Diagnostics.Assert("Seek", nameof(NotSupportedException), seek.GetType().Name);
        NotSupportedException setLength = Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Diagnostics.Act("SetLength", setLength.GetType().Name);
        Diagnostics.Assert("SetLength", nameof(NotSupportedException), setLength.GetType().Name);
    }

    [TestMethod]
    public void Write_WritesEachLineFeedAsCrLfAndLeavesCarriageReturns()
    {
        using MemoryStream inner = new();
        using LineFeedToCrLfStream stream = new(inner);
        byte[] input = "_a\nb\r\n_"u8.ToArray();
        Diagnostics.Bytes("input", input);
        Diagnostics.Arrange("offset, count", "1, 5");

        stream.Write(input, 1, 5);
        Diagnostics.Bytes("inner", inner.ToArray());
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Diff("inner", "a\r\nb\r\r\n"u8.ToArray(), inner.ToArray());
        CollectionAssert.AreEqual("a\r\nb\r\r\n"u8.ToArray(), inner.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_WritesEachLineFeedAsCrLf()
    {
        using MemoryStream inner = new();
        using LineFeedToCrLfStream stream = new(inner);
        byte[] input = "\n1\n"u8.ToArray();
        Diagnostics.Bytes("input", input);
        Diagnostics.Arrange("input length", input.Length);

        await stream.WriteAsync(input.AsMemory());
        Diagnostics.Bytes("inner", inner.ToArray());
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Diff("inner", "\r\n1\r\n"u8.ToArray(), inner.ToArray());
        CollectionAssert.AreEqual("\r\n1\r\n"u8.ToArray(), inner.ToArray());
    }

    [TestMethod]
    public async Task Flush_ReachesTheInnerStream()
    {
        using BufferedStream inner = new(new MemoryStream());
        using LineFeedToCrLfStream stream = new(inner);
        Diagnostics.Arrange("writes", "[1] then [2], each followed by a flush");

        stream.Write([1], 0, 1);
        stream.Flush();
        await stream.WriteAsync(new byte[] { 2 }.AsMemory());
        await stream.FlushAsync();
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Assert("inner length", 2, inner.Length);
        Assert.AreEqual(2, inner.Length);
    }

    [TestMethod]
    public void Dispose_NotOwningTheInnerStream_LeavesItOpen()
    {
        using MemoryStream inner = new();
        Diagnostics.Arrange("ownsInner", false);

        new LineFeedToCrLfStream(inner).Dispose();
        Diagnostics.Act("inner CanWrite", inner.CanWrite);

        Diagnostics.Assert("inner CanWrite", true, inner.CanWrite);
        Assert.IsTrue(inner.CanWrite);
    }

    [TestMethod]
    public void Dispose_OwningTheInnerStream_DisposesIt()
    {
        MemoryStream inner = new();
        Diagnostics.Arrange("ownsInner", true);

        new LineFeedToCrLfStream(inner, ownsInner: true).Dispose();
        Diagnostics.Act("inner CanWrite", inner.CanWrite);

        Diagnostics.Assert("inner CanWrite", false, inner.CanWrite);
        Assert.IsFalse(inner.CanWrite);
    }
}
