namespace Curl.Extension.GraphQl;

[TestClass]
public sealed class CopyingStreamTests
{
    [TestMethod]
    public async Task Write_PassesBytesOnAndKeepsACopy()
    {
        MemoryStream output = new();
        MemoryStream copy = new();
        using CopyingStream stream = new(output, copy);

        stream.Write([1, 2], 0, 2);
        await stream.WriteAsync(new byte[] { 0, 3, 4 }, 1, 2, CancellationToken.None);
        await stream.WriteAsync(new ReadOnlyMemory<byte>([5]));
        stream.Flush();
        await stream.FlushAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, output.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, copy.ToArray());
    }

    [TestMethod]
    public void Stream_IsWriteOnly()
    {
        using CopyingStream stream = new(new MemoryStream(), new MemoryStream());

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
