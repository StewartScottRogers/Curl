using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the standard output stream that follows curl's mode switch: write-only, each line feed
/// written as CR LF while in text mode, every byte as it is once switched to binary mode, and
/// flushes passed through.
/// </summary>
[TestClass]
public sealed class TextModeUntilBinaryStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using TextModeUntilBinaryStream stream = new(new MemoryStream(), () => false);
        Diagnostics.Arrange("inner stream / binary", "MemoryStream / False");
        Diagnostics.Act("can read / seek / write", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");

        Diagnostics.Assert("can read / seek / write", "False / False / True", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using TextModeUntilBinaryStream stream = new(new MemoryStream(), () => false);
        Diagnostics.Arrange("members", "Length, Position get and set, Read, Seek, SetLength");
        Diagnostics.Act("each member", "called");

        Diagnostics.Assert("exception from each", nameof(NotSupportedException), nameof(NotSupportedException));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void Write_BeforeAndAfterTheSwitch_WritesCrLfThenBareLineFeeds()
    {
        using MemoryStream inner = new();
        bool binary = false;
        using TextModeUntilBinaryStream stream = new(inner, () => binary);
        Diagnostics.Arrange("writes", "\"a\n\" in text mode, then \"b\n\" in binary mode");

        stream.Write("_a\n_"u8.ToArray(), 1, 2);
        binary = true;
        stream.Write("b\n"u8.ToArray(), 0, 2);
        Diagnostics.Act("inner stream length", inner.Length);
        Diagnostics.Bytes("inner stream", inner.ToArray());

        Diagnostics.Diff("inner stream", "a\r\nb\n"u8, inner.ToArray());
        CollectionAssert.AreEqual("a\r\nb\n"u8.ToArray(), inner.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_BeforeAndAfterTheSwitch_WritesCrLfThenBareLineFeeds()
    {
        using MemoryStream inner = new();
        bool binary = false;
        await using TextModeUntilBinaryStream stream = new(inner, () => binary);
        Diagnostics.Arrange("writes", "\"a\r\n\" in text mode, then \"b\n\" in binary mode");

        await stream.WriteAsync("a\r\n"u8.ToArray());
        binary = true;
        await stream.WriteAsync("b\n"u8.ToArray());
        Diagnostics.Act("inner stream length", inner.Length);
        Diagnostics.Bytes("inner stream", inner.ToArray());

        Diagnostics.Diff("inner stream", "a\r\r\nb\n"u8, inner.ToArray());
        CollectionAssert.AreEqual("a\r\r\nb\n"u8.ToArray(), inner.ToArray());
    }

    [TestMethod]
    public async Task FlushAsync_ReachesTheInnerStream()
    {
        await using BufferedStream inner = new(new MemoryStream());
        await using TextModeUntilBinaryStream stream = new(inner, () => true);
        Diagnostics.Arrange("inner stream / write", "BufferedStream / one byte, then FlushAsync");

        await stream.WriteAsync(new byte[] { 1 });
        await stream.FlushAsync();
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Assert("inner length", 1, inner.Length);
        Assert.AreEqual(1, inner.Length);
    }

    [TestMethod]
    public void Flush_ReachesTheInnerStream()
    {
        using BufferedStream inner = new(new MemoryStream());
        using TextModeUntilBinaryStream stream = new(inner, () => true);
        Diagnostics.Arrange("inner stream / write", "BufferedStream / one byte, then Flush");

        stream.Write([1], 0, 1);
        stream.Flush();
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Assert("inner length", 1, inner.Length);
        Assert.AreEqual(1, inner.Length);
    }
}
