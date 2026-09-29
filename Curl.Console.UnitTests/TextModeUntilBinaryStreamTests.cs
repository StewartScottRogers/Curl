namespace Curl.Console;

/// <summary>
/// Pins the standard output stream that follows curl's mode switch: write-only, each line feed
/// written as CR LF while in text mode, every byte as it is once switched to binary mode, and
/// flushes passed through.
/// </summary>
[TestClass]
public sealed class TextModeUntilBinaryStreamTests
{
    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using TextModeUntilBinaryStream stream = new(new MemoryStream(), () => false);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using TextModeUntilBinaryStream stream = new(new MemoryStream(), () => false);

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

        stream.Write("_a\n_"u8.ToArray(), 1, 2);
        binary = true;
        stream.Write("b\n"u8.ToArray(), 0, 2);

        CollectionAssert.AreEqual("a\r\nb\n"u8.ToArray(), inner.ToArray());
    }

    [TestMethod]
    public void Flush_ReachesTheInnerStream()
    {
        using BufferedStream inner = new(new MemoryStream());
        using TextModeUntilBinaryStream stream = new(inner, () => true);

        stream.Write([1], 0, 1);
        stream.Flush();

        Assert.AreEqual(1, inner.Length);
    }
}
