namespace Curl.Console;

[TestClass]
public sealed class TerminalBinaryOutputRefusingStreamTests
{
    [TestMethod]
    public void Write_TextBytes_PassesThemOn()
    {
        using MemoryStream inner = new();
        using TerminalBinaryOutputRefusingStream stream = new(inner);

        stream.Write("abc"u8.ToArray(), 0, 3);
        stream.Flush();

        CollectionAssert.AreEqual("abc"u8.ToArray(), inner.ToArray());
        Assert.IsFalse(stream.HasRefused);
    }

    [TestMethod]
    public void Write_NulByteInTheFirstBytes_RefusesWithoutWriting()
    {
        using MemoryStream inner = new();
        using TerminalBinaryOutputRefusingStream stream = new(inner);

        Assert.ThrowsExactly<IOException>(() => stream.Write([0x61, 0x00]));

        Assert.IsTrue(stream.HasRefused);
        Assert.AreEqual(0, inner.Length);
    }

    [TestMethod]
    public async Task WriteAsync_NulByteAfterTheCheckedBytes_PassesItOn()
    {
        using MemoryStream inner = new();
        using TerminalBinaryOutputRefusingStream stream = new(inner);

        await stream.WriteAsync(new byte[TerminalBinaryOutputRefusingStream.CheckedByteCount].AsMemory(0, 0), TestContextToken);
        await stream.WriteAsync(Enumerable.Repeat((byte)'a', TerminalBinaryOutputRefusingStream.CheckedByteCount).ToArray(), 0, TerminalBinaryOutputRefusingStream.CheckedByteCount, TestContextToken);
        await stream.WriteAsync(new byte[] { 0x00 }, TestContextToken);
        await stream.FlushAsync(TestContextToken);

        Assert.AreEqual(TerminalBinaryOutputRefusingStream.CheckedByteCount + 1, inner.Length);
        Assert.IsFalse(stream.HasRefused);
    }

    [TestMethod]
    public async Task WriteAsync_NulByteInTheFirstBytes_Refuses()
    {
        using MemoryStream inner = new();
        using TerminalBinaryOutputRefusingStream stream = new(inner);

        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync(new byte[] { 0x00 }, TestContextToken).AsTask());

        Assert.IsTrue(stream.HasRefused);
    }

    [TestMethod]
    public void Members_AWriteOnlyStream_ReadSeekAndLengthAreNotSupported()
    {
        using TerminalBinaryOutputRefusingStream stream = new(new MemoryStream());

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
    public void Dispose_Called_DisposesTheInnerStream()
    {
        MemoryStream inner = new();
        TerminalBinaryOutputRefusingStream stream = new(inner);

        stream.Dispose();

        Assert.IsFalse(inner.CanWrite);
    }

    private static CancellationToken TestContextToken => CancellationToken.None;
}
