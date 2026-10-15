using Curl.Api.Callbacks;

namespace Curl.Engine;

[TestClass]
public sealed class CallbackWriteStreamTests
{
    [TestMethod]
    public async Task WriteAsync_WriteCallbackTakesEveryByte_HandsTheChunkOver()
    {
        List<byte[]> chunks = [];
        WriteCallback callback = data =>
        {
            chunks.Add(data.ToArray());
            return data.Length;
        };
        await using CallbackWriteStream stream = CallbackWriteStream.Of(callback);

        await stream.WriteAsync(new byte[] { 9, 1, 2, 9 }, 1, 2, CancellationToken.None);
        stream.Write([3], 0, 1);
        await stream.FlushAsync(CancellationToken.None);

        Assert.HasCount(2, chunks);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, chunks[0]);
        CollectionAssert.AreEqual(new byte[] { 3 }, chunks[1]);
    }

    [TestMethod]
    public void Write_HeaderCallbackTakesFewerBytes_ThrowsIOException()
    {
        HeaderCallback callback = header => header.Length - 1;
        using CallbackWriteStream stream = CallbackWriteStream.Of(callback);

        Assert.ThrowsExactly<IOException>(() => stream.Write([1, 2]));
    }

    [TestMethod]
    public void Capabilities_WriteOnlyStream_RefuseReadSeekAndLength()
    {
        using CallbackWriteStream stream = new(data => data.Length);

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
