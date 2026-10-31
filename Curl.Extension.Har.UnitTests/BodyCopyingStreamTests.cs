namespace Curl.Extension.Har.UnitTests;

[TestClass]
public sealed class BodyCopyingStreamTests
{
    [TestMethod]
    public async Task Writes_PassTheBytesOnAndKeepACopy()
    {
        using MemoryStream output = new();
        await using BodyCopyingStream stream = new(output);

        stream.Write([1, 2, 3], 1, 2);
        await stream.WriteAsync(new byte[] { 4, 5 }.AsMemory());
        await stream.WriteAsync([6, 7, 8], 0, 1, CancellationToken.None);
        stream.Flush();
        await stream.FlushAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 2, 3, 4, 5, 6 }, output.ToArray());
        CollectionAssert.AreEqual(new byte[] { 2, 3, 4, 5, 6 }, stream.Copied());
    }

    [TestMethod]
    public void TheStream_IsWriteOnlyAndNotSeekable()
    {
        using BodyCopyingStream stream = new(Stream.Null);

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
